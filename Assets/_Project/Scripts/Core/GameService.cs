using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cryptid.Core
{
    /// <summary>
    /// Lightweight Service Locator for accessing game systems.
    /// 
    /// Provides a central registry for accessing managers (MapManager, TurnManager, etc.)
    /// without tight coupling or singleton anti-patterns.
    /// 
    /// Usage:
    ///   // Register (typically in GameBootstrapper)
    ///   GameService.Register&lt;MapGenerator&gt;(mapGenerator);
    ///   
    ///   // Retrieve (from anywhere)
    ///   var map = GameService.Get&lt;MapGenerator&gt;();
    /// 
    /// All services are cleared on scene unload to prevent stale references.
    /// </summary>
    public static class GameService
    {
        private static readonly Dictionary<Type, object> _services = new();

        /// <summary>
        /// Registers a service instance. Overwrites any existing registration of the same type.
        /// </summary>
        public static void Register<T>(T service) where T : class
        {
            var type = typeof(T);

            if (service == null)
            {
                Debug.LogError($"[GameService] Cannot register null for {type.Name}.");
                return;
            }

            if (_services.ContainsKey(type))
            {
                Debug.LogWarning($"[GameService] Overwriting existing service: {type.Name}");
            }

            _services[type] = service;
            Debug.Log($"[GameService] Registered: {type.Name}");
        }

        /// <summary>
        /// Retrieves a registered service. Returns null if not found.
        /// </summary>
        public static T Get<T>() where T : class
        {
            var type = typeof(T);

            if (_services.TryGetValue(type, out var service))
            {
                return service as T;
            }

            Debug.LogWarning($"[GameService] Service not found: {type.Name}");
            return null;
        }

        /// <summary>
        /// Checks if a service is registered.
        /// </summary>
        public static bool Has<T>() where T : class
        {
            return _services.ContainsKey(typeof(T));
        }

        /// <summary>
        /// Unregisters a specific service type.
        /// </summary>
        public static void Unregister<T>() where T : class
        {
            var type = typeof(T);
            if (_services.Remove(type))
            {
                Debug.Log($"[GameService] Unregistered: {type.Name}");
            }
        }

        /// <summary>
        /// Clears all registered services. Called on scene teardown.
        /// </summary>
        public static void ClearAll()
        {
            _services.Clear();
            Debug.Log("[GameService] All services cleared.");
        }

        /// <summary>
        /// Returns the count of registered services (for debugging).
        /// </summary>
        public static int ServiceCount => _services.Count;
    }
}
