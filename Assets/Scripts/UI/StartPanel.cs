// SPDX-FileCopyrightText: Copyright 2024 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Yuchen Zhang <yuchenz27@outlook.com>
// SPDX-FileContributor: Botao Amber Hu <botao@reality.design>
// SPDX-License-Identifier: MIT

using UnityEngine;
using UnityEngine.UI;
using Netcode.Transports.NearbyConnections;

public class StartPanel : MonoBehaviour
{
    [SerializeField]
    private Toggle _autoAdvertiseToggle;
    [SerializeField]
    private Toggle _autoApproveToggle;
    [SerializeField]
    private Toggle _autoBrowseToggle;
    [SerializeField]
    private Toggle _autoSendRequestToggle;

    public void Start()
    {
        _autoAdvertiseToggle.SetIsOnWithoutNotify(NBCTransport.Instance.AutoAdvertise);
        _autoApproveToggle.SetIsOnWithoutNotify(NBCTransport.Instance.AutoApproveConnectionRequest);
        _autoBrowseToggle.SetIsOnWithoutNotify(NBCTransport.Instance.AutoBrowse);
        _autoSendRequestToggle.SetIsOnWithoutNotify(NBCTransport.Instance.AutoSendConnectionRequest);
    }
    
    public void OnNicknameChanged(string nickname)
    {
        NBCTransport.Instance.ConfigureNickname(nickname);

        Debug.Log($"Nickname configured to {nickname}");
    }

    public void OnAutoAdvertiseToggled(bool value)
    {
        NBCTransport.Instance.AutoAdvertise = value;

        Debug.Log($"AutoAdvertise changed to {value}");
    }

    public void OnAutoApproveConnectionRequestToggled(bool value)
    {
        NBCTransport.Instance.AutoApproveConnectionRequest = value;

        Debug.Log($"AutoApproveConnectionRequest changed to {value}");
    }

    public void OnAutoBrowseToggled(bool value)
    {
        NBCTransport.Instance.AutoBrowse = value;

        Debug.Log($"AutoBrowse changed to {value}");
    }

    public void OnAutoSendConnectionRequestToggled(bool value)
    {
        NBCTransport.Instance.AutoSendConnectionRequest = value;

        Debug.Log($"AutoSendConnectionRequest changed to {value}");
    }
}
