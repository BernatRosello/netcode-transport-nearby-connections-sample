using System;
using System.Linq;
using System.Collections;
using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEditor;


#if UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace Netcode.Transports.NearbyConnections
{

    public class NBCTransport : NetworkTransport
    {
#if (UNITY_IOS || UNITY_VISIONOS) && !UNITY_EDITOR
        public const string IMPORT_LIBRARY = "__Internal";
#elif UNITY_ANDROID && !UNITY_EDITOR
        public const string IMPORT_LIBRARY = "nc_unity";
#else
        public const string IMPORT_LIBRARY = "nc";
#endif

#if UNITY_ANDROID
        #region Android Implementation Details
        [Header("Permission Dialog Prefab")]
        [SerializeField] private GameObject _permissionDialogPrefab;
        [SerializeField] private RectTransform _permissionDialogParentTransform;
        private GameObject _activeDialog;

        private static int AndroidVersion()
        {
#if !UNITY_EDITOR
            using (var buildVersion = new AndroidJavaClass("android.os.Build$VERSION"))
            {
                return buildVersion.GetStatic<int>("SDK_INT");
            }
#else       
            // Default to target for editor and build process purposes
            // like manifest permission injection on post process build.
            return (int)PlayerSettings.Android.targetSdkVersion;
#endif
        }

        public static class AndroidPermissionCheck
        {
            private static readonly AndroidJavaObject activity =
                new AndroidJavaClass("com.unity3d.player.UnityPlayer")
                .GetStatic<AndroidJavaObject>("currentActivity");

            public static bool HasPermission(string permission)
            {
                int result = activity.Call<int>(
                    "checkSelfPermission",
                    permission
                );

                // PackageManager.PERMISSION_GRANTED = 0
                return result == 0;
            }
        }

        public readonly struct PermissionDef
        {
            public readonly string Name;
            public readonly string MinSdk;
            public readonly string MaxSdk;
            public readonly bool RuntimePermission;

            public PermissionDef(string name, string minSdk = null, string maxSdk = null, bool runtimePerm = false)
            {
                Name = name;
                MinSdk = minSdk;
                MaxSdk = maxSdk;
                RuntimePermission = runtimePerm;
            }

            public bool AppliesTo(int sdk)
            {
                int minSdkNum = string.IsNullOrEmpty(MinSdk) ? 0 : int.Parse(MinSdk);

                int maxSdkNum = string.IsNullOrEmpty(MaxSdk) ? int.MaxValue : int.Parse(MaxSdk);
                return sdk >= minSdkNum && sdk <= maxSdkNum;
            }

            public bool IsRuntimePermission(int sdk)
            {
                if (sdk >= 23 && RuntimePermission && AppliesTo(sdk))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        public static class NearbyPermissionDefinitions
        {
            /// <summary>
            /// https://developers.google.com/nearby/connections/android/get-started#request_permissions
            /// </summary>
            private static readonly PermissionDef[] _permissions = {
                // Required for Nearby Connections across Android versions (we have to remove the specified maxSdk:31, because it is outdated in the docs)
                new("android.permission.ACCESS_WIFI_STATE"),
                new("android.permission.CHANGE_WIFI_STATE"),

                new("android.permission.BLUETOOTH",         maxSdk: "30"),
                new("android.permission.BLUETOOTH_ADMIN",   maxSdk: "30"),

                new("android.permission.ACCESS_COARSE_LOCATION",  maxSdk: "28"),
                new("android.permission.ACCESS_FINE_LOCATION",    minSdk: "29", maxSdk: "31", runtimePerm: true),

                // Android 12+ Bluetooth permissions
                new("android.permission.BLUETOOTH_SCAN",        minSdk: "31", runtimePerm: true),
                new("android.permission.BLUETOOTH_ADVERTISE",   minSdk: "31", runtimePerm: true),
                new("android.permission.BLUETOOTH_CONNECT",     minSdk: "31", runtimePerm: true),

                // Android 13+ Nearby WiFi
                new("android.permission.NEARBY_WIFI_DEVICES", minSdk: "32"),

                // Optional for file payloads (NO LONGER SUPPORTED API 33+)
                new("android.permission.READ_EXTERNAL_STORAGE", minSdk: "16", maxSdk: "32", runtimePerm: true)
            };
            /// <summary>
            /// Gets all the permissions necessary to run on the current device's SDK level (if in editor defaults to targetSdk).
            /// </summary>
            public static PermissionDef[] Permissions => _permissions.Where(perm => perm.AppliesTo(AndroidVersion())).ToArray();
            /// <summary>
            /// Gets the RUNTIME permissions necessary to run on the current device's SDK level (if in editor defaults to targetSdk).
            /// </summary>
            public static PermissionDef[] RuntimePermissions => _permissions.Where(perm => perm.IsRuntimePermission(AndroidVersion())).ToArray();
        }
        #endregion
#endif
        public class SessionData
        {
            public string name { get; }
            public string serviceId { get; }
            public bool lowPower { get; }
            public ConnectionType type { get; }
            public ConnectionStrategy strategy { get; }

            public SessionData(string _name, string _serviceId, ConnectionType _type, bool _lowPower, ConnectionStrategy _strategy)
            {
                name = _name;
                serviceId = _serviceId;
                type = _type;
                lowPower = _lowPower;
                strategy = _strategy;
            }
        }

        public class TransportHashes : IReadOnlyDictionary<string, ulong>
        {
            private static ulong GetHashCodeUInt64(string input)
            {
                // NON - DETERMINISTIC ! :(
                /*
                var s1 = input.Substring(0, input.Length / 2);
                var s2 = input.Substring(input.Length / 2);

                ulong x = ((ulong)s1.GetHashCode()) << 0x20 | (uint)s2.GetHashCode();

                return x;
                */

                // DETERMINISTIC ;)
                const ulong offset = 1469598103934665603;
                const ulong prime = 1099511628211;
                ulong hash = offset;

                for (int i = 0; i < input.Length; i++)
                {
                    hash ^= (byte)input[i];
                    hash *= prime;
                }

                return hash;
            }

            private readonly Dictionary<string, ulong> hashDict = new Dictionary<string, ulong>();
            private readonly Dictionary<ulong, string> stringDict = new Dictionary<ulong, string>();

            public ulong Add(string s)
            {
                var hash = GetHashCodeUInt64(s);
                hashDict.Add(s, hash);
                stringDict.Add(hash, s);
                return hash;
            }

            public ulong Remove(string s)
            {
                if (hashDict.ContainsKey(s))
                {
                    stringDict.Remove(hashDict[s]);
                }
                hashDict.Remove(s);
                return default;
            }

            public string Remove(ulong hash)
            {
                if (stringDict.ContainsKey(hash))
                {
                    hashDict.Remove(stringDict[hash]);
                }
                stringDict.Remove(hash);
                return default;
            }

            public ulong this[string key] => hashDict[key];

            public string this[ulong hash] => stringDict[hash];

            public IEnumerable<string> Keys => hashDict.Keys;

            public IEnumerable<ulong> Values => hashDict.Values;

            public int Count => hashDict.Count;

            public bool ContainsKey(string endpointId)
            {
                return hashDict.ContainsKey(endpointId);
            }

            public bool ContainsKey(ulong transportId)
            {
                return stringDict.ContainsKey(transportId);
            }

            public IEnumerator<KeyValuePair<string, ulong>> GetEnumerator()
            {
                return hashDict.GetEnumerator();
            }

            public bool TryGetValue(string key, out ulong value)
            {
                return hashDict.TryGetValue(key, out value);
            }

            public void AddServer(string endpointId)
            {
                hashDict.Add(endpointId, Instance.ServerClientId);
                stringDict.Add(Instance.ServerClientId, endpointId);
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return ((IEnumerable)hashDict).GetEnumerator();
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

        /// <summary>
        /// The connection type which Nearby Connection used to establish a connection.
        /// </summary>
        public enum ConnectionType : int
        {
            /// <summary>
            /// Nearby Connections will change the device's Wi-Fi or Bluetooth status only if necessary.
            /// </summary>
            BALANCED = 0,
            /// <summary>
            /// Nearby Connections will change the device's Wi-Fi or Bluetooth status to enhance throughput. This may cause the device to lose its internet connection.
            /// </summary>
            DISRUPTIVE = 1,
            /// <summary>
            /// Nearby Connections should not change the device's Wi-Fi or Bluetooth status.
            /// </summary>
            NON_DISRUPTIVE = 2
        }
        public enum ConnectionStrategy : int
        {
            /// <summary>
            /// Peer-to-peer strategy that supports an M-to-N, or cluster-shaped, connection topology.
            /// In other words, this enables connecting amorphous clusters of devices within radio range (~100m),
            /// where each device can both initiate outgoing connections to M other devices and accept incoming connections from N other devices.
            /// https://developers.google.com/android/reference/com/google/android/gms/nearby/connection/Strategy#public-static-final-strategy-p2p_cluster
            /// </summary>
            P2P_CLUSTER = 0,
            /// <summary>
            /// Peer-to-peer strategy that supports a 1-to-1 connection topology. In other words,
            /// this enables connecting to a single device within radio range (~100m).
            /// This strategy will give the absolute highest bandwidth, but will not allow multiple connections at a time.
            /// https://developers.google.com/android/reference/com/google/android/gms/nearby/connection/Strategy#public-static-final-strategy-p2p_point_to_point
            /// </summary>
            P2P_POINT_TO_POINT = 1,
            /// <summary>
            /// Peer-to-peer strategy that supports a 1-to-N, or star-shaped, connection topology.
            /// In other words, this enables connecting devices within radio range (~100m) in a star shape,
            /// where each device can, at any given time, play the role of either a hub (where it can accept incoming connections from N other devices),
            /// or a spoke (where it can initiate an outgoing connection to a single hub), but not both.
            /// </summary>
            P2P_STAR = 2
        }

        public static NBCTransport Instance => s_instance;
        private static NBCTransport s_instance;

        private static ILogger logger = Debug.unityLogger;
        private static string kTag = "NBC-Transport";
        private bool PermissionsReady
        {
            get
            {
#if UNITY_ANDROID //&& !UNITY_EDITOR
                foreach(var perm in NearbyPermissionDefinitions.Permissions)
                {
                    if (!AndroidPermissionCheck.HasPermission(perm.Name))
                    {
                        logger.LogWarning(kTag, $"Permission {perm.Name} granted=FALSE !");
                        return false;
                    }
                }
#endif
                return true;
            }
        }

        public override ulong ServerClientId => 0;

        private SessionData _sessionData = null;

        [Header("Common Config")]
        [SerializeField, Tooltip("Unique service ID for this Nearby service (match on all peers).")]
        private string _configServiceId = "untiy-nc";
        [SerializeField, Tooltip("This will be the name of your device in the network.")]
        private string _configNickname = "UnityPeer";
        [SerializeField, Tooltip("This will be the connection type used in the P2P Nearby network.")]
        private ConnectionType _configConnectionType = ConnectionType.DISRUPTIVE;

        [SerializeField, Tooltip("LowPower setting to be used by this endpoint in the next initialized Nearby Session.")]
        private bool _configLowPower = false;

        [SerializeField, Tooltip("This will be the connection strategy used in the P2P Nearby network.")]
        private ConnectionStrategy _configConnectionStrategy = ConnectionStrategy.P2P_STAR;

        public string ServiceId => _sessionData.serviceId;
        public string Nickname => _sessionData.name;
        public ConnectionType TypeOfConnection => _sessionData.type;
        public bool LowPower => _sessionData.lowPower;
        public ConnectionStrategy Strategy => _sessionData.strategy;
        public bool IsNearbyInitialized => _sessionData != null;

        [Header("Host Config")]
        public bool AutoAdvertise;
        public bool AutoApproveConnectionRequest;

        [Header("Client Config")]
        public bool AutoBrowse;
        public bool AutoSendConnectionRequest;

        private bool _isAdvertising = false;
        private bool _isBrowsing = false;
        public bool IsBrowsing => _isBrowsing;
        public bool IsAdvertising => _isAdvertising;

        private readonly Dictionary<string, string> _endpointNames = new();
        private readonly Dictionary<string, EndpointStatus> _endpointStatuses = new();

        public Dictionary<string, string> EndpointNames => _endpointNames;
        public Dictionary<string, EndpointStatus> EndpointStatuses => _endpointStatuses;
        private Dictionary<string, string> _pendingAuthCodes = new();

        public List<(string id, string name)> GetEndpointsByStatus(params EndpointStatus[] statuses)
        {
            return _endpointNames.Where(kvp => statuses.Contains(_endpointStatuses[kvp.Key])).Select(kvp => (kvp.Key, kvp.Value)).ToList();
        }

        public List<(string id, string name)> ConnectedEndpoints => GetEndpointsByStatus(EndpointStatus.CONNECTED);
        public List<(string id, string name)> FoundAndPendingEndpoints => GetEndpointsByStatus(EndpointStatus.DISCOVERING, EndpointStatus.ADVERTISING, EndpointStatus.REQUESTED, EndpointStatus.REQUESTING);
        public List<(string id, string name)> FoundEndpoints => GetEndpointsByStatus(EndpointStatus.DISCOVERING, EndpointStatus.ADVERTISING);
        public List<(string id, string name, string authCode)> PendingRequestEndpoints =>
            GetEndpointsByStatus(EndpointStatus.REQUESTING, EndpointStatus.REQUESTED)
                .Where(ep => _pendingAuthCodes.ContainsKey(ep.id))
                .Select(ep => (ep.id, ep.name, _pendingAuthCodes[ep.id]))
                .ToList();
        public List<(string id, string name)> GetAllEndpoints() => EndpointNames.Select(kvp => (kvp.Key, kvp.Value)).ToList();

        private TransportHashes _transportIds = new TransportHashes();

        // -------------------------------------------------------------------------------------
        // Native imports (wrappers for nc_unity_adapter.h)
        // -------------------------------------------------------------------------------------

        [DllImport(IMPORT_LIBRARY)] private static extern void NBC_Initialize();
        [DllImport(IMPORT_LIBRARY)] private static extern void NBC_Shutdown();

        [DllImport(IMPORT_LIBRARY)] private static extern void NBC_StartAdvertising(string name, string serviceId, int connectionType, bool lowPower, int strategy);
        [DllImport(IMPORT_LIBRARY)] private static extern void NBC_StopAdvertising();
        [DllImport(IMPORT_LIBRARY)] private static extern void NBC_StartDiscovery(string serviceId, bool lowPower, int strategy);
        [DllImport(IMPORT_LIBRARY)] private static extern void NBC_StopDiscovery();

        [DllImport(IMPORT_LIBRARY)] private static extern void NBC_RequestConnection(string name, string endpointId);
        [DllImport(IMPORT_LIBRARY)] private static extern void NBC_AcceptConnection(string endpointId);
        [DllImport(IMPORT_LIBRARY)] private static extern void NBC_RejectConnection(string endpointId);
        [DllImport(IMPORT_LIBRARY)] private static extern void NBC_Disconnect(string endpointId);
        [DllImport(IMPORT_LIBRARY)] private static extern void NBC_SendBytes(string endpointId, byte[] data, int len);

        // Callbacks
        [DllImport(IMPORT_LIBRARY)] private static extern void NBC_SetOnPeerFound(OnDiscoveryPeerFoundCallback cb);
        [DllImport(IMPORT_LIBRARY)] private static extern void NBC_SetOnPeerLost(OnPeerLostCallback cb);
        [DllImport(IMPORT_LIBRARY)] private static extern void NBC_SetOnConnectionInitiated(OnConnectionInitiatedCallback cb);
        [DllImport(IMPORT_LIBRARY)] private static extern void NBC_SetOnConnectionEstablished(OnConnectionEstablishedCallback cb);
        [DllImport(IMPORT_LIBRARY)] private static extern void NBC_SetOnConnectionDisconnected(OnConnectionDisconnectedCallback cb);
        [DllImport(IMPORT_LIBRARY)] private static extern void NBC_SetOnPayloadReceived(OnPayloadReceivedCallback cb);

        // -------------------------------------------------------------------------------------
        // Callback delegate signatures (MUST match C)
        // -------------------------------------------------------------------------------------
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void OnDiscoveryPeerFoundCallback(string endpointId, string name);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void OnPeerLostCallback(string endpointId);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void OnConnectionInitiatedCallback(string endpointId, string name, string authDigits, int authStatus);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void OnConnectionEstablishedCallback(string endpointId);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void OnConnectionDisconnectedCallback(string endpointId);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void OnPayloadReceivedCallback(string endpointId, IntPtr data, int len);

        // -------------------------------------------------------------------------------------
        // Callback methods invoked by native layer
        // -------------------------------------------------------------------------------------

        [AOT.MonoPInvokeCallback(typeof(OnDiscoveryPeerFoundCallback))]
        private static void OnDiscoveryPeerFoundDelegate(string endpointId, string name)
        {
            if (s_instance == null) return;

            s_instance._transportIds.AddServer(endpointId);
            s_instance._endpointNames.Add(endpointId, name);
            s_instance._endpointStatuses.Add(endpointId, EndpointStatus.ADVERTISING);

            s_instance.OnBrowserFoundPeer?.InvokeOnMainThread(endpointId, name);

            if (s_instance.AutoSendConnectionRequest)
                s_instance.SendConnectionRequest(endpointId);
        }

        [AOT.MonoPInvokeCallback(typeof(OnPeerLostCallback))]
        private static void OnDiscoveryPeerLostDelegate(string endpointId)
        {
            if (s_instance == null) return;
            string disconnectedName = s_instance._endpointNames[endpointId];
            s_instance.OnBrowserLostPeer?.InvokeOnMainThread(endpointId, disconnectedName);
            s_instance.RemoveEndpointData(endpointId);
        }

        // This is the point at which an Advertising endpoint (the Server, in P2P_START Strategy) first "sees" the discovering endpoint,
        // when it receives it's connection request.
        [AOT.MonoPInvokeCallback(typeof(OnConnectionInitiatedCallback))]
        private static void OnConnectionInitiatedDelegate(string endpointId, string name, string authDigits, int authStatus)
        {
            if (s_instance == null) return;
            switch (authStatus)
            {
                case 0:
                    s_instance._pendingAuthCodes.Add(endpointId, authDigits);

                    // As advertisers we only get to see the peer upon them requesting a connections, so this is where we register their info
                    if (s_instance._isAdvertising)
                    {
                        s_instance.EndpointNames.Add(endpointId, name);
                        s_instance.EndpointStatuses.Add(endpointId, EndpointStatus.REQUESTING);
                        s_instance._transportIds.Add(endpointId);
                        s_instance.OnAdvertiserReceivedConnectionRequest?.InvokeOnMainThread(endpointId, name);

                    }
                    else if (s_instance._isBrowsing)
                    {
                        s_instance._endpointStatuses[endpointId] = EndpointStatus.REQUESTED;
                        s_instance.OnBrowserSentConnectionRequest?.InvokeOnMainThread(endpointId, name);
                    }

                    if (s_instance.AutoApproveConnectionRequest)
                    {
                        s_instance.ApproveConnectionRequest(endpointId);
                    }
                    break;

                default:
                    if (!s_instance._pendingAuthCodes.Remove(endpointId))
                        logger.LogWarning(NBCTransport.kTag, "Couldn't find auth code for endpoint[" + endpointId + "]");
                    break;
            }

            logger.Log(NBCTransport.kTag, "Name: " + s_instance._endpointNames[endpointId] + " EndpointId:" + endpointId + " Status: " + s_instance._endpointStatuses[endpointId]);
            logger.Log(NBCTransport.kTag, "AuthDigits: " + authDigits + " AuthStatus: " + authStatus);

            s_instance.OnConnectingWithPeer?.InvokeOnMainThread(endpointId);
        }

        // Unity Transport Actions

        [AOT.MonoPInvokeCallback(typeof(OnConnectionEstablishedCallback))]
        private static void OnConnectionEstablishedDelegate(string endpointId)
        {
            if (s_instance == null) return;
            logger.Log(NBCTransport.kTag, "Established connection to endpoint " + endpointId + " with name " + s_instance.EndpointNames[endpointId] + " and transportId: " + s_instance._transportIds[endpointId]);

            s_instance.MainThreadInvokeOnTransportEvent(NetworkEvent.Connect,
                s_instance._transportIds[endpointId], default);

            s_instance._endpointStatuses[endpointId] = EndpointStatus.CONNECTED;
        }

        [AOT.MonoPInvokeCallback(typeof(OnConnectionDisconnectedCallback))]
        private static void OnConnectionDisconnectedDelegate(string endpointId)
        {
            if (s_instance == null) return;

            if (s_instance._transportIds.ContainsKey(endpointId))
            {
                s_instance.MainThreadInvokeOnTransportEvent(NetworkEvent.Disconnect,
                    s_instance._transportIds[endpointId], default);
            }

            s_instance.RemoveEndpointData(endpointId);
        }

        [AOT.MonoPInvokeCallback(typeof(OnPayloadReceivedCallback))]
        private static void OnPayloadReceivedDelegate(string endpointId, IntPtr dataPtr, int len)
        {
            if (s_instance == null) return;
            if (s_instance._transportIds.ContainsKey(endpointId))
            {
                byte[] data = new byte[len];
                Marshal.Copy(dataPtr, data, 0, len);
                s_instance.MainThreadInvokeOnTransportEvent(NetworkEvent.Data, s_instance._transportIds[endpointId],
                    new ArraySegment<byte>(data, 0, len));
            }
        }

        private void MainThreadInvokeOnTransportEvent(NetworkEvent eventType, ulong clientId, ArraySegment<byte> payload, float? receiveTime = null)
        {
            MainThreadExtensions.InvokeOnMainThread(() =>
            {
                if (receiveTime == null)
                    receiveTime = Time.realtimeSinceStartup;
                s_instance.InvokeOnTransportEvent(eventType, clientId, payload, (float)receiveTime);
            });
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

            logger.logEnabled = true;
        }

        public void ConfigureNickname(string nickname)
        {
            _configNickname = nickname;
        }

        public void ConfigureServiceId(string serviceId)
        {
            _configServiceId = serviceId;
        }

        public void ConfigureNetworkingStrategy(ConnectionStrategy strategy)
        {
            _configConnectionStrategy = strategy;
        }

        public override void Initialize(NetworkManager networkManager)
        {
            if (!PermissionsReady)
            {
                // Start permission flow
                StartCoroutine(RequestPermissions());
            }

            // NOW safe to initialize native adapter
            _sessionData = new SessionData(_configNickname, _configServiceId, _configConnectionType, _configLowPower, _configConnectionStrategy);

            NBC_Initialize();

            NBC_SetOnPeerFound(OnDiscoveryPeerFoundDelegate);
            NBC_SetOnPeerLost(OnDiscoveryPeerLostDelegate);
            NBC_SetOnConnectionInitiated(OnConnectionInitiatedDelegate);
            NBC_SetOnConnectionEstablished(OnConnectionEstablishedDelegate);
            NBC_SetOnConnectionDisconnected(OnConnectionDisconnectedDelegate);
            NBC_SetOnPayloadReceived(OnPayloadReceivedDelegate);
        }

        public override bool StartServer()
        {
            if (!PermissionsReady)
            {
                logger.LogError(NBCTransport.kTag,"Can't start transport, because necessary permissions haven't been granted by the user");
                StartCoroutine(RequestPermissions());
                return false;
            }

            if (AutoAdvertise)
                StartAdvertising();
            return true;
        }

        public override bool StartClient()
        {
            if (!PermissionsReady)
            {
                return false;
            }

            if (AutoBrowse)
                StartBrowsing();
            return true;
        }

        public override void Shutdown()
        {
            NBC_Shutdown();
            _sessionData = null;
            _isAdvertising = false;
            _isBrowsing = false;
        }

        #region Permission Management
        private IEnumerator RequestPermissions()
        {
#if UNITY_ANDROID //&& !UNITY_EDITOR
            var callbacks = new PermissionCallbacks();
            callbacks.PermissionDenied += (string _) => { ShowGoToSettingsDialog(); };
            Permission.RequestUserPermissions(NearbyPermissionDefinitions.Permissions.Select(perm => perm.Name).ToArray(), callbacks);
#endif
            yield return null;
        }

#if UNITY_ANDROID //&& !UNITY_EDITOR
        public void ShowGoToSettingsDialog()
        {
            // Prevent multiple dialogs if something loops
            if (_activeDialog != null)
                return;

            if (_permissionDialogPrefab == null)
            {
                logger.LogError(NBCTransport.kTag,"[Permissions] No permissionDialogPrefab assigned.");
                return;
            }

            // Instantiate the dialog
            _activeDialog = Instantiate(_permissionDialogPrefab, _permissionDialogParentTransform);

            // Get the controller on the instance
            var controller = _activeDialog.GetComponent<PermissionDialogController>();
            if (controller == null)
            {
                logger.LogError(NBCTransport.kTag,"[Permissions] PermissionDialogPrefab missing PermissionDialogController component.");
                return;
            }

            // Hook up button callbacks
            controller.OnOpenSettings = () =>
            {
                Debug.Log("[Permissions] Opening app settings...");
                OpenAppSettings();
                CloseDialog();
            };

            controller.OnCancel = () =>
            {
                Debug.Log("[Permissions] User canceled permission settings dialog.");
                CloseDialog();
            };
        }

        private void CloseDialog()
        {
            if (_activeDialog != null)
            {
                Destroy(_activeDialog);
                _activeDialog = null;
            }
        }

        public void OpenAppSettings()
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

                AndroidJavaClass uriClass = new AndroidJavaClass("android.net.Uri");
                AndroidJavaObject uri = uriClass.CallStatic<AndroidJavaObject>("fromParts",
                    "package",
                    currentActivity.Call<string>("getPackageName"),
                    null);

                AndroidJavaObject intent = new AndroidJavaObject(
                    "android.content.Intent",
                    "android.settings.APPLICATION_DETAILS_SETTINGS",
                    uri
                );

                currentActivity.Call("startActivity", intent);
            }
            Debug.Log("[Permissions] Cannot open settings in editor.");
        }

#endif

        #endregion

        // -------------------------------------------------------------------------------------
        // Public control
        // -------------------------------------------------------------------------------------

        public void StartAdvertising()
        {
            if (!_isAdvertising)
            {
                _endpointStatuses.Clear();
                logger.Log(NBCTransport.kTag, "[NBC] StartAdvertising()");
                NBC_StartAdvertising(Nickname, ServiceId, (int)TypeOfConnection, LowPower, (int)Strategy);
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
                _endpointNames.Clear();
                logger.Log(NBCTransport.kTag, "[NBC] StartDiscovery()");
                NBC_StartDiscovery(ServiceId, LowPower, (int)Strategy);
                _isBrowsing = true;
            }
        }

        public void StopBrowsing()
        {
            if (_isBrowsing)
            {
                NBC_StopDiscovery();
                _isBrowsing = false;
                _endpointNames.Clear();
            }
        }

        public void SendConnectionRequest(string endpointId)
        {
            // For Nearby, just initiate connection
            logger.Log(NBCTransport.kTag, $"[NBC] Send connection request to {endpointId}");
            NBC_RequestConnection(Nickname, endpointId);
            _endpointStatuses[endpointId] = EndpointStatus.REQUESTED;
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
            if (_transportIds.ContainsKey(transportId))
            {
                logger.Log(NBCTransport.kTag, "Sending " + data.Count + "bytes to " + _transportIds[transportId] + " with transportId: " + transportId);
                NBC_SendBytes(_transportIds[transportId], data.Array, data.Count);
            }
            else
            {
                logger.Log(NBCTransport.kTag, "Can't send " + data.Count + "bytes to endpoint with transportId: " + transportId + ". Key NOT persent in Transport Id Dictionary");
            }
        }

        public override ulong GetCurrentRtt(ulong transportId) => 0;

        public override void DisconnectLocalClient() { }
        public override void DisconnectRemoteClient(ulong transportId)
        {
            NBC_Disconnect(_transportIds[transportId]);
        }

        private void RemoveEndpointData(string endpointId)
        {
            _transportIds.Remove(endpointId);
            _endpointNames.Remove(endpointId);
            _endpointStatuses.Remove(endpointId);
            _pendingAuthCodes.Remove(endpointId);
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
        public event Action<string, string> OnAdvertiserReceivedConnectionRequest;
        public event Action<string, string> OnBrowserSentConnectionRequest;

        /// <summary>
        /// Invoked when the advertiser approves an incoming connection request.
        /// The first parameter is the connection request key in the dict.
        /// The second parameter is the name of the peer who sent the connection request.
        /// </summary>
        public event Action<string> OnAdvertiserApprovedConnectionRequest;
        public event Action<string> OnBrowserApprovedConnectionRequest;

        /// <summary>
        /// Invoked when initializes connection with a new peer. This event should be used only for notification purpose.
        /// The first parameter is the name of the connecting peer.
        /// </summary>
        public event Action<string> OnConnectingWithPeer;
    }
}
