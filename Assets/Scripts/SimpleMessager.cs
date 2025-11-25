// SPDX-FileCopyrightText: Copyright 2024 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Yuchen Zhang <yuchenz27@outlook.com>
// SPDX-FileContributor: Botao Amber Hu <botao@reality.design>
// SPDX-License-Identifier: MIT

using UnityEngine;
using Unity.Netcode;

public class SimpleMessager : NetworkBehaviour
{
    public override void OnNetworkSpawn()
    {
        Debug.Log("[SimpleMessager] OnNetworkSpawn");
    }

    private void Update()
    {
        if (IsSpawned && IsServer)
        {
            HowAreYouClientRpc();
        }
    }

    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    private void HowAreYouClientRpc()
    {
        if (!IsServer)
        {
            Debug.Log($"[SimpleMessager] How are you? {Time.time}");
            IAmFineThankYouServerRpc();
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void IAmFineThankYouServerRpc(RpcParams rpcParams = default)
    {
        Debug.Log($"[SimpleMessager] I am fine thank you! from clientId {rpcParams.Receive.SenderClientId} {Time.time}");
    } 
}
