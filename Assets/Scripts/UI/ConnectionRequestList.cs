// SPDX-FileCopyrightText: Copyright 2024 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Yuchen Zhang <yuchenz27@outlook.com>
// SPDX-FileContributor: Botao Amber Hu <botao@reality.design>
// SPDX-License-Identifier: MIT

using System.Collections.Generic;
using UnityEngine;
using Netcode.Transports.NearbyConnections;
using Unity.Netcode;

public class ConnectionRequestList : MonoBehaviour
{
    [SerializeField] private ConnectionRequestSlot _connectionRequestSlotPrefab;

    [SerializeField] private RectTransform _root;

    private NBCTransport _nbcTransport;

    public List<ConnectionRequestSlot> ConnectionRequestSlotList => _connectionRequestSlotList;

    private readonly List<ConnectionRequestSlot> _connectionRequestSlotList = new();

    private void Start()
    {
        // Get the reference of the nbc transport
        _nbcTransport = NBCTransport.Instance;

        _nbcTransport.OnBrowserLostPeer += (string _, string _) => UpdateConnectionRequestList();
        _nbcTransport.OnAdvertiserReceivedConnectionRequest += (string _, string _) => UpdateConnectionRequestList();
        NetworkManager.Singleton.OnClientConnectedCallback += (ulong _) => UpdateConnectionRequestList();
        UpdateConnectionRequestList();
    }

    private void UpdateConnectionRequestList()
    {
        foreach (var slot in _connectionRequestSlotList)
        {
            Destroy(slot.gameObject);
        }
        _connectionRequestSlotList.Clear();

        Debug.Log("Updating Connection Request List");
        foreach (var endpoint in _nbcTransport.PendingRequestEndpoints)
        {
            Debug.Log("Adding pending request endpoint: " + endpoint.name);
            var connectionRequestSlotInstance = Instantiate(_connectionRequestSlotPrefab);
            connectionRequestSlotInstance.Init(endpoint.id, endpoint.name, endpoint.authCode);
            connectionRequestSlotInstance.transform.SetParent(_root, false);

            _connectionRequestSlotList.Add(connectionRequestSlotInstance);
        }
    }
}
