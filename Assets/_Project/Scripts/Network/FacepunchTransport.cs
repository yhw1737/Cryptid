using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Steamworks;
using Steamworks.Data;
using Unity.Netcode;
using UnityEngine;

namespace Cryptid.Network
{
    /// <summary>
    /// NGO NetworkTransport backed by Steam Networking Sockets (Facepunch.Steamworks).
    ///
    /// Features:
    ///   - Uses Steam Relay (no port forwarding needed)
    ///   - P2P connections via SteamId
    ///   - Reliable/unreliable channels
    ///
    /// Host creates a SocketManager; clients connect via SteamId.
    /// Works over Steam's relay network — peers don't need to be on the same LAN.
    /// </summary>
    public class FacepunchTransport : NetworkTransport
    {
        /// <summary>SteamId of the host to connect to (set before StartClient).</summary>
        public SteamId TargetSteamId;

        private SocketManager _socketManager;
        private Steamworks.ConnectionManager _connectionManager;

        // Map NGO clientId ↔ Steam connection
        private readonly Dictionary<ulong, Connection> _clientConnections = new();
        private readonly Dictionary<Connection, ulong> _connectionClients = new();
        private ulong _nextClientId = 1;

        // Host's own connection to self
        private const ulong HOST_CLIENT_ID = 0;

        // Pending events
        private readonly Queue<TransportEvent> _events = new();

        private struct TransportEvent
        {
            public NetworkEvent Type;
            public ulong ClientId;
            public ArraySegment<byte> Payload;
        }

        // ---------------------------------------------------------
        // NetworkTransport Overrides
        // ---------------------------------------------------------

        public override ulong ServerClientId => HOST_CLIENT_ID;

        public override void Initialize(NetworkManager networkManager = null) { }

        public override bool StartServer()
        {
            if (!SteamManager.Initialized)
            {
                Debug.LogError("[FacepunchTransport] Cannot start server — Steam not initialized.");
                return false;
            }

            _socketManager = SteamNetworkingSockets.CreateRelaySocket<CryptidSocketManager>();
            ((CryptidSocketManager)_socketManager).Transport = this;
            Debug.Log("[FacepunchTransport] Server started (Steam Relay Socket).");
            return true;
        }

        public override bool StartClient()
        {
            if (!SteamManager.Initialized)
            {
                Debug.LogError("[FacepunchTransport] Cannot start client — Steam not initialized.");
                return false;
            }

            if (TargetSteamId.Value == 0)
            {
                Debug.LogError("[FacepunchTransport] TargetSteamId not set.");
                return false;
            }

            try
            {
                _connectionManager = SteamNetworkingSockets
                    .ConnectRelay<CryptidConnectionManager>(TargetSteamId);
                ((CryptidConnectionManager)_connectionManager).Transport = this;
                Debug.Log($"[FacepunchTransport] Connecting to host {TargetSteamId}...");
                return true;
            }
            catch (System.ArgumentException ex)
            {
                Debug.LogError($"[FacepunchTransport] Failed to connect to Steam relay: {ex.Message}\n" +
                              $"Ensure Steam is running and the room code is valid.");
                return false;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[FacepunchTransport] Unexpected error connecting to Steam relay: {ex}");
                return false;
            }
        }

        public override NetworkEvent PollEvent(out ulong clientId, out ArraySegment<byte> payload,
            out float receiveTime)
        {
            // Process Steam callbacks
            _socketManager?.Receive();
            _connectionManager?.Receive();

            if (_events.Count > 0)
            {
                var e = _events.Dequeue();
                clientId = e.ClientId;
                payload = e.Payload;
                receiveTime = Time.realtimeSinceStartup;
                return e.Type;
            }

            clientId = 0;
            payload = default;
            receiveTime = Time.realtimeSinceStartup;
            return NetworkEvent.Nothing;
        }

        public override void Send(ulong clientId, ArraySegment<byte> payload,
            NetworkDelivery networkDelivery)
        {
            SendType sendType = networkDelivery switch
            {
                NetworkDelivery.Reliable => SendType.Reliable,
                NetworkDelivery.ReliableSequenced => SendType.Reliable,
                NetworkDelivery.ReliableFragmentedSequenced => SendType.Reliable,
                _ => SendType.Unreliable
            };

            if (_socketManager != null && _clientConnections.TryGetValue(clientId, out var conn))
            {
                // Server sending to client
                SendData(conn, payload, sendType);
            }
            else if (_connectionManager != null)
            {
                // Client sending to server
                SendData(_connectionManager.Connection, payload, sendType);
            }
        }

        public override void DisconnectRemoteClient(ulong clientId)
        {
            if (_clientConnections.TryGetValue(clientId, out var conn))
            {
                conn.Close();
                _connectionClients.Remove(conn);
                _clientConnections.Remove(clientId);
            }
        }

