using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class RoyalMenuThemeStyler : MonoBehaviour
{
    public static RoyalMenuThemeStyler Instance { get; private set; }

    // Royal Lacquer Arcade Palette (delegated to shared RoyalUiThemeKit tokens)
    private static Color WoodDark => RoyalUiThemeKit.LacquerEbony;
    private static Color WoodWarm => RoyalUiThemeKit.LacquerWarm;
    private static Color WoodInset => RoyalUiThemeKit.LacquerInset;
    private static Color BrassGold => RoyalUiThemeKit.BrassAntique;
    private static Color BrassBright => RoyalUiThemeKit.BrassBright;
    private static Color CreamWhite => RoyalUiThemeKit.IvoryText;
    private static Color MutedCream => RoyalUiThemeKit.WarmMutedText;
    private static Color DarkInk => RoyalUiThemeKit.DarkGoldInk;
    private static Color CrimsonRed => RoyalUiThemeKit.ArcadeCrimson;
    private static Color EmeraldReady => RoyalUiThemeKit.ArcadeJade;
    private static Color SlateSecondary => RoyalUiThemeKit.ArcadeSlate;

    private static readonly Color[] FactionColors =
    {
        new Color(0.22f, 0.62f, 1.00f, 1f), // 0: Xanh Dương
        new Color(1.00f, 0.28f, 0.30f, 1f), // 1: Đỏ
        new Color(0.22f, 0.88f, 0.44f, 1f), // 2: Xanh Lá
        new Color(1.00f, 0.82f, 0.20f, 1f)  // 3: Vàng
    };

    private static readonly string[] FactionNames =
    {
        "CHUỒNG XANH DƯƠNG",
        "CHUỒNG ĐỎ",
        "CHUỒNG XANH LÁ",
        "CHUỒNG VÀNG"
    };

    private enum TokenSpriteKind
    {
        Horse = 0,
        Dice = 1,
        Crown = 2,
        Star = 3,
        Check = 4
    }

    private struct FloatingToken
    {
        public RectTransform rect;
        public Vector2 basePos;
        public float bobSpeed;
        public float bobAmp;
        public float swayAmp;
        public float rotSpeed;
        public float phase;
    }

    private struct SeatCardView
    {
        public GameObject root;
        public Image cardBg;
        public Outline cardOutline;
        public Image factionStripe;
        public Image avatarBg;
        public Image skinPreviewImage;
        public Image avatarFallbackIcon;
        public TMP_Text seatHeaderText;
        public TMP_Text playerNameText;
        public TMP_Text skinNameText;
        public Image statusBadgeBg;
        public TMP_Text statusBadgeText;
    }

    private readonly List<FloatingToken> floatingTokens = new List<FloatingToken>();
    private readonly SeatCardView[] seatCards = new SeatCardView[4];
    private bool hasLobbySeatCards;
    private RectTransform glowBannerRect;
    private TMP_Text lobbyReadyBtnLabel;
    private Image lobbyReadyBtnBg;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        EnsureCreated();
    }

    public static RoyalMenuThemeStyler EnsureCreated()
    {
        if (Instance != null)
            return Instance;

        RoyalMenuThemeStyler existing = Object.FindFirstObjectByType<RoyalMenuThemeStyler>();
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        GameObject go = new GameObject("RoyalMenuThemeStyler");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<RoyalMenuThemeStyler>();
        return Instance;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        BgmManager.EnsureCreated();
        ApplyForScene(SceneManager.GetActiveScene().name);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyForScene(scene.name);
    }

    public void ApplyForScene(string sceneName)
    {
        floatingTokens.Clear();
        hasLobbySeatCards = false;
        glowBannerRect = null;
        lobbyReadyBtnLabel = null;
        lobbyReadyBtnBg = null;

        if (string.IsNullOrEmpty(sceneName))
            return;

        if (sceneName.IndexOf("Auth", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            StyleAuthScene();
        }
        else if (sceneName.IndexOf("Lobby", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            StyleLobbyScene();
        }

        BgmManager.EnsureCreated().AttachSfxToAllSceneButtons();
    }

    private void Update()
    {
        float t = Time.unscaledTime;

        // Animate lively background tokens
        for (int i = 0; i < floatingTokens.Count; i++)
        {
            FloatingToken tk = floatingTokens[i];
            if (tk.rect == null)
                continue;

            float yOffset = Mathf.Sin(t * tk.bobSpeed + tk.phase) * tk.bobAmp;
            float xOffset = Mathf.Cos(t * (tk.bobSpeed * 0.72f) + tk.phase) * tk.swayAmp;
            tk.rect.anchoredPosition = tk.basePos + new Vector2(xOffset, yOffset);
            tk.rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * tk.rotSpeed + tk.phase) * 10f);
        }

        if (glowBannerRect != null)
        {
            float pulse = 1f + Mathf.Sin(t * 2.2f) * 0.02f;
            glowBannerRect.localScale = new Vector3(pulse, pulse, 1f);
        }

        if (hasLobbySeatCards)
        {
            UpdateLobbySeatCardsLive();
        }
    }

    // =========================================================================
    // AUTH SCENE STYLING (Reference resolution: 1280 x 720)
    // =========================================================================
    private void StyleAuthScene()
    {
        GameObject bgObj = FindInActiveScene("Background");
        Transform canvasRoot = bgObj != null && bgObj.transform.parent != null ? bgObj.transform.parent : null;

        if (bgObj != null)
        {
            Image bgImg = bgObj.GetComponent<Image>();
            if (bgImg != null)
                bgImg.color = new Color(0.82f, 0.72f, 0.60f, 1f);

            BuildLivelyBoardBackdrop(bgObj.transform, true);
        }

        if (canvasRoot != null)
        {
            BuildTopRightQuitButton(canvasRoot);
        }

        GameObject authCard = FindInActiveScene("AuthCard");
        if (authCard != null)
        {
            RectTransform cardRect = authCard.GetComponent<RectTransform>();
            if (cardRect != null)
            {
                cardRect.anchoredPosition = new Vector2(0f, 4f);
                cardRect.sizeDelta = new Vector2(548f, 646f);
            }

            StyleMahoganyPanel(authCard, WoodDark, BrassGold, 2.2f);
            BuildRoyalHeaderDecor(authCard.transform);
            EnsureSectionRibbonPlaque(authCard.transform, "AuthTitlePlaque", new Vector2(0f, 240f), new Vector2(464f, 58f), RoyalUiThemeKit.CrimsonVelvet);
            BuildCardQuitButton(authCard.transform);
        }

        StyleText("Title", "CỜ CÁ NGỰA 3D", 32f, BrassBright, FontStyles.Bold);
        SetRectPositionAndSize("Title", new Vector2(0f, 240f), new Vector2(450f, 52f));

        StyleText("Subtitle", "ĐẤU TRƯỜNG PARTY CLASH — ĐĂNG NHẬP KỲ THỦ", 14f, CreamWhite, FontStyles.Bold);
        SetRectPositionAndSize("Subtitle", new Vector2(0f, 194f), new Vector2(480f, 30f));

        StyleText("StatusText", "Đăng nhập hoặc tạo tài khoản mới để vào sảnh", 13.5f, BrassGold, FontStyles.Italic);
        SetRectPositionAndSize("StatusText", new Vector2(0f, -242f), new Vector2(470f, 36f));

        string[] inputs =
        {
            "LoginUsernameInput",
            "LoginPasswordInput",
            "RegisterUsernameInput",
            "RegisterDisplayNameInput",
            "RegisterPasswordInput",
            "RegisterConfirmPasswordInput"
        };
        foreach (string inputName in inputs)
            StyleRoyalInputField(inputName);

        SetInputPlaceholder("LoginUsernameInput", "Tên đăng nhập (Username)...");
        SetInputPlaceholder("LoginPasswordInput", "Mật khẩu (Password)...");
        SetInputPlaceholder("RegisterUsernameInput", "Tên đăng nhập mới...");
        SetInputPlaceholder("RegisterDisplayNameInput", "Biệt danh hiển thị trong trận...");
        SetInputPlaceholder("RegisterPasswordInput", "Mật khẩu...");
        SetInputPlaceholder("RegisterConfirmPasswordInput", "Nhập lại mật khẩu...");

        StyleRoyalButton("LoginButton", RoyalUiThemeKit.ArcadeGoldCta, DarkInk, "ĐĂNG NHẬP VÀO SẢNH", true);
        StyleRoyalButton("RegisterButton", RoyalUiThemeKit.ArcadeGoldCta, DarkInk, "HOÀN TẤT ĐĂNG KÝ", true);
        StyleRoyalButton("ShowRegisterButton", SlateSecondary, CreamWhite, "Tạo Tài Khoản Mới", false);
        StyleRoyalButton("ShowLoginButton", SlateSecondary, CreamWhite, "Quay Lại Đăng Nhập", false);
        StyleRoyalButton("LoginPasswordToggle", WoodWarm, BrassBright, null, false);
        StyleRoyalButton("RegisterPasswordToggle", WoodWarm, BrassBright, null, false);
        StyleRoyalButton("RegisterConfirmPasswordToggle", WoodWarm, BrassBright, null, false);
    }

    // =========================================================================
    // LOBBY SCENE STYLING (Reference resolution: 1280 x 720)
    // =========================================================================
    private void StyleLobbyScene()
    {
        GameObject bgObj = FindInActiveScene("Background");
        Transform canvasRoot = bgObj != null && bgObj.transform.parent != null ? bgObj.transform.parent : null;

        if (bgObj != null)
        {
            Image bgImg = bgObj.GetComponent<Image>();
            if (bgImg != null)
                bgImg.color = new Color(0.82f, 0.72f, 0.60f, 1f);

            BuildLivelyBoardBackdrop(bgObj.transform, false);
        }

        if (canvasRoot != null)
        {
            BuildTopRightQuitButton(canvasRoot);
            BuildTopRightHelpButton(canvasRoot, new Vector2(-182f, -14f));
            EnsureLobbyTitlePlaque(canvasRoot);
        }

        // Cleanly position LobbyTitle and LobbySubtitle inside top-left header plaque
        StyleText("LobbyTitle", "SẢNH ĐẤU CỜ CÁ NGỰA 3D", 25.5f, BrassBright, FontStyles.Bold);
        SetRectPositionAndSize("LobbyTitle", new Vector2(-335f, 317f), new Vector2(490f, 36f));
        SetTextAlignment("LobbyTitle", TextAlignmentOptions.Left);

        StyleText("LobbySubtitle", "Tạo bàn chơi, chia sẻ Mã Phòng và sẵn sàng xuất quân!", 13.5f, CreamWhite, FontStyles.Normal);
        SetRectPositionAndSize("LobbySubtitle", new Vector2(-335f, 286f), new Vector2(490f, 24f));
        SetTextAlignment("LobbySubtitle", TextAlignmentOptions.Left);

        GameObject roomPanel = FindInActiveScene("RoomPanel");
        GameObject playersPanel = FindInActiveScene("PlayersPanel");

        if (roomPanel != null)
        {
            RectTransform rpRect = roomPanel.GetComponent<RectTransform>();
            if (rpRect != null)
            {
                rpRect.anchoredPosition = new Vector2(-320f, -34f);
                rpRect.sizeDelta = new Vector2(494f, 524f);
            }
            StyleMahoganyPanel(roomPanel, WoodDark, BrassGold, 2.2f);
            BuildFactionCornerGems(roomPanel.transform);
            EnsureSectionRibbonPlaque(roomPanel.transform, "RoomHeaderPlaque", new Vector2(0f, 212f), new Vector2(436f, 40f), RoyalUiThemeKit.CrimsonVelvet);
        }

        if (playersPanel != null)
        {
            RectTransform ppRect = playersPanel.GetComponent<RectTransform>();
            if (ppRect != null)
            {
                ppRect.anchoredPosition = new Vector2(290f, -34f);
                ppRect.sizeDelta = new Vector2(524f, 524f);
            }
            StyleMahoganyPanel(playersPanel, WoodDark, BrassGold, 2.2f);
            BuildFactionCornerGems(playersPanel.transform);
            EnsureSectionRibbonPlaque(playersPanel.transform, "PlayersHeaderPlaque", new Vector2(0f, 216f), new Vector2(466f, 40f), RoyalUiThemeKit.NavyRibbon);
        }

        StyleText("RoomHeader", "BÀN ĐIỀU KHIỂN PHÒNG", 19f, BrassBright, FontStyles.Bold);
        SetRectPositionAndSize("RoomHeader", new Vector2(0f, 212f), new Vector2(420f, 34f));
        SetTextAlignment("RoomHeader", TextAlignmentOptions.Center);

        StyleText("PlayersHeader", "SẢNH KỲ THỦ (4 CHUỒNG)", 19f, BrassBright, FontStyles.Bold);
        SetRectPositionAndSize("PlayersHeader", new Vector2(0f, 216f), new Vector2(440f, 34f));
        SetTextAlignment("PlayersHeader", TextAlignmentOptions.Center);

        // Reposition RoomPanel controls cleanly so nothing is cramped
        SetRectPositionAndSize("JoinCodeText", new Vector2(0f, 166f), new Vector2(430f, 34f));
        SetRectPositionAndSize("JoinCodeInput", new Vector2(0f, 114f), new Vector2(430f, 48f));
        SetRectPositionAndSize("CreateRoomButton", new Vector2(-112f, 46f), new Vector2(206f, 48f));
        SetRectPositionAndSize("JoinRoomButton", new Vector2(112f, 46f), new Vector2(206f, 48f));
        SetRectPositionAndSize("ReconnectButton", new Vector2(-112f, -14f), new Vector2(206f, 44f));
        SetRectPositionAndSize("LeaveRoomButton", new Vector2(112f, -14f), new Vector2(206f, 44f));
        SetRectPositionAndSize("CopyCodeButton", new Vector2(-112f, -72f), new Vector2(206f, 42f));
        SetRectPositionAndSize("BackToAuthButton", new Vector2(112f, -72f), new Vector2(206f, 42f));
        SetRectPositionAndSize("LobbyShopButton", new Vector2(-112f, -130f), new Vector2(206f, 46f));
        SetRectPositionAndSize("LobbySkinsButton", new Vector2(112f, -130f), new Vector2(206f, 46f));
        SetRectPositionAndSize("StatusRelay", new Vector2(0f, -210f), new Vector2(430f, 30f));

        // Reposition PlayersPanel bottom buttons & status text so 4 Seat Cards fit with generous breathing room
        SetRectPositionAndSize("ReadyButton", new Vector2(-114f, -172f), new Vector2(212f, 50f));
        SetRectPositionAndSize("StartGameButton", new Vector2(114f, -172f), new Vector2(212f, 50f));
        SetRectPositionAndSize("StatusText", new Vector2(0f, -226f), new Vector2(460f, 30f));

        if (playersPanel != null)
        {
            BuildLobbySeatCards(playersPanel.transform);
        }

        // Style JoinCodeText badge
        GameObject joinCodeObj = FindInActiveScene("JoinCodeText");
        if (joinCodeObj != null)
        {
            TMP_Text jcText = joinCodeObj.GetComponent<TMP_Text>();
            if (jcText != null)
            {
                jcText.color = BrassBright;
                jcText.fontStyle = FontStyles.Bold;
                jcText.fontSize = 18.5f;
            }
        }

        StyleRoyalInputField("JoinCodeInput");
        SetInputPlaceholder("JoinCodeInput", "Nhập Mã Phòng (VD: AB12CD)...");

        StyleRoyalButton("CreateRoomButton", RoyalUiThemeKit.ArcadeGoldCta, DarkInk, "Tạo Phòng", true);
        StyleRoyalButton("JoinRoomButton", RoyalUiThemeKit.ArcadeGoldCta, DarkInk, "Vào Phòng", true);
        StyleRoyalButton("ReconnectButton", SlateSecondary, CreamWhite, "Kết Nối Lại", false);
        StyleRoyalButton("LeaveRoomButton", SlateSecondary, CreamWhite, "Rời Phòng", false);
        StyleRoyalButton("CopyCodeButton", SlateSecondary, BrassBright, "Sao Chép Mã", false);
        StyleRoyalButton("BackToAuthButton", CrimsonRed, Color.white, "Đăng Xuất", false);
        StyleRoyalButton("LobbyShopButton", WoodWarm, BrassBright, "Cửa Hàng", false);
        StyleRoyalButton("LobbySkinsButton", WoodWarm, BrassBright, "Tủ Skin", false);

        StyleRoyalButton("ReadyButton", EmeraldReady, Color.white, "SẴN SÀNG", true);
        StyleRoyalButton("StartGameButton", RoyalUiThemeKit.ArcadeGoldCta, DarkInk, "BẮT ĐẦU TRẬN", true);

        GameObject readyBtnObj = FindInActiveScene("ReadyButton");
        if (readyBtnObj != null)
        {
            lobbyReadyBtnBg = readyBtnObj.GetComponent<Image>();
            lobbyReadyBtnLabel = readyBtnObj.GetComponentInChildren<TMP_Text>(true);
        }

        // Hide plain PlayerListText visual alpha (keep active so LobbyManager still writes to it without null error)
        GameObject playerListObj = FindInActiveScene("PlayerListText");
        if (playerListObj != null)
        {
            TMP_Text plText = playerListObj.GetComponent<TMP_Text>();
            if (plText != null)
            {
                plText.color = new Color(1f, 1f, 1f, 0f);
                plText.raycastTarget = false;
            }
        }

        StyleText("StatusRelay", null, 14f, MutedCream, FontStyles.Italic);
        StyleText("StatusText", null, 14.5f, BrassBright, FontStyles.Bold);
    }

    // =========================================================================
    // 4 VISUAL FACTION SEAT CARDS IN LOBBY
    // =========================================================================
    private void BuildLobbySeatCards(Transform playersPanel)
    {
        Transform existingRoot = playersPanel.Find("RoyalSeatCardsContainer");
        if (existingRoot != null)
            Destroy(existingRoot.gameObject);

        GameObject container = new GameObject("RoyalSeatCardsContainer", typeof(RectTransform));
        container.transform.SetParent(playersPanel, false);

        RectTransform cRect = container.GetComponent<RectTransform>();
        cRect.anchorMin = new Vector2(0.5f, 0.5f);
        cRect.anchorMax = new Vector2(0.5f, 0.5f);
        cRect.pivot = new Vector2(0.5f, 0.5f);
        // Centered at y = 26, height = 310 -> top = +181 (well below PlayersHeader at +216), bottom = -129 (well above ReadyButton at -172)
        cRect.anchoredPosition = new Vector2(0f, 26f);
        cRect.sizeDelta = new Vector2(476f, 310f);

        float cardHeight = 68f;
        float spacing = 8f;
        float startY = 114f;

        for (int i = 0; i < 4; i++)
        {
            float y = startY - i * (cardHeight + spacing);
            seatCards[i] = CreateSingleSeatCard(container.transform, i, new Vector2(0f, y), new Vector2(472f, cardHeight));
        }

        hasLobbySeatCards = true;
        UpdateLobbySeatCardsLive();
    }

    private SeatCardView CreateSingleSeatCard(Transform parent, int seatIndex, Vector2 pos, Vector2 size)
    {
        SeatCardView view = new SeatCardView();
        Color factionCol = FactionColors[seatIndex % FactionColors.Length];

        GameObject card = new GameObject($"SeatCard_{seatIndex}", typeof(RectTransform), typeof(Image), typeof(Outline), typeof(Shadow));
        card.transform.SetParent(parent, false);
        view.root = card;

        RectTransform rect = card.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;

        view.cardBg = card.GetComponent<Image>();
        view.cardBg.sprite = RoyalUiThemeKit.GetLacquerPanelSprite();
        view.cardBg.type = Image.Type.Sliced;
        view.cardBg.color = WoodInset;

        view.cardOutline = card.GetComponent<Outline>();
        view.cardOutline.effectColor = new Color(factionCol.r, factionCol.g, factionCol.b, 0.55f);
        view.cardOutline.effectDistance = new Vector2(1.5f, -1.5f);

        Shadow shadow = card.GetComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
        shadow.effectDistance = new Vector2(0f, -3f);

        // Left Faction Color Stripe
        GameObject stripe = new GameObject("FactionStripe", typeof(RectTransform), typeof(Image));
        stripe.transform.SetParent(card.transform, false);
        RectTransform stripeRect = stripe.GetComponent<RectTransform>();
        stripeRect.anchorMin = new Vector2(0f, 0.1f);
        stripeRect.anchorMax = new Vector2(0f, 0.9f);
        stripeRect.pivot = new Vector2(0f, 0.5f);
        stripeRect.anchoredPosition = new Vector2(4f, 0f);
        stripeRect.sizeDelta = new Vector2(6f, 0f);
        view.factionStripe = stripe.GetComponent<Image>();
        view.factionStripe.sprite = RoyalUiThemeKit.GetPillCapsuleSprite();
        view.factionStripe.type = Image.Type.Sliced;
        view.factionStripe.color = factionCol;

        // Avatar / Skin Portrait Frame
        GameObject avatarObj = new GameObject("AvatarFrame", typeof(RectTransform), typeof(Image), typeof(Outline));
        avatarObj.transform.SetParent(card.transform, false);
        RectTransform avRect = avatarObj.GetComponent<RectTransform>();
        avRect.anchorMin = avRect.anchorMax = new Vector2(0f, 0.5f);
        avRect.pivot = new Vector2(0f, 0.5f);
        avRect.anchoredPosition = new Vector2(18f, 0f);
        avRect.sizeDelta = new Vector2(48f, 48f);

        view.avatarBg = avatarObj.GetComponent<Image>();
        view.avatarBg.sprite = RoyalUiThemeKit.GetRecessedSlotSprite();
        view.avatarBg.type = Image.Type.Sliced;
        view.avatarBg.color = new Color(factionCol.r * 0.30f, factionCol.g * 0.30f, factionCol.b * 0.30f, 0.95f);

        Outline avOutline = avatarObj.GetComponent<Outline>();
        avOutline.effectColor = factionCol;
        avOutline.effectDistance = new Vector2(1.5f, -1.5f);

        GameObject previewObj = new GameObject("SkinPreview", typeof(RectTransform), typeof(Image));
        previewObj.transform.SetParent(avatarObj.transform, false);
        RectTransform prevRect = previewObj.GetComponent<RectTransform>();
        prevRect.anchorMin = Vector2.zero;
        prevRect.anchorMax = Vector2.one;
        prevRect.offsetMin = new Vector2(3f, 3f);
        prevRect.offsetMax = new Vector2(-3f, -3f);
        view.skinPreviewImage = previewObj.GetComponent<Image>();
        view.skinPreviewImage.preserveAspect = true;
        view.skinPreviewImage.enabled = false;

        GameObject iconObj = new GameObject("FallbackSpriteIcon", typeof(RectTransform), typeof(Image));
        iconObj.transform.SetParent(avatarObj.transform, false);
        RectTransform icRect = iconObj.GetComponent<RectTransform>();
        icRect.anchorMin = Vector2.zero;
        icRect.anchorMax = Vector2.one;
        icRect.offsetMin = new Vector2(8f, 8f);
        icRect.offsetMax = new Vector2(-8f, -8f);
        view.avatarFallbackIcon = iconObj.GetComponent<Image>();
        view.avatarFallbackIcon.sprite = GetOrCreateSprite(TokenSpriteKind.Horse);
        view.avatarFallbackIcon.preserveAspect = true;
        view.avatarFallbackIcon.color = factionCol;
        view.avatarFallbackIcon.raycastTarget = false;

        // Seat Header ("CHUỒNG XANH DƯƠNG - VỊ TRÍ #1")
        view.seatHeaderText = CreateTmpLabel(card.transform, "SeatHeader", $"{FactionNames[seatIndex]} - VỊ TRÍ #{seatIndex + 1}", 11f, factionCol, FontStyles.Bold, TextAlignmentOptions.Left);
        view.seatHeaderText.overflowMode = TextOverflowModes.Ellipsis;
        view.seatHeaderText.textWrappingMode = TextWrappingModes.NoWrap;
        RectTransform shRect = view.seatHeaderText.rectTransform;
        shRect.anchorMin = shRect.anchorMax = new Vector2(0f, 0.5f);
        shRect.pivot = new Vector2(0f, 0.5f);
        shRect.anchoredPosition = new Vector2(78f, 18f);
        shRect.sizeDelta = new Vector2(256f, 16f);

        // Player Display Name (Single line, no wrap so it never collides with SkinLabel)
        view.playerNameText = CreateTmpLabel(card.transform, "PlayerName", "Ghế trống - Chờ người vào...", 15.5f, MutedCream, FontStyles.Bold, TextAlignmentOptions.Left);
        view.playerNameText.overflowMode = TextOverflowModes.Ellipsis;
        view.playerNameText.textWrappingMode = TextWrappingModes.NoWrap;
        RectTransform pnRect = view.playerNameText.rectTransform;
        pnRect.anchorMin = pnRect.anchorMax = new Vector2(0f, 0.5f);
        pnRect.pivot = new Vector2(0f, 0.5f);
        pnRect.anchoredPosition = new Vector2(78f, 0f);
        pnRect.sizeDelta = new Vector2(256f, 20f);

        // Equipped Skin Label
        view.skinNameText = CreateTmpLabel(card.transform, "SkinLabel", "Chưa có kỳ thủ", 12f, BrassGold, FontStyles.Normal, TextAlignmentOptions.Left);
        view.skinNameText.overflowMode = TextOverflowModes.Ellipsis;
        view.skinNameText.textWrappingMode = TextWrappingModes.NoWrap;
        RectTransform skRect = view.skinNameText.rectTransform;
        skRect.anchorMin = skRect.anchorMax = new Vector2(0f, 0.5f);
        skRect.pivot = new Vector2(0f, 0.5f);
        skRect.anchoredPosition = new Vector2(78f, -18f);
        skRect.sizeDelta = new Vector2(256f, 16f);

        // Status Badge on the right
        GameObject badgeObj = new GameObject("StatusBadge", typeof(RectTransform), typeof(Image), typeof(Outline));
        badgeObj.transform.SetParent(card.transform, false);
        RectTransform bRect = badgeObj.GetComponent<RectTransform>();
        bRect.anchorMin = bRect.anchorMax = new Vector2(1f, 0.5f);
        bRect.pivot = new Vector2(1f, 0.5f);
        bRect.anchoredPosition = new Vector2(-14f, 0f);
        bRect.sizeDelta = new Vector2(110f, 32f);

        view.statusBadgeBg = badgeObj.GetComponent<Image>();
        view.statusBadgeBg.sprite = RoyalUiThemeKit.GetPillCapsuleSprite();
        view.statusBadgeBg.type = Image.Type.Sliced;
        view.statusBadgeBg.color = new Color(0.18f, 0.14f, 0.11f, 0.92f);

        Outline badgeOutline = badgeObj.GetComponent<Outline>();
        badgeOutline.effectColor = new Color(0.92f, 0.73f, 0.28f, 0.45f);
        badgeOutline.effectDistance = new Vector2(1f, -1f);

        view.statusBadgeText = CreateTmpLabel(badgeObj.transform, "BadgeText", "TRỐNG", 12f, MutedCream, FontStyles.Bold, TextAlignmentOptions.Center);
        StretchFull(view.statusBadgeText.rectTransform);

        return view;
    }

    private void UpdateLobbySeatCardsLive()
    {
        LobbyManager lobby = LobbyManager.Instance;
        SkinCatalog catalog = SkinCatalog.Load();

        bool isConnectedRoom = lobby != null &&
                               NetworkManager.Singleton != null &&
                               NetworkManager.Singleton.IsListening &&
                               lobby.playerClientIds != null &&
                               lobby.readyStates != null;

        int count = isConnectedRoom
            ? Mathf.Min(lobby.playerClientIds.Count, lobby.readyStates.Count)
            : 0;

        ulong localClientId = NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : ulong.MaxValue;
        bool localReady = false;

        for (int i = 0; i < 4; i++)
        {
            SeatCardView view = seatCards[i];
            if (view.root == null)
                continue;

            Color factionCol = FactionColors[i % FactionColors.Length];

            if (i < count)
            {
                bool isLocal = lobby.playerClientIds[i] == localClientId;
                bool isReady = lobby.readyStates[i];
                if (isLocal)
                    localReady = isReady;

                string pName = i < lobby.playerNames.Count ? lobby.playerNames[i].ToString() : $"Kỳ Thủ {i + 1}";
                if (string.IsNullOrWhiteSpace(pName))
                    pName = $"Kỳ Thủ {i + 1}";
                if (isLocal)
                    pName += " (Bạn)";

                string skinId = i < lobby.playerSkinIds.Count ? lobby.playerSkinIds[i].ToString() : SkinCatalog.DefaultSkinId;
                SkinDefinition skinDef = catalog != null ? catalog.Get(skinId) : null;
                string skinDisplay = skinDef != null && !string.IsNullOrWhiteSpace(skinDef.displayName)
                    ? skinDef.displayName
                    : "Ngựa Truyền Thống";

                view.playerNameText.text = pName;
                view.playerNameText.color = CreamWhite;
                view.skinNameText.text = $"Chiến mã: {skinDisplay}";
                view.skinNameText.color = BrassBright;

                if (skinDef != null && skinDef.previewImage != null)
                {
                    view.skinPreviewImage.sprite = skinDef.previewImage;
                    view.skinPreviewImage.enabled = true;
                    view.avatarFallbackIcon.enabled = false;
                }
                else
                {
                    view.skinPreviewImage.enabled = false;
                    view.avatarFallbackIcon.enabled = true;
                    view.avatarFallbackIcon.sprite = GetOrCreateSprite(TokenSpriteKind.Horse);
                    view.avatarFallbackIcon.color = CreamWhite;
                }

                view.cardBg.color = isLocal
                    ? new Color(0.24f, 0.15f, 0.08f, 0.98f)
                    : new Color(0.15f, 0.10f, 0.06f, 0.96f);

                view.cardOutline.effectColor = isReady
                    ? EmeraldReady
                    : (isLocal ? BrassBright : factionCol);

                if (isReady)
                {
                    view.statusBadgeBg.color = EmeraldReady;
                    view.statusBadgeText.text = "SẴN SÀNG";
                    view.statusBadgeText.color = Color.white;
                }
                else
                {
                    view.statusBadgeBg.color = new Color(0.55f, 0.36f, 0.10f, 0.95f);
                    view.statusBadgeText.text = "ĐANG CHỜ...";
                    view.statusBadgeText.color = BrassBright;
                }
            }
            else
            {
                view.playerNameText.text = "Ghế trống - Chờ người vào...";
                view.playerNameText.color = new Color(0.76f, 0.70f, 0.60f, 0.88f);
                view.skinNameText.text = "Chưa có kỳ thủ";
                view.skinNameText.color = new Color(0.62f, 0.55f, 0.46f, 0.78f);
                view.skinPreviewImage.enabled = false;
                view.avatarFallbackIcon.enabled = true;
                view.avatarFallbackIcon.sprite = GetOrCreateSprite(TokenSpriteKind.Horse);
                view.avatarFallbackIcon.color = new Color(factionCol.r, factionCol.g, factionCol.b, 0.75f);

                // Subtle dark mahogany tinted slightly by faction color
                view.cardBg.color = new Color(
                    Mathf.Lerp(0.10f, factionCol.r * 0.25f, 0.35f),
                    Mathf.Lerp(0.06f, factionCol.g * 0.25f, 0.35f),
                    Mathf.Lerp(0.04f, factionCol.b * 0.25f, 0.35f),
                    0.92f);
                view.cardOutline.effectColor = new Color(factionCol.r, factionCol.g, factionCol.b, 0.45f);
                view.statusBadgeBg.color = new Color(0.18f, 0.13f, 0.09f, 0.88f);
                view.statusBadgeText.text = "TRỐNG";
                view.statusBadgeText.color = new Color(0.72f, 0.66f, 0.56f, 0.85f);
            }
        }

        if (lobbyReadyBtnLabel != null && lobbyReadyBtnBg != null)
        {
            if (isConnectedRoom && localReady)
            {
                lobbyReadyBtnLabel.text = "HỦY SẴN SÀNG";
                lobbyReadyBtnBg.color = new Color(0.68f, 0.42f, 0.12f, 1f);
            }
            else
            {
                lobbyReadyBtnLabel.text = "SẴN SÀNG";
                lobbyReadyBtnBg.color = EmeraldReady;
            }
        }
    }

    // =========================================================================
    // LIVELY BOARD-GAME BACKDROP & PROCEDURAL SPRITE TOKENS (1280 x 720)
    // =========================================================================
    private void BuildLivelyBoardBackdrop(Transform backgroundTransform, bool isAuthScene)
    {
        Transform old = backgroundTransform.Find("LivelyBoardAtmosphere");
        if (old != null)
            Destroy(old.gameObject);

        GameObject root = new GameObject("LivelyBoardAtmosphere", typeof(RectTransform));
        root.transform.SetParent(backgroundTransform, false);
        RectTransform rootRect = root.GetComponent<RectTransform>();
        StretchFull(rootRect);

        // Warm golden vignette bar at top & bottom
        CreateEdgeTrim(root.transform, true);
        CreateEdgeTrim(root.transform, false);

        // Positions tailored for 1280x720 reference canvas (x in [-600..600], y in [-320..320])
        Vector2[] positions = isAuthScene
            ? new[]
            {
                new Vector2(-460f, 190f),
                new Vector2(460f, 190f),
                new Vector2(-480f, -170f),
                new Vector2(480f, -170f),
                new Vector2(-370f, 10f),
                new Vector2(370f, 10f),
                new Vector2(-350f, 275f),
                new Vector2(350f, 275f)
            }
            : new[]
            {
                new Vector2(-602f, 140f),
                new Vector2(598f, 140f),
                new Vector2(-602f, -140f),
                new Vector2(598f, -140f),
                new Vector2(-15f, 185f),
                new Vector2(-15f, -185f),
                new Vector2(120f, 312f),
                new Vector2(310f, 312f)
            };

        TokenSpriteKind[] kinds =
        {
            TokenSpriteKind.Horse,
            TokenSpriteKind.Horse,
            TokenSpriteKind.Horse,
            TokenSpriteKind.Horse,
            TokenSpriteKind.Dice,
            TokenSpriteKind.Dice,
            TokenSpriteKind.Crown,
            TokenSpriteKind.Star
        };

        for (int i = 0; i < positions.Length; i++)
        {
            Color col = i < 4 ? FactionColors[i] : BrassGold;
            GameObject tokenObj = new GameObject($"FloatingToken_{i}", typeof(RectTransform), typeof(Image), typeof(Outline), typeof(Shadow));
            tokenObj.transform.SetParent(root.transform, false);

            RectTransform rt = tokenObj.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = positions[i];
            float size = i < 4 ? 50f : 42f;
            rt.sizeDelta = new Vector2(size, size);

            Image badgeBg = tokenObj.GetComponent<Image>();
            badgeBg.sprite = RoyalUiThemeKit.GetCircleMedallionSprite();
            badgeBg.color = new Color(0.15f, 0.09f, 0.055f, 0.88f);
            badgeBg.raycastTarget = false;

            Outline outline = tokenObj.GetComponent<Outline>();
            outline.effectColor = new Color(col.r, col.g, col.b, 0.65f);
            outline.effectDistance = new Vector2(1.4f, -1.4f);

            Shadow shadow = tokenObj.GetComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
            shadow.effectDistance = new Vector2(0f, -3f);

            GameObject spriteChild = new GameObject("IconSprite", typeof(RectTransform), typeof(Image));
            spriteChild.transform.SetParent(tokenObj.transform, false);
            RectTransform scRect = spriteChild.GetComponent<RectTransform>();
            scRect.anchorMin = Vector2.zero;
            scRect.anchorMax = Vector2.one;
            scRect.offsetMin = new Vector2(10f, 10f);
            scRect.offsetMax = new Vector2(-10f, -10f);

            Image spImg = spriteChild.GetComponent<Image>();
            spImg.sprite = GetOrCreateSprite(kinds[i % kinds.Length]);
            spImg.preserveAspect = true;
            spImg.color = new Color(col.r, col.g, col.b, 0.95f);
            spImg.raycastTarget = false;

            floatingTokens.Add(new FloatingToken
            {
                rect = rt,
                basePos = positions[i],
                bobSpeed = 1.35f + (i % 4) * 0.25f,
                bobAmp = 8f + (i % 3) * 3f,
                swayAmp = 4f + (i % 2) * 3f,
                rotSpeed = 0.9f + (i % 3) * 0.22f,
                phase = i * 0.85f
            });
        }
    }

    private void CreateEdgeTrim(Transform parent, bool isTop)
    {
        GameObject trim = new GameObject(isTop ? "TopGoldTrim" : "BottomGoldTrim", typeof(RectTransform), typeof(Image));
        trim.transform.SetParent(parent, false);
        RectTransform rt = trim.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, isTop ? 1f : 0f);
        rt.anchorMax = new Vector2(1f, isTop ? 1f : 0f);
        rt.pivot = new Vector2(0.5f, isTop ? 1f : 0f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(0f, 4f);

        Image img = trim.GetComponent<Image>();
        img.color = new Color(BrassGold.r, BrassGold.g, BrassGold.b, 0.78f);
        img.raycastTarget = false;
    }

    private void BuildRoyalHeaderDecor(Transform cardTransform)
    {
        Transform old = cardTransform.Find("RoyalHeaderBanner");
        if (old != null)
            Destroy(old.gameObject);

        GameObject banner = new GameObject("RoyalHeaderBanner", typeof(RectTransform));
        banner.transform.SetParent(cardTransform, false);
        RectTransform rt = banner.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -14f);
        rt.sizeDelta = new Vector2(220f, 18f);
        glowBannerRect = rt;

        // 4 Faction color gems at the top of AuthCard
        float startX = -54f;
        for (int i = 0; i < 4; i++)
        {
            GameObject gem = new GameObject($"FactionGem_{i}", typeof(RectTransform), typeof(Image), typeof(Outline));
            gem.transform.SetParent(banner.transform, false);
            RectTransform gRect = gem.GetComponent<RectTransform>();
            gRect.anchorMin = gRect.anchorMax = new Vector2(0.5f, 0.5f);
            gRect.anchoredPosition = new Vector2(startX + i * 36f, 0f);
            gRect.sizeDelta = new Vector2(22f, 10f);

            Image gImg = gem.GetComponent<Image>();
            gImg.color = FactionColors[i];
            gImg.raycastTarget = false;

            Outline gOut = gem.GetComponent<Outline>();
            gOut.effectColor = BrassBright;
            gOut.effectDistance = new Vector2(1f, -1f);
        }
    }

    private void BuildFactionCornerGems(Transform panelTransform)
    {
        Transform old = panelTransform.Find("CornerFactionBar");
        if (old != null)
            Destroy(old.gameObject);

        GameObject bar = new GameObject("CornerFactionBar", typeof(RectTransform));
        bar.transform.SetParent(panelTransform, false);
        RectTransform rt = bar.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -4f);
        rt.sizeDelta = new Vector2(-12f, 5f);

        for (int i = 0; i < 4; i++)
        {
            GameObject seg = new GameObject($"Seg_{i}", typeof(RectTransform), typeof(Image));
            seg.transform.SetParent(bar.transform, false);
            RectTransform sRect = seg.GetComponent<RectTransform>();
            sRect.anchorMin = new Vector2(i * 0.25f, 0f);
            sRect.anchorMax = new Vector2((i + 1) * 0.25f, 1f);
            sRect.offsetMin = new Vector2(2f, 0f);
            sRect.offsetMax = new Vector2(-2f, 0f);

            Image sImg = seg.GetComponent<Image>();
            sImg.sprite = RoyalUiThemeKit.GetPillCapsuleSprite();
            sImg.type = Image.Type.Sliced;
            sImg.color = FactionColors[i];
            sImg.raycastTarget = false;
        }
    }

    private static void EnsureSectionRibbonPlaque(Transform parent, string plaqueName, Vector2 anchoredPos, Vector2 size, Color fillTint)
    {
        if (parent == null)
            return;

        Transform existing = parent.Find(plaqueName);
        if (existing != null)
            Destroy(existing.gameObject);

        GameObject plaque = new GameObject(plaqueName, typeof(RectTransform), typeof(Image), typeof(Outline), typeof(Shadow));
        plaque.transform.SetParent(parent, false);
        plaque.transform.SetAsFirstSibling();

        RectTransform rt = plaque.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        Image img = plaque.GetComponent<Image>();
        img.sprite = RoyalUiThemeKit.GetRibbonHeaderSprite();
        img.type = Image.Type.Sliced;
        img.color = fillTint;
        img.raycastTarget = false;

        Outline ol = plaque.GetComponent<Outline>();
        ol.effectColor = BrassGold;
        ol.effectDistance = new Vector2(1.4f, -1.4f);

        Shadow sh = plaque.GetComponent<Shadow>();
        sh.effectColor = new Color(0f, 0f, 0f, 0.55f);
        sh.effectDistance = new Vector2(0f, -3f);
    }

    private static void EnsureLobbyTitlePlaque(Transform canvasRoot)
    {
        if (canvasRoot == null)
            return;

        Transform existing = canvasRoot.Find("RoyalLobbyTitlePlaque");
        if (existing != null)
            Destroy(existing.gameObject);

        GameObject plaque = new GameObject("RoyalLobbyTitlePlaque", typeof(RectTransform));
        plaque.transform.SetParent(canvasRoot, false);

        RectTransform rt = plaque.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(-335f, 304f);
        rt.sizeDelta = new Vector2(548f, 76f);

        RoyalUiThemeKit.ApplyLacquerPanel(plaque, RoyalUiThemeKit.CrimsonVelvet, BrassGold, 1.8f, false);

        // Place behind LobbyTitle and LobbySubtitle in sibling order
        GameObject lobbyTitle = FindInActiveScene("LobbyTitle");
        if (lobbyTitle != null && lobbyTitle.transform.parent == canvasRoot)
            plaque.transform.SetSiblingIndex(Mathf.Max(0, lobbyTitle.transform.GetSiblingIndex()));
    }

    private static void BuildTopRightHelpButton(Transform canvasRoot, Vector2 anchoredPos)
    {
        RoyalUiThemeKit.BuildHelpGuideButton(canvasRoot, new Vector2(1f, 1f), anchoredPos, 40f);
    }

    // =========================================================================
    // QUIT GAME ("THOÁT GAME") BUTTONS
    // =========================================================================
    private void BuildTopRightQuitButton(Transform canvasRoot)
    {
        Transform existing = canvasRoot.Find("RoyalQuitGameButton");
        if (existing != null)
            Destroy(existing.gameObject);

        GameObject btnObj = new GameObject("RoyalQuitGameButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline), typeof(Shadow));
        btnObj.transform.SetParent(canvasRoot, false);

        RectTransform rt = btnObj.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-18f, -14f);
        rt.sizeDelta = new Vector2(154f, 40f);

        RoyalUiThemeKit.ApplyArcadeButton(btnObj, CrimsonRed, BrassGold, 1.5f);

        Button btn = btnObj.GetComponent<Button>();
        btn.targetGraphic = btnObj.GetComponent<Image>();
        btn.onClick.AddListener(QuitApplication);

        GameObject iconObj = new GameObject("ExitIcon", typeof(RectTransform), typeof(Image));
        iconObj.transform.SetParent(rt, false);
        RectTransform iRt = iconObj.GetComponent<RectTransform>();
        iRt.anchorMin = iRt.anchorMax = new Vector2(0f, 0.5f);
        iRt.pivot = new Vector2(0f, 0.5f);
        iRt.anchoredPosition = new Vector2(11f, 1f);
        iRt.sizeDelta = new Vector2(19f, 19f);
        Image iImg = iconObj.GetComponent<Image>();
        iImg.sprite = RoyalUiThemeKit.GetIcon(RoyalUiThemeKit.IconKind.DoorExit);
        iImg.preserveAspect = true;
        iImg.color = Color.white;
        iImg.raycastTarget = false;

        TMP_Text label = CreateTmpLabel(btnObj.transform, "QuitLabel", "THOÁT GAME", 13.5f, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
        RectTransform lRt = label.rectTransform;
        lRt.anchorMin = Vector2.zero;
        lRt.anchorMax = Vector2.one;
        lRt.offsetMin = new Vector2(28f, 2f);
        lRt.offsetMax = new Vector2(-8f, -1f);

        AttachHoverScale(btnObj);
    }

    private void BuildCardQuitButton(Transform authCard)
    {
        Transform existing = authCard.Find("AuthCardQuitButton");
        if (existing != null)
            Destroy(existing.gameObject);

        GameObject btnObj = new GameObject("AuthCardQuitButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline), typeof(Shadow));
        btnObj.transform.SetParent(authCard, false);

        RectTransform rt = btnObj.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 14f);
        rt.sizeDelta = new Vector2(210f, 36f);

        RoyalUiThemeKit.ApplyArcadeButton(btnObj, new Color(0.56f, 0.14f, 0.16f, 0.98f), new Color(0.92f, 0.73f, 0.28f, 0.85f), 1.3f);

        Button btn = btnObj.GetComponent<Button>();
        btn.targetGraphic = btnObj.GetComponent<Image>();
        btn.onClick.AddListener(QuitApplication);

        TMP_Text label = CreateTmpLabel(btnObj.transform, "Label", "Thoát Trò Chơi", 13.5f, CreamWhite, FontStyles.Bold, TextAlignmentOptions.Center);
        StretchFull(label.rectTransform);

        AttachHoverScale(btnObj);
    }

    public static void QuitApplication()
    {
        Debug.Log("[RoyalMenuThemeStyler] Quitting application...");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // =========================================================================
    // PROCEDURAL SPRITE GENERATOR (NO UNICODE FONT DEPENDENCY)
    // =========================================================================
    private static Sprite GetOrCreateSprite(TokenSpriteKind kind)
    {
        switch (kind)
        {
            case TokenSpriteKind.Horse: return RoyalUiThemeKit.GetIcon(RoyalUiThemeKit.IconKind.Horse);
            case TokenSpriteKind.Dice: return RoyalUiThemeKit.GetIcon(RoyalUiThemeKit.IconKind.Dice);
            case TokenSpriteKind.Crown: return RoyalUiThemeKit.GetIcon(RoyalUiThemeKit.IconKind.Crown);
            case TokenSpriteKind.Star: return RoyalUiThemeKit.GetIcon(RoyalUiThemeKit.IconKind.Star);
            case TokenSpriteKind.Check: return RoyalUiThemeKit.GetIcon(RoyalUiThemeKit.IconKind.Check);
            default: return RoyalUiThemeKit.GetIcon(RoyalUiThemeKit.IconKind.Star);
        }
    }

    // =========================================================================
    // COMPONENT STYLING HELPERS
    // =========================================================================
    private static void StyleMahoganyPanel(GameObject panel, Color bgCol, Color borderCol, float borderWidth)
    {
        RoyalUiThemeKit.ApplyLacquerPanel(panel, bgCol, borderCol, borderWidth, true);
    }

    private static void SetRectPositionAndSize(string objectName, Vector2 anchoredPos, Vector2 sizeDelta)
    {
        GameObject go = FindInActiveScene(objectName);
        if (go == null)
            return;

        RectTransform rt = go.GetComponent<RectTransform>();
        if (rt == null)
            return;

        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;
    }

    private static void SetTextAlignment(string objectName, TextAlignmentOptions alignment)
    {
        GameObject go = FindInActiveScene(objectName);
        if (go == null)
            return;

        TMP_Text txt = go.GetComponent<TMP_Text>();
        if (txt != null)
            txt.alignment = alignment;
    }

    private static void StyleText(string objectName, string overrideText, float fontSize, Color color, FontStyles style)
    {
        GameObject go = FindInActiveScene(objectName);
        if (go == null)
            return;

        TMP_Text txt = go.GetComponent<TMP_Text>();
        if (txt == null)
            return;

        if (overrideText != null)
            txt.text = overrideText;
        txt.fontSize = fontSize;
        txt.color = color;
        txt.fontStyle = style;
    }

    private static void StyleRoyalInputField(string objectName)
    {
        GameObject go = FindInActiveScene(objectName);
        if (go == null)
            return;

        TMP_InputField input = go.GetComponent<TMP_InputField>();
        if (input == null)
            return;

        RoyalUiThemeKit.ApplyRecessedSlot(go, WoodInset, new Color(BrassGold.r, BrassGold.g, BrassGold.b, 0.78f), 1.4f);

        MaskedPasswordInput masked = input.GetComponent<MaskedPasswordInput>();
        if (masked != null)
        {
            // Keep input.textComponent transparent so it NEVER overlaps MaskedPasswordInput.displayText!
            if (input.textComponent != null)
                input.textComponent.color = new Color(0f, 0f, 0f, 0f);
            if (masked.displayText != null)
                masked.displayText.color = CreamWhite;
        }
        else if (input.textComponent != null)
        {
            input.textComponent.color = CreamWhite;
        }

        if (input.placeholder is TMP_Text ph)
            ph.color = MutedCream;
    }

    private static void SetInputPlaceholder(string objectName, string placeholderValue)
    {
        GameObject go = FindInActiveScene(objectName);
        if (go == null)
            return;

        TMP_InputField input = go.GetComponent<TMP_InputField>();
        if (input != null && input.placeholder is TMP_Text ph)
            ph.text = placeholderValue;
    }

    private static void StyleRoyalButton(string objectName, Color bgCol, Color fgCol, string labelOverride, bool isPrimaryGold)
    {
        GameObject go = FindInActiveScene(objectName);
        if (go == null)
            return;

        Button btn = go.GetComponent<Button>();
        if (btn == null)
            return;

        Color border = isPrimaryGold ? BrassBright : new Color(BrassGold.r, BrassGold.g, BrassGold.b, 0.72f);
        RoyalUiThemeKit.ApplyArcadeButton(go, bgCol, border, isPrimaryGold ? 1.6f : 1.3f);

        TMP_Text label = btn.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            if (!string.IsNullOrEmpty(labelOverride))
                label.text = labelOverride;
            label.color = fgCol;
            label.fontStyle = FontStyles.Bold;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
        }

        Transform iconTr = btn.transform.Find("FunctionIcon");
        if (iconTr != null)
        {
            Image iconImg = iconTr.GetComponent<Image>();
            if (iconImg != null)
            {
                if (objectName == "LobbyShopButton")
                    iconImg.sprite = RoyalUiThemeKit.GetIcon(RoyalUiThemeKit.IconKind.ShopStore);
                else if (objectName == "LobbySkinsButton")
                    iconImg.sprite = RoyalUiThemeKit.GetIcon(RoyalUiThemeKit.IconKind.SkinTunic);

                iconImg.color = fgCol;
            }

            if (objectName == "StartGameButton" && label != null)
            {
                RectTransform iconRt = iconTr.GetComponent<RectTransform>();
                if (iconRt != null)
                    iconRt.anchoredPosition = new Vector2(18f, 0f);
                RectTransform lblRt = label.rectTransform;
                if (lblRt.anchorMin == lblRt.anchorMax)
                {
                    lblRt.anchoredPosition = new Vector2(14f, 0f);
                    lblRt.sizeDelta = new Vector2(164f, 48f);
                }
                else
                {
                    lblRt.offsetMin = new Vector2(34f, 0f);
                    lblRt.offsetMax = new Vector2(-6f, 0f);
                }
                label.fontSize = 15.5f;
            }
        }

        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1f, 0.97f, 0.88f, 1f);
        cb.pressedColor = new Color(0.84f, 0.78f, 0.68f, 1f);
        cb.selectedColor = cb.highlightedColor;
        btn.colors = cb;

        RoyalButtonHoverEffect hover = AttachHoverScale(go);
        if (hover != null)
        {
            bool pulseCta = isPrimaryGold && (objectName == "LoginButton" || objectName == "RegisterButton" || objectName == "StartGameButton");
            hover.SetIdlePulse(pulseCta);
        }
    }

    private static RoyalButtonHoverEffect AttachHoverScale(GameObject target)
    {
        RoyalButtonHoverEffect effect = target.GetComponent<RoyalButtonHoverEffect>();
        if (effect == null)
            effect = target.AddComponent<RoyalButtonHoverEffect>();
        return effect;
    }

    private static TMP_Text CreateTmpLabel(Transform parent, string name, string text, float size, Color color, FontStyles style, TextAlignmentOptions align)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null)
            tmp.font = TMP_Settings.defaultFontAsset;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.fontStyle = style;
        tmp.alignment = align;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static GameObject FindInActiveScene(string objectName)
    {
        Scene activeScene = SceneManager.GetActiveScene();
        foreach (GameObject root in activeScene.GetRootGameObjects())
        {
            GameObject found = FindInHierarchy(root.transform, objectName);
            if (found != null)
                return found;
        }
        return null;
    }

    private static GameObject FindInHierarchy(Transform current, string objectName)
    {
        if (current.name == objectName)
            return current.gameObject;

        for (int i = 0; i < current.childCount; i++)
        {
            GameObject found = FindInHierarchy(current.GetChild(i), objectName);
            if (found != null)
                return found;
        }
        return null;
    }
}

