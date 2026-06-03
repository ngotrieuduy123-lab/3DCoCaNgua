using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NetworkExitManager : MonoBehaviour
{
    public string mainMenuSceneName = "MainMenu";

    public void LeaveToMainMenu()
    {
        StartCoroutine(LeaveRoutine());
    }

    public void ExitGameWhilePlaying()
    {
        StartCoroutine(LeaveRoutine());
    }

    public void GameOverBackToMainMenu()
    {
        StartCoroutine(LeaveRoutine());
    }

    IEnumerator LeaveRoutine()
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