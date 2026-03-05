using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cryptid.Core;
using Unity.Services.Core;
using Unity.Services.Vivox;
using UnityEngine;

namespace Cryptid.Network
{
    /// <summary>
    /// Manages Vivox voice chat lifecycle: initialization, login, channel join/leave,
    /// and input/output device management.
    ///
    /// Provides public API for:
    ///   - Muting/unmuting microphone (input) and speaker (output)
    ///   - Listing and selecting audio devices
    ///   - Joining/leaving voice channels
    ///
    /// Singleton — created once by ConnectionManager and persists across scenes.
    /// </summary>
    public class VivoxManager : MonoBehaviour
    {
        public static VivoxManager Instance { get; private set; }

        // ---------------------------------------------------------
        // State
        // ---------------------------------------------------------

        private bool _initialized;
        private bool _loggedIn;
        private string _currentChannel;

        // Mute states
        private bool _inputMuted;   // Microphone muted
        private bool _outputMuted;  // Speaker/headset muted

        /// <summary>Whether the microphone is muted.</summary>
        public bool IsInputMuted => _inputMuted;

        /// <summary>Whether the speaker output is muted.</summary>
        public bool IsOutputMuted => _outputMuted;

        /// <summary>Whether Vivox is initialized and logged in.</summary>
        public bool IsReady => _initialized && _loggedIn;

        /// <summary>Currently joined channel name, or null.</summary>
        public string CurrentChannel => _currentChannel;

        // Events
        /// <summary>Fired when mute states change. Args: (inputMuted, outputMuted)</summary>
        public event Action<bool, bool> OnMuteStateChanged;

        /// <summary>Fired when available devices change.</summary>
        public event Action OnDevicesChanged;

        /// <summary>Fired when a participant joins or leaves the channel.</summary>
        public event Action OnParticipantsChanged;

