// SPDX-FileCopyrightText: Copyright 2024 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Yuchen Zhang <yuchenz27@outlook.com>
// SPDX-FileContributor: Botao Amber Hu <botao@reality.design>
// SPDX-License-Identifier: MIT

using UnityEngine;
using Unity.Netcode;

public class RttCounter : NetworkBehaviour
{
    public float Rtt => _rtt;

    private float _rtt = 0f;
    private Unity.Netcode.RpcParams myparams;

    private void Update()
    {
        if (IsSpawned && !IsServer)
        {
            RequestRttServerRpc(Time.time);
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestRttServerRpc(float clientTimestamp, RpcParams rpcParams = default)
    {
        RespondRttClientRpc(clientTimestamp, RpcTarget.Single(rpcParams.Receive.SenderClientId, RpcTargetUse.Temp));
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void RespondRttClientRpc(float clientTimestamp, RpcParams rpcParams = default)
    {
        _rtt = (Time.time - clientTimestamp) * 1000f;
    }
}
