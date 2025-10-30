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

        _nbcTransport.OnBrowserFoundPeer += OnBrowserFoundPeer;
        _nbcTransport.OnBrowserLostPeer += OnBrowserLostPeer;
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        UpdateNearbyHostList();
    }

    private void OnBrowserFoundPeer(int _, string hostName)
    {
        UpdateNearbyHostList();
    }

    private void OnBrowserLostPeer(int _, string hostName)
    {
        UpdateNearbyHostList();
    }

    private void OnClientConnected(ulong _)
    {
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

        foreach (var nearbyHostKey in _nbcTransport.NearbyHostDict.Keys)
        {
            var hostName = _nbcTransport.NearbyHostDict[nearbyHostKey];

            var nearbyHostSlotInstance = Instantiate(_nearbyHostSlotPrefab);
            nearbyHostSlotInstance.Init(nearbyHostKey, hostName);
            nearbyHostSlotInstance.transform.SetParent(_root, false);

            _nearbyHostSlotList.Add(nearbyHostSlotInstance);
        }
    }
}
