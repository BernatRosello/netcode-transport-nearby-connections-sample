// SPDX-FileCopyrightText: Copyright 2024 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Yuchen Zhang <yuchenz27@outlook.com>
// SPDX-FileContributor: Botao Amber Hu <botao@reality.design>
// SPDX-License-Identifier: MIT

using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Netcode.Transports.NearbyConnections;

public class NearbyHostList : MonoBehaviour
{
    [SerializeField] private NearbyHostSlot _nearbyHostSlotPrefab;
    [SerializeField] private ConnectionRequestSlot _connectionRequestSlotPrefab;

    [SerializeField] private RectTransform _root;

    private NBCTransport _nbcTransport;

    public List<EndpointGUISlot> NearbyHostSlotList => _nearbyHostSlotList;

    private readonly List<EndpointGUISlot> _nearbyHostSlotList = new();

    private void Start()
    {
        // Get the reference of the nbc transport
        _nbcTransport = NBCTransport.Instance;

        _nbcTransport.OnBrowserFoundPeer += (string _,string _) => UpdateNearbyHostList() ;
        _nbcTransport.OnLostPeer +=  (string _, string _) => UpdateNearbyHostList() ;
        _nbcTransport.OnBrowserSentConnectionRequest += (string _, string _) => UpdateNearbyHostList();
        NetworkManager.Singleton.OnClientConnectedCallback += (ulong _) => UpdateNearbyHostList();
        UpdateNearbyHostList();
    }

    private void UpdateNearbyHostList()
    {
        // We destroy and instantiate every connection request slot in every frame.
        // This is wasteful and unnecessary. But it is less error-prone.
        // You can register callbacks instead.
        foreach (var slot in _nearbyHostSlotList)
        {
            Destroy(slot.gameObject);
        }
        _nearbyHostSlotList.Clear();

        foreach (var endpoint in _nbcTransport.FoundEndpoints)
        {
            var nearbyHostSlotInstance = Instantiate(_nearbyHostSlotPrefab);
            nearbyHostSlotInstance.Init(endpoint.id, endpoint.name);
            nearbyHostSlotInstance.transform.SetParent(_root, false);

            _nearbyHostSlotList.Add(nearbyHostSlotInstance);
        }

        Debug.Log("Updating Nearby Host List");
        foreach (var endpoint in _nbcTransport.PendingRequestEndpoints)
        {
            Debug.Log("Adding pending request endpoint: " + endpoint.name);
            var nearbyHostSlotInstance = Instantiate(_connectionRequestSlotPrefab);
            nearbyHostSlotInstance.Init(endpoint.id, endpoint.name, endpoint.authCode);
            nearbyHostSlotInstance.transform.SetParent(_root, false);

            _nearbyHostSlotList.Add(nearbyHostSlotInstance);
        }
    }
}
