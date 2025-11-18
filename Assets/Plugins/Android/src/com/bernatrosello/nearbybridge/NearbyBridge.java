// NearbyBridge.java
package com.bernatrosello.nearbybridge;

import java.util.List;
import java.util.concurrent.ConcurrentHashMap;

import com.google.android.gms.nearby.Nearby;
import com.google.android.gms.nearby.connection.*;

import android.app.Activity;
import android.util.Log;

public class NearbyBridge {
    private static final String TAG = "NearbyBridge";
    private static Activity sActivity;
    private static ConnectionsClient sClient;
    
    // Native callbacks implemented in jni_nc_unity.cpp
    private static native void nativeOnPeerFound(String endpointId, String name);
    private static native void nativeOnPeerLost(String endpointId);
    private static native void nativeOnConnectionInitiated(String endpointId, String name, String authDigits, int authStatus);
    private static native void nativeOnConnectionEstablished(String endpointId);
    private static native void nativeOnConnectionDisconnected(String endpointId);
    private static native void nativeOnPayloadReceived(String endpointId, byte[] data);

    static {
        // ensure the library name matches the built .so (strip lib prefix and .so)
        // if you build libnc_unity.so -> loadLibrary("nc_unity")
        System.loadLibrary("nc_unity");
    }

    // Called from native to initialize with Unity activity.
    public static void initialize(Activity unityActivity) {
        sActivity = unityActivity;
        if (sClient == null && sActivity != null) {
            sClient = Nearby.getConnectionsClient(sActivity);
// DEBUG
        } else {
            Log.w(TAG, "Could not fetch Nearby Connections Client: " + sClient != null ? "Because client was already fetched (no need to fetch again)." : "Because the passed Activity is null.");
        }
    }

    // convenience overload used by native that passes serviceId string
    public static void initialize() {
        // called from native with serviceId as parameter.
        // We need an Activity: get it from UnityPlayer.currentActivity if not provided by Unity
        try {
            Class<?> unityPlayer = Class.forName("com.unity3d.player.UnityPlayer");
            Activity activity = (Activity) unityPlayer.getField("currentActivity").get(null);
            initialize(activity);
        } catch (Exception e) {
            Log.w(TAG, "Could not locate UnityPlayer.currentActivity: " + e);
        }
    }

    public static void shutdown() {
        if (sClient != null) {
            sClient.stopAllEndpoints();
            sClient.stopAdvertising();
            sClient.stopDiscovery();
            sClient = null;
        }
    }

    // ---------------- Method interface ----------------
    public static void startDiscovery(String serviceId, boolean lowPower, int strategy) {
        if (sClient == null) {
            Log.w(TAG, "startDiscovery: client null");
            return;
        }
        DiscoveryOptions options = new DiscoveryOptions.Builder()
        .setLowPower(lowPower)
        .setStrategy(IntToStrategy(strategy))
        .build();
        sClient.startDiscovery(serviceId, endpointDiscoveryCallback, options)
                .addOnSuccessListener(unused -> Log.d(TAG, "Discovery started"))
                .addOnFailureListener(e -> Log.e(TAG, "Discovery failed", e));
    }

    public static void stopDiscovery() {
        if (sClient != null) sClient.stopDiscovery();
    }

    public static void startAdvertising(String endpointName, String serviceId, int connectionType, boolean lowPower, int strategy) {
        if (sClient == null) {
            Log.w(TAG, "startAdvertising: client null");
            return;
        }
        AdvertisingOptions options = new AdvertisingOptions.Builder()
            .setConnectionType(connectionType)
            .setLowPower(lowPower)
            .setStrategy(IntToStrategy(strategy))
            .build();
        sClient.startAdvertising(endpointName, serviceId, connectionLifecycleCallback, options)
                .addOnSuccessListener(unused -> Log.d(TAG, "Advertising started"))
                .addOnFailureListener(e -> Log.e(TAG, "Advertising failed", e));
    }

    public static void stopAdvertising() {
        if (sClient != null) sClient.stopAdvertising();
    }
    
    public static void requestConnection(String name, String endpointId) {
        if (sClient == null) {
            Log.w(TAG, "requestConnection: client null");
            return;
        }
        sClient.requestConnection(name, endpointId, connectionLifecycleCallback);
    }

    public static void acceptConnection(String endpointId) {
        // we used hashCode() for ID mapping — we need to map back to endpointId string.
        // This simple implementation scans endpoints map to find matching hash. Not ideal for collisions.
        if (sClient != null) {
            Log.d(TAG, "Accepting connection to endpoint["+endpointId+"]");
            sClient.acceptConnection(endpointId, payloadCallback);
        } else
        {
            Log.w(TAG, "Failed to accept connection from [" + endpointId+ "]");
        }
    }

    public static void rejectConnection(String endpointId) {
        if (sClient != null) {
            Log.d(TAG, "Rejecting connection to endpoint["+endpointId+"]");
            sClient.rejectConnection(endpointId);
        }
    }

