using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuUI : MonoBehaviour
{
    public string lobbySceneName = "LobbyScene";

    public void StartGame()
    {
        SceneManager.LoadScene(lobbySceneName);
    }

    public void OpenSetting()
    {
        Debug.Log("Open setting later");
    }

    public void OpenHistory()
    {
        Debug.Log("Open history later");
    }

    public void OpenLogin()
    {
        Debug.Log("Open login later");
    }

    public void ExitApp()
    {
        Application.Quit();
    }
}