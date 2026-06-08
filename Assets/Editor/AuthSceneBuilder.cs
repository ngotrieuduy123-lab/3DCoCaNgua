using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class AuthSceneBuilder
{
    const string ScenePath = "Assets/Scenes/AuthScene.unity";

    [MenuItem("Tools/3DCoCaNgua/Rebuild Auth Scene")]
    public static void BuildAuthScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "AuthScene";

        GameObject canvasObj = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObj.GetComponent<Canvas>();

        CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform canvasRect = canvasObj.GetComponent<RectTransform>();
        canvasRect.anchorMin = Vector2.zero;
        canvasRect.anchorMax = Vector2.one;
        canvasRect.offsetMin = Vector2.zero;
        canvasRect.offsetMax = Vector2.zero;

        GameObject background = new GameObject("Background", typeof(RectTransform), typeof(Image));
        background.transform.SetParent(canvasObj.transform, false);
        RectTransform backgroundRect = background.GetComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;
        background.GetComponent<Image>().color = new Color(0.89f, 0.93f, 0.97f, 1f);

        GameObject card = CreatePanel("AuthCard", canvasObj.transform, Vector2.zero, new Vector2(520, 600), Color.white);

        TextMeshProUGUI title = CreateText("Title", card.transform, new Vector2(0, 245), new Vector2(460, 64), 36, FontStyles.Bold);
        title.text = "3D Co Ca Ngua";

        TextMeshProUGUI subtitle = CreateText("Subtitle", card.transform, new Vector2(0, 205), new Vector2(460, 44), 18, FontStyles.Normal);
        subtitle.text = "MongoDB account login";
        subtitle.color = new Color(0.33f, 0.38f, 0.46f, 1f);

        GameObject loginPanel = CreatePanel("LoginPanel", card.transform, new Vector2(0, -15), new Vector2(440, 330), new Color(1f, 1f, 1f, 0f));
        TMP_InputField loginUsername = CreateInput("LoginUsernameInput", loginPanel.transform, new Vector2(0, 95), new Vector2(400, 54), "Username", false);
        TMP_InputField loginPassword = CreateInput("LoginPasswordInput", loginPanel.transform, new Vector2(0, 25), new Vector2(400, 54), "Password", true);
        CreatePasswordToggle("LoginPasswordToggle", loginPassword);
        Button loginButton = CreateButton("LoginButton", loginPanel.transform, new Vector2(0, -55), new Vector2(400, 56), "Login");
        Button toRegisterButton = CreateButton("ShowRegisterButton", loginPanel.transform, new Vector2(0, -125), new Vector2(400, 46), "Create account");

        GameObject registerPanel = CreatePanel("RegisterPanel", card.transform, new Vector2(0, -15), new Vector2(440, 390), new Color(1f, 1f, 1f, 0f));
        TMP_InputField registerUsername = CreateInput("RegisterUsernameInput", registerPanel.transform, new Vector2(0, 135), new Vector2(400, 50), "Username", false);
        TMP_InputField registerDisplayName = CreateInput("RegisterDisplayNameInput", registerPanel.transform, new Vector2(0, 75), new Vector2(400, 50), "Display name", false);
        TMP_InputField registerPassword = CreateInput("RegisterPasswordInput", registerPanel.transform, new Vector2(0, 15), new Vector2(400, 50), "Password", true);
        CreatePasswordToggle("RegisterPasswordToggle", registerPassword);
        TMP_InputField registerConfirm = CreateInput("RegisterConfirmPasswordInput", registerPanel.transform, new Vector2(0, -45), new Vector2(400, 50), "Confirm password", true);
        CreatePasswordToggle("RegisterConfirmPasswordToggle", registerConfirm);
        Button registerButton = CreateButton("RegisterButton", registerPanel.transform, new Vector2(0, -115), new Vector2(400, 54), "Register");
        Button toLoginButton = CreateButton("ShowLoginButton", registerPanel.transform, new Vector2(0, -180), new Vector2(400, 44), "Back to login");

        TextMeshProUGUI status = CreateText("StatusText", card.transform, new Vector2(0, -260), new Vector2(440, 60), 18, FontStyles.Normal);
        status.text = "";
        status.color = new Color(0.17f, 0.24f, 0.32f, 1f);

        GameObject databaseObj = new GameObject("DatabaseManager");
        databaseObj.AddComponent<DatabaseManager>();

        GameObject authObj = new GameObject("AuthUIManager");
        AuthUI auth = authObj.AddComponent<AuthUI>();
        auth.loginPanel = loginPanel;
        auth.registerPanel = registerPanel;
        auth.loginUsernameInput = loginUsername;
        auth.loginPasswordInput = loginPassword;
        auth.loginButton = loginButton;
        auth.registerUsernameInput = registerUsername;
        auth.registerDisplayNameInput = registerDisplayName;
        auth.registerPasswordInput = registerPassword;
        auth.registerConfirmPasswordInput = registerConfirm;
        auth.registerButton = registerButton;
        auth.showLoginButton = toLoginButton;
        auth.showRegisterButton = toRegisterButton;
        auth.statusText = status;
        auth.nextSceneName = "LobbyScene";

        UnityEventTools.AddPersistentListener(loginButton.onClick, auth.Login);
        UnityEventTools.AddPersistentListener(registerButton.onClick, auth.Register);
        UnityEventTools.AddPersistentListener(toRegisterButton.onClick, auth.ShowRegister);
        UnityEventTools.AddPersistentListener(toLoginButton.onClick, auth.ShowLogin);

        GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        eventSystem.transform.position = Vector3.zero;

        GameObject cameraObj = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cameraObj.tag = "MainCamera";
        cameraObj.transform.position = new Vector3(0, 0, -10);
        Camera camera = cameraObj.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.89f, 0.93f, 0.97f, 1f);
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 1f;

        GameObject lightObj = new GameObject("Directional Light", typeof(Light));
        lightObj.GetComponent<Light>().type = LightType.Directional;
        lightObj.transform.rotation = Quaternion.Euler(50, -30, 0);

        loginPanel.SetActive(true);
        registerPanel.SetActive(false);

        EditorSceneManager.SaveScene(scene, ScenePath);
        EnsureSceneInBuildSettings();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    static GameObject CreatePanel(string name, Transform parent, Vector2 pos, Vector2 size, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        SetRect(go.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
        go.GetComponent<Image>().color = color;
        return go;
    }

    static TextMeshProUGUI CreateText(string name, Transform parent, Vector2 pos, Vector2 size, int fontSize, FontStyles style)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        SetRect(go.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);

        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = new Color(0.08f, 0.1f, 0.13f, 1f);
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.Normal;
        return text;
    }

    static Button CreateButton(string name, Transform parent, Vector2 pos, Vector2 size, string label)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        SetRect(go.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);

        Image image = go.GetComponent<Image>();
        image.color = new Color(0.1f, 0.46f, 0.9f, 1f);

        Button button = go.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.1f, 0.46f, 0.9f, 1f);
        colors.highlightedColor = new Color(0.16f, 0.55f, 1f, 1f);
        colors.pressedColor = new Color(0.06f, 0.32f, 0.7f, 1f);
        colors.disabledColor = new Color(0.45f, 0.5f, 0.56f, 1f);
        button.colors = colors;

        TextMeshProUGUI text = CreateText(name + "Label", go.transform, Vector2.zero, size, 24, FontStyles.Bold);
        text.text = label;
        text.color = Color.white;
        return button;
    }

    static TMP_InputField CreateInput(string name, Transform parent, Vector2 pos, Vector2 size, string placeholderText, bool password)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
        go.transform.SetParent(parent, false);
        SetRect(go.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
        go.GetComponent<Image>().color = new Color(0.96f, 0.97f, 0.99f, 1f);

        GameObject viewport = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
        viewport.transform.SetParent(go.transform, false);
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        SetRect(viewportRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        viewportRect.offsetMin = new Vector2(16, 6);
        viewportRect.offsetMax = password ? new Vector2(-92, -6) : new Vector2(-16, -6);

        GameObject textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(viewport.transform, false);
        RectTransform textRect = textGo.GetComponent<RectTransform>();
        SetRect(textRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        TextMeshProUGUI text = textGo.AddComponent<TextMeshProUGUI>();
        text.fontSize = 21;
        text.color = password ? new Color(0f, 0f, 0f, 0f) : new Color(0.08f, 0.1f, 0.13f, 1f);
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.textWrappingMode = TextWrappingModes.NoWrap;

        GameObject placeholderGo = new GameObject("Placeholder", typeof(RectTransform));
        placeholderGo.transform.SetParent(viewport.transform, false);
        RectTransform placeholderRect = placeholderGo.GetComponent<RectTransform>();
        SetRect(placeholderRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        TextMeshProUGUI placeholder = placeholderGo.AddComponent<TextMeshProUGUI>();
        placeholder.text = placeholderText;
        placeholder.fontSize = 21;
        placeholder.fontStyle = FontStyles.Italic;
        placeholder.color = new Color(0.48f, 0.52f, 0.58f, 1f);
        placeholder.alignment = TextAlignmentOptions.MidlineLeft;
        placeholder.textWrappingMode = TextWrappingModes.NoWrap;

        TMP_InputField input = go.GetComponent<TMP_InputField>();
        input.textViewport = viewportRect;
        input.textComponent = text;
        input.placeholder = placeholder;
        input.contentType = TMP_InputField.ContentType.Standard;
        input.inputType = TMP_InputField.InputType.Standard;
        input.asteriskChar = '*';
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.characterLimit = password ? 64 : 32;
        input.text = "";

        if (password)
        {
            GameObject maskGo = new GameObject("MaskText", typeof(RectTransform));
            maskGo.transform.SetParent(viewport.transform, false);
            RectTransform maskRect = maskGo.GetComponent<RectTransform>();
            SetRect(maskRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            TextMeshProUGUI maskText = maskGo.AddComponent<TextMeshProUGUI>();
            maskText.fontSize = 21;
            maskText.color = new Color(0.08f, 0.1f, 0.13f, 1f);
            maskText.alignment = TextAlignmentOptions.MidlineLeft;
            maskText.textWrappingMode = TextWrappingModes.NoWrap;
            maskText.raycastTarget = false;

            MaskedPasswordInput maskedInput = go.AddComponent<MaskedPasswordInput>();
            maskedInput.input = input;
            maskedInput.displayText = maskText;
        }

        return input;
    }

    static Button CreatePasswordToggle(string name, TMP_InputField input)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(input.transform, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.anchoredPosition = new Vector2(-4f, 0f);
        rect.sizeDelta = new Vector2(74f, -8f);

        Image image = go.GetComponent<Image>();
        image.color = new Color(0.84f, 0.87f, 0.92f, 1f);

        Button button = go.GetComponent<Button>();

        TextMeshProUGUI text = CreateText(name + "Label", go.transform, Vector2.zero, new Vector2(74f, 38f), 16, FontStyles.Bold);
        text.text = "Show";
        text.color = new Color(0.12f, 0.16f, 0.22f, 1f);
        text.textWrappingMode = TextWrappingModes.NoWrap;

        PasswordVisibilityToggle toggle = go.AddComponent<PasswordVisibilityToggle>();
        toggle.targetInput = input.GetComponent<MaskedPasswordInput>();
        toggle.label = text;

        UnityEventTools.AddPersistentListener(button.onClick, toggle.Toggle);
        return button;
    }

    static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
    }

    static void EnsureSceneInBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        scenes.RemoveAll(scene => scene.path == ScenePath);
        scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
