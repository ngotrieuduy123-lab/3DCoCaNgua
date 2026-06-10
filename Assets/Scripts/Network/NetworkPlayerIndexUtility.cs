using Unity.Netcode;
using UnityEngine;

public static class NetworkPlayerIndexUtility
{
    public static int GetLocalPlayerIndex()
    {
        if (NetworkManager.Singleton == null)
            return -1;

        return GetPlayerIndex(NetworkManager.Singleton.LocalClientId);
    }

    public static int GetPlayerIndex(ulong clientId)
    {
        if (NetworkPlayerSlotManager.Instance != null)
        {
            int slotPlayerIndex = NetworkPlayerSlotManager.Instance.GetPlayerIndex(clientId);

            if (slotPlayerIndex >= 0)
                return slotPlayerIndex;
        }

        string clientKey = "PlayerIndexForClient_" + clientId;

        if (PlayerPrefs.HasKey(clientKey))
            return PlayerPrefs.GetInt(clientKey);

        if (NetworkManager.Singleton != null &&
            clientId == NetworkManager.Singleton.LocalClientId &&
            PlayerPrefs.HasKey("LocalPlayerIndex"))
            return PlayerPrefs.GetInt("LocalPlayerIndex");

        if (NetworkManager.Singleton != null &&
            clientId == NetworkManager.ServerClientId)
            return 0;

        return clientId <= int.MaxValue ? (int)clientId : -1;
    }
}
