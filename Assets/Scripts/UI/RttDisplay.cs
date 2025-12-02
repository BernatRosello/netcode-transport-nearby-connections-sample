// SPDX-FileCopyrightText: Copyright 2024 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Yuchen Zhang <yuchenz27@outlook.com>
// SPDX-FileContributor: Botao Amber Hu <botao@reality.design>
// SPDX-License-Identifier: MIT

using UnityEngine;
using TMPro;
using Unity.Netcode;
using Unity.Mathematics;

public class RttDisplay : MonoBehaviour
{
    [SerializeField] private RttCounter _rttCounter;

    [SerializeField] private TMP_Text _networkTickRateText;
    [SerializeField] private TMP_Text _clockDelta;
    [SerializeField] private TMP_Text _rpcPingText;
    [SerializeField] private TMP_Text _msgPingText;

    private void Start()
    {
    }

    private void Update()
    {
        _networkTickRateText.text = $"tick rate: {NetworkManager.Singleton.NetworkTickSystem.TickRate} tps ({1000.0/NetworkManager.Singleton.NetworkTickSystem.TickRate:F4} ms)";
        _clockDelta.text = $"clock delta: {_rttCounter.ClockDeltaMs:F4} ms";
        _rpcPingText.text = $"rpc ping: {_rttCounter.RpcRttMs/2.0:F4} ms";
        _msgPingText.text = $"msg ping: {_rttCounter.MsgRttMs/2.0:F4} ms";
    }
}