        public override void DisconnectLocalClient()
        {
            _connectionManager?.Close();
        }

        public override ulong GetCurrentRtt(ulong clientId) => 0;

        public override void Shutdown()
        {
            try
            {
                _connectionManager?.Close();
                _socketManager?.Close();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[FacepunchTransport] Shutdown warning: {e.Message}");
            }

            _connectionManager = null;
            _socketManager = null;
            _clientConnections.Clear();
            _connectionClients.Clear();
            _events.Clear();
            _nextClientId = 1;

            Debug.Log("[FacepunchTransport] Shut down.");
        }

        // ---------------------------------------------------------
        // Internal: Data Transfer
        // ---------------------------------------------------------

        private static void SendData(Connection conn, ArraySegment<byte> data, SendType sendType)
        {
            var pinned = GCHandle.Alloc(data.Array, GCHandleType.Pinned);
            try
            {
                IntPtr ptr = pinned.AddrOfPinnedObject() + data.Offset;
                conn.SendMessage(ptr, data.Count, sendType);
            }
            finally
            {
                pinned.Free();
            }
        }

        // ---------------------------------------------------------
        // Internal: Event Handlers (called by SocketManager/ConnectionManager)
        // ---------------------------------------------------------

        internal void OnServerConnected(Connection conn, ConnectionInfo info)
        {
            ulong clientId = _nextClientId++;
            _clientConnections[clientId] = conn;
            _connectionClients[conn] = clientId;

            _events.Enqueue(new TransportEvent
            {
                Type = NetworkEvent.Connect,
                ClientId = clientId,
                Payload = default
            });

            Debug.Log($"[FacepunchTransport] Client {clientId} connected " +
                      $"(Steam: {info.Identity.SteamId})");
        }

        internal void OnServerDisconnected(Connection conn, ConnectionInfo info)
        {
            if (_connectionClients.TryGetValue(conn, out ulong clientId))
            {
                _connectionClients.Remove(conn);
                _clientConnections.Remove(clientId);

                _events.Enqueue(new TransportEvent
                {
                    Type = NetworkEvent.Disconnect,
                    ClientId = clientId,
                    Payload = default
                });
            }
        }

        internal void OnServerMessage(Connection conn, IntPtr data, int size)
        {
            if (!_connectionClients.TryGetValue(conn, out ulong clientId)) return;

            byte[] buffer = new byte[size];
            Marshal.Copy(data, buffer, 0, size);

            _events.Enqueue(new TransportEvent
            {
                Type = NetworkEvent.Data,
                ClientId = clientId,
                Payload = new ArraySegment<byte>(buffer)
            });
        }

        internal void OnClientConnected(ConnectionInfo info)
        {
            _events.Enqueue(new TransportEvent
            {
                Type = NetworkEvent.Connect,
                ClientId = HOST_CLIENT_ID,
                Payload = default
            });
        }

        internal void OnClientDisconnected(ConnectionInfo info)
        {
            _events.Enqueue(new TransportEvent
            {
                Type = NetworkEvent.Disconnect,
                ClientId = HOST_CLIENT_ID,
                Payload = default
            });
        }

        internal void OnClientMessage(IntPtr data, int size)
        {
            byte[] buffer = new byte[size];
            Marshal.Copy(data, buffer, 0, size);

            _events.Enqueue(new TransportEvent
            {
                Type = NetworkEvent.Data,
                ClientId = HOST_CLIENT_ID,
                Payload = new ArraySegment<byte>(buffer)
            });
        }
    }

    // =================================================================
    // Steam Socket Manager (Server Side)
    // =================================================================

    internal class CryptidSocketManager : SocketManager
    {
        internal FacepunchTransport Transport;

        public override void OnConnecting(Connection conn, ConnectionInfo info)
        {
            conn.Accept();
        }

        public override void OnConnected(Connection conn, ConnectionInfo info)
        {
            Transport?.OnServerConnected(conn, info);
        }

        public override void OnDisconnected(Connection conn, ConnectionInfo info)
        {
            Transport?.OnServerDisconnected(conn, info);
        }

        public override void OnMessage(Connection conn, NetIdentity identity,
            IntPtr data, int size, long messageNum, long recvTime, int channel)
        {
            Transport?.OnServerMessage(conn, data, size);
        }
    }

    // =================================================================
    // Steam Connection Manager (Client Side)
    // =================================================================

    internal class CryptidConnectionManager : Steamworks.ConnectionManager
    {
        internal FacepunchTransport Transport;

        public override void OnConnected(ConnectionInfo info)
        {
            Transport?.OnClientConnected(info);
        }

        public override void OnDisconnected(ConnectionInfo info)
        {
            Transport?.OnClientDisconnected(info);
        }

        public override void OnMessage(IntPtr data, int size, long messageNum, long recvTime,
            int channel)
        {
            Transport?.OnClientMessage(data, size);
        }
    }
}