public class RoyalButtonHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    private Vector3 baseScale = Vector3.one;
    private float targetScale = 1f;
    private float currentScale = 1f;
    private bool isHovered;
    private bool isPressed;
    private bool idlePulseEnabled;
    private Button cachedButton;

    public void SetIdlePulse(bool enabled)
    {
        idlePulseEnabled = enabled;
    }

    private void Awake()
    {
        baseScale = transform.localScale;
        if (baseScale.sqrMagnitude < 0.01f)
            baseScale = Vector3.one;

        cachedButton = GetComponent<Button>();
        if (cachedButton != null)
        {
            cachedButton.onClick.RemoveListener(OnButtonClickedSfx);
            cachedButton.onClick.AddListener(OnButtonClickedSfx);
        }
    }

    private void OnEnable()
    {
        isHovered = false;
        isPressed = false;
        targetScale = 1f;
        currentScale = 1f;
        transform.localScale = baseScale;
    }

    private void Update()
    {
        float desired = targetScale;
        if (idlePulseEnabled && !isHovered && !isPressed && (cachedButton == null || cachedButton.interactable))
            desired = 1f + Mathf.Sin(Time.unscaledTime * 3.8f) * 0.018f;

        currentScale = Mathf.Lerp(currentScale, desired, Time.unscaledDeltaTime * 14f);
        transform.localScale = baseScale * currentScale;
    }

    private void OnButtonClickedSfx()
    {
        BgmManager.PlayUiClick();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (cachedButton != null && !cachedButton.interactable)
            return;

        isHovered = true;
        targetScale = 1.045f;
        BgmManager.PlayUiHover();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        isPressed = false;
        targetScale = 1f;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (cachedButton != null && !cachedButton.interactable)
            return;

        isPressed = true;
        targetScale = 0.955f;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isPressed = false;
        targetScale = isHovered ? 1.045f : 1f;
    }
}
