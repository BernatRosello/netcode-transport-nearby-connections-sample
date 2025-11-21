using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using Unity.Collections;

public class RttCounter : NetworkBehaviour
{
    public float Rtt => _rtt * 1000f;
    public float RpcPing => _ping * 1000f;
    public float MessagePing => _messagePing * 1000f;   // NEW

    private float _ping = 0f;               // RPC exponential moving average RTT
    private float _messagePing = 0f;        // NEW: lightweight message RTT EMA

    private int _pingCount = 0;
    private float _timeAccumulator = 0f;

    private float _rtt = 0f;

    // Track send times for each ping
    private Dictionary<int, float> _sendTimes = new Dictionary<int, float>();
    
    // NEW: track lightweight message ping send times
    private Dictionary<int, float> _msgSendTimes = new Dictionary<int, float>();

    // Smoothing factor for exponential moving average (0.1–0.3 recommended)
    private const float alpha = 0.2f;

    private void Start()
    {
        // Server registers handler for lightweight ping
        if (IsServer)
        {
            NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(
                "msgping",
                OnReceiveMsgPing
            );
        }

        // Client registers pong handler
        if (IsClient)
        {
            NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(
                "msgpong",
                OnReceiveMsgPong
            );
        }
    }

    private void Update()
    {
        if (IsSpawned && IsClient)
        {
            _rtt = (NetworkManager.Singleton.LocalTime - NetworkManager.Singleton.ServerTime).TimeAsFloat;

            _timeAccumulator += Time.deltaTime;

            // ------------------------------
            // Existing RPC ping (unchanged)
            // ------------------------------
            if (_timeAccumulator > 0.5f)
            {
                _timeAccumulator = 0f;
                _pingCount++;

                _sendTimes[_pingCount] = Time.realtimeSinceStartup;
                _msgSendTimes[_pingCount] = Time.realtimeSinceStartup;

                PingRpc(_pingCount, default);
                SendMsgPing(_pingCount);
            }
        }
    }

    // =====================================================
    //  RPC PING (UNCHANGED)
    // =====================================================

    [Rpc(SendTo.Server)]
    public void PingRpc(int pingCount, RpcParams rpcParams)
    {
        PongRpc(
            pingCount,
            "PONG!",
            RpcTarget.Single(rpcParams.Receive.SenderClientId, RpcTargetUse.Temp)
        );
    }

    [Rpc(SendTo.SpecifiedInParams)]
    void PongRpc(int pingCount, string message, RpcParams rpcParams)
    {
        if (_sendTimes.TryGetValue(pingCount, out float sendTime))
        {
            float rtt = Time.realtimeSinceStartup - sendTime;

            _ping = (_ping == 0f) ? rtt : alpha * rtt + (1f - alpha) * _ping;

            Debug.Log($"RPC RTT: {rtt * 1000f:0.0} ms | Avg {_ping * 1000f:0.0} ms");

            _sendTimes.Remove(pingCount);
        }
        else
        {
            Debug.LogWarning($"Received pong for unknown RPC ping {pingCount}");
        }
    }

    // =====================================================
    //  NEW: LIGHTWEIGHT MESSAGE PING
    // =====================================================

    /// <summary>
    /// Client → Server: send unmanaged unreliable ping
    /// </summary>
    private void SendMsgPing(int id)
    {
        var manager = NetworkManager.Singleton.CustomMessagingManager;

        using (var writer = new FastBufferWriter(sizeof(int), Allocator.Temp))
        {
            if (writer.TryBeginWriteValue(id))
            {
                manager.SendNamedMessage(
                    "msgping",
                    NetworkManager.ServerClientId,
                    writer,
                    NetworkDelivery.Unreliable
                );
            }
            else
            {
                Debug.LogError($"Failed to write into unmanaged ping var");
            }
        }
    }

    /// <summary>
    /// Server receives messagePing. Immediately sends msgpong back.
    /// </summary>
    private void OnReceiveMsgPing(ulong sender, FastBufferReader reader)
    {
        reader.ReadValue(out int id);

        var manager = NetworkManager.Singleton.CustomMessagingManager;

        using (var writer = new FastBufferWriter(sizeof(int), Allocator.Temp))
        {
            
            if (writer.TryBeginWriteValue(id))
            {
                manager.SendNamedMessage(
                    "msgpong",
                    sender,
                    writer,
                    NetworkDelivery.Unreliable
                );
            }
            else
            {
                Debug.LogError($"Failed to write into unmanaged ping var");
            }
        }
    }

    /// <summary>
    /// Client receives msgpong and computes lightweight RTT
    /// </summary>
    private void OnReceiveMsgPong(ulong sender, FastBufferReader reader)
    {
        reader.ReadValue(out int id);

        if (_msgSendTimes.TryGetValue(id, out float sendTime))
        {
            float rtt = Time.realtimeSinceStartup - sendTime;

            _messagePing = (_messagePing == 0f)
                ? rtt
                : alpha * rtt + (1f - alpha) * _messagePing;

            Debug.Log($"Message RTT: {rtt * 1000f:0.0} ms | Avg {_messagePing * 1000f:0.0} ms");

            _msgSendTimes.Remove(id);
        }
    }
}
