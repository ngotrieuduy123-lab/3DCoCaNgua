using System.Collections.Generic;
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
    string currentTurnDisplayName = "";
    bool hudConfigured;

    // Shared procedural sprites for Royal Tabletop theme
    static Sprite roundedPanelSprite;
    static Sprite roundedPillSprite;
    static Sprite circlePipSprite;

    // Zone 1: Top-Center Royal Match & Event Banner
    RectTransform topCenterBanner;
    TMP_Text matchClockText;
    TMP_Text turnHeaderText;
    TMP_Text turnCountdownText;
    Image turnCountdownPillBg;
    TMP_Text eventBannerText;

    // Zone 2: Left-Side Player Cards Column
    RectTransform leftScoreboardColumn;
    readonly PlayerCardWidgets[] playerCards = new PlayerCardWidgets[4];
    TMP_Text feedText;

    // Zone 3: Bottom-Right Tactical Dice & Roll Dock
    RectTransform diceDockPanel;
    RectTransform actionSlotPanel;
    Button dice1SlotButton;
    Button dice2SlotButton;
    Button bonusSlotButton;
    Image dice1SlotBg;
    Image dice2SlotBg;
    Image bonusSlotBg;
    Outline dice1SlotOutline;
    Outline dice2SlotOutline;
    Outline bonusSlotOutline;
    TMP_Text dice1SlotTitle;
    TMP_Text dice1SlotSub;
    TMP_Text dice2SlotTitle;
    TMP_Text dice2SlotSub;
    TMP_Text bonusSlotTitle;
    TMP_Text bonusSlotSub;
    Image dice1MiniBox;
    Image dice2MiniBox;
    readonly Image[] dice1MiniPips = new Image[7];
    readonly Image[] dice2MiniPips = new Image[7];

    int latestTurnSeconds = DiceRuleUtility.TurnDecisionSeconds;
    int latestMatchSeconds = DiceRuleUtility.MatchDurationSeconds;
    bool latestIsFinalRound = false;

    class PlayerCardWidgets
    {
        public GameObject root;
        public Image cardBg;
        public Outline cardOutline;
        public Image emblemBg;
        public TMP_Text rankText;
        public TMP_Text nameText;
        public TMP_Text statsText;
        public TMP_Text scoreText;
        public GameObject activeTurnBadge;
    }

    // Royal Tabletop Color Palette
    static readonly Color WoodDarkBg = new Color(0.086f, 0.067f, 0.051f, 0.94f);
    static readonly Color WoodInnerBg = new Color(0.137f, 0.106f, 0.078f, 0.96f);
    static readonly Color WoodActiveCardBg = new Color(0.20f, 0.15f, 0.09f, 0.98f);
    static readonly Color GoldBorder = new Color(0.847f, 0.651f, 0.282f, 0.95f);
    static readonly Color GoldBright = new Color(1.00f, 0.84f, 0.28f, 1.00f);
    static readonly Color BronzeMuted = new Color(0.45f, 0.35f, 0.20f, 0.80f);
    static readonly Color IvoryText = new Color(0.97f, 0.94f, 0.88f, 1.00f);
    static readonly Color WarmMutedText = new Color(0.80f, 0.74f, 0.64f, 1.00f);
    static readonly Color ActiveSlotGoldBg = new Color(0.76f, 0.52f, 0.12f, 0.98f);
    static readonly Color ReadySlotWoodBg = new Color(0.16f, 0.125f, 0.095f, 0.96f);
    static readonly Color DisabledSlotBg = new Color(0.09f, 0.075f, 0.06f, 0.65f);
    static readonly Color ComboActiveBg = new Color(0.82f, 0.26f, 0.12f, 0.98f);
    static readonly Color IvoryDieFace = new Color(0.97f, 0.94f, 0.86f, 1.00f);
    static readonly Color DiePipDark = new Color(0.12f, 0.09f, 0.07f, 1.00f);
    static readonly Color DiePipRed = new Color(0.82f, 0.16f, 0.16f, 1.00f);

    void Awake()
    {
        ConfigureHudPopup();
    }

    void Start()
    {
        ConfigureHudPopup();
        StyleRoomControlPanelIfPresent();
        StyleRollButtonIntoDockIfPresent();
    }

    public static Sprite GetRoundedPanelSprite()
    {
        if (roundedPanelSprite == null)
            roundedPanelSprite = CreateRoundedSprite(64, 14);
        return roundedPanelSprite;
    }

    public static Sprite GetRoundedPillSprite()
    {
        if (roundedPillSprite == null)
            roundedPillSprite = CreateRoundedSprite(48, 16);
        return roundedPillSprite;
    }

    public static Sprite GetCirclePipSprite()
    {
        if (circlePipSprite == null)
            circlePipSprite = CreateCircleSprite(32);
        return circlePipSprite;
    }

    public void SetTurn(PlayerColor color)
    {
        ConfigureHudPopup();
        currentTurnColor = color;
        RenderTurnHeader();
        RefreshActiveTurnHighlightOnCards();
    }

    public void SetTurnDisplayName(int playerIndex, string displayName)
    {
        ConfigureHudPopup();
        currentTurnDisplayName = GetSafeDisplayName(playerIndex, displayName);
        RenderTurnHeader();
        RefreshActiveTurnHighlightOnCards();
    }

    public void SetLocalPlayer(int playerIndex)
    {
        ConfigureHudPopup();
        localPlayerIndex = playerIndex;
        RenderTurnHeader();
    }

    void RenderTurnHeader()
    {
        int turnIdx = (int)currentTurnColor;
        string displayName = GetSafeDisplayName(turnIdx, currentTurnDisplayName);
        string colorName = GetVietnameseColorName(turnIdx);
        string turnHex = ColorUtility.ToHtmlStringRGB(GetPlayerColor(turnIdx));

        if (turnHeaderText != null)
        {
            string youTag = (localPlayerIndex >= 0 && localPlayerIndex == turnIdx) ? " <color=#FFD95A>(LƯỢT BẠN)</color>" : "";
            turnHeaderText.text = $"LƯỢT: <color=#{turnHex}><b>{displayName}</b> ({colorName})</color>{youTag}";
        }

        if (turnText != null)
        {
            turnText.text = $"Turn: {displayName}";
        }
    }

    void RefreshActiveTurnHighlightOnCards()
    {
        int activeIdx = (int)currentTurnColor;
        for (int i = 0; i < playerCards.Length; i++)
        {
            PlayerCardWidgets card = playerCards[i];
            if (card == null || card.root == null)
                continue;

            bool isCurrent = i == activeIdx;
            if (card.cardBg != null)
                card.cardBg.color = isCurrent ? WoodActiveCardBg : WoodDarkBg;

            if (card.cardOutline != null)
            {
                card.cardOutline.effectColor = isCurrent ? GoldBright : BronzeMuted;
                card.cardOutline.effectDistance = isCurrent ? new Vector2(2.2f, -2.2f) : new Vector2(1.2f, -1.2f);
            }

            if (card.activeTurnBadge != null)
                card.activeTurnBadge.SetActive(isCurrent);
        }
    }

    public void SetDice(int dice1, int dice2, int total)
    {
        ConfigureHudPopup();
        if (diceText != null)
        {
            diceText.text = (dice1 <= 0 && dice2 <= 0)
                ? "Dice: -"
                : $"[{dice1}] & [{dice2}]";
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

        StyleDiceSlotCard(
            dice1SlotButton,
            dice1SlotBg,
            dice1SlotOutline,
            dice1SlotTitle,
            dice1SlotSub,
            dice1MiniBox,
            dice1MiniPips,
            "VIÊN 1",
            d1,
            d1Used,
            d1Ready,
            selectedSlot == TurnActionSlot.Dice1 && d1Ready,
            canInteract && d1Ready
        );

        StyleDiceSlotCard(
            dice2SlotButton,
            dice2SlotBg,
            dice2SlotOutline,
            dice2SlotTitle,
            dice2SlotSub,
            dice2MiniBox,
            dice2MiniPips,
            "VIÊN 2",
            d2,
            d2Used,
            d2Ready,
            selectedSlot == TurnActionSlot.Dice2 && d2Ready,
            canInteract && d2Ready
        );

        StyleBonusSlotCard(
            bonusReady,
            bonusUsed,
            selectedSlot == TurnActionSlot.Bonus3 && bonusReady,
            canInteract && bonusReady
        );
    }

    void StyleDiceSlotCard(
        Button btn,
        Image bg,
        Outline outline,
        TMP_Text titleLabel,
        TMP_Text subLabel,
        Image miniDieBox,
        Image[] pips,
        string slotTitle,
        int dieValue,
        bool isUsed,
        bool isReady,
        bool isSelected,
        bool interactable)
    {
        if (btn == null || bg == null)
            return;

        btn.interactable = interactable;

        if (isSelected)
        {
            bg.color = ActiveSlotGoldBg;
            if (outline != null)
            {
                outline.effectColor = GoldBright;
                outline.effectDistance = new Vector2(2.5f, -2.5f);
            }
        }
        else if (isReady)
        {
            bg.color = ReadySlotWoodBg;
            if (outline != null)
            {
                outline.effectColor = GoldBorder;
                outline.effectDistance = new Vector2(1.5f, -1.5f);
            }
        }
        else
        {
            bg.color = DisabledSlotBg;
            if (outline != null)
            {
                outline.effectColor = new Color(0.32f, 0.25f, 0.16f, 0.55f);
                outline.effectDistance = new Vector2(1f, -1f);
            }
        }

        if (titleLabel != null)
        {
            titleLabel.text = isSelected ? $">> {slotTitle}" : slotTitle;
            titleLabel.color = isSelected ? new Color(0.12f, 0.08f, 0.03f, 1f) : (isReady ? GoldBright : WarmMutedText);
        }

        if (subLabel != null)
        {
            if (dieValue <= 0)
                subLabel.text = "Chờ gieo";
            else if (isUsed)
                subLabel.text = "Đã dùng";
            else
                subLabel.text = $"{dieValue} bước";

            subLabel.color = isSelected ? new Color(0.14f, 0.09f, 0.04f, 1f) : (isReady ? IvoryText : new Color(0.62f, 0.58f, 0.50f, 0.78f));
        }

        if (miniDieBox != null)
        {
            miniDieBox.color = (dieValue > 0 && !isUsed)
                ? IvoryDieFace
                : new Color(0.82f, 0.78f, 0.70f, 0.35f);
            SetMiniDiePips(pips, dieValue > 0 ? dieValue : 0, !isUsed);
        }
    }

    void StyleBonusSlotCard(bool bonusReady, bool bonusUsed, bool isSelected, bool interactable)
    {
        if (bonusSlotButton == null || bonusSlotBg == null)
            return;

        bonusSlotButton.interactable = interactable;

        if (isSelected || bonusReady)
        {
            bonusSlotBg.color = isSelected ? ComboActiveBg : new Color(0.58f, 0.20f, 0.10f, 0.95f);
            if (bonusSlotOutline != null)
            {
                bonusSlotOutline.effectColor = GoldBright;
                bonusSlotOutline.effectDistance = new Vector2(2.2f, -2.2f);
            }
        }
        else
        {
            bonusSlotBg.color = DisabledSlotBg;
            if (bonusSlotOutline != null)
            {
                bonusSlotOutline.effectColor = new Color(0.32f, 0.25f, 0.16f, 0.55f);
                bonusSlotOutline.effectDistance = new Vector2(1f, -1f);
            }
        }

        if (bonusSlotTitle != null)
        {
            bonusSlotTitle.text = bonusReady ? ">> COMBO +3" : "COMBO +3";
            bonusSlotTitle.color = bonusReady ? GoldBright : WarmMutedText;
        }

        if (bonusSlotSub != null)
        {
            if (bonusUsed)
                bonusSlotSub.text = "Đã dùng";
            else if (bonusReady)
                bonusSlotSub.text = "Đi +3 bước!";
            else
                bonusSlotSub.text = "Đá địch để mở";

            bonusSlotSub.color = bonusReady ? IvoryText : new Color(0.62f, 0.58f, 0.50f, 0.75f);
        }
    }

    public void RefreshPartyClashHud(BoardManager board)
    {
        RefreshPartyClashHud(board, latestMatchSeconds, latestIsFinalRound);
    }

    public void RefreshPartyClashHud(BoardManager board, int matchRemainingSeconds, bool isFinalRound)
    {
        ConfigureHudPopup();
        latestMatchSeconds = matchRemainingSeconds;
        latestIsFinalRound = isFinalRound;

        RenderTimersText();

        if (board == null)
            return;

        int playerCount = 2;
        if (NetworkTurnManager.Instance != null && NetworkTurnManager.Instance.IsSpawned)
            playerCount = Mathf.Clamp(NetworkTurnManager.Instance.playerCount.Value, 2, 4);
        else if (TurnManager.Instance != null)
            playerCount = Mathf.Clamp(TurnManager.Instance.playerCount, 2, 4);
        else
            playerCount = Mathf.Clamp(PlayerPrefs.GetInt("PlayerCount", 2), 2, 4);

        board.UpdateEventBannerText(playerCount);

        if (eventBannerText != null)
            eventBannerText.text = $"[ {board.lastEventBannerText} ]";

        if (feedText != null)
        {
            feedText.text = string.IsNullOrWhiteSpace(board.lastFeedMessage)
                ? "Mẹo: Đạp Ô Tăng Tốc (+3 bước) | Qua bước 28 (+1đ & thành Tinh Nhuệ)"
                : board.lastFeedMessage;
        }

        int[] rankOrder = board.GetSortedRankingOrder(playerCount);
        int[] playerRank = new int[4];
        for (int r = 0; r < rankOrder.Length; r++)
        {
            int pIdx = rankOrder[r];
            if (pIdx >= 0 && pIdx < 4)
                playerRank[pIdx] = r + 1;
        }

        int activeTurnIdx = (int)currentTurnColor;

        for (int i = 0; i < 4; i++)
        {
            PlayerCardWidgets card = playerCards[i];
            if (card == null || card.root == null)
                continue;

            bool activeInMatch = i < playerCount && board.IsPlayerParticipating(i);
            card.root.SetActive(activeInMatch);
            if (!activeInMatch)
                continue;

            string pName = GetSafeDisplayName(i, PlayerPrefs.GetString("TurnDisplayName_" + i, ""));
            string youBadge = (localPlayerIndex == i) ? " <size=82%><color=#8AE6FF>[BẠN]</color></size>" : "";
            string bountyBadge = (board.bountyTargetPlayer == i && board.playerScores[i] > 0)
                ? " <size=80%><color=#FFD24C>[TOP 1]</color></size>"
                : "";

            if (card.rankText != null)
                card.rankText.text = $"#{Mathf.Max(1, playerRank[i])}";

            if (card.nameText != null)
                card.nameText.text = $"{pName}{youBadge}{bountyBadge}";

            if (card.statsText != null)
                card.statsText.text = $"Về đích: <b>{board.playerHomeCounts[i]}</b>   |   Đá hạ: <b>{board.playerKickCounts[i]}</b>";

            if (card.scoreText != null)
                card.scoreText.text = $"{board.playerScores[i]}đ";

            bool isCurrent = i == activeTurnIdx;
            if (card.cardBg != null)
                card.cardBg.color = isCurrent ? WoodActiveCardBg : WoodDarkBg;

            if (card.cardOutline != null)
            {
                card.cardOutline.effectColor = isCurrent ? GoldBright : BronzeMuted;
                card.cardOutline.effectDistance = isCurrent ? new Vector2(2.2f, -2.2f) : new Vector2(1.2f, -1.2f);
            }

            if (card.activeTurnBadge != null)
                card.activeTurnBadge.SetActive(isCurrent);
        }
    }

    void RenderTimersText()
    {
        int mins = Mathf.Max(0, latestMatchSeconds) / 60;
        int secs = Mathf.Max(0, latestMatchSeconds) % 60;

        if (matchClockText != null)
        {
            matchClockText.text = latestIsFinalRound
                ? "<color=#FFD24C>VÒNG CUỐI!</color>"
                : $"TRẬN: {mins:00}:{secs:00}";
        }

        if (turnCountdownText != null)
        {
            bool urgent = latestTurnSeconds <= 5;
            turnCountdownText.text = $"{latestTurnSeconds}s";
            turnCountdownText.color = urgent ? new Color(1f, 0.38f, 0.32f, 1f) : GoldBright;
        }

        if (turnCountdownPillBg != null)
        {
            turnCountdownPillBg.color = latestTurnSeconds <= 5
                ? new Color(0.42f, 0.10f, 0.10f, 0.96f)
                : WoodInnerBg;
        }

        if (timerText != null)
            timerText.text = $"{latestTurnSeconds}s";
    }

    public void SetMessage(string message)
    {
        ConfigureHudPopup();
        if (messageText != null)
        {
            messageText.color = IvoryText;
            messageText.text = string.IsNullOrWhiteSpace(message)
                ? "Hãy gieo xúc xắc hoặc chọn quân cờ đang phát sáng"
                : message;
        }
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

        RectTransform canvasRect = turnText.rectTransform.parent as RectTransform;
        if (canvasRect == null)
            return;

        // Keep legacy turnText and diceText off-screen so no external references break while hiding raw plain text
        StyleHudText(turnText, new Vector2(-2500f, 2500f), new Vector2(200f, 30f), 14f, IvoryText);
        StyleHudText(diceText, new Vector2(-2500f, 2500f), new Vector2(200f, 30f), 14f, IvoryText);
        if (timerText != null)
            StyleHudText(timerText, new Vector2(-2500f, 2500f), new Vector2(120f, 30f), 14f, IvoryText);
        if (winnerText != null)
            StyleHudText(winnerText, new Vector2(-2500f, 2500f), new Vector2(200f, 30f), 14f, IvoryText);

        CreateTopCenterRoyalBanner(canvasRect);
        CreateLeftPlayerCardsColumn(canvasRect);
        CreateBottomRightDiceDock(canvasRect);

        hudConfigured = true;
        RenderTurnHeader();
        RenderTimersText();
        SetActionSlots(0, false, 0, false, false, false, TurnActionSlot.None, false);
    }

    void CreateTopCenterRoyalBanner(RectTransform canvasRect)
    {
        GameObject bannerObj = new GameObject("RoyalTopCenterBanner");
        bannerObj.layer = turnText.gameObject.layer;
        bannerObj.transform.SetParent(canvasRect, false);

        topCenterBanner = bannerObj.AddComponent<RectTransform>();
        topCenterBanner.anchorMin = new Vector2(0.5f, 1f);
        topCenterBanner.anchorMax = new Vector2(0.5f, 1f);
        topCenterBanner.pivot = new Vector2(0.5f, 1f);
        topCenterBanner.anchoredPosition = new Vector2(20f, -12f);
        topCenterBanner.sizeDelta = new Vector2(680f, 108f);

        Image bannerBg = bannerObj.AddComponent<Image>();
        bannerBg.sprite = GetRoundedPanelSprite();
        bannerBg.type = Image.Type.Sliced;
        bannerBg.color = WoodDarkBg;
        bannerBg.raycastTarget = false;

        AddRoyalFrame(bannerObj, GoldBorder, new Vector2(2f, -2f));

        // Left pill: Match Clock (10:00)
        GameObject clockPill = CreateRoundedSubPanel(
            topCenterBanner,
            "MatchClockPill",
            new Vector2(0f, 1f),
            new Vector2(14f, -10f),
            new Vector2(172f, 34f),
            WoodInnerBg,
            BronzeMuted
        );
        matchClockText = CreateLabel(
            clockPill.GetComponent<RectTransform>(),
            "MatchClockLabel",
            Vector2.zero,
            new Vector2(172f, 34f),
            16f,
            FontStyles.Bold,
            GoldBright,
            TextAlignmentOptions.Center
        );

        // Center pill: Active Player Turn
        GameObject turnPill = CreateRoundedSubPanel(
            topCenterBanner,
            "TurnCrestPill",
            new Vector2(0f, 1f),
            new Vector2(194f, -10f),
            new Vector2(344f, 34f),
            WoodInnerBg,
            GoldBorder
        );
        turnHeaderText = CreateLabel(
            turnPill.GetComponent<RectTransform>(),
            "TurnHeaderLabel",
            Vector2.zero,
            new Vector2(344f, 34f),
            17f,
            FontStyles.Bold,
            IvoryText,
            TextAlignmentOptions.Center
        );

        // Right pill: Turn Countdown (20s)
        GameObject timerPill = CreateRoundedSubPanel(
            topCenterBanner,
            "TurnTimerPill",
            new Vector2(0f, 1f),
            new Vector2(546f, -10f),
            new Vector2(120f, 34f),
            WoodInnerBg,
            BronzeMuted
        );
        turnCountdownPillBg = timerPill.GetComponent<Image>();
        turnCountdownText = CreateLabel(
            timerPill.GetComponent<RectTransform>(),
            "TurnCountdownLabel",
            Vector2.zero,
            new Vector2(120f, 34f),
            18f,
            FontStyles.Bold,
            GoldBright,
            TextAlignmentOptions.Center
        );

        // Middle ribbon: Event Banner
        eventBannerText = CreateLabel(
            topCenterBanner,
            "EventBannerText",
            new Vector2(16f, -48f),
            new Vector2(648f, 24f),
            14.5f,
            FontStyles.Bold,
            GoldBright,
            TextAlignmentOptions.Center
        );

        // Reparent existing messageText into the bottom row of the Royal Banner
        messageText.transform.SetParent(topCenterBanner, false);
        RectTransform msgRt = messageText.rectTransform;
        msgRt.anchorMin = new Vector2(0f, 1f);
        msgRt.anchorMax = new Vector2(0f, 1f);
        msgRt.pivot = new Vector2(0f, 1f);
        msgRt.anchoredPosition = new Vector2(16f, -74f);
        msgRt.sizeDelta = new Vector2(648f, 26f);
        messageText.fontSize = 15.5f;
        messageText.fontStyle = FontStyles.Bold;
        messageText.alignment = TextAlignmentOptions.Center;
        messageText.color = IvoryText;
        messageText.raycastTarget = false;
        if (string.IsNullOrWhiteSpace(messageText.text))
            messageText.text = "Hãy bấm GIEO 2 XÚC XẮC để bắt đầu lượt!";
    }

    void CreateLeftPlayerCardsColumn(RectTransform canvasRect)
    {
        GameObject colObj = new GameObject("RoyalLeftPlayerCards");
        colObj.layer = turnText.gameObject.layer;
        colObj.transform.SetParent(canvasRect, false);

        leftScoreboardColumn = colObj.AddComponent<RectTransform>();
        leftScoreboardColumn.anchorMin = new Vector2(0f, 1f);
        leftScoreboardColumn.anchorMax = new Vector2(0f, 1f);
        leftScoreboardColumn.pivot = new Vector2(0f, 1f);
        leftScoreboardColumn.anchoredPosition = new Vector2(16f, -12f);
        leftScoreboardColumn.sizeDelta = new Vector2(336f, 430f);

        // Header plate
        GameObject headerPlate = CreateRoundedSubPanel(
            leftScoreboardColumn,
            "ScoreboardHeaderPlate",
            new Vector2(0f, 1f),
            new Vector2(0f, 0f),
            new Vector2(336f, 36f),
            WoodDarkBg,
            GoldBorder
        );
        CreateLabel(
            headerPlate.GetComponent<RectTransform>(),
            "HeaderTitle",
            Vector2.zero,
            new Vector2(336f, 36f),
            15.5f,
            FontStyles.Bold,
            GoldBright,
            TextAlignmentOptions.Center
        ).text = "BẢNG XẾP HẠNG ĐIỂM";

        for (int i = 0; i < 4; i++)
        {
            float yOffset = -44f - i * 76f;
            playerCards[i] = CreateSinglePlayerCard(leftScoreboardColumn, i, new Vector2(0f, yOffset));
        }

        // Action Feed Strip at the bottom of the column
        GameObject feedBox = CreateRoundedSubPanel(
            leftScoreboardColumn,
            "RoyalActionFeedBox",
            new Vector2(0f, 1f),
            new Vector2(0f, -352f),
            new Vector2(336f, 52f),
            WoodDarkBg,
            BronzeMuted
        );
        feedText = CreateLabel(
            feedBox.GetComponent<RectTransform>(),
            "ActionFeedText",
            new Vector2(12f, -6f),
            new Vector2(312f, 40f),
            13.5f,
            FontStyles.Italic,
            WarmMutedText,
            TextAlignmentOptions.MidlineLeft
        );
        feedText.textWrappingMode = TextWrappingModes.Normal;
        feedText.text = "Mẹo: Đạp Ô Tăng Tốc (+3 bước) | Qua bước 28 (+1đ & thành Tinh Nhuệ)";
    }

    PlayerCardWidgets CreateSinglePlayerCard(RectTransform parent, int playerIndex, Vector2 anchoredPos)
    {
        PlayerCardWidgets w = new PlayerCardWidgets();

        w.root = CreateRoundedSubPanel(
            parent,
            $"RoyalPlayerCard_{playerIndex}",
            new Vector2(0f, 1f),
            anchoredPos,
            new Vector2(336f, 68f),
            WoodDarkBg,
            BronzeMuted
        );
        w.cardBg = w.root.GetComponent<Image>();
        w.cardOutline = w.root.GetComponent<Outline>();

        RectTransform cardRt = w.root.GetComponent<RectTransform>();

        // Left Color Crest Emblem (46x48)
        GameObject emblemObj = CreateRoundedSubPanel(
            cardRt,
            "FactionEmblem",
            new Vector2(0f, 1f),
            new Vector2(10f, -10f),
            new Vector2(48f, 48f),
            GetPlayerColor(playerIndex),
            GoldBorder
        );
        w.emblemBg = emblemObj.GetComponent<Image>();
        w.rankText = CreateLabel(
            emblemObj.GetComponent<RectTransform>(),
            "RankLabel",
            Vector2.zero,
            new Vector2(48f, 48f),
            18f,
            FontStyles.Bold,
            Color.white,
            TextAlignmentOptions.Center
        );
        w.rankText.text = $"#{playerIndex + 1}";

        // Player Name & Tags
        w.nameText = CreateLabel(
            cardRt,
            "PlayerNameLabel",
            new Vector2(68f, -10f),
            new Vector2(182f, 26f),
            16.5f,
            FontStyles.Bold,
            IvoryText,
            TextAlignmentOptions.MidlineLeft
        );
        w.nameText.text = GetSafeDisplayName(playerIndex, "");

        // Sub-stats (Home & Kick counts)
        w.statsText = CreateLabel(
            cardRt,
            "PlayerStatsLabel",
            new Vector2(68f, -36f),
            new Vector2(182f, 22f),
            13f,
            FontStyles.Normal,
            WarmMutedText,
            TextAlignmentOptions.MidlineLeft
        );
        w.statsText.text = "Về đích: <b>0</b>   |   Đá hạ: <b>0</b>";

        // Right Recessed Score Box (68x48)
        GameObject scoreBox = CreateRoundedSubPanel(
            cardRt,
            "ScoreBox",
            new Vector2(0f, 1f),
            new Vector2(256f, -10f),
            new Vector2(70f, 48f),
            WoodInnerBg,
            GoldBorder
        );
        w.scoreText = CreateLabel(
            scoreBox.GetComponent<RectTransform>(),
            "ScoreValueLabel",
            Vector2.zero,
            new Vector2(70f, 48f),
            20f,
            FontStyles.Bold,
            GoldBright,
            TextAlignmentOptions.Center
        );
        w.scoreText.text = "0đ";

        // Small glowing "ĐANG ĐI" tag on top-right of card
        w.activeTurnBadge = CreateRoundedSubPanel(
            cardRt,
            "ActiveTurnTag",
            new Vector2(0f, 1f),
            new Vector2(184f, -4f),
            new Vector2(66f, 18f),
            GoldBright,
            GoldBorder
        );
        TMP_Text tagTxt = CreateLabel(
            w.activeTurnBadge.GetComponent<RectTransform>(),
            "TagLabel",
            Vector2.zero,
            new Vector2(66f, 18f),
            10.5f,
            FontStyles.Bold,
            new Color(0.12f, 0.08f, 0.03f, 1f),
            TextAlignmentOptions.Center
        );
        tagTxt.text = "ĐANG ĐI";
        w.activeTurnBadge.SetActive(false);

        return w;
    }

    void CreateBottomRightDiceDock(RectTransform canvasRect)
    {
        GameObject dockObj = new GameObject("RoyalBottomRightDiceDock");
        dockObj.layer = turnText.gameObject.layer;
        dockObj.transform.SetParent(canvasRect, false);

        diceDockPanel = dockObj.AddComponent<RectTransform>();
        diceDockPanel.anchorMin = new Vector2(1f, 0f);
        diceDockPanel.anchorMax = new Vector2(1f, 0f);
        diceDockPanel.pivot = new Vector2(1f, 0f);
        diceDockPanel.anchoredPosition = new Vector2(-18f, 18f);
        diceDockPanel.sizeDelta = new Vector2(436f, 168f);

        Image dockBg = dockObj.AddComponent<Image>();
        dockBg.sprite = GetRoundedPanelSprite();
        dockBg.type = Image.Type.Sliced;
        dockBg.color = WoodDarkBg;
        dockBg.raycastTarget = false;

        AddRoyalFrame(dockObj, GoldBorder, new Vector2(2f, -2f));

        // Action Slots Row inside the top of the dock
        GameObject barObject = new GameObject("PartyClashActionSlots");
        barObject.layer = turnText.gameObject.layer;
        barObject.transform.SetParent(diceDockPanel, false);

        actionSlotPanel = barObject.AddComponent<RectTransform>();
        actionSlotPanel.anchorMin = new Vector2(0f, 1f);
        actionSlotPanel.anchorMax = new Vector2(0f, 1f);
        actionSlotPanel.pivot = new Vector2(0f, 1f);
        actionSlotPanel.anchoredPosition = new Vector2(12f, -12f);
        actionSlotPanel.sizeDelta = new Vector2(412f, 76f);

        dice1SlotButton = CreateVisualDieSlotButton(
            actionSlotPanel,
            "Dice1SlotBtn",
            new Vector2(0f, 0f),
            new Vector2(132f, 74f),
            out dice1SlotBg,
            out dice1SlotOutline,
            out dice1SlotTitle,
            out dice1SlotSub,
            out dice1MiniBox,
            dice1MiniPips,
            () => OnActionSlotClicked(TurnActionSlot.Dice1)
        );

        dice2SlotButton = CreateVisualDieSlotButton(
            actionSlotPanel,
            "Dice2SlotBtn",
            new Vector2(140f, 0f),
            new Vector2(132f, 74f),
            out dice2SlotBg,
            out dice2SlotOutline,
            out dice2SlotTitle,
            out dice2SlotSub,
            out dice2MiniBox,
            dice2MiniPips,
            () => OnActionSlotClicked(TurnActionSlot.Dice2)
        );

        bonusSlotButton = CreateBonusSlotButton(
            actionSlotPanel,
            "Bonus3SlotBtn",
            new Vector2(280f, 0f),
            new Vector2(132f, 74f),
            out bonusSlotBg,
            out bonusSlotOutline,
            out bonusSlotTitle,
            out bonusSlotSub,
            () => OnActionSlotClicked(TurnActionSlot.Bonus3)
        );
    }

    Button CreateVisualDieSlotButton(
        RectTransform parent,
        string name,
        Vector2 anchoredPos,
        Vector2 size,
        out Image bgImage,
        out Outline outline,
        out TMP_Text titleLabel,
        out TMP_Text subLabel,
        out Image miniDieBox,
        Image[] pips,
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
        bgImage.sprite = GetRoundedPanelSprite();
        bgImage.type = Image.Type.Sliced;
        bgImage.color = DisabledSlotBg;

        outline = btnObj.AddComponent<Outline>();
        outline.effectColor = BronzeMuted;
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        Shadow shadow = btnObj.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.5f);
        shadow.effectDistance = new Vector2(0f, -3f);

        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = bgImage;
        btn.onClick.AddListener(onClick);

        // Mini Ivory Die Face on the left (48x48)
        GameObject dieObj = new GameObject("MiniDieFace");
        dieObj.layer = turnText.gameObject.layer;
        dieObj.transform.SetParent(rt, false);

        RectTransform dieRt = dieObj.AddComponent<RectTransform>();
        dieRt.anchorMin = new Vector2(0f, 0.5f);
        dieRt.anchorMax = new Vector2(0f, 0.5f);
        dieRt.pivot = new Vector2(0f, 0.5f);
        dieRt.anchoredPosition = new Vector2(8f, 0f);
        dieRt.sizeDelta = new Vector2(46f, 46f);

        miniDieBox = dieObj.AddComponent<Image>();
        miniDieBox.sprite = GetRoundedPanelSprite();
        miniDieBox.type = Image.Type.Sliced;
        miniDieBox.color = new Color(0.82f, 0.78f, 0.70f, 0.35f);
        miniDieBox.raycastTarget = false;

        Vector2[] pipOffsets =
        {
            new Vector2(-12f, 12f), new Vector2(12f, 12f),
            new Vector2(-12f, 0f), Vector2.zero, new Vector2(12f, 0f),
            new Vector2(-12f, -12f), new Vector2(12f, -12f)
        };

        for (int i = 0; i < 7; i++)
        {
            GameObject pipGo = new GameObject($"Pip_{i}");
            pipGo.layer = turnText.gameObject.layer;
            pipGo.transform.SetParent(dieRt, false);

            RectTransform pipRt = pipGo.AddComponent<RectTransform>();
            pipRt.anchorMin = new Vector2(0.5f, 0.5f);
            pipRt.anchorMax = new Vector2(0.5f, 0.5f);
            pipRt.pivot = new Vector2(0.5f, 0.5f);
            pipRt.anchoredPosition = pipOffsets[i];
            pipRt.sizeDelta = new Vector2(8.5f, 8.5f);

            Image pipImg = pipGo.AddComponent<Image>();
            pipImg.sprite = GetCirclePipSprite();
            pipImg.color = DiePipDark;
            pipImg.raycastTarget = false;
            pipImg.enabled = false;
            pips[i] = pipImg;
        }

        titleLabel = CreateLabel(
            rt,
            "SlotTitle",
            new Vector2(60f, -12f),
            new Vector2(68f, 24f),
            13.5f,
            FontStyles.Bold,
            GoldBright,
            TextAlignmentOptions.MidlineLeft
        );

        subLabel = CreateLabel(
            rt,
            "SlotSub",
            new Vector2(60f, -38f),
            new Vector2(68f, 24f),
            13f,
            FontStyles.Bold,
            IvoryText,
            TextAlignmentOptions.MidlineLeft
        );

        return btn;
    }

    Button CreateBonusSlotButton(
        RectTransform parent,
        string name,
        Vector2 anchoredPos,
        Vector2 size,
        out Image bgImage,
        out Outline outline,
        out TMP_Text titleLabel,
        out TMP_Text subLabel,
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
        bgImage.sprite = GetRoundedPanelSprite();
        bgImage.type = Image.Type.Sliced;
        bgImage.color = DisabledSlotBg;

        outline = btnObj.AddComponent<Outline>();
        outline.effectColor = BronzeMuted;
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        Shadow shadow = btnObj.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.5f);
        shadow.effectDistance = new Vector2(0f, -3f);

        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = bgImage;
        btn.onClick.AddListener(onClick);

        titleLabel = CreateLabel(
            rt,
            "BonusTitle",
            new Vector2(8f, -12f),
            new Vector2(116f, 26f),
            14f,
            FontStyles.Bold,
            GoldBright,
            TextAlignmentOptions.Center
        );

        subLabel = CreateLabel(
            rt,
            "BonusSub",
            new Vector2(8f, -38f),
            new Vector2(116f, 24f),
            12.5f,
            FontStyles.Bold,
            WarmMutedText,
            TextAlignmentOptions.Center
        );

        return btn;
    }

    void SetMiniDiePips(Image[] pips, int value, bool activeAlpha)
    {
        if (pips == null || pips.Length < 7)
            return;

        for (int i = 0; i < 7; i++)
        {
            if (pips[i] != null)
                pips[i].enabled = false;
        }

        if (value < 1 || value > 6)
            return;

        Color pipColor = (value == 1 || value == 4) ? DiePipRed : DiePipDark;
        if (!activeAlpha)
            pipColor.a = 0.35f;

        switch (value)
        {
            case 1: EnablePips(pips, pipColor, 3); break;
            case 2: EnablePips(pips, pipColor, 0, 6); break;
            case 3: EnablePips(pips, pipColor, 0, 3, 6); break;
            case 4: EnablePips(pips, pipColor, 0, 1, 5, 6); break;
            case 5: EnablePips(pips, pipColor, 0, 1, 3, 5, 6); break;
            case 6: EnablePips(pips, pipColor, 0, 1, 2, 4, 5, 6); break;
        }
    }

    static void EnablePips(Image[] pips, Color color, params int[] indices)
    {
        foreach (int idx in indices)
        {
            if (idx >= 0 && idx < pips.Length && pips[idx] != null)
            {
                pips[idx].color = color;
                pips[idx].enabled = true;
            }
        }
    }

    void StyleRollButtonIntoDockIfPresent()
    {
        GameObject netRollObj = GameObject.Find("Network Roll Dice");
        if (netRollObj == null)
            return;

        RectTransform rt = netRollObj.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-30f, 30f);
            rt.sizeDelta = new Vector2(412f, 56f);
            rt.SetAsLastSibling();
        }

        Image btnImg = netRollObj.GetComponent<Image>();
        if (btnImg != null)
        {
            btnImg.sprite = GetRoundedPanelSprite();
            btnImg.type = Image.Type.Sliced;
            btnImg.color = new Color(0.88f, 0.64f, 0.16f, 1f);
        }

        Outline outline = netRollObj.GetComponent<Outline>() ?? netRollObj.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 0.92f, 0.55f, 1f);
        outline.effectDistance = new Vector2(2f, -2f);

        Shadow shadow = netRollObj.GetComponent<Shadow>() ?? netRollObj.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.65f);
        shadow.effectDistance = new Vector2(0f, -4f);

        TMP_Text btnText = netRollObj.GetComponentInChildren<TMP_Text>(true);
        if (btnText != null)
        {
            btnText.text = "GIEO 2 XÚC XẮC";
            btnText.fontSize = 21f;
            btnText.fontStyle = FontStyles.Bold;
            btnText.color = new Color(0.14f, 0.09f, 0.03f, 1f);
            btnText.alignment = TextAlignmentOptions.Center;
        }
    }

    void StyleRoomControlPanelIfPresent()
    {
        GameObject roomPanel = GameObject.Find("RoomControlPanel");
        if (roomPanel == null)
            return;

        RectTransform rt = roomPanel.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-16f, -12f);
            rt.sizeDelta = new Vector2(184f, 64f);
        }

        Image panelImg = roomPanel.GetComponent<Image>();
        if (panelImg != null)
        {
            panelImg.sprite = GetRoundedPanelSprite();
            panelImg.type = Image.Type.Sliced;
            panelImg.color = WoodDarkBg;
        }

        AddRoyalFrame(roomPanel, GoldBorder, new Vector2(1.8f, -1.8f));

        Transform titleTr = roomPanel.transform.Find("RoomControlTitle");
        if (titleTr != null)
            titleTr.gameObject.SetActive(false);

        Transform statusTr = roomPanel.transform.Find("RoomControlStatus");
        if (statusTr != null)
            statusTr.gameObject.SetActive(false);

        Transform outBtnTr = roomPanel.transform.Find("OutRoomButton");
        if (outBtnTr != null)
        {
            RectTransform outRt = outBtnTr.GetComponent<RectTransform>();
            if (outRt != null)
            {
                outRt.anchorMin = new Vector2(0.5f, 0.5f);
                outRt.anchorMax = new Vector2(0.5f, 0.5f);
                outRt.pivot = new Vector2(0.5f, 0.5f);
                outRt.anchoredPosition = Vector2.zero;
                outRt.sizeDelta = new Vector2(160f, 42f);
            }

            Image outImg = outBtnTr.GetComponent<Image>();
            if (outImg != null)
            {
                outImg.sprite = GetRoundedPanelSprite();
                outImg.type = Image.Type.Sliced;
                outImg.color = new Color(0.68f, 0.14f, 0.16f, 0.98f);
            }

            Outline btnOutline = outBtnTr.gameObject.GetComponent<Outline>() ?? outBtnTr.gameObject.AddComponent<Outline>();
            btnOutline.effectColor = GoldBorder;
            btnOutline.effectDistance = new Vector2(1.5f, -1.5f);

            TMP_Text outLbl = outBtnTr.GetComponentInChildren<TMP_Text>(true);
            if (outLbl != null)
            {
                outLbl.text = "RỜI PHÒNG";
                outLbl.fontSize = 16.5f;
                outLbl.fontStyle = FontStyles.Bold;
                outLbl.color = IvoryText;
                outLbl.alignment = TextAlignmentOptions.Center;
            }
        }
    }

    GameObject CreateRoundedSubPanel(
        RectTransform parent,
        string name,
        Vector2 anchor,
        Vector2 anchoredPos,
        Vector2 size,
        Color bgColor,
        Color outlineColor)
    {
        GameObject go = new GameObject(name);
        go.layer = turnText.gameObject.layer;
        go.transform.SetParent(parent, false);

        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        Image img = go.AddComponent<Image>();
        img.sprite = GetRoundedPanelSprite();
        img.type = Image.Type.Sliced;
        img.color = bgColor;
        img.raycastTarget = false;

        AddRoyalFrame(go, outlineColor, new Vector2(1.5f, -1.5f));
        return go;
    }

    static void AddRoyalFrame(GameObject target, Color borderColor, Vector2 borderThickness)
    {
        Outline outline = target.GetComponent<Outline>() ?? target.AddComponent<Outline>();
        outline.effectColor = borderColor;
        outline.effectDistance = borderThickness;

        Shadow shadow = target.GetComponent<Shadow>() ?? target.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
        shadow.effectDistance = new Vector2(0f, -4f);
    }

    TMP_Text CreateLabel(
        RectTransform parent,
        string name,
        Vector2 anchoredPos,
        Vector2 size,
        float fontSize,
        FontStyles style,
        Color color,
        TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name);
        go.layer = turnText.gameObject.layer;
        go.transform.SetParent(parent, false);

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        if (turnText != null && turnText.font != null)
            tmp.font = turnText.font;

        tmp.fontStyle = style;
        tmp.alignment = alignment;
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
        text.overflowMode = TextOverflowModes.Truncate;
        text.raycastTarget = false;
    }

    Color GetPlayerColor(int playerIndex)
    {
        switch (playerIndex)
        {
            case 0: return new Color(0.22f, 0.48f, 0.92f, 1f); // Royal Sapphire Blue
            case 1: return new Color(0.86f, 0.22f, 0.22f, 1f); // Crimson Ruby Red
            case 2: return new Color(0.18f, 0.68f, 0.36f, 1f); // Emerald Forest Green
            case 3: return new Color(0.88f, 0.66f, 0.14f, 1f); // Imperial Amber Gold
            default: return IvoryText;
        }
    }

    static string GetVietnameseColorName(int playerIndex)
    {
        switch (playerIndex)
        {
            case 0: return "Xanh Dương";
            case 1: return "Đỏ";
            case 2: return "Xanh Lá";
            case 3: return "Vàng";
            default: return ((PlayerColor)playerIndex).ToString();
        }
    }

    string GetSafeDisplayName(int playerIndex, string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            displayName = PlayerPrefs.GetString("TurnDisplayName_" + playerIndex, "");

        if (string.IsNullOrWhiteSpace(displayName))
            displayName = ((PlayerColor)playerIndex).ToString();

        displayName = displayName.Trim();

        if (displayName.Length > 16)
            displayName = displayName.Substring(0, 16);

        return displayName;
    }

    static Sprite CreateRoundedSprite(int size, int radius)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        float r = radius;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x < r ? r - x - 0.5f : (x >= size - r ? x - (size - r - 1) - 0.5f : 0f);
                float dy = y < r ? r - y - 0.5f : (y >= size - r ? y - (size - r - 1) - 0.5f : 0f);
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01(r + 0.5f - dist);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        tex.Apply();
        return Sprite.Create(
            tex,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            new Vector4(radius, radius, radius, radius)
        );
    }

    static Sprite CreateCircleSprite(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        float center = (size - 1) * 0.5f;
        float radius = size * 0.46f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                float alpha = Mathf.Clamp01(radius + 0.6f - dist);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}
