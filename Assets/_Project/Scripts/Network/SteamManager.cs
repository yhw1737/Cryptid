using System;
using Steamworks;
using Steamworks.Data;
using UnityEngine;

namespace Cryptid.Network
{
    /// <summary>
    /// Singleton that manages Steamworks lifecycle and provides
    /// Steam Networking Sockets relay transport for NGO.
    ///
    /// Must exist before any ConnectionManager networking calls.
    /// Created automatically by ConnectionManager.EnsureSteam().
    /// </summary>
    public class SteamManager : MonoBehaviour
    {
        public static SteamManager Instance { get; private set; }

        /// <summary>Whether Steam was initialized successfully.</summary>
        public static bool Initialized { get; private set; }

        /// <summary>Local player Steam display name.</summary>
        public static string PlayerName => Initialized ? SteamClient.Name : "Player";

        /// <summary>Local player SteamId.</summary>
        public static SteamId MySteamId => SteamClient.SteamId;

        // App ID used during development (Spacewar test app)
        private const uint APP_ID = 4525130;

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

            try
            {
                SteamClient.Init(APP_ID, asyncCallbacks: false);
                Initialized = true;
                Debug.Log($"[SteamManager] Steam initialized. User: {SteamClient.Name} " +
                          $"(ID: {SteamClient.SteamId})");
            }
            catch (Exception e)
            {
                Initialized = false;
                Debug.LogError($"[SteamManager] Steam init failed: {e.Message}. " +
                               "Make sure Steam client is running.");
            }
        }

        private void Update()
        {
            if (Initialized)
                SteamClient.RunCallbacks();
        }

        private void OnApplicationQuit()
        {
            Shutdown();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Shutdown();
                Instance = null;
            }
        }

        private void Shutdown()
        {
            if (!Initialized) return;
            Initialized = false;
            SteamClient.Shutdown();
            Debug.Log("[SteamManager] Steam shut down.");
        }
    }
}
