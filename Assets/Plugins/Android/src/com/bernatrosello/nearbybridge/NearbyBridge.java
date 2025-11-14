// NearbyBridge.java
package com.bernatrosello.nearbybridge;

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
    private static native void nativeOnPayloadReceived(String endpointId, byte[] payload)
    private static native void nativeOnPeerFound(int endpointId, String name);
    private static native void nativeOnPeerLost(int endpointId);
    private static native void nativeOnConnectionRequested(int endpointId, String name);
    private static native void nativeOnConnectionEstablished(int endpointId);
    private static native void nativeOnConnectionDisconnected(int endpointId);
    private static native void nativeOnDataReceived(int endpointId, byte[] data);

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

    // ---------------- discovery / advertising ----------------
    public static void startDiscovery(String serviceId, ) {
        if (sClient == null) {
            Log.w(TAG, "startDiscovery: client null");
            return;
        }
        DiscoveryOptions options = new DiscoveryOptions.Builder().setStrategy(Strategy.P2P_STAR).build();
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

    public static void sendConnectionRequest() {
        if (sClient == null) {
            Log.w(TAG, "sendConnectionRequest: client null");
            return;
        }
        sClient.requestConnection()
    }

    // ---------------- connection lifecycle ----------------
    private static final EndpointDiscoveryCallback endpointDiscoveryCallback = new EndpointDiscoveryCallback() {
        @Override
        public void onEndpointFound(String endpointId, DiscoveredEndpointInfo info) {
            endpoints.put(endpointId, info.getEndpointName());
            // Map endpointId string to an integer id for native/csharp. Simple hash (make deterministic)
            int eid = Math.abs(endpointId.hashCode());
            nativeOnPeerFound(eid, info.getEndpointInfo(), info.getEndpointName(), info.getServiceId());
        }
        @Override
        public void onEndpointLost(String endpointId) {
            int eid = Math.abs(endpointId.hashCode());
            endpoints.remove(endpointId);
            nativeOnPeerLost(eid);
        }
    };

    private static final PayloadCallback payloadCallback = new PayloadCallback() {
        @Override
        public void onPayloadReceived(String endpointId, Payload payload) {
            if (payload.getType() == Payload.Type.BYTES) {
                byte[] b = payload.asBytes();
                int eid = Math.abs(endpointId.hashCode());
                nativeOnDataReceived(eid, b);
            } else {
                // streaming / file payloads can be supported here
                Log.e("Payload Type:" + payload.getType() + " unsupported by NearbyBridge");
            }
        }

        @Override
        public void onPayloadTransferUpdate(String endpointId, PayloadTransferUpdate update) {
            // optional: map to progress callback
        }
    };

    private static final ConnectionLifecycleCallback connectionLifecycleCallback = new ConnectionLifecycleCallback() {
        @Override
        public void onConnectionInitiated(String endpointId, ConnectionInfo info) {
            int eid = Math.abs(endpointId.hashCode());
            nativeOnConnectionRequested(eid, info.getEndpointName());
            // do NOT automatically accept — leave C# to decide. But your adapter previously auto-accepted.
            // For convenience, accept automatically here: sClient.acceptConnection(endpointId, payloadCallback);
        }

        @Override
        public void onConnectionResult(String endpointId, ConnectionResolution result) {
            int eid = Math.abs(endpointId.hashCode());
            if (result.getStatus().isSuccess()) {
                nativeOnConnectionEstablished(eid);
            } else {
                nativeOnConnectionDisconnected(eid);
            }
        }

        @Override
        public void onDisconnected(String endpointId) {
            int eid = Math.abs(endpointId.hashCode());
            nativeOnConnectionDisconnected(eid);
        }
    };

    // ---------------- connection operations (called from native) ----------------
    public static void acceptConnection(int endpointIntId) {
        // we used hashCode() for ID mapping — we need to map back to endpointId string.
        // This simple implementation scans endpoints map to find matching hash. Not ideal for collisions.
        String ep = findEndpointByHash(endpointIntId);
        if (ep != null && sClient != null) {
            sClient.acceptConnection(ep, payloadCallback);
        } else
        {
            Log.w("Failed to accept connection from [" + endpoints[endpointIntId]"]");
        }
    }

    public static void rejectConnection(int endpointIntId) {
        String ep = findEndpointByHash(endpointIntId);
        if (ep != null && sClient != null) {
            sClient.rejectConnection(ep);
        }
    }

    public static void disconnect(int endpointIntId) {
        String ep = findEndpointByHash(endpointIntId);
        if (ep != null && sClient != null) {
            sClient.disconnectFromEndpoint(ep);
        }
    }

    public static void sendBytes(int endpointIntId, byte[] data) {
        String ep = findEndpointByHash(endpointIntId);
        if (ep != null && sClient != null) {
            sClient.sendPayload(ep, Payload.fromBytes(data));
        } else {
            Log.w(TAG, "sendBytes: endpoint not found");
        }
    }

    private static Strategy IntToStrategy(int strategyEnumVal) {
        switch(strategyEnumVal) {
            case 1:
                return Strategy.P2P_POINT_TO_POINT;

            case 2:
                return Strategy.P2P_STAR;

            case 0:
            default:
                return Strategy.P2P_CLUSTER;
        }
    }
}
