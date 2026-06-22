using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class MinimalUiRedesign
{
    const string AuthScenePath = "Assets/Scenes/AuthScene.unity";
    const string LobbyScenePath = "Assets/Scenes/LobbyScene.unity";
    const string BackgroundPath = "Assets/UI/Generated/minimal_ludo_background.png";
    const string IconFolder = "Assets/UI/Generated/Icons";

    static readonly Color Surface = new Color(0.075f, 0.09f, 0.12f, 0.96f);
    static readonly Color SurfaceSoft = new Color(0.11f, 0.13f, 0.17f, 0.94f);
    static readonly Color Field = new Color(0.14f, 0.16f, 0.20f, 1f);
    static readonly Color Primary = new Color(0.78f, 0.83f, 0.88f, 1f);
    static readonly Color Secondary = new Color(0.20f, 0.23f, 0.28f, 1f);
    static readonly Color Text = new Color(0.95f, 0.96f, 0.97f, 1f);
    static readonly Color Muted = new Color(0.64f, 0.68f, 0.73f, 1f);
    static readonly Color DarkText = new Color(0.08f, 0.10f, 0.13f, 1f);
    static readonly Color LogoutRed = new Color(0.72f, 0.16f, 0.18f, 1f);

    [MenuItem("Tools/3DCoCaNgua/Apply Minimal Auth UI")]
    public static void ApplyAuth()
    {
        Scene scene = EditorSceneManager.OpenScene(AuthScenePath, OpenSceneMode.Single);
        ApplyBackground("Background");
        StylePanel("AuthCard", Surface);

        SetText("Title", "Cờ Cá Ngựa 3D", 36f, Text, FontStyles.Bold);
        SetText("Subtitle", "Đăng nhập để tiếp tục", 17f, Muted, FontStyles.Normal);
        SetText("StatusText", "", 16f, Muted, FontStyles.Normal);

        string[] inputs = { "LoginUsernameInput", "LoginPasswordInput", "RegisterUsernameInput", "RegisterDisplayNameInput", "RegisterPasswordInput", "RegisterConfirmPasswordInput" };
        foreach (string input in inputs) StyleInput(input);

        StyleButton("LoginButton", Primary, DarkText);
        StyleButton("RegisterButton", Primary, DarkText);
        StyleButton("ShowRegisterButton", Secondary, Text);
        StyleButton("ShowLoginButton", Secondary, Text);
        StyleButton("LoginPasswordToggle", Secondary, Text);
        StyleButton("RegisterPasswordToggle", Secondary, Text);
        StyleButton("RegisterConfirmPasswordToggle", Secondary, Text);

        string[] accents = { "AccentBlue", "AccentRed", "AccentGreen", "AccentYellow" };
        foreach (string accent in accents)
        {
            GameObject target = Find(accent);
            if (target != null) Object.DestroyImmediate(target);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    [MenuItem("Tools/3DCoCaNgua/Apply Minimal Lobby UI")]
    public static void ApplyLobby()
    {
        GenerateIconSet();
        Scene scene = EditorSceneManager.OpenScene(LobbyScenePath, OpenSceneMode.Single);
        ApplyBackground("Background");

        if (Find("TopBand") != null) Find("TopBand").SetActive(false);
        if (Find("BottomBand") != null) Find("BottomBand").SetActive(false);
        StylePanel("RoomPanel", Surface);
        StylePanel("PlayersPanel", Surface);

        RectTransform roomPanel = Find("RoomPanel")?.GetComponent<RectTransform>();
        RectTransform playersPanel = Find("PlayersPanel")?.GetComponent<RectTransform>();
        if (roomPanel != null) { roomPanel.anchoredPosition = new Vector2(-330f, -24f); roomPanel.sizeDelta = new Vector2(500f, 520f); }
        if (playersPanel != null) { playersPanel.anchoredPosition = new Vector2(305f, 0f); playersPanel.sizeDelta = new Vector2(510f, 520f); }

        SetText("LobbyTitle", "Phòng chờ", 34f, Text, FontStyles.Bold);
        SetText("LobbySubtitle", "Tạo phòng, chia sẻ mã và bắt đầu khi mọi người đã sẵn sàng.", 16f, Muted, FontStyles.Normal);
        SetText("RoomHeader", "Phòng", 24f, Text, FontStyles.Bold);
        SetText("PlayersHeader", "Người chơi", 24f, Text, FontStyles.Bold);

        string[] neutralTexts = { "JoinCodeText", "StatusRelay", "PlayerListText", "StatusText" };
        foreach (string name in neutralTexts)
        {
            TMP_Text text = Find(name)?.GetComponent<TMP_Text>();
            if (text != null) text.color = name == "JoinCodeText" ? Primary : Muted;
        }

        StyleInput("JoinCodeInput");

        string[] primaryButtons = { "CreateRoomButton", "JoinRoomButton", "ReadyButton", "StartGameButton" };
        foreach (string button in primaryButtons) StyleButton(button, Primary, DarkText);

        string[] secondaryButtons = { "ReconnectButton", "LeaveRoomButton", "CopyCodeButton" };
        foreach (string button in secondaryButtons) StyleButton(button, Secondary, Text);

        SetButtonLabel("CreateRoomButton", "Create");
        SetButtonLabel("JoinRoomButton", "Join");
        SetButtonLabel("ReconnectButton", "Reconnect");
        SetButtonLabel("LeaveRoomButton", "Leave");
        SetButtonLabel("CopyCodeButton", "Copy code");
        SetButtonLabel("ReadyButton", "Ready");
        SetButtonLabel("StartGameButton", "Start");

        AddButtonIcon("CreateRoomButton", "plus", DarkText);
        AddButtonIcon("JoinRoomButton", "enter", DarkText);
        AddButtonIcon("ReconnectButton", "refresh", Text);
        AddButtonIcon("LeaveRoomButton", "exit", Text);
        AddButtonIcon("CopyCodeButton", "copy", Text);
        RemoveButtonIcon("ReadyButton");
        AddButtonIcon("StartGameButton", "play", DarkText);

        Button logoutButton = Find("BackToAuthButton")?.GetComponent<Button>();
        if (logoutButton != null)
        {
            StyleButton("BackToAuthButton", LogoutRed, Color.white);
            TMP_Text label = logoutButton.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = "Logout";
            AddButtonIcon("BackToAuthButton", "logout", Color.white);
        }

        AddLobbyCosmeticsButtons(roomPanel);

        RectTransform relayStatus = Find("StatusRelay")?.GetComponent<RectTransform>();
        if (relayStatus != null)
        {
            relayStatus.anchoredPosition = new Vector2(0f, -232f);
            relayStatus.sizeDelta = new Vector2(430f, 30f);
        }

        string[] dots = { "BlueDot", "RedDot", "GreenDot", "YellowDot" };
        foreach (string dot in dots)
        {
            GameObject target = Find(dot);
            if (target != null) target.SetActive(false);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    static void AddLobbyCosmeticsButtons(RectTransform roomPanel)
    {
        if (roomPanel == null)
            return;

        GameObject oldShop = Find("LobbyShopButton");
        GameObject oldSkins = Find("LobbySkinsButton");
        if (oldShop != null) Object.DestroyImmediate(oldShop);
        if (oldSkins != null) Object.DestroyImmediate(oldSkins);

        LobbyCosmeticsUI bridge = Object.FindFirstObjectByType<LobbyCosmeticsUI>();
        if (bridge == null)
        {
            GameObject bridgeObject = new GameObject("LobbyCosmeticsController");
            bridgeObject.transform.SetParent(roomPanel.parent, false);
            bridge = bridgeObject.AddComponent<LobbyCosmeticsUI>();
        }

        Button shop = CreateLobbyButton("LobbyShopButton", roomPanel, new Vector2(-112f, -180f), "Shop");
        Button skins = CreateLobbyButton("LobbySkinsButton", roomPanel, new Vector2(112f, -180f), "Skin");
        UnityEventTools.AddPersistentListener(shop.onClick, bridge.OpenShop);
        UnityEventTools.AddPersistentListener(skins.onClick, bridge.OpenSkins);
        AddButtonIcon("LobbyShopButton", "shop", DarkText);
        AddButtonIcon("LobbySkinsButton", "skin", DarkText);
    }

    static Button CreateLobbyButton(string name, Transform parent, Vector2 position, string labelText)
    {
        GameObject target = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        target.transform.SetParent(parent, false);
        RectTransform rect = target.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(206f, 46f);

        target.GetComponent<Image>().color = Primary;
        Button button = target.GetComponent<Button>();

        GameObject labelObject = new GameObject(name + "Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(target.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.text = labelText;
        label.fontSize = 18f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = DarkText;
        label.raycastTarget = false;

        return button;
    }

    static void ApplyBackground(string objectName)
    {
        Image image = Find(objectName)?.GetComponent<Image>();
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundPath);

        if (image == null || sprite == null)
            return;

        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        image.color = Color.white;
    }

    static void StylePanel(string objectName, Color color)
    {
        Image image = Find(objectName)?.GetComponent<Image>();
        if (image != null) image.color = color;
    }

    static void SetText(string objectName, string value, float size, Color color, FontStyles style)
    {
        TMP_Text text = Find(objectName)?.GetComponent<TMP_Text>();
        if (text == null) return;
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.fontStyle = style;
    }

    static void StyleInput(string objectName)
    {
        TMP_InputField input = Find(objectName)?.GetComponent<TMP_InputField>();
        if (input == null) return;

        Image image = input.GetComponent<Image>();
        if (image != null) image.color = Field;

        if (input.textComponent != null) input.textComponent.color = Text;
        if (input.placeholder is TMP_Text placeholder) placeholder.color = Muted;

        foreach (TMP_Text childText in input.GetComponentsInChildren<TMP_Text>(true))
            if (childText != input.placeholder)
                childText.color = Text;

        MaskedPasswordInput masked = input.GetComponent<MaskedPasswordInput>();
        if (masked != null && masked.displayText != null)
            masked.displayText.color = Text;

        ColorBlock colors = input.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.92f, 0.94f, 0.98f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = new Color(0.84f, 0.87f, 0.91f, 1f);
        input.colors = colors;
    }

    static void StyleButton(string objectName, Color background, Color foreground)
    {
        Button button = Find(objectName)?.GetComponent<Button>();
        if (button == null) return;

        Image image = button.GetComponent<Image>();
        if (image != null) image.color = background;

        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.color = foreground;

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.90f, 0.93f, 0.96f, 1f);
        colors.pressedColor = new Color(0.76f, 0.80f, 0.84f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;
    }

    static void SetButtonLabel(string objectName, string value)
    {
        Button button = Find(objectName)?.GetComponent<Button>();
        TMP_Text label = button != null ? button.GetComponentInChildren<TMP_Text>(true) : null;
        if (label != null) label.text = value;
    }

    static void AddButtonIcon(string buttonName, string iconName, Color color)
    {
        Button button = Find(buttonName)?.GetComponent<Button>();
        if (button == null) return;

        Transform oldIcon = button.transform.Find("FunctionIcon");
        if (oldIcon != null) Object.DestroyImmediate(oldIcon.gameObject);

        string iconPath = $"{IconFolder}/{iconName}.png";
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
        if (sprite == null)
        {
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(iconPath))
                if (asset is Sprite loadedSprite) { sprite = loadedSprite; break; }
        }
        if (sprite == null) return;

        GameObject iconObject = new GameObject("FunctionIcon", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(button.transform, false);
        RectTransform rect = iconObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(25f, 0f);
        rect.sizeDelta = new Vector2(22f, 22f);

        Image image = iconObject.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.preserveAspect = true;
        image.raycastTarget = false;

        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            RectTransform labelRect = label.rectTransform;
            if (labelRect.anchorMin == labelRect.anchorMax)
            {
                RectTransform buttonRect = button.GetComponent<RectTransform>();
                labelRect.anchoredPosition = new Vector2(12f, 0f);
                labelRect.sizeDelta = new Vector2(Mathf.Max(80f, buttonRect.rect.width - 44f), buttonRect.rect.height);
            }
            else
            {
                labelRect.offsetMin = new Vector2(32f, 0f);
                labelRect.offsetMax = new Vector2(-4f, 0f);
            }
        }
    }

    static void RemoveButtonIcon(string buttonName)
    {
        Button button = Find(buttonName)?.GetComponent<Button>();
        if (button == null) return;

        Transform icon = button.transform.Find("FunctionIcon");
        if (icon != null) Object.DestroyImmediate(icon.gameObject);

        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label == null) return;

        RectTransform labelRect = label.rectTransform;
        RectTransform buttonRect = button.GetComponent<RectTransform>();
        labelRect.anchoredPosition = Vector2.zero;
        labelRect.sizeDelta = buttonRect.rect.size;
    }

    static void GenerateIconSet()
    {
        if (!Directory.Exists(IconFolder)) Directory.CreateDirectory(IconFolder);

        string[] names = { "plus", "enter", "refresh", "exit", "copy", "check", "play", "logout", "shop", "skin" };
        foreach (string name in names)
        {
            string path = $"{IconFolder}/{name}.png";
            if (File.Exists(path)) continue;

            Texture2D texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            texture.SetPixels32(new Color32[32 * 32]);
            DrawIcon(texture, name);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }

        AssetDatabase.Refresh();
        foreach (string name in names)
        {
            string path = $"{IconFolder}/{name}.png";
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 32f;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
        }
    }

    static void DrawIcon(Texture2D texture, string name)
    {
        switch (name)
        {
            case "plus": Line(texture, 16, 7, 16, 25, 3); Line(texture, 7, 16, 25, 16, 3); break;
            case "enter": Line(texture, 7, 16, 24, 16, 3); Line(texture, 18, 10, 24, 16, 3); Line(texture, 18, 22, 24, 16, 3); break;
            case "refresh": Arc(texture, 16, 16, 10, 35, 310, 3); Line(texture, 23, 7, 25, 14, 3); Line(texture, 23, 7, 16, 9, 3); break;
            case "exit": Box(texture, 6, 7, 15, 25, 2); Line(texture, 13, 16, 26, 16, 3); Line(texture, 21, 11, 26, 16, 3); Line(texture, 21, 21, 26, 16, 3); break;
            case "copy": Box(texture, 10, 6, 25, 21, 2); Box(texture, 6, 10, 21, 26, 2); break;
            case "check": Line(texture, 6, 17, 13, 24, 3); Line(texture, 13, 24, 27, 8, 3); break;
            case "play": Triangle(texture, 10, 6, 10, 26, 26, 16); break;
            case "logout": Box(texture, 6, 6, 16, 26, 2); Line(texture, 14, 16, 27, 16, 3); Line(texture, 22, 11, 27, 16, 3); Line(texture, 22, 21, 27, 16, 3); break;
            case "shop": Box(texture, 7, 13, 25, 25, 2); Line(texture, 5, 13, 9, 7, 3); Line(texture, 9, 7, 23, 7, 3); Line(texture, 23, 7, 27, 13, 3); Line(texture, 5, 13, 27, 13, 2); break;
            case "skin": Arc(texture, 16, 12, 5, 0, 360, 3); Line(texture, 10, 19, 6, 26, 3); Line(texture, 22, 19, 26, 26, 3); Line(texture, 6, 26, 26, 26, 3); Line(texture, 10, 19, 22, 19, 3); break;
        }
    }

    static void Pixel(Texture2D texture, int x, int y)
    {
        if (x >= 0 && x < texture.width && y >= 0 && y < texture.height)
            texture.SetPixel(x, y, Color.white);
    }

    static void Line(Texture2D texture, int x0, int y0, int x1, int y1, int width)
    {
        int dx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
        int dy = -Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
        int error = dx + dy;
        while (true)
        {
            for (int ox = -width / 2; ox <= width / 2; ox++)
                for (int oy = -width / 2; oy <= width / 2; oy++) Pixel(texture, x0 + ox, y0 + oy);
            if (x0 == x1 && y0 == y1) break;
            int twice = 2 * error;
            if (twice >= dy) { error += dy; x0 += sx; }
            if (twice <= dx) { error += dx; y0 += sy; }
        }
    }

    static void Box(Texture2D texture, int left, int bottom, int right, int top, int width)
    {
        Line(texture, left, bottom, right, bottom, width); Line(texture, right, bottom, right, top, width);
        Line(texture, right, top, left, top, width); Line(texture, left, top, left, bottom, width);
    }

    static void Arc(Texture2D texture, int cx, int cy, int radius, int start, int end, int width)
    {
        Vector2 previous = Vector2.zero;
        bool hasPrevious = false;
        for (int angle = start; angle <= end; angle += 5)
        {
            float radians = angle * Mathf.Deg2Rad;
            Vector2 point = new Vector2(cx + Mathf.Cos(radians) * radius, cy + Mathf.Sin(radians) * radius);
            if (hasPrevious) Line(texture, Mathf.RoundToInt(previous.x), Mathf.RoundToInt(previous.y), Mathf.RoundToInt(point.x), Mathf.RoundToInt(point.y), width);
            previous = point;
            hasPrevious = true;
        }
    }

    static void Triangle(Texture2D texture, int ax, int ay, int bx, int by, int cx, int cy)
    {
        for (int y = ay; y <= by; y++)
        {
            float t = (y - ay) / (float)(by - ay);
            int right = Mathf.RoundToInt(Mathf.Lerp(ax, cx, 1f - Mathf.Abs(t * 2f - 1f)));
            Line(texture, ax, y, right, y, 1);
        }
    }

    static GameObject Find(string objectName)
    {
        foreach (GameObject target in Resources.FindObjectsOfTypeAll<GameObject>())
            if (target.name == objectName && target.scene.IsValid() && target.scene.isLoaded)
                return target;

        return null;
    }
}
