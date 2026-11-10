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
        ApplyReadableInputColors();
        ShowLogin();
        SetStatus($"Đăng nhập hoặc tạo tài khoản mới để vào sảnh.");
        RoyalMenuThemeStyler.EnsureCreated().ApplyForScene(SceneManager.GetActiveScene().name);
        BgmManager.EnsureCreated().UpdateBgmForScene(SceneManager.GetActiveScene().name);
        CreateServerInfoBadge();
    }

    void CreateServerInfoBadge()
    {
        try
        {
            Canvas canvas = GetComponentInParent<Canvas>() ?? FindFirstObjectByType<Canvas>();
            if (canvas == null) return;

            GameObject badgeObj = new GameObject("ServerInfoBadge");
            badgeObj.transform.SetParent(canvas.transform, false);

            RectTransform rt = badgeObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0, 8);
            rt.sizeDelta = new Vector2(0, 24);

            TextMeshProUGUI text = badgeObj.AddComponent<TextMeshProUGUI>();
            text.text = $"<color=#8e9bb0>Server: </color><color=#ffd166>{ServerConfig.BackendUrl}</color> <color=#6c7a89>| TCP: {ServerConfig.TcpHost}:{ServerConfig.TcpPort} (server_config.txt)</color>";
            text.fontSize = 12;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[AuthUI] Could not create server badge: {ex.Message}");
        }
    }

    public void QuitGame()
    {
        RoyalMenuThemeStyler.QuitApplication();
    }

    void ApplyReadableInputColors()
    {
        ApplyReadableInputColor(loginUsernameInput);
        ApplyReadableInputColor(loginPasswordInput);
        ApplyReadableInputColor(registerUsernameInput);
        ApplyReadableInputColor(registerDisplayNameInput);
        ApplyReadableInputColor(registerPasswordInput);
        ApplyReadableInputColor(registerConfirmPasswordInput);
    }

    static void ApplyReadableInputColor(TMP_InputField input)
    {
        if (input == null)
            return;

        Color textColor = Color.white;
        Color placeholderColor = new Color(0.68f, 0.72f, 0.78f, 1f);
        MaskedPasswordInput masked = input.GetComponent<MaskedPasswordInput>();

        if (masked != null)
        {
            if (masked.displayText != null)
                masked.displayText.color = textColor;
        }
        else if (input.textComponent != null)
        {
            input.textComponent.color = textColor;
        }

        if (input.placeholder is TMP_Text placeholder)
            placeholder.color = placeholderColor;
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
        SetStatus("Đang đăng nhập...");

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
            SetStatus("Mật khẩu xác nhận không khớp.");
            return;
        }

        SetBusy(true);
        SetStatus("Đang khởi tạo tài khoản...");

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
