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

    [SerializeField] private RectTransform _root;

    private NBCTransport _nbcTransport;

    public List<NearbyHostSlot> NearbyHostSlotList => _nearbyHostSlotList;

    private readonly List<NearbyHostSlot> _nearbyHostSlotList = new();

    private void Start()
    {
        // Get the reference of the nbc transport
        _nbcTransport = NBCTransport.Instance;

        _nbcTransport.OnBrowserFoundPeer += (string _,string _) => { UpdateNearbyHostList(); } ;
        _nbcTransport.OnBrowserLostPeer +=  (string _, string _) => { UpdateNearbyHostList(); } ;
        NetworkManager.Singleton.OnClientConnectedCallback +=  (ulong _) => { UpdateNearbyHostList(); } ;
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

        foreach (var nearbyEndpointId in _nbcTransport.NearbyHostDict.Keys)
        {
            if (_nbcTransport.EndpointStatuses[nearbyEndpointId])
            var hostName = _nbcTransport.NearbyHostDict[nearbyEndpointId];

            var nearbyHostSlotInstance = Instantiate(_nearbyHostSlotPrefab);
            nearbyHostSlotInstance.Init(nearbyEndpointId, hostName);
            nearbyHostSlotInstance.transform.SetParent(_root, false);

            _nearbyHostSlotList.Add(nearbyHostSlotInstance);
        }
    }
}
