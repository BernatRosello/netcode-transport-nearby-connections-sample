using UnityEngine;
using Unity.Netcode;
using System;
using TMPro;
using Netcode.Transports.NearbyConnections;

using Strategy = Netcode.Transports.NearbyConnections.NBCTransport.ConnectionStrategy;


public class ConnectionStrategySetter : MonoBehaviour
{
    [SerializeField] TMP_Dropdown _dropdown;

    void Start()
    {
        ReadStrategy();
    }

    public void ChangeStrategy(int newStrategy)
    {
        if (Enum.IsDefined(typeof(Strategy), newStrategy))
            NBCTransport.Instance.ConfigureNetworkingStrategy((Strategy)newStrategy);
    }

    public void ReadStrategy()
    {
        _dropdown.value = (int)NBCTransport.Instance.ConfiguredConnectionStrategy;
    }
}
