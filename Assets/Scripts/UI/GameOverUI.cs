using TMPro;
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
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void BackToMainMenu()
    {
        SceneManager.LoadScene(mainMenuSceneName);
    }
}