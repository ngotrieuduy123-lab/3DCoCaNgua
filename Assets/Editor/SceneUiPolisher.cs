using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class SceneUiPolisher
{
    const string AuthScenePath = "Assets/Scenes/AuthScene.unity";
    const string LobbyScenePath = "Assets/Scenes/LobbyScene.unity";

    static readonly Color Ink = new Color(0.08f, 0.1f, 0.13f, 1f);
    static readonly Color Muted = new Color(0.36f, 0.42f, 0.5f, 1f);
    static readonly Color Panel = new Color(0.98f, 0.99f, 1f, 1f);
    static readonly Color Field = new Color(0.93f, 0.95f, 0.98f, 1f);
    static readonly Color Blue = new Color(0.08f, 0.31f, 0.86f, 1f);
    static readonly Color Red = new Color(0.86f, 0.13f, 0.18f, 1f);
    static readonly Color Green = new Color(0.04f, 0.62f, 0.32f, 1f);
    static readonly Color Yellow = new Color(0.96f, 0.74f, 0.14f, 1f);

    [MenuItem("Tools/3DCoCaNgua/Polish Auth Scene")]
    public static void PolishAuthScene()
    {
        Scene scene = EditorSceneManager.OpenScene(AuthScenePath, OpenSceneMode.Single);

        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
            return;

        Image background = GameObject.Find("Background")?.GetComponent<Image>();
        if (background != null)
            background.color = new Color(0.9f, 0.94f, 0.98f, 1f);

        Transform card = GameObject.Find("AuthCard")?.transform;
        if (card != null)
        {
            Image cardImage = card.GetComponent<Image>();
            if (cardImage != null)
                cardImage.color = Panel;

            RectTransform cardRect = card.GetComponent<RectTransform>();
            cardRect.sizeDelta = new Vector2(560, 620);
        }

        SetText("Title", "3D Co Ca Ngua", 38, FontStyles.Bold, Ink);
        SetText("Subtitle", "Online ludo with MongoDB accounts", 17, FontStyles.Normal, Muted);
        SetText("StatusText", "Login or create an account.", 17, FontStyles.Normal, Muted);

        StyleInput("LoginUsernameInput");
        StyleInput("LoginPasswordInput");
        StyleInput("RegisterUsernameInput");
        StyleInput("RegisterDisplayNameInput");
        StyleInput("RegisterPasswordInput");
        StyleInput("RegisterConfirmPasswordInput");

        StyleButton("LoginButton", Blue, Color.white, 24);
        StyleButton("RegisterButton", Blue, Color.white, 23);
        StyleButton("ShowRegisterButton", new Color(0.12f, 0.14f, 0.18f, 1f), Color.white, 21);
        StyleButton("ShowLoginButton", new Color(0.12f, 0.14f, 0.18f, 1f), Color.white, 20);
        StyleButton("LoginPasswordToggle", new Color(0.84f, 0.88f, 0.93f, 1f), Ink, 15);
        StyleButton("RegisterPasswordToggle", new Color(0.84f, 0.88f, 0.93f, 1f), Ink, 15);
        StyleButton("RegisterConfirmPasswordToggle", new Color(0.84f, 0.88f, 0.93f, 1f), Ink, 15);

        if (card != null)
        {
            CreateAuthAccent(card, "AccentBlue", Blue, new Vector2(-250, 245), new Vector2(52, 52));
            CreateAuthAccent(card, "AccentRed", Red, new Vector2(-250, 185), new Vector2(34, 34));
            CreateAuthAccent(card, "AccentGreen", Green, new Vector2(250, -214), new Vector2(42, 42));
            CreateAuthAccent(card, "AccentYellow", Yellow, new Vector2(250, -263), new Vector2(28, 28));
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    [MenuItem("Tools/3DCoCaNgua/Rebuild Lobby UI")]
    public static void RebuildLobbyUi()
    {
        Scene scene = EditorSceneManager.OpenScene(LobbyScenePath, OpenSceneMode.Single);

        RelayManager relay = Object.FindFirstObjectByType<RelayManager>();
        LobbyManager lobby = Object.FindFirstObjectByType<LobbyManager>();

        foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if (canvas.gameObject.scene == scene)
                Object.DestroyImmediate(canvas.gameObject);

        EnsureEventSystem();

        GameObject canvasObject = new GameObject("LobbyCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvasComponent = canvasObject.GetComponent<Canvas>();
        canvasComponent.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        Stretch(canvasRect);

        GameObject background = CreatePanel("Background", canvasObject.transform, Vector2.zero, Vector2.zero, new Color(0.88f, 0.93f, 0.98f, 1f));
        Stretch(background.GetComponent<RectTransform>());

        CreateBand("TopBand", canvasObject.transform, new Vector2(0, 310), new Vector2(1280, 180), Blue);
        CreateBand("BottomBand", canvasObject.transform, new Vector2(0, -328), new Vector2(1280, 96), new Color(0.1f, 0.12f, 0.16f, 1f));

        TMP_Text title = CreateText("LobbyTitle", canvasObject.transform, new Vector2(-430, 294), new Vector2(360, 52), 34, FontStyles.Bold, Color.white, TextAlignmentOptions.Left);
        title.text = "Online Lobby";

        TMP_Text subtitle = CreateText("LobbySubtitle", canvasObject.transform, new Vector2(-410, 255), new Vector2(440, 34), 16, FontStyles.Normal, new Color(0.86f, 0.91f, 1f, 1f), TextAlignmentOptions.Left);
        subtitle.text = "Create a room, share the code, ready up, then start.";

        CreateColorDot(canvasObject.transform, "BlueDot", Blue, new Vector2(410, 292), 32);
        CreateColorDot(canvasObject.transform, "RedDot", Red, new Vector2(452, 292), 32);
        CreateColorDot(canvasObject.transform, "GreenDot", Green, new Vector2(494, 292), 32);
        CreateColorDot(canvasObject.transform, "YellowDot", Yellow, new Vector2(536, 292), 32);

        GameObject roomPanel = CreatePanel("RoomPanel", canvasObject.transform, new Vector2(-330, 30), new Vector2(500, 430), Panel);
        GameObject playersPanel = CreatePanel("PlayersPanel", canvasObject.transform, new Vector2(305, 30), new Vector2(510, 430), Panel);

        TMP_Text roomHeader = CreateText("RoomHeader", roomPanel.transform, new Vector2(0, 172), new Vector2(430, 40), 24, FontStyles.Bold, Ink, TextAlignmentOptions.Left);
        roomHeader.text = "Room controls";

        TMP_Text joinCodeText = CreateText("JoinCodeText", roomPanel.transform, new Vector2(0, 126), new Vector2(430, 44), 22, FontStyles.Bold, Blue, TextAlignmentOptions.Left);
        joinCodeText.text = "Code: -";

        TMP_InputField joinInput = CreateInput("JoinCodeInput", roomPanel.transform, new Vector2(0, 67), new Vector2(430, 52), "Enter room code");

        Button createButton = CreateButton("CreateRoomButton", roomPanel.transform, new Vector2(-112, 0), new Vector2(206, 52), "Create", Blue, Color.white, 20);
        Button joinButton = CreateButton("JoinRoomButton", roomPanel.transform, new Vector2(112, 0), new Vector2(206, 52), "Join", Green, Color.white, 20);
        Button reconnectButton = CreateButton("ReconnectButton", roomPanel.transform, new Vector2(-112, -64), new Vector2(206, 46), "Reconnect", new Color(0.1f, 0.14f, 0.18f, 1f), Color.white, 17);
        Button leaveButton = CreateButton("LeaveRoomButton", roomPanel.transform, new Vector2(112, -64), new Vector2(206, 46), "Leave", Red, Color.white, 17);
        Button copyButton = CreateButton("CopyCodeButton", roomPanel.transform, new Vector2(-112, -122), new Vector2(206, 42), "Copy code", new Color(0.84f, 0.88f, 0.93f, 1f), Ink, 16);
        Button backButton = CreateButton("BackToAuthButton", roomPanel.transform, new Vector2(112, -122), new Vector2(206, 42), "Back", new Color(0.84f, 0.88f, 0.93f, 1f), Ink, 16);

        TMP_Text relayStatus = CreateText("StatusRelay", roomPanel.transform, new Vector2(0, -180), new Vector2(430, 52), 15, FontStyles.Normal, Muted, TextAlignmentOptions.Left);
        relayStatus.text = "Unity Services ready.";

        TMP_Text playersHeader = CreateText("PlayersHeader", playersPanel.transform, new Vector2(0, 172), new Vector2(440, 40), 24, FontStyles.Bold, Ink, TextAlignmentOptions.Left);
        playersHeader.text = "Players";

        TMP_Text playerList = CreateText("PlayerListText", playersPanel.transform, new Vector2(0, 62), new Vector2(440, 190), 20, FontStyles.Normal, Ink, TextAlignmentOptions.TopLeft);
        playerList.text = "Waiting for players...";

        TMP_Text lobbyStatus = CreateText("StatusText", playersPanel.transform, new Vector2(0, -82), new Vector2(440, 50), 16, FontStyles.Normal, Muted, TextAlignmentOptions.Left);
        lobbyStatus.text = "";

        Button readyButton = CreateButton("ReadyButton", playersPanel.transform, new Vector2(-112, -146), new Vector2(206, 54), "Ready", Yellow, Ink, 20);
        Button startButton = CreateButton("StartGameButton", playersPanel.transform, new Vector2(112, -146), new Vector2(206, 54), "Start", Blue, Color.white, 20);

        LoadingOverlay loading = CreateLoadingOverlay(canvasObject.transform);

        if (relay != null)
        {
            relay.joinCodeText = joinCodeText;
            relay.joinCodeInput = joinInput;
            relay.statusText = relayStatus;
            relay.loadingOverlay = loading;

            UnityEventTools.AddPersistentListener(createButton.onClick, relay.CreateRelay);
            UnityEventTools.AddPersistentListener(joinButton.onClick, relay.JoinRelay);
            UnityEventTools.AddPersistentListener(reconnectButton.onClick, relay.Reconnect);
            UnityEventTools.AddPersistentListener(leaveButton.onClick, relay.LeaveRoom);
            UnityEventTools.AddPersistentListener(copyButton.onClick, relay.CopyJoinCode);
            UnityEventTools.AddPersistentListener(backButton.onClick, relay.BackToAuth);
        }

        if (lobby != null)
        {
            lobby.roomCodeText = joinCodeText;
            lobby.playerListText = playerList;
            lobby.statusText = lobbyStatus;
            lobby.loadingOverlay = loading;

            UnityEventTools.AddPersistentListener(readyButton.onClick, lobby.ToggleReady);
            UnityEventTools.AddPersistentListener(startButton.onClick, lobby.StartGame);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    static LoadingOverlay CreateLoadingOverlay(Transform parent)
    {
        GameObject root = CreatePanel("LoadingOverlayRoot", parent, Vector2.zero, Vector2.zero, new Color(0.04f, 0.06f, 0.09f, 0.72f));
        Stretch(root.GetComponent<RectTransform>());

        GameObject box = CreatePanel("LoadingBox", root.transform, Vector2.zero, new Vector2(310, 190), new Color(0.98f, 0.99f, 1f, 1f));

        GameObject spinner = new GameObject("Spinner", typeof(RectTransform));
        spinner.transform.SetParent(box.transform, false);
        SetRect(spinner.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 38), new Vector2(82, 82));
        spinner.AddComponent<LoadingSpinner>();

        for (int i = 0; i < 12; i++)
        {
            GameObject dot = new GameObject("Dot" + i, typeof(RectTransform), typeof(Image));
            dot.transform.SetParent(spinner.transform, false);
            RectTransform rect = dot.GetComponent<RectTransform>();
            float angle = i * Mathf.PI * 2f / 12f;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 34f;
            rect.sizeDelta = new Vector2(9, 18);
            rect.localRotation = Quaternion.Euler(0, 0, i * 30f);

            Image image = dot.GetComponent<Image>();
            image.color = new Color(0.08f, 0.31f, 0.86f, 0.28f + i * 0.05f);
        }

        TMP_Text message = CreateText("LoadingMessage", box.transform, new Vector2(0, -54), new Vector2(260, 44), 18, FontStyles.Bold, Ink, TextAlignmentOptions.Center);
        message.text = "Please wait...";

        LoadingOverlay overlay = root.AddComponent<LoadingOverlay>();
        overlay.root = root;
        overlay.messageText = message;
        root.SetActive(false);
        return overlay;
    }

    static void EnsureEventSystem()
    {
        EventSystem eventSystem = Object.FindFirstObjectByType<EventSystem>();
        if (eventSystem == null)
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            return;
        }

        if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
            eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
    }

    static void StyleInput(string name)
    {
        GameObject go = GameObject.Find(name);
        if (go == null) return;

        Image image = go.GetComponent<Image>();
        if (image != null)
            image.color = Field;

        TMP_InputField input = go.GetComponent<TMP_InputField>();
        if (input != null)
        {
            if (input.textComponent != null && input.GetComponent<MaskedPasswordInput>() == null)
                input.textComponent.color = Ink;

            if (input.placeholder is TMP_Text placeholder)
                placeholder.color = new Color(0.48f, 0.54f, 0.62f, 1f);
        }
    }

    static void StyleButton(string name, Color background, Color textColor, int fontSize)
    {
        GameObject go = GameObject.Find(name);
        if (go == null) return;

        Image image = go.GetComponent<Image>();
        if (image != null)
            image.color = background;

        Button button = go.GetComponent<Button>();
        if (button != null)
            button.colors = ButtonColors(background);

        TMP_Text label = go.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.color = textColor;
            label.fontSize = fontSize;
            label.fontStyle = FontStyles.Bold;
        }
    }

    static void SetText(string name, string value, int fontSize, FontStyles style, Color color)
    {
        TMP_Text text = GameObject.Find(name)?.GetComponent<TMP_Text>();
        if (text == null) return;

        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
    }

    static GameObject CreatePanel(string name, Transform parent, Vector2 pos, Vector2 size, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        SetRect(go.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
        go.GetComponent<Image>().color = color;
        return go;
    }

    static void CreateBand(string name, Transform parent, Vector2 pos, Vector2 size, Color color)
    {
        CreatePanel(name, parent, pos, size, color);
    }

    static TMP_Text CreateText(string name, Transform parent, Vector2 pos, Vector2 size, int fontSize, FontStyles style, Color color, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        SetRect(go.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);

        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.Normal;
        return text;
    }

    static TMP_InputField CreateInput(string name, Transform parent, Vector2 pos, Vector2 size, string placeholderText)
    {
        GameObject go = CreatePanel(name, parent, pos, size, Field);
        TMP_InputField input = go.AddComponent<TMP_InputField>();

        GameObject viewport = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
        viewport.transform.SetParent(go.transform, false);
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        Stretch(viewportRect);
        viewportRect.offsetMin = new Vector2(16, 6);
        viewportRect.offsetMax = new Vector2(-16, -6);

        TMP_Text text = CreateText("Text", viewport.transform, Vector2.zero, Vector2.zero, 20, FontStyles.Normal, Ink, TextAlignmentOptions.MidlineLeft);
        Stretch(text.GetComponent<RectTransform>());
        text.text = "";
        text.textWrappingMode = TextWrappingModes.NoWrap;

        TMP_Text placeholder = CreateText("Placeholder", viewport.transform, Vector2.zero, Vector2.zero, 20, FontStyles.Italic, new Color(0.48f, 0.54f, 0.62f, 1f), TextAlignmentOptions.MidlineLeft);
        Stretch(placeholder.GetComponent<RectTransform>());
        placeholder.text = placeholderText;
        placeholder.textWrappingMode = TextWrappingModes.NoWrap;

        input.textViewport = viewportRect;
        input.textComponent = text;
        input.placeholder = placeholder;
        input.contentType = TMP_InputField.ContentType.Standard;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.characterLimit = 16;
        return input;
    }

    static Button CreateButton(string name, Transform parent, Vector2 pos, Vector2 size, string label, Color background, Color textColor, int fontSize)
    {
        GameObject go = CreatePanel(name, parent, pos, size, background);
        Button button = go.AddComponent<Button>();
        button.colors = ButtonColors(background);

        TMP_Text text = CreateText(name + "Label", go.transform, Vector2.zero, size, fontSize, FontStyles.Bold, textColor, TextAlignmentOptions.Center);
        text.text = label;
        return button;
    }

    static void CreateColorDot(Transform parent, string name, Color color, Vector2 pos, float size)
    {
        GameObject dot = CreatePanel(name, parent, pos, new Vector2(size, size), color);
        dot.GetComponent<Image>().raycastTarget = false;
    }

    static void CreateAuthAccent(Transform parent, string name, Color color, Vector2 pos, Vector2 size)
    {
        if (parent.Find(name) != null)
            Object.DestroyImmediate(parent.Find(name).gameObject);

        GameObject go = CreatePanel(name, parent, pos, size, color);
        go.GetComponent<Image>().raycastTarget = false;
        go.transform.SetAsFirstSibling();
    }

    static ColorBlock ButtonColors(Color baseColor)
    {
        ColorBlock colors = ColorBlock.defaultColorBlock;
        colors.normalColor = baseColor;
        colors.highlightedColor = Color.Lerp(baseColor, Color.white, 0.15f);
        colors.pressedColor = Color.Lerp(baseColor, Color.black, 0.12f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.55f, 0.58f, 0.64f, 1f);
        colors.colorMultiplier = 1f;
        return colors;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
    }
}
