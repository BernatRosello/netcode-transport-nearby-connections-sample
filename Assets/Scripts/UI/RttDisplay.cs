// SPDX-FileCopyrightText: Copyright 2024 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Yuchen Zhang <yuchenz27@outlook.com>
// SPDX-FileContributor: Botao Amber Hu <botao@reality.design>
// SPDX-License-Identifier: MIT

using UnityEngine;
using TMPro;

public class RttDisplay : MonoBehaviour
{
    [SerializeField] private RttCounter _rttCounter;

    [SerializeField] private TMP_Text _rttText;
    [SerializeField] private TMP_Text _rpcPingText;
    [SerializeField] private TMP_Text _msgPingText;

    private void Start()
    {
    }

    private void Update()
    {
        _rttText.text = $"server/local time diff: {_rttCounter.Rtt:F4} ms";
        _rpcPingText.text = $"rpc ping: {_rttCounter.RpcPing:F4} ms";
        _msgPingText.text = $"msg ping: {_rttCounter.MessagePing:F4} ms";
    }
}
