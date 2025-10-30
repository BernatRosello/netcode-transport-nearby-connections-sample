// SPDX-FileCopyrightText: Copyright 2024 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Yuchen Zhang <yuchenz27@outlook.com>
// SPDX-FileContributor: Botao Amber Hu <botao@reality.design>
// SPDX-License-Identifier: MIT

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using Netcode.Transports.NearbyConnections;

public class ClientPanel : MonoBehaviour
{
    [SerializeField] private TMP_Text _browsingStatusText;

    [SerializeField] private Button _startBrowsingButton;

    [SerializeField] private Button _stopBrowsingButton;

    private NBCTransport _nbcTransport;

    private const string BROWSING_STATUS_PFEFIX = "Browsing Status: ";

    private void Start()
    {
        _nbcTransport = NBCTransport.Instance;
    }

    private void Update()
    {
        // The client can no longer browse after connected to the network
        if (NetworkManager.Singleton.IsConnectedClient)
        {
            _browsingStatusText.text = BROWSING_STATUS_PFEFIX + "Not Browsing";
            _startBrowsingButton.interactable = false;
            _stopBrowsingButton.interactable = false;
            return;
        }

        if (_nbcTransport.IsBrowsing)
        {
            _browsingStatusText.text = BROWSING_STATUS_PFEFIX + "Browsing";
            _startBrowsingButton.interactable = false;
            _stopBrowsingButton.interactable = true;
        }
        else
        {
            _browsingStatusText.text = BROWSING_STATUS_PFEFIX + "Not Browsing";
            _startBrowsingButton.interactable = true;
            _stopBrowsingButton.interactable = false;
        }
    }
}