        // ---------------------------------------------------------
        // Lifecycle
        // ---------------------------------------------------------

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                LeaveChannelSync();
                Instance = null;
            }
        }

        // ---------------------------------------------------------
        // Initialization
        // ---------------------------------------------------------

        /// <summary>
        /// Initializes Unity Gaming Services and Vivox.
        /// Call once at startup. Safe to call multiple times.
        /// </summary>
        public async Task InitializeAsync()
        {
            if (_initialized) return;

            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                    await UnityServices.InitializeAsync();

                await VivoxService.Instance.InitializeAsync();

                // Verify the SDK actually initialized (it may log errors without throwing)
                if (VivoxService.Instance.InitializationState != VivoxInitializationState.Initialized)
                {
                    Debug.LogWarning("[VivoxManager] Vivox SDK did not initialize. " +
                        "Is the project linked at Edit > Project Settings > Services?");
                    return;
                }

                _initialized = true;

                // Subscribe to device change events
                VivoxService.Instance.AvailableInputDevicesChanged += OnInputDevicesChanged;
                VivoxService.Instance.AvailableOutputDevicesChanged += OnOutputDevicesChanged;

                Debug.Log("[VivoxManager] Vivox initialized successfully.");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[VivoxManager] Initialization failed (voice chat disabled): {e.Message}");
            }
        }

        // ---------------------------------------------------------
        // Login / Logout
        // ---------------------------------------------------------

        /// <summary>
        /// Logs in to Vivox with the given display name.
        /// </summary>
        public async Task LoginAsync(string displayName)
        {
            if (!_initialized)
            {
                Debug.LogWarning("[VivoxManager] Cannot login — not initialized.");
                return;
            }

            if (_loggedIn) return;

            try
            {
                var options = new LoginOptions { DisplayName = displayName };
                await VivoxService.Instance.LoginAsync(options);

                // Verify login actually succeeded (SDK may log errors without throwing)
                if (!VivoxService.Instance.IsLoggedIn)
                {
                    Debug.LogWarning("[VivoxManager] Login did not succeed — voice chat unavailable.");
                    return;
                }

                _loggedIn = true;

                VivoxService.Instance.ParticipantAddedToChannel += OnParticipantAdded;
                VivoxService.Instance.ParticipantRemovedFromChannel += OnParticipantRemoved;

                Debug.Log($"[VivoxManager] Logged in as '{displayName}'.");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[VivoxManager] Login failed (voice chat disabled): {e.Message}");
            }
        }

        /// <summary>Logs out of Vivox.</summary>
        public async Task LogoutAsync()
        {
            if (!_loggedIn) return;

            try
            {
                await LeaveChannelAsync();
                await VivoxService.Instance.LogoutAsync();
                _loggedIn = false;

                VivoxService.Instance.ParticipantAddedToChannel -= OnParticipantAdded;
                VivoxService.Instance.ParticipantRemovedFromChannel -= OnParticipantRemoved;

                Debug.Log("[VivoxManager] Logged out.");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[VivoxManager] Logout error: {e.Message}");
            }
        }

        // ---------------------------------------------------------
        // Channel Management
        // ---------------------------------------------------------

        /// <summary>
        /// Joins a voice channel. Creates it if needed.
        /// </summary>
        /// <param name="channelName">Channel name (e.g. "lobby" or game session ID).</param>
        public async Task JoinChannelAsync(string channelName)
        {
            if (!_loggedIn)
            {
                Debug.LogWarning("[VivoxManager] Cannot join channel — not logged in.");
                return;
            }

            if (_currentChannel == channelName) return;

            // Leave current channel first
            if (!string.IsNullOrEmpty(_currentChannel))
                await LeaveChannelAsync();

            try
            {
                await VivoxService.Instance.JoinGroupChannelAsync(
                    channelName, ChatCapability.AudioOnly);
                _currentChannel = channelName;
                Debug.Log($"[VivoxManager] Joined channel '{channelName}'.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[VivoxManager] Join channel failed: {e.Message}");
            }
        }

        /// <summary>Leaves the current voice channel.</summary>
        public async Task LeaveChannelAsync()
        {
            if (string.IsNullOrEmpty(_currentChannel)) return;

            try
            {
                await VivoxService.Instance.LeaveChannelAsync(_currentChannel);
                Debug.Log($"[VivoxManager] Left channel '{_currentChannel}'.");
                _currentChannel = null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[VivoxManager] Leave channel error: {e.Message}");
            }
        }

        /// <summary>Synchronous leave for use in OnDestroy.</summary>
        private void LeaveChannelSync()
        {
            if (string.IsNullOrEmpty(_currentChannel)) return;
            try
            {
                VivoxService.Instance.LeaveAllChannelsAsync();
                _currentChannel = null;
            }
            catch (Exception) { /* ignore in shutdown */ }
        }

        // ---------------------------------------------------------
        // Mute Controls
        // ---------------------------------------------------------

        /// <summary>Toggles microphone mute.</summary>
        public void ToggleInputMute()
        {
            SetInputMuted(!_inputMuted);
        }

        /// <summary>Toggles speaker/headset mute.</summary>
        public void ToggleOutputMute()
        {
            SetOutputMuted(!_outputMuted);
        }

        /// <summary>Sets microphone mute state.</summary>
        public void SetInputMuted(bool muted)
        {
            _inputMuted = muted;

            if (_initialized)
            {
                if (muted) VivoxService.Instance.MuteInputDevice();
                else VivoxService.Instance.UnmuteInputDevice();
            }

            OnMuteStateChanged?.Invoke(_inputMuted, _outputMuted);
            Debug.Log($"[VivoxManager] Mic {(muted ? "muted" : "unmuted")}");
        }

        /// <summary>Sets speaker mute state.</summary>
        public void SetOutputMuted(bool muted)
        {
            _outputMuted = muted;

            if (_initialized)
            {
                if (muted) VivoxService.Instance.MuteOutputDevice();
                else VivoxService.Instance.UnmuteOutputDevice();
            }

            OnMuteStateChanged?.Invoke(_inputMuted, _outputMuted);
            Debug.Log($"[VivoxManager] Speaker {(muted ? "muted" : "unmuted")}");
        }

        // ---------------------------------------------------------
        // Device Management
        // ---------------------------------------------------------

        /// <summary>Gets available input (microphone) devices.</summary>
        public IReadOnlyCollection<VivoxInputDevice> GetInputDevices()
        {
            if (!_initialized) return Array.Empty<VivoxInputDevice>();
            return VivoxService.Instance.AvailableInputDevices;
        }

        /// <summary>Gets available output (speaker) devices.</summary>
        public IReadOnlyCollection<VivoxOutputDevice> GetOutputDevices()
        {
            if (!_initialized) return Array.Empty<VivoxOutputDevice>();
            return VivoxService.Instance.AvailableOutputDevices;
        }

        /// <summary>Gets the currently active input device.</summary>
        public VivoxInputDevice GetActiveInputDevice()
        {
            if (!_initialized) return null;
            return VivoxService.Instance.ActiveInputDevice;
        }

        /// <summary>Gets the currently active output device.</summary>
        public VivoxOutputDevice GetActiveOutputDevice()
        {
            if (!_initialized) return null;
            return VivoxService.Instance.ActiveOutputDevice;
        }

        /// <summary>Sets the active input (microphone) device.</summary>
        public async Task SetInputDeviceAsync(VivoxInputDevice device)
        {
            if (!_initialized) return;
            try
            {
                await VivoxService.Instance.SetActiveInputDeviceAsync(device);
                Debug.Log($"[VivoxManager] Input device set to: {device.DeviceName}");
                OnDevicesChanged?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogError($"[VivoxManager] Set input device failed: {e.Message}");
            }
        }

        /// <summary>Sets the active output (speaker) device.</summary>
        public async Task SetOutputDeviceAsync(VivoxOutputDevice device)
        {
            if (!_initialized) return;
            try
            {
                await VivoxService.Instance.SetActiveOutputDeviceAsync(device);
                Debug.Log($"[VivoxManager] Output device set to: {device.DeviceName}");
                OnDevicesChanged?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogError($"[VivoxManager] Set output device failed: {e.Message}");
            }
        }

        // ---------------------------------------------------------
        // Event Handlers
        // ---------------------------------------------------------

        private void OnInputDevicesChanged()
        {
            OnDevicesChanged?.Invoke();
        }

        private void OnOutputDevicesChanged()
        {
            OnDevicesChanged?.Invoke();
        }

        private void OnParticipantAdded(VivoxParticipant participant)
        {
            Debug.Log($"[VivoxManager] Participant joined: {participant.DisplayName}");
            OnParticipantsChanged?.Invoke();
        }

        private void OnParticipantRemoved(VivoxParticipant participant)
        {
            Debug.Log($"[VivoxManager] Participant left: {participant.DisplayName}");
            OnParticipantsChanged?.Invoke();
        }
    }
}
