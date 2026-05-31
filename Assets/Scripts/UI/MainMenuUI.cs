using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuUI : MonoBehaviour
{
    public string gameSceneName = "GameScene";

    public void StartGame(int playerCount)
    {
        PlayerPrefs.SetInt("PlayerCount", playerCount);
        SceneManager.LoadScene(gameSceneName);
    }
}