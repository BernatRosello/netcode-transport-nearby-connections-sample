// SPDX-FileCopyrightText: 2025
// SPDX-License-Identifier: MIT

using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

#if UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace Netcode.Transports.NearbyConnections
{
    public sealed class SessionData
    {
        public string name {get;}
        public string serviceId {get;}

        public SessionData(string _name, string _serviceId)
        {
            name = _name;
            serviceId = _serviceId;
        }
    }

    public class NBCTransport : NetworkTransport
    {
#if (UNITY_IOS || UNITY_VISIONOS) && !UNITY_EDITOR
        public const string IMPORT_LIBRARY = "__Internal";
#elif UNITY_ANDROID //&& !UNITY_EDITOR
    public const string IMPORT_LIBRARY = "nc_unity";
#else
        public const string IMPORT_LIBRARY = "nc";
#endif

        public static NBCTransport Instance => s_instance;
        private static NBCTransport s_instance;

        public override ulong ServerClientId => 0;

        private SessionData _sessionData;

        [SerializeField, Tooltip("Unique service ID for this Nearby service (match on all peers).")]
        private string configServiceId = "untiy-nc";
        [SerializeField, Tooltip("This will be the name of your device in the network.")]
        private string configNickname = "UnityPeer";

        public string ServiceId => _sessionData.serviceId;
        public string Nickname => _sessionData.name;

        [Header("Host Config")]
        public bool AutoAdvertise = true;
        public bool AutoApproveConnectionRequest = true;

        [Header("Client Config")]
        public bool AutoBrowse = true;
        public bool AutoSendConnectionRequest = true;

        private bool _isAdvertising = false;
        private bool _isBrowsing = false;

        private readonly Dictionary<int, string> _nearbyHostDict = new();
        private readonly Dictionary<int, string> _pendingConnectionRequestDict = new();

        public Dictionary<int, string> NearbyHostDict => _nearbyHostDict;
        public Dictionary<int, string> PendingConnectionRequestDict => _pendingConnectionRequestDict;
        public bool IsAdvertising => _isAdvertising;
        public bool IsBrowsing => _isBrowsing;

        // -------------------------------------------------------------------------------------
        // Native imports (wrappers for nc_unity_adapter.h)
        // -------------------------------------------------------------------------------------

        [DllImport(IMPORT_LIBRARY)] private static extern void NBC_Initialize(string serviceId);
        [DllImport(IMPORT_LIBRARY)] private static extern void NBC_Shutdown();

        [DllImport(IMPORT_LIBRARY)] private static extern void NBC_StartAdvertising();
        [DllImport(IMPORT_LIBRARY)] private static extern void NBC_StopAdvertising();
        [DllImport(IMPORT_LIBRARY)] private static extern void NBC_StartDiscovery();
        [DllImport(IMPORT_LIBRARY)] private static extern void NBC_StopDiscovery();

        [DllImport(IMPORT_LIBRARY)] private static extern void NBC_AcceptConnection(int endpointId);
        [DllImport(IMPORT_LIBRARY)] private static extern void NBC_RejectConnection(int endpointId);
        [DllImport(IMPORT_LIBRARY)] private static extern void NBC_Disconnect(int endpointId);
        [DllImport(IMPORT_LIBRARY)] private static extern void NBC_SendBytes(int endpointId, byte[] data, int len);

        // Callbacks
        [DllImport(IMPORT_LIBRARY)] private static extern void NBC_SetOnPeerFound(OnPeerFoundCallback cb);
        [DllImport(IMPORT_LIBRARY)] private static extern void NBC_SetOnPeerLost(OnPeerLostCallback cb);
        [DllImport(IMPORT_LIBRARY)] private static extern void NBC_SetOnConnectionRequested(OnConnectionRequestedCallback cb);
        [DllImport(IMPORT_LIBRARY)] private static extern void NBC_SetOnConnectionEstablished(OnConnectionEstablishedCallback cb);
        [DllImport(IMPORT_LIBRARY)] private static extern void NBC_SetOnConnectionDisconnected(OnConnectionDisconnectedCallback cb);
        [DllImport(IMPORT_LIBRARY)] private static extern void NBC_SetOnDataReceived(OnDataReceivedCallback cb);

        // -------------------------------------------------------------------------------------
        // Callback delegate signatures (MUST match C)
        // -------------------------------------------------------------------------------------
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void OnPeerFoundCallback(int endpointId, string name);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void OnPeerLostCallback(int endpointId);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void OnConnectionRequestedCallback(int endpointId, string name);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void OnConnectionEstablishedCallback(int endpointId);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void OnConnectionDisconnectedCallback(int endpointId);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void OnDataReceivedCallback(int endpointId, IntPtr data, int len);

        // -------------------------------------------------------------------------------------
        // Callback methods invoked by native layer
        // -------------------------------------------------------------------------------------

        [AOT.MonoPInvokeCallback(typeof(OnPeerFoundCallback))]
        private static void OnPeerFoundDelegate(int endpointId, string name)
        {
            if (s_instance == null) return;
            if (!s_instance._nearbyHostDict.ContainsKey(endpointId))
                s_instance._nearbyHostDict.Add(endpointId, name);

            s_instance.OnBrowserFoundPeer?.Invoke(endpointId, name);

            if (s_instance.AutoSendConnectionRequest && s_instance._isBrowsing)
                s_instance.SendConnectionRequest(endpointId);
        }

        [AOT.MonoPInvokeCallback(typeof(OnPeerLostCallback))]
        private static void OnPeerLostDelegate(int endpointId)
        {
            if (s_instance == null) return;
            if (s_instance._nearbyHostDict.ContainsKey(endpointId))
                s_instance._nearbyHostDict.Remove(endpointId);
            s_instance.OnBrowserLostPeer?.Invoke(endpointId, "");
        }

        [AOT.MonoPInvokeCallback(typeof(OnConnectionRequestedCallback))]
        private static void OnConnectionRequestedDelegate(int endpointId, string name)
        {
            if (s_instance == null) return;
            if (!s_instance.AutoApproveConnectionRequest)
                s_instance._pendingConnectionRequestDict[endpointId] = name;
            s_instance.OnAdvertiserReceivedConnectionRequest?.Invoke(endpointId, name);

            if (s_instance.AutoApproveConnectionRequest)
                s_instance.ApproveConnectionRequest(endpointId);
        }

        [AOT.MonoPInvokeCallback(typeof(OnConnectionEstablishedCallback))]
        private static void OnConnectionEstablishedDelegate(int endpointId)
        {
            if (s_instance == null) return;
            s_instance._isBrowsing = false;
            s_instance._isAdvertising = false;
            s_instance.InvokeOnTransportEvent(NetworkEvent.Connect, (ulong)endpointId,
                default, Time.realtimeSinceStartup);
        }

        [AOT.MonoPInvokeCallback(typeof(OnConnectionDisconnectedCallback))]
        private static void OnConnectionDisconnectedDelegate(int endpointId)
        {
            if (s_instance == null) return;
            s_instance.InvokeOnTransportEvent(NetworkEvent.Disconnect, (ulong)endpointId,
                default, Time.realtimeSinceStartup);
        }

        [AOT.MonoPInvokeCallback(typeof(OnDataReceivedCallback))]
        private static void OnDataReceivedDelegate(int endpointId, IntPtr dataPtr, int len)
        {
            if (s_instance == null) return;
            byte[] data = new byte[len];
            Marshal.Copy(dataPtr, data, 0, len);
            s_instance.InvokeOnTransportEvent(NetworkEvent.Data, (ulong)endpointId,
                new ArraySegment<byte>(data, 0, len), Time.realtimeSinceStartup);
        }

        // -------------------------------------------------------------------------------------
        // Unity lifecycle
        // -------------------------------------------------------------------------------------

        private void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                Destroy(gameObject);
            }
            else
            {
                s_instance = this;
            }
        }

        public override void Initialize(NetworkManager networkManager)
        {
            StartCoroutine(RequestNearbyPermissions());
            _sessionData = new SessionData(configNickname, configServiceId);

            // Initialize native NC layer
            NBC_Initialize(_sessionData.name, _sessionData.serviceId);

            // Hook native callbacks
            NBC_SetOnPeerFound(OnPeerFoundDelegate);
            NBC_SetOnPeerLost(OnPeerLostDelegate);
            NBC_SetOnConnectionRequested(OnConnectionRequestedDelegate);
            NBC_SetOnConnectionEstablished(OnConnectionEstablishedDelegate);
            NBC_SetOnConnectionDisconnected(OnConnectionDisconnectedDelegate);
            NBC_SetOnDataReceived(OnDataReceivedDelegate);
        }

        public override bool StartServer()
        {
            if (AutoAdvertise)
                StartAdvertising();
            return true;
        }

        public override bool StartClient()
        {
            if (AutoBrowse)
                StartBrowsing();
            return true;
        }

        public override void Shutdown()
        {
            NBC_Shutdown();
            _pendingConnectionRequestDict.Clear();
            _nearbyHostDict.Clear();
            _sessionData = null;
            _isAdvertising = false;
            _isBrowsing = false;
        }

        private System.Collections.IEnumerator RequestNearbyPermissions()
        {
        #if UNITY_ANDROID && !UNITY_EDITOR
            string[] permissions = new string[]
            {
                "android.permission.ACCESS_FINE_LOCATION",
                "android.permission.BLUETOOTH_CONNECT",
                "android.permission.BLUETOOTH_SCAN",
                "android.permission.BLUETOOTH_ADVERTISE",
                "android.permission.NEARBY_WIFI_DEVICES"
            };

            foreach (string perm in permissions)
            {
                if (!Permission.HasUserAuthorizedPermission(perm))
                {
                    Permission.RequestUserPermission(perm);
                    // Give the system a tiny delay so dialogs can appear one by one
                    yield return new WaitForSeconds(0.2f);
                }
            }
        #endif
            yield break; // harmless no-op in Editor or non-Android
        }

        // -------------------------------------------------------------------------------------
        // Public control
        // -------------------------------------------------------------------------------------

        public void StartAdvertising()
        {
            if (!_isAdvertising)
            {
                _pendingConnectionRequestDict.Clear();
                Debug.Log("[NBC] StartAdvertising()");
                NBC_StartAdvertising();
                _isAdvertising = true;
            }
        }

        public void StopAdvertising()
        {
            if (_isAdvertising)
            {
                NBC_StopAdvertising();
                _isAdvertising = false;
            }
        }

        public void StartBrowsing()
        {
            if (!_isBrowsing)
            {
                _nearbyHostDict.Clear();
                Debug.Log("[NBC] StartDiscovery()");
                NBC_StartDiscovery();
                _isBrowsing = true;
            }
        }

        public void StopBrowsing()
        {
            if (_isBrowsing)
            {
                NBC_StopDiscovery();
                _isBrowsing = false;
                _nearbyHostDict.Clear();
            }
        }

        public void SendConnectionRequest(int endpointId)
        {
            // For Nearby, just initiate connection
            Debug.Log($"[NBC] Send connection request to {endpointId}");
            NBC_AcceptConnection(endpointId);
        }

        public void ApproveConnectionRequest(int endpointId)
        {
            s_instance.OnAdvertiserApprovedConnectionRequest?.Invoke(endpointId);
            NBC_AcceptConnection(endpointId);
        }

        // -------------------------------------------------------------------------------------
        // NGO Transport interface
        // -------------------------------------------------------------------------------------

        // NOT IN USE, TRANSPORT IS PURELY EVENT BASED !!!!
        public override NetworkEvent PollEvent(out ulong transportId, out ArraySegment<byte> payload, out float receiveTime)
        {
            transportId = 0;
            payload = default;
            receiveTime = Time.realtimeSinceStartup;
            return NetworkEvent.Nothing;
        }

        public override void Send(ulong transportId, ArraySegment<byte> data, NetworkDelivery delivery)
        {
            //bool reliable = !(delivery == NetworkDelivery.Unreliable || delivery == NetworkDelivery.UnreliableSequenced);
            NBC_SendBytes((int)transportId, data.Array, data.Count);
        }

        public override ulong GetCurrentRtt(ulong transportId) => 0;

        public override void DisconnectLocalClient() { }
        public override void DisconnectRemoteClient(ulong transportId)
        {
            NBC_Disconnect((int)transportId);
        }

        // -------------------------------------------------------------------------------------
        // Unity Events
        // -------------------------------------------------------------------------------------

        /// <summary>
        /// Invoked when the browser finds a nearby host peer.
        /// The first parameter is the host peer key in the dict.
        /// The second parameter is the name of the host peer.
        /// </summary>
        public event Action<int, string> OnBrowserFoundPeer;

        /// <summary>
        /// Invoked when the browser loses a nearby host peer.
        /// The first parameter is the host peer key in the dict.
        /// The second parameter is the name of the host peer.
        /// </summary>
        public event Action<int, string> OnBrowserLostPeer;

        /// <summary>
        /// Invoked when the advertiser receives a connection request.
        /// The first parameter is the connection request key in the dict.
        /// The second parameter is the name of the peer who sent the connection request.
        /// </summary>
        public event Action<int, string> OnAdvertiserReceivedConnectionRequest;

        public event Action<int> OnAdvertiserApprovedConnectionRequest;

        /// <summary>
        /// Invoked when initializes connection with a new peer. This event should be used only for notification purpose.
        /// The first parameter is the name of the connecting peer.
        /// </summary>
        public event Action<string> OnConnectingWithPeer;
    }
}
