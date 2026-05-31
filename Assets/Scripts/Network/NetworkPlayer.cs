using Unity.Netcode;
using UnityEngine;

public class NetworkPlayer : NetworkBehaviour
{
    public NetworkVariable<int> playerColorIndex = new NetworkVariable<int>();

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            playerColorIndex.Value = (int)OwnerClientId;
        }

        playerColorIndex.OnValueChanged += OnColorChanged;

        UpdateColorText();
    }

    void OnColorChanged(int oldValue, int newValue)
    {
        UpdateColorText();
    }

    void UpdateColorText()
    {
        Debug.Log("Client " + OwnerClientId + " color = " + (PlayerColor)playerColorIndex.Value);
    }

    public PlayerColor GetPlayerColor()
    {
        return (PlayerColor)playerColorIndex.Value;
    }
}