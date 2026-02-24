using System;
using UnityEngine;

namespace Cryptid.Core
{
    /// <summary>
    /// Countdown timer for turn time limits.
    /// Supports two durations: regular turn (30s) and penalty placement (15s).
    /// Fires <see cref="OnTimerExpired"/> when time runs out.
    /// Provides <see cref="OnTimerTick"/> every frame for UI updates.
    /// </summary>
    public class TurnTimer : MonoBehaviour
    {
        // ---------------------------------------------------------
        // Settings
        // ---------------------------------------------------------

        public const float TurnDuration    = 30f;
        public const float PenaltyDuration = 15f;

        // ---------------------------------------------------------
        // State
        // ---------------------------------------------------------

        private float _remaining;
        private float _total;
        private bool _isRunning;

        // ---------------------------------------------------------
        // Properties
        // ---------------------------------------------------------

        /// <summary>Seconds remaining on the timer.</summary>
        public float Remaining => _remaining;

        /// <summary>Total duration of the current countdown.</summary>
        public float Total => _total;

        /// <summary>0..1 normalized progress (1 = full time, 0 = expired).</summary>
        public float NormalizedRemaining => _total > 0f ? _remaining / _total : 0f;

        /// <summary>Whether the timer is actively counting down.</summary>
        public bool IsRunning => _isRunning;

        // ---------------------------------------------------------
        // Events
        // ---------------------------------------------------------

        /// <summary>Fired every frame while running. Args: remaining seconds.</summary>
        public event Action<float> OnTimerTick;

        /// <summary>Fired once when countdown reaches zero.</summary>
        public event Action OnTimerExpired;

        // ---------------------------------------------------------
        // Public API
        // ---------------------------------------------------------

        /// <summary>Starts a regular turn timer (30s).</summary>
        public void StartTurnTimer()
        {
            _total = TurnDuration;
            _remaining = TurnDuration;
            _isRunning = true;
        }

        /// <summary>Starts a penalty placement timer (15s).</summary>
        public void StartPenaltyTimer()
        {
            _total = PenaltyDuration;
            _remaining = PenaltyDuration;
            _isRunning = true;
        }

        /// <summary>Stops the timer without firing expired event.</summary>
        public void Stop()
        {
            _isRunning = false;
        }

        /// <summary>Pauses the timer (can be resumed by calling Resume).</summary>
        public void Pause() => _isRunning = false;

        /// <summary>Resumes a paused timer.</summary>
        public void Resume()
        {
            if (_remaining > 0f) _isRunning = true;
        }

        // ---------------------------------------------------------
        // Lifecycle
        // ---------------------------------------------------------

        private void Update()
        {
            if (!_isRunning) return;

            _remaining -= Time.deltaTime;
            OnTimerTick?.Invoke(_remaining);

            if (_remaining <= 0f)
            {
                _remaining = 0f;
                _isRunning = false;
                OnTimerExpired?.Invoke();
            }
        }
    }
}
