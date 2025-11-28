using UnityEngine;
using Unity.Netcode;
using System;
using TMPro;

public class TickRateSetter : MonoBehaviour
{
    [SerializeField] TMP_InputField _inputField;
    public void ChangeTickRate(string newTickRate)
    {
        uint rate = 0;
        UInt32.TryParse(newTickRate, out rate);
        if (rate > 0) 
        {
            NetworkManager.Singleton.NetworkConfig.TickRate = (uint)rate;
        }
    }

    public void ReadTickRate()
    {
        _inputField.text = NetworkManager.Singleton.NetworkConfig.TickRate.ToString();
    }
}
