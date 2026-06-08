using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameOverUI : MonoBehaviour
{
    public GameObject gameOverPanel;
    public TMP_Text rankingText;

    public string mainMenuSceneName = "LobbyScene";

    void Start()
    {
        gameOverPanel.SetActive(false);
    }

    public void ShowRanking(string ranking)
    {
        gameOverPanel.SetActive(true);
        rankingText.text = ranking;
    }

    public void RestartGame()
    {
        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsListening &&
            NetworkManager.Singleton.IsServer)
        {
            NetworkManager.Singleton.SceneManager.LoadScene(
                SceneManager.GetActiveScene().name,
                LoadSceneMode.Single
            );
            return;
        }

        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void BackToMainMenu()
    {
        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsListening &&
            NetworkManager.Singleton.IsServer)
        {
            NetworkManager.Singleton.SceneManager.LoadScene(
                mainMenuSceneName,
                LoadSceneMode.Single
            );
            return;
        }

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            NetworkManager.Singleton.Shutdown();

        SceneManager.LoadScene(mainMenuSceneName);
    }
}
