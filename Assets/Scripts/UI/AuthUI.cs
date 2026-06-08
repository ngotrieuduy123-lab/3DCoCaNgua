using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class AuthUI : MonoBehaviour
{
    [Header("Panels")]
    public GameObject loginPanel;
    public GameObject registerPanel;

    [Header("Login")]
    public TMP_InputField loginUsernameInput;
    public TMP_InputField loginPasswordInput;
    public Button loginButton;

    [Header("Register")]
    public TMP_InputField registerUsernameInput;
    public TMP_InputField registerDisplayNameInput;
    public TMP_InputField registerPasswordInput;
    public TMP_InputField registerConfirmPasswordInput;
    public Button registerButton;

    [Header("Navigation")]
    public Button showLoginButton;
    public Button showRegisterButton;
    public TMP_Text statusText;
    public string nextSceneName = "LobbyScene";

    bool isBusy;

    void Start()
    {
        ConfigurePasswordFields();
        ShowLogin();
        SetStatus("Login or create an account.");
    }

    public void ShowLogin()
    {
        if (loginPanel != null) loginPanel.SetActive(true);
        if (registerPanel != null) registerPanel.SetActive(false);
        SetStatus("");
    }

    public void ShowRegister()
    {
        if (loginPanel != null) loginPanel.SetActive(false);
        if (registerPanel != null) registerPanel.SetActive(true);
        SetStatus("");
    }

    public async void Login()
    {
        if (isBusy) return;

        SetBusy(true);
        SetStatus("Logging in...");

        DatabaseManager.AuthResult result = await DatabaseManager.Instance.LoginPlayerDetailed(
            loginUsernameInput.text,
            GetPassword(loginPasswordInput)
        );

        SetBusy(false);
        SetStatus(result.Message);

        if (result.Success)
            SceneManager.LoadScene(nextSceneName);
    }

    public async void Register()
    {
        if (isBusy) return;

        string password = GetPassword(registerPasswordInput);
        string confirmPassword = GetPassword(registerConfirmPasswordInput);

        if (password != confirmPassword)
        {
            SetStatus("Passwords do not match.");
            return;
        }

        SetBusy(true);
        SetStatus("Creating account...");

        DatabaseManager.AuthResult result = await DatabaseManager.Instance.RegisterPlayerDetailed(
            registerUsernameInput.text,
            password,
            registerDisplayNameInput.text
        );

        SetBusy(false);
        SetStatus(result.Message);

        if (result.Success)
            SceneManager.LoadScene(nextSceneName);
    }

    void SetBusy(bool busy)
    {
        isBusy = busy;

        if (loginButton != null) loginButton.interactable = !busy;
        if (registerButton != null) registerButton.interactable = !busy;
        if (showLoginButton != null) showLoginButton.interactable = !busy;
        if (showRegisterButton != null) showRegisterButton.interactable = !busy;
    }

    void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;
    }

    void ConfigurePasswordFields()
    {
        ConfigurePasswordField(loginPasswordInput);
        ConfigurePasswordField(registerPasswordInput);
        ConfigurePasswordField(registerConfirmPasswordInput);
    }

    void ConfigurePasswordField(TMP_InputField input)
    {
        if (input == null) return;

        input.contentType = TMP_InputField.ContentType.Standard;
        input.inputType = TMP_InputField.InputType.Standard;
        input.asteriskChar = '*';
        MaskedPasswordInput maskedInput = input.GetComponent<MaskedPasswordInput>();
        if (maskedInput != null)
        {
            maskedInput.Configure();
            maskedInput.Clear();
            maskedInput.SetVisible(false);
        }
        else
        {
            input.SetTextWithoutNotify("");
        }

        input.ForceLabelUpdate();
    }

    string GetPassword(TMP_InputField input)
    {
        if (input == null)
            return string.Empty;

        MaskedPasswordInput maskedInput = input.GetComponent<MaskedPasswordInput>();
        return maskedInput != null ? maskedInput.Password : input.text;
    }
}
