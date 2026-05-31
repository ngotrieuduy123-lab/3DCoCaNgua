using TMPro;
using Unity.Netcode;
using UnityEngine;

public class NetworkTestUI : MonoBehaviour
{
    public TMP_Text statusText;

    void SetStatus(string message)
    {
        Debug.Log(message);

        if (statusText != null)
            statusText.text = message;
    }

    public void StartHost()
    {
        bool result = NetworkManager.Singleton.StartHost();
        if (result)
        {
            int count = PlayerPrefs.GetInt("PlayerCount", 2);
            NetworkTurnManager.Instance.SetPlayerCount(count);
        }
        SetStatus("Start Host: " + result);
    }

    public void StartClient()
    {
        bool result = NetworkManager.Singleton.StartClient();
        SetStatus("Start Client: " + result);
    }

    public void Shutdown()
    {
        NetworkManager.Singleton.Shutdown();
        SetStatus("Network shutdown");
    }
}