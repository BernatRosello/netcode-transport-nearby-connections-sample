// SPDX-FileCopyrightText: 2025
// SPDX-License-Identifier: MIT

using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Linq;
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

    public enum EndpointStatus
    {
        UNINITIALIZED,
        IDLE,
        ADVERTISING,
        DISCOVERING,
        REQUESTING,
        REQUESTED,
        CONNECTED,
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
        public bool AutoAdvertise = false;
        public bool AutoApproveConnectionRequest = false;

        [Header("Client Config")]
        public bool AutoBrowse = false;
        public bool AutoSendConnectionRequest = false;

        private bool _isAdvertising = false;
        private bool _isBrowsing = false;
        public bool IsBrowsing => _isBrowsing;
        public bool IsAdvertising => _isAdvertising;

        private readonly Dictionary<string, string> _endpointNames = new();
        private readonly Dictionary<string, EndpointStatus> _endpointStatuses = new();

        public Dictionary<string, string> EndpointNames => _endpointNames;
        public Dictionary<string, EndpointStatus> EndpointStatuses => _endpointStatuses;
        public EndpointStatus LocalEndpointStatus {get; private set;}

        public List<(string id, string name)> GetEndpointsByStatus(params EndpointStatus[] statuses) { return _endpointNames.Where(kvp => statuses.Contains(kvp.Value) ); }
        public List<(string id, string name)>  ConnectedEndpoints => GetEndpointsByStatus(EndpointStatus.CONNECTED);
        public List<(string id, string name)> FoundEndpoints => GetEndpointsByStatus(EndpointStatus.DISCOVERING, EndpointStatus.ADVERTISING, EndpointStatus.REQUESTED, EndpointStatus.REQUESTING);
        public List<(string id, string name)> PendingRequestEndpoints => GetEndpointsByStatus(EndpointStatus.REQUESTED);

        // -------------------------------------------------------------------------------------
        // Native imports (wrappers for nc_unity_adapter.h)
        // -------------------------------------------------------------------------------------

        [DllImport(IMPORT_LIBRARY)] private static extern void NBC_Initialize();
        [DllImport(IMPORT_LIBRARY)] private static extern void NBC_Shutdown();

        [DllImport(IMPORT_LIBRARY)] private static extern void NBC_StartAdvertising(string name, string serviceId, int connectionType, bool lowPower, int strategy);
        [DllImport(IMPORT_LIBRARY)] private static extern void NBC_StopAdvertising();
        [DllImport(IMPORT_LIBRARY)] private static extern void NBC_StartDiscovery(string servieId, bool lowPower, int strategy);
        [DllImport(IMPORT_LIBRARY)] private static extern void NBC_StopDiscovery();

        [DllImport(IMPORT_LIBRARY)] private static extern void NBC_RequestConnection(string name, string endpointId);
        [DllImport(IMPORT_LIBRARY)] private static extern void NBC_AcceptConnection(string endpointId);
        [DllImport(IMPORT_LIBRARY)] private static extern void NBC_RejectConnection(string endpointId);
        [DllImport(IMPORT_LIBRARY)] private static extern void NBC_Disconnect(string endpointId);
        [DllImport(IMPORT_LIBRARY)] private static extern void NBC_SendBytes(string endpointId, byte[] data, int len);

        // Callbacks
        [DllImport(IMPORT_LIBRARY)] private static extern void NBC_SetOnPeerFound(OnPeerFoundCallback cb);
        [DllImport(IMPORT_LIBRARY)] private static extern void NBC_SetOnPeerLost(OnPeerLostCallback cb);
        [DllImport(IMPORT_LIBRARY)] private static extern void NBC_SetOnConnectionInitiated(OnConnectionInitiatedCallback cb);
        [DllImport(IMPORT_LIBRARY)] private static extern void NBC_SetOnConnectionEstablished(OnConnectionEstablishedCallback cb);
        [DllImport(IMPORT_LIBRARY)] private static extern void NBC_SetOnConnectionDisconnected(OnConnectionDisconnectedCallback cb);
        [DllImport(IMPORT_LIBRARY)] private static extern void NBC_SetOnPayloadReceived(OnPayloadReceivedCallback cb);

        // -------------------------------------------------------------------------------------
        // Callback delegate signatures (MUST match C)
        // -------------------------------------------------------------------------------------
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void OnPeerFoundCallback(string endpointId, string name);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void OnPeerLostCallback(string endpointId);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void OnConnectionInitiatedCallback(string endpointId, string name, string authDigits, int authStatus);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void OnConnectionEstablishedCallback(string endpointId);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void OnConnectionDisconnectedCallback(string endpointId);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void OnPayloadReceivedCallback(string endpointId, IntPtr data, int len);

        // -------------------------------------------------------------------------------------
        // Callback methods invoked by native layer
        // -------------------------------------------------------------------------------------

        [AOT.MonoPInvokeCallback(typeof(OnPeerFoundCallback))]
        private static void OnPeerFoundDelegate(string endpointId, string name)
        {
            if (s_instance == null) return;
            if (!s_instance._endpointNames.ContainsKey(endpointId))
                s_instance._endpointNames.Add(endpointId, name);
            else
                s_instance._endpointNames[endpointId] = name;

            if (!s_instance._endpointStatuses.ContainsKey(endpointId))
                s_instance._endpointStatuses.Add(endpointId, EndpointStatus.ADVERTISING);
            else
                s_instance._endpointStatuses[endpointId] = EndpointStatus.ADVERTISING;

            s_instance.OnBrowserFoundPeer?.InvokeOnMainThread(endpointId, name);

            if (s_instance.AutoSendConnectionRequest && s_instance._isBrowsing)
                s_instance.SendConnectionRequest(endpointId);
        }

        [AOT.MonoPInvokeCallback(typeof(OnPeerLostCallback))]
        private static void OnPeerLostDelegate(string endpointId)
        {
            if (s_instance == null) return;
            string disconnectedName;
            if (s_instance._endpointNames.ContainsKey(endpointId))
            {
                disconnectedName = s_instance._endpointNames[endpointId];
                s_instance._endpointNames.Remove(endpointId);
            }
            s_instance.OnBrowserLostPeer?.Invoke(endpointId, disconnectedName);
        }

        [AOT.MonoPInvokeCallback(typeof(OnConnectionInitiatedCallback))]
        private static void OnConnectionInitiatedDelegate(string endpointId, string name, string authDigits, int authStatus)
        {
            if (s_instance == null) return;
            if (LocalEndpointStatus == EndpointStatus.ADVERTISING)
            {
                _endpointStatuses[endpointId] = EndpointStatus.REQUESTING;
                s_instance.OnAdvertiserReceivedConnectionRequest?.InvokeOnMainThread(endpointId, name);

            } else if (LocalEndpointStatus == EndpointStatus.DISCOVERING)
            {
                s_instance._endpointStatuses[endpointId] = EndpointStatus.REQUESTED;
            }
            Debug.Log("Authdigits: " + authDigits);

            if (s_instance.AutoApproveConnectionRequest)
            {
                s_instance.ApproveConnectionRequest(endpointId);
            }

            Debug.Log("AuthDigits: "+authDigits+" AuthStatus: " + authStatus);
        }

        [AOT.MonoPInvokeCallback(typeof(OnConnectionEstablishedCallback))]
        private static void OnConnectionEstablishedDelegate(string endpointId)
        {
            if (s_instance == null) return;
            s_instance._isBrowsing = false;
            s_instance._isAdvertising = false;
            s_instance.InvokeOnTransportEvent(NetworkEvent.Connect, (ulong)endpointId,
                default, Time.realtimeSinceStartup);
        }

        [AOT.MonoPInvokeCallback(typeof(OnConnectionDisconnectedCallback))]
        private static void OnConnectionDisconnectedDelegate(string endpointId)
        {
            if (s_instance == null) return;
            s_instance.InvokeOnTransportEvent(NetworkEvent.Disconnect, (ulong)endpointId,
                default, Time.realtimeSinceStartup);
        }

        [AOT.MonoPInvokeCallback(typeof(OnPayloadReceivedCallback))]
        private static void OnPayloadReceivedDelegate(string endpointId, IntPtr dataPtr, int len)
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

        public void ConfigureNickname(string nickname)
        {
            configNickname = nickname;
        }

        public void ConfigureServiceId(string serviceId)
        {
            configServiceId = configServiceId;
        }

        public override void Initialize(NetworkManager networkManager)
        {
            StartCoroutine(RequestNearbyPermissions());
            _sessionData = new SessionData(configNickname, configServiceId);

            // Initialize native NC layer
            NBC_Initialize();

            // Hook native callbacks
            NBC_SetOnPeerFound(OnPeerFoundDelegate);
            NBC_SetOnPeerLost(OnPeerLostDelegate);
            NBC_SetOnConnectionInitiated(OnConnectionInitiatedDelegate);
            NBC_SetOnConnectionEstablished(OnConnectionEstablishedDelegate);
            NBC_SetOnConnectionDisconnected(OnConnectionDisconnectedDelegate);
            NBC_SetOnPayloadReceived(OnPayloadReceivedDelegate);
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
            _endpointStatuses.Clear();
            _endpointNames.Clear();
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
                _endpointStatuses.Clear();
                Debug.Log("[NBC] StartAdvertising()");
                NBC_StartAdvertising();
                _isAdvertising = true;
                LocalEndpointStatus = EndpointStatus.ADVERTISING;
            }
        }

        public void StopAdvertising()
        {
            if (_isAdvertising)
            {
                NBC_StopAdvertising();
                _isAdvertising = false;
                
                if (LocalEndpointStatus == EndpointStatus.ADVERTISING)
                    LocalEndpointStatus = EndpointStatus.IDLE;
            }
        }

        public void StartBrowsing()
        {
            if (!_isBrowsing)
            {
                _endpointNames.Clear();
                Debug.Log("[NBC] StartDiscovery()");
                NBC_StartDiscovery();
                _isBrowsing = true;
                LocalEndpointStatus = EndpointStatus.DISCOVERING;
            }
        }

        public void StopBrowsing()
        {
            if (_isBrowsing)
            {
                NBC_StopDiscovery();
                _isBrowsing = false;
                _endpointNames.Clear();
                
                if (LocalEndpointStatus == EndpointStatus.DISCOVERING)
                    LocalEndpointStatus = EndpointStatus.IDLE;
            }
        }

        public void SendConnectionRequest(string endpointId)
        {
            // For Nearby, just initiate connection
            Debug.Log($"[NBC] Send connection request to {endpointId}");
            NBC_RequestConnection(Nickname, endpointId);
            _endpointStatuses[endpointId] = EndpointStatus.REQUESTED;
            LocalEndpointStatus = EndpointStatus.REQUESTING;
        }

        public void ApproveConnectionRequest(string endpointId)
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
        public event Action<string, string> OnBrowserFoundPeer;

        /// <summary>
        /// Invoked when the browser loses a nearby host peer.
        /// The first parameter is the host peer key in the dict.
        /// The second parameter is the name of the host peer.
        /// </summary>
        public event Action<string, string> OnBrowserLostPeer;

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