    public static void disconnect(String endpointId) {
        if (sClient != null) {
            Log.d(TAG, "Disconnecting from endpoint["+endpointId+"]");
            sClient.disconnectFromEndpoint(endpointId);
        }
    }

    public static void sendBytes(String endpointId, byte[] data) {
        if (endpointId != null && sClient != null) {
            sClient.sendPayload(endpointId, Payload.fromBytes(data));
        } else {
            Log.w(TAG, "sendBytes: endpoint not found");
        }
    }

    public static void sendBytes(List<String> endpointIds, byte[] data) {
        if (endpointIds != null && sClient != null) {
            sClient.sendPayload(endpointIds, Payload.fromBytes(data));
        } else {
            Log.w(TAG, "sendBytes: endpoint not found");
        }
    }

    // ---------------- Callbacks ----------------
    private static final EndpointDiscoveryCallback endpointDiscoveryCallback = new EndpointDiscoveryCallback() {
        @Override
        public void onEndpointFound(String endpointId, DiscoveredEndpointInfo info) {
            nativeOnPeerFound(endpointId, info.getEndpointName());
        }
        @Override
        public void onEndpointLost(String endpointId) {
            nativeOnPeerLost(endpointId);
        }
    };

    private static final PayloadCallback payloadCallback = new PayloadCallback() {
        @Override
        public void onPayloadReceived(String endpointId, Payload payload) {
            Log.d(TAG, "Endpoint["+endpointId+"]<= Payload["+ payload.getId() +"]");
            if (payload.getType() == Payload.Type.BYTES) {
                byte[] b = payload.asBytes();
                nativeOnPayloadReceived(endpointId, b);
            } else {
                // streaming / file payloads can be supported here
                Log.e(TAG, "Payload["+payload.getId()+"] Type:" + payload.getType() + " unsupported by NearbyBridge");
            }
        }

        @Override
        public void onPayloadTransferUpdate(String endpointId, PayloadTransferUpdate payloadUpdate) {
            Log.d(TAG, "Endpoint["+endpointId+"]<= Payload[" + payloadUpdate.getPayloadId() + "] progress: " + payloadUpdate.getBytesTransferred() + "/" + payloadUpdate.getTotalBytes());
            // TODO: map to native progress callback ?
        }
    };

    private static final ConnectionLifecycleCallback connectionLifecycleCallback = new ConnectionLifecycleCallback() {
        @Override
        public void onBandwidthChanged(String endpointId, BandwidthInfo bandwidthInfo) {
            Log.d(TAG, "Bandwith to endpoint[" + endpointId + "] changed to: " + bandwidthInfo.getQuality());
        }
        
        @Override
        public void onConnectionInitiated(String endpointId, ConnectionInfo info) {
            // This is where connection negotation starts on both requesting and requested endpoints
            Log.d(TAG, "Establishing encrypted channel between local endpoint and endpoint[" + endpointId + "]");
            /*new AlertDialog.Builder(context)
                .setTitle("Accept connection to " + info.getEndpointName())
                .setMessage("Confirm the code matches on both devices: " + info.getAuthenticationDigits())
                .setPositiveButton(
                    "Accept",
                    (DialogInterface dialog, int which) ->
                        // The user confirmed, so we can accept the connection.
                        Nearby.getConnectionsClient(context)
                            .acceptConnection(endpointId, payloadCallback))
                .setNegativeButton(
                    android.R.string.cancel,
                    (DialogInterface dialog, int which) ->
                        // The user canceled, so we should reject the connection.
                        Nearby.getConnectionsClient(context).rejectConnection(endpointId))
                .setIcon(android.R.drawable.ic_dialog_alert)
                .show();*/
            nativeOnConnectionInitiated(endpointId, info.getEndpointName(), info.getAuthenticationDigits(), info.getAuthenticationStatus());
        }

        @Override
        public void onConnectionResult(String endpointId, ConnectionResolution resolution) {
            if (resolution.getStatus().isSuccess()) {
                Log.d(TAG, "Established connection with endpoint[" + endpointId + "]");
                nativeOnConnectionEstablished(endpointId);
            } else {
                // TODO: HANDLE RESOLUTION OF CONNECTION, PERHAPS VERIFICATION ETC.
                nativeOnConnectionDisconnected(endpointId);
            }
        }

        @Override
        public void onDisconnected(String endpointId) {
            nativeOnConnectionDisconnected(endpointId);
        }
    };

    // ---------------- Utility Functions ----------------
    
    private static Strategy IntToStrategy(int strategyEnumVal) {
        switch(strategyEnumVal) {
            case 0:
            default:
                return Strategy.P2P_CLUSTER;
                
            case 1:
                return Strategy.P2P_POINT_TO_POINT;

            case 2:
                return Strategy.P2P_STAR;
        }
    }
}
