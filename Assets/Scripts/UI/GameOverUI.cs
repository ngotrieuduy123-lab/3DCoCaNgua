using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameOverUI : MonoBehaviour
{
    public GameObject gameOverPanel;
    public TMP_Text rankingText;

    public string mainMenuSceneName = "MainMenu";

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
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void BackToMainMenu()
    {
        if (Unity.Netcode.NetworkManager.Singleton != null &&
            Unity.Netcode.NetworkManager.Singleton.IsListening)
        {
            Unity.Netcode.NetworkManager.Singleton.Shutdown();
        }

        SceneManager.LoadScene(mainMenuSceneName);
    }
}