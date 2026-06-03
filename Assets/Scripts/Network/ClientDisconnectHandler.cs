using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ClientDisconnectHandler : MonoBehaviour
{
    public string mainMenuSceneName = "MainMenu";

    void Start()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }
    }

    void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    void OnClientDisconnected(ulong clientId)
    {
        if (NetworkManager.Singleton == null) return;

        if (NetworkManager.Singleton.IsServer)
            return;

        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            StartCoroutine(ReturnToMainMenu());
        }
    }

    IEnumerator ReturnToMainMenu()
    {
        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }

        yield return null;

        SceneManager.LoadScene(mainMenuSceneName, LoadSceneMode.Single);
    }
}