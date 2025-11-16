// SPDX-FileCopyrightText: Copyright 2024 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Yuchen Zhang <yuchenz27@outlook.com>
// SPDX-FileContributor: Botao Amber Hu <botao@reality.design>
// SPDX-License-Identifier: MIT

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Netcode.Transports.NearbyConnections;

public class ConnectionRequestSlot : EndpointGUISlot
{
    [SerializeField] private TMP_Text _nickname;
    [SerializeField] private TMP_Text _authCode;

    [SerializeField] private Button _approveButton;

    public void Init(string connectionRequestKey, string nickname, string authCode)
    {
        _nickname.text = nickname;
        _authCode.text = authCode;
        _approveButton.onClick.AddListener(() =>
        {
            NBCTransport.Instance.ApproveConnectionRequest(connectionRequestKey);
        });
    }
}
