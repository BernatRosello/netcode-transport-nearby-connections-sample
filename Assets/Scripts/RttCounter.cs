using UnityEngine;
using Unity.Netcode;
using Unity.Collections;
using System.Collections.Generic;

public class RttCounter : NetworkBehaviour
{
    // ----- Exposed values (ms) -----
    public float RpcRttMs => _rpcRtt * 1000f;
    public float MsgRttMs => _msgRtt * 1000f;
    public float ClockDeltaMs => _clockDelta * 1000f;   // client clock vs host clock

    // ----- EMA values -----
    private float _rpcRtt;
    private float _msgRtt;
    private float _clockDelta;

    // Ping counters
    private int _counter;
    private float _tickTimer;

    // Send timestamps
    private readonly Dictionary<int, float> _rpcSendTimes = new();
    private readonly Dictionary<int, float> _msgSendTimes = new();

    // EMA smoothing (0.1–0.3 recommended)
    private const float Alpha = 0.2f;

    // Named message IDs
    private const string MsgPing = "msgping";
    private const string MsgPong = "msgpong";

    // ---------------------------------------------------------------------
    // LIFECYCLE
    // ---------------------------------------------------------------------

     public override void OnNetworkSpawn()
    {
        // Server registers handler for lightweight ping
        if (IsServer)
        {
            NetworkManager.CustomMessagingManager.RegisterNamedMessageHandler(
                MsgPing,
                OnReceiveMsgPing
            );
        }

        // Client registers pong handler
        if (IsClient)
        {
            NetworkManager.CustomMessagingManager.RegisterNamedMessageHandler(
                MsgPong,
                OnReceiveMsgPong
            );
        }
    }
    
    public override void OnNetworkDespawn()
    {
        if (NetworkManager.Singleton.CustomMessagingManager != null)
        {
            // De-register when the associated NetworkObject is despawned.
            if (IsServer) NetworkManager.CustomMessagingManager.UnregisterNamedMessageHandler(MsgPing);
            if (IsClient) NetworkManager.CustomMessagingManager.UnregisterNamedMessageHandler(MsgPong);
        }

        _rpcSendTimes.Clear();
        _msgSendTimes.Clear();
    }

    // ---------------------------------------------------------------------
    // UPDATE LOOP
    // ---------------------------------------------------------------------

    private void Update()
    {
        // Only clients measure RTT to host
        if (!IsClient || !IsSpawned || IsHost) return;

        // Send one RTT batch per ~0.5s “network tick”
        _tickTimer += Time.deltaTime;
        if (_tickTimer < 0.5f) return;
        _tickTimer = 0f;

        _counter++;

        // --- RPC RTT ---
        _rpcSendTimes[_counter] = Time.realtimeSinceStartup;
        PingRpc(_counter);

        // --- Lightweight Message RTT ---
        _msgSendTimes[_counter] = Time.realtimeSinceStartup;
        SendMsgPing(_counter);

        // --- Clock delta (one way, NOT RTT) ---
        RequestClockDeltaServerRpc(NetworkManager.LocalTime.TimeAsFloat);
    }

    // ---------------------------------------------------------------------
    // 1) RPC RTT
    // ---------------------------------------------------------------------

    [Rpc(SendTo.Server)]
    private void PingRpc(int id, RpcParams rpcParams = default)
    {
        // Respond directly back to originating client
        PongRpc(id, RpcTarget.Single(rpcParams.Receive.SenderClientId, RpcTargetUse.Temp));
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void PongRpc(int id, RpcParams _ = default)
    {
        if (!_rpcSendTimes.TryGetValue(id, out float sent)) return;

        float rtt = Time.realtimeSinceStartup - sent;
        _rpcRtt = (_rpcRtt == 0f) ? rtt : Mathf.Lerp(_rpcRtt, rtt, Alpha);
        _rpcSendTimes.Remove(id);

        // Debug UI
        // Debug.Log($"RPC RTT: {_rpcRtt * 1000f:0.0} ms");
    }

    // ---------------------------------------------------------------------
    // 2) LIGHTWEIGHT MESSAGE RTT
    // ---------------------------------------------------------------------

    private void SendMsgPing(int id)
    {
        using var writer = new FastBufferWriter(sizeof(int), Allocator.Temp);
        if(!writer.TryBeginWrite(sizeof(int)))
        {
            throw new System.OverflowException("Not enough space in the buffer");
        }
        writer.WriteValue(id);

        NetworkManager.CustomMessagingManager.SendNamedMessage(
            MsgPing,
            NetworkManager.ServerClientId,        // always host
            writer,
            NetworkDelivery.Unreliable
        );
    }

    private void OnReceiveMsgPing(ulong sender, FastBufferReader reader)
    {
        if(!reader.TryBeginRead(sizeof(int)))
        {
            throw new System.OverflowException("Not enough space in the reader buffer");
        }
        reader.ReadValue(out int id);

        using var writer = new FastBufferWriter(sizeof(int), Allocator.Temp);
        if(!writer.TryBeginWrite(sizeof(int)))
        {
            throw new System.OverflowException("Not enough space in the writer buffer");
        }
        writer.WriteValue(id);

        NetworkManager.CustomMessagingManager.SendNamedMessage(
            MsgPong,
            sender,
            writer,
            NetworkDelivery.Unreliable
        );
    }

    private void OnReceiveMsgPong(ulong sender, FastBufferReader reader)
    {
        if(!reader.TryBeginRead(sizeof(int)))
        {
            throw new System.OverflowException("Not enough space in the reader buffer");
        }
        reader.ReadValue(out int id);

        if (!_msgSendTimes.TryGetValue(id, out float sent)) return;

        float rtt = Time.realtimeSinceStartup - sent;
        _msgRtt = (_msgRtt == 0f) ? rtt : Mathf.Lerp(_msgRtt, rtt, Alpha);

        _msgSendTimes.Remove(id);

        // Debug UI
        // Debug.Log($"Message RTT: {_msgRtt * 1000f:0.0} ms");
    }

    // ---------------------------------------------------------------------
    // 3) CLOCK DELTA (one way, not RTT)
    // ---------------------------------------------------------------------

    [Rpc(SendTo.Server)]
    private void RequestClockDeltaServerRpc(float clientTime, RpcParams rpcParams = default)
    {
        RespondClockDeltaClientRpc(clientTime, NetworkManager.ServerTime.TimeAsFloat,
            RpcTarget.Single(rpcParams.Receive.SenderClientId, RpcTargetUse.Temp));
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void RespondClockDeltaClientRpc(float clientSent, float hostTime, RpcParams _ = default)
    {
        float now = NetworkManager.LocalTime.TimeAsFloat;
        float oneWay = (now - clientSent) * 0.5f;     // estimate

        float delta = (hostTime + oneWay) - now;
        _clockDelta = (_clockDelta == 0f) ? delta : Mathf.Lerp(_clockDelta, delta, Alpha);
    }
}
