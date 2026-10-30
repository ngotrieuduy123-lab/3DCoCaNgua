using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class GameplayUI : MonoBehaviour
{
    public TMP_Text turnText;
    public TMP_Text diceText;
    public TMP_Text messageText;
    public TMP_Text winnerText;
    public GameObject gameOverPanel;
    public TMP_Text gameOverText;
    public TMP_Text timerText;

    int localPlayerIndex = -1;
    PlayerColor currentTurnColor;
    RectTransform hudPopup;
    RectTransform turnNamePopup;
    TMP_Text turnNameText;
    string currentTurnDisplayName = "";
    bool hudConfigured;

    // Party Clash v0.2 HUD Elements
    RectTransform actionSlotPanel;
    Button dice1SlotButton;
    Button dice2SlotButton;
    Button bonusSlotButton;
    Image dice1SlotBg;
    Image dice2SlotBg;
    Image bonusSlotBg;
    TMP_Text dice1SlotText;
    TMP_Text dice2SlotText;
    TMP_Text bonusSlotText;

    RectTransform scoreboardPopup;
    TMP_Text matchClockText;
    TMP_Text eventBannerText;
    TMP_Text scoreboardBodyText;
    TMP_Text feedText;
    int latestTurnSeconds = DiceRuleUtility.TurnDecisionSeconds;
    int latestMatchSeconds = DiceRuleUtility.MatchDurationSeconds;
    bool latestIsFinalRound = false;

    static readonly Color PanelColor = new Color(0.055f, 0.06f, 0.07f, 0.86f);
    static readonly Color PrimaryTextColor = new Color(0.95f, 0.96f, 0.98f, 1f);
    static readonly Color SecondaryTextColor = new Color(0.74f, 0.78f, 0.84f, 1f);
    static readonly Color ActiveSlotColor = new Color(0.95f, 0.76f, 0.14f, 0.96f);
    static readonly Color AvailableSlotColor = new Color(0.18f, 0.24f, 0.34f, 0.94f);
    static readonly Color DisabledSlotColor = new Color(0.11f, 0.12f, 0.15f, 0.65f);

    void Awake()
    {
        ConfigureHudPopup();
    }

    void Start()
    {
        ConfigureHudPopup();
    }

    public void SetTurn(PlayerColor color)
    {
        ConfigureHudPopup();
        currentTurnColor = color;
        RenderTurnText();
        RenderTurnNameText();
    }

    public void SetTurnDisplayName(int playerIndex, string displayName)
    {
        ConfigureHudPopup();
        currentTurnDisplayName = GetSafeDisplayName(playerIndex, displayName);
        RenderTurnNameText();
    }

    public void SetLocalPlayer(int playerIndex)
    {
        ConfigureHudPopup();
        localPlayerIndex = playerIndex;
        RenderTurnText();
    }

    void RenderTurnText()
    {
        if (turnText == null)
            return;

        string turnColor = currentTurnColor.ToString();
        string turnHex = ColorUtility.ToHtmlStringRGB(GetPlayerColor((int)currentTurnColor));
        string turnLine = "Turn: <color=#" + turnHex + ">" + turnColor + "</color>";

        if (localPlayerIndex >= 0)
        {
            string localName = ((PlayerColor)localPlayerIndex).ToString();
            string localHex = ColorUtility.ToHtmlStringRGB(GetPlayerColor(localPlayerIndex));
            turnText.text = "You: <color=#" + localHex + ">" + localName + "</color>\n" + turnLine;
        }
        else
        {
            turnText.text = turnLine;
        }

        turnText.color = PrimaryTextColor;
    }

    void RenderTurnNameText()
    {
        if (turnNameText == null)
            return;

        int turnIndex = (int)currentTurnColor;
        string displayName = GetSafeDisplayName(turnIndex, currentTurnDisplayName);
        string turnHex = ColorUtility.ToHtmlStringRGB(GetPlayerColor(turnIndex));

        turnNameText.text = "<color=#" + turnHex + ">" + displayName + "</color> Turn";
        turnNameText.color = PrimaryTextColor;
    }

    public void SetDice(int dice1, int dice2, int total)
    {
        ConfigureHudPopup();
        if (diceText != null)
        {
            diceText.color = SecondaryTextColor;
            if (dice1 <= 0 && dice2 <= 0)
                diceText.text = "Dice: -";
            else
                diceText.text = $"Xúc xắc: [{dice1}] & [{dice2}] (Chọn từng viên)";
        }
    }

    public void SetActionSlots(
        int d1,
        bool d1Used,
        int d2,
        bool d2Used,
        bool bonusAvailable,
        bool bonusUsed,
        TurnActionSlot selectedSlot,
        bool canInteract)
    {
        ConfigureHudPopup();

        bool d1Ready = d1 > 0 && !d1Used;
        bool d2Ready = d2 > 0 && !d2Used;
        bool bonusReady = bonusAvailable && !bonusUsed;

        StyleSlotButton(
            dice1SlotButton,
            dice1SlotBg,
            dice1SlotText,
            d1 <= 0 ? "Viên 1: -" : (d1Used ? $"Viên 1: {d1} (Đã đi)" : $"Viên 1: [{d1}]"),
            selectedSlot == TurnActionSlot.Dice1 && d1Ready,
            d1Ready && canInteract
        );

        StyleSlotButton(
            dice2SlotButton,
            dice2SlotBg,
            dice2SlotText,
            d2 <= 0 ? "Viên 2: -" : (d2Used ? $"Viên 2: {d2} (Đã đi)" : $"Viên 2: [{d2}]"),
            selectedSlot == TurnActionSlot.Dice2 && d2Ready,
            d2Ready && canInteract
        );

        StyleSlotButton(
            bonusSlotButton,
            bonusSlotBg,
            bonusSlotText,
            bonusUsed ? "Combo: Đã dùng" : (bonusAvailable ? "Combo: [+3 ô]!" : "Combo: +3 (Đá để mở)"),
            selectedSlot == TurnActionSlot.Bonus3 && bonusReady,
            bonusReady && canInteract
        );
    }

    void StyleSlotButton(
        Button btn,
        Image bg,
        TMP_Text label,
        string text,
        bool isSelected,
        bool interactable)
    {
        if (btn == null || bg == null || label == null)
            return;

        btn.interactable = interactable;
        label.text = text;

        if (isSelected)
        {
            bg.color = ActiveSlotColor;
            label.color = new Color(0.06f, 0.07f, 0.09f, 1f);
        }
        else if (interactable)
        {
            bg.color = AvailableSlotColor;
            label.color = PrimaryTextColor;
        }
        else
        {
            bg.color = DisabledSlotColor;
            label.color = new Color(0.55f, 0.58f, 0.64f, 0.85f);
        }
    }

    public void RefreshPartyClashHud(BoardManager board, int matchRemainingSeconds = -1, bool isFinalRound = false)
    {
        ConfigureHudPopup();

        if (matchRemainingSeconds >= 0)
            latestMatchSeconds = matchRemainingSeconds;

        latestIsFinalRound = isFinalRound;
        RenderTimersText();

        if (board == null)
            return;

        if (eventBannerText != null)
            eventBannerText.text = string.IsNullOrEmpty(board.lastEventBannerText)
                ? $"Vòng {board.currentRound} | Đấu Tính Điểm Party Clash"
                : board.lastEventBannerText;

        if (feedText != null)
            feedText.text = string.IsNullOrEmpty(board.lastFeedMessage)
                ? "Mẹo: Đạp Ô Xanh (+3 bước) | Qua bước 28 (+1đ & thành Tinh Nhuệ)"
                : board.lastFeedMessage;

        if (scoreboardBodyText != null)
        {
            int playerCount = Mathf.Clamp(PlayerPrefs.GetInt("PlayerCount", 2), 2, 4);
            if (NetworkTurnManager.Instance != null && NetworkTurnManager.Instance.IsSpawned)
                playerCount = Mathf.Clamp(NetworkTurnManager.Instance.playerCount.Value, 2, 4);

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < playerCount; i++)
            {
                string colorHex = ColorUtility.ToHtmlStringRGB(GetPlayerColor(i));
                string pName = GetSafeDisplayName(i, "");
                bool active = board.IsPlayerParticipating(i);
                string statusTag = active ? "" : " (Rời)";
                string bountyTag = (board.activeEvent == PartyEventType.BountyHunt && board.bountyTargetPlayer == i)
                    ? " <color=#FF5555>[TRUY NÃ]</color>"
                    : "";

                sb.Append($"<color=#{colorHex}><b>{pName}</b></color>{statusTag}: ");
                sb.Append($"<b>{board.playerScores[i]}đ</b> ");
                sb.Append($"<size=85%>(Nhà:{board.playerHomeCounts[i]} | Đá:{board.playerKickCounts[i]})</size>{bountyTag}");

                if (i < playerCount - 1)
                    sb.Append("\n");
            }

            scoreboardBodyText.text = sb.ToString();
        }
    }

    void RenderTimersText()
    {
        int mins = Mathf.Max(0, latestMatchSeconds) / 60;
        int secs = Mathf.Max(0, latestMatchSeconds) % 60;
        string matchClockStr = latestIsFinalRound
            ? $"<color=#FFCC33>VÒNG CUỐI!</color> | Lượt: {latestTurnSeconds}s"
            : $"Trận: {mins:00}:{secs:00} | Lượt: {latestTurnSeconds}s";

        if (matchClockText != null)
            matchClockText.text = matchClockStr;

        if (timerText != null)
            timerText.text = $"Lượt: {latestTurnSeconds}s";
    }

    public void SetMessage(string message)
    {
        ConfigureHudPopup();
        if (messageText != null)
            messageText.color = SecondaryTextColor;

        if (messageText != null)
            messageText.text = message;
    }

    public void SetWinner(int playerIndex)
    {
        if (winnerText != null)
            winnerText.text = "Player " + playerIndex + " finished";
    }

    public void SetGameOver(string message = "Game Over")
    {
        if (gameOverPanel != null)
            gameOverPanel.SetActive(true);

        if (gameOverText != null)
            gameOverText.text = message;

        SetMessage(message);
    }

    public void ClearDice()
    {
        ConfigureHudPopup();
        if (diceText != null)
            diceText.text = "Dice: -";

        SetActionSlots(0, false, 0, false, false, false, TurnActionSlot.None, false);
    }

    public void SetTimer(int value)
    {
        latestTurnSeconds = value;
        RenderTimersText();
    }

    public void SetMatchTimer(int matchRemainingSeconds, bool isFinalRound)
    {
        latestMatchSeconds = matchRemainingSeconds;
        latestIsFinalRound = isFinalRound;
        RenderTimersText();
    }

    void ConfigureHudPopup()
    {
        if (hudConfigured || turnText == null || diceText == null || messageText == null)
            return;

        RectTransform parent = turnText.rectTransform.parent as RectTransform;

        if (parent == null)
            return;

        GameObject popupObject = new GameObject("TurnHudPopup");
        popupObject.layer = turnText.gameObject.layer;
        popupObject.transform.SetParent(parent, false);

        hudPopup = popupObject.AddComponent<RectTransform>();
        hudPopup.anchorMin = new Vector2(0f, 1f);
        hudPopup.anchorMax = new Vector2(0f, 1f);
        hudPopup.pivot = new Vector2(0f, 1f);
        hudPopup.anchoredPosition = new Vector2(14f, -14f);
        hudPopup.sizeDelta = new Vector2(390f, 150f);

        Image panelImage = popupObject.AddComponent<Image>();
        panelImage.color = PanelColor;
        panelImage.raycastTarget = false;

        int firstTextIndex = Mathf.Min(
            turnText.rectTransform.GetSiblingIndex(),
            Mathf.Min(diceText.rectTransform.GetSiblingIndex(), messageText.rectTransform.GetSiblingIndex())
        );
        hudPopup.SetSiblingIndex(firstTextIndex);

        StyleHudText(turnText, new Vector2(28f, -24f), new Vector2(340f, 54f), 22f, PrimaryTextColor);
        StyleHudText(diceText, new Vector2(28f, -82f), new Vector2(360f, 30f), 18f, SecondaryTextColor);
        StyleHudText(messageText, new Vector2(28f, -114f), new Vector2(360f, 36f), 18f, SecondaryTextColor);

        CreateTurnNamePopup(parent);
        CreateActionSlotBar(parent);
        CreateScoreboardPopup(parent);

        hudConfigured = true;
        SetActionSlots(0, false, 0, false, false, false, TurnActionSlot.None, false);
    }

    void CreateTurnNamePopup(RectTransform parent)
    {
        GameObject popupObject = new GameObject("TurnDisplayNamePopup");
        popupObject.layer = turnText.gameObject.layer;
        popupObject.transform.SetParent(parent, false);

        turnNamePopup = popupObject.AddComponent<RectTransform>();
        turnNamePopup.anchorMin = new Vector2(0f, 1f);
        turnNamePopup.anchorMax = new Vector2(0f, 1f);
        turnNamePopup.pivot = new Vector2(0f, 1f);
        turnNamePopup.anchoredPosition = new Vector2(414f, -14f);
        turnNamePopup.sizeDelta = new Vector2(260f, 52f);

        Image panelImage = popupObject.AddComponent<Image>();
        panelImage.color = new Color(0.055f, 0.06f, 0.07f, 0.78f);
        panelImage.raycastTarget = false;

        GameObject textObject = new GameObject("TurnDisplayNameText");
        textObject.layer = turnText.gameObject.layer;
        textObject.transform.SetParent(popupObject.transform, false);

        turnNameText = textObject.AddComponent<TextMeshProUGUI>();
        turnNameText.alignment = TextAlignmentOptions.MidlineLeft;
        StyleHudText(turnNameText, new Vector2(14f, -8f), new Vector2(232f, 36f), 20f, PrimaryTextColor);

        RenderTurnNameText();
    }

    void CreateActionSlotBar(RectTransform parent)
    {
        GameObject barObject = new GameObject("PartyClashActionSlots");
        barObject.layer = turnText.gameObject.layer;
        barObject.transform.SetParent(parent, false);

        actionSlotPanel = barObject.AddComponent<RectTransform>();
        actionSlotPanel.anchorMin = new Vector2(0f, 1f);
        actionSlotPanel.anchorMax = new Vector2(0f, 1f);
        actionSlotPanel.pivot = new Vector2(0f, 1f);
        actionSlotPanel.anchoredPosition = new Vector2(14f, -170f);
        actionSlotPanel.sizeDelta = new Vector2(390f, 52f);

        dice1SlotButton = CreateSlotButton(
            actionSlotPanel,
            "Dice1SlotBtn",
            new Vector2(0f, 0f),
            new Vector2(120f, 44f),
            out dice1SlotBg,
            out dice1SlotText,
            () => OnActionSlotClicked(TurnActionSlot.Dice1)
        );

        dice2SlotButton = CreateSlotButton(
            actionSlotPanel,
            "Dice2SlotBtn",
            new Vector2(126f, 0f),
            new Vector2(120f, 44f),
            out dice2SlotBg,
            out dice2SlotText,
            () => OnActionSlotClicked(TurnActionSlot.Dice2)
        );

        bonusSlotButton = CreateSlotButton(
            actionSlotPanel,
            "Bonus3SlotBtn",
            new Vector2(252f, 0f),
            new Vector2(138f, 44f),
            out bonusSlotBg,
            out bonusSlotText,
            () => OnActionSlotClicked(TurnActionSlot.Bonus3)
        );
    }

    Button CreateSlotButton(
        RectTransform parent,
        string name,
        Vector2 anchoredPos,
        Vector2 size,
        out Image bgImage,
        out TMP_Text buttonLabel,
        UnityEngine.Events.UnityAction onClick)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.layer = turnText.gameObject.layer;
        btnObj.transform.SetParent(parent, false);

        RectTransform rt = btnObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        bgImage = btnObj.AddComponent<Image>();
        bgImage.color = DisabledSlotColor;

        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = bgImage;
        btn.onClick.AddListener(onClick);

        GameObject txtObj = new GameObject("Label");
        txtObj.layer = turnText.gameObject.layer;
        txtObj.transform.SetParent(btnObj.transform, false);

        RectTransform txtRt = txtObj.AddComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = new Vector2(6f, 2f);
        txtRt.offsetMax = new Vector2(-6f, -2f);

        buttonLabel = txtObj.AddComponent<TextMeshProUGUI>();
        buttonLabel.font = turnText.font;
        buttonLabel.fontSize = 15f;
        buttonLabel.fontStyle = FontStyles.Bold;
        buttonLabel.alignment = TextAlignmentOptions.Center;
        buttonLabel.color = PrimaryTextColor;
        buttonLabel.raycastTarget = false;

        return btn;
    }

    void CreateScoreboardPopup(RectTransform parent)
    {
        GameObject panelObj = new GameObject("PartyClashScoreboardPopup");
        panelObj.layer = turnText.gameObject.layer;
        panelObj.transform.SetParent(parent, false);

        scoreboardPopup = panelObj.AddComponent<RectTransform>();
        scoreboardPopup.anchorMin = new Vector2(1f, 1f);
        scoreboardPopup.anchorMax = new Vector2(1f, 1f);
        scoreboardPopup.pivot = new Vector2(1f, 1f);
        scoreboardPopup.anchoredPosition = new Vector2(-14f, -14f);
        scoreboardPopup.sizeDelta = new Vector2(410f, 196f);

        Image bg = panelObj.AddComponent<Image>();
        bg.color = PanelColor;
        bg.raycastTarget = false;

        matchClockText = CreateChildTmpText(scoreboardPopup, "MatchClockText", new Vector2(14f, -10f), new Vector2(382f, 26f), 18f, FontStyles.Bold, PrimaryTextColor);
        eventBannerText = CreateChildTmpText(scoreboardPopup, "EventBannerText", new Vector2(14f, -36f), new Vector2(382f, 24f), 15f, FontStyles.Bold, new Color(1f, 0.85f, 0.32f, 1f));
        scoreboardBodyText = CreateChildTmpText(scoreboardPopup, "ScoreboardBodyText", new Vector2(14f, -64f), new Vector2(382f, 96f), 16f, FontStyles.Normal, PrimaryTextColor);
        scoreboardBodyText.textWrappingMode = TextWrappingModes.Normal;
        feedText = CreateChildTmpText(scoreboardPopup, "ActionFeedText", new Vector2(14f, -162f), new Vector2(382f, 26f), 14f, FontStyles.Italic, SecondaryTextColor);
    }

    TMP_Text CreateChildTmpText(
        RectTransform parent,
        string name,
        Vector2 anchoredPos,
        Vector2 size,
        float fontSize,
        FontStyles style,
        Color color)
    {
        GameObject go = new GameObject(name);
        go.layer = turnText.gameObject.layer;
        go.transform.SetParent(parent, false);

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        if (turnText != null && turnText.font != null)
            tmp.font = turnText.font;

        tmp.fontStyle = style;
        tmp.alignment = TextAlignmentOptions.TopLeft;
        StyleHudText(tmp, anchoredPos, size, fontSize, color);
        return tmp;
    }

    void OnActionSlotClicked(TurnActionSlot slot)
    {
        bool isNetworkMode = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        if (isNetworkMode)
        {
            if (NetworkDiceManager.Instance != null)
                NetworkDiceManager.Instance.SelectActionSlot(slot);
        }
        else
        {
            DiceManager localDice = FindFirstObjectByType<DiceManager>();
            if (localDice != null)
                localDice.SelectActionSlot(slot);
        }
    }

    void StyleHudText(TMP_Text text, Vector2 anchoredPosition, Vector2 size, float fontSize, Color color)
    {
        if (text == null)
            return;

        RectTransform rect = text.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        text.fontSize = fontSize;
        text.color = color;
        text.richText = true;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
    }

    Color GetPlayerColor(int playerIndex)
    {
        switch (playerIndex)
        {
            case 0: return new Color(0.46f, 0.65f, 1f);
            case 1: return new Color(1f, 0.45f, 0.45f);
            case 2: return new Color(0.45f, 0.82f, 0.56f);
            case 3: return new Color(1f, 0.82f, 0.35f);
            default: return PrimaryTextColor;
        }
    }

    string GetSafeDisplayName(int playerIndex, string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            displayName = PlayerPrefs.GetString("TurnDisplayName_" + playerIndex, "");

        if (string.IsNullOrWhiteSpace(displayName))
            displayName = ((PlayerColor)playerIndex).ToString();

        displayName = displayName.Trim();

        if (displayName.Length > 18)
            displayName = displayName.Substring(0, 18);

        return displayName;
    }
}
