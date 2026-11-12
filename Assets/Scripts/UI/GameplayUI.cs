using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class GameplayUI : MonoBehaviour
{
    public static GameplayUI Instance;

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

    // Shared procedural sprites for Royal Lacquer Arcade theme
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
    RectTransform feedBoxRect;

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
    Image dice1QuestionOverlay;
    Image dice2QuestionOverlay;
    Image bonusSwordIcon;
    readonly Image[] dice1MiniPips = new Image[7];
    readonly Image[] dice2MiniPips = new Image[7];

    int latestTurnSeconds = DiceRuleUtility.TurnDecisionSeconds;
    int latestMatchSeconds = DiceRuleUtility.MatchDurationSeconds;
    bool latestIsFinalRound = false;

    class PlayerCardWidgets
    {
        public GameObject root;
        public RectTransform rootRt;
        public Image cardBg;
        public Outline cardOutline;
        public Image emblemBg;
        public Image crownIcon;
        public TMP_Text rankText;
        public TMP_Text nameText;
        public TMP_Text statsText;
        public TMP_Text scoreText;
        public GameObject activeTurnBadge;
        public Image badgeBg;
        public Image badgeIcon;
        public TMP_Text badgeText;
    }

    // Royal Lacquer Arcade Color Palette (from RoyalUiThemeKit)
    static Color WoodDarkBg => RoyalUiThemeKit.LacquerEbony;
    static Color WoodInnerBg => RoyalUiThemeKit.LacquerInset;
    static Color WoodActiveCardBg => RoyalUiThemeKit.LacquerActive;
    static Color GoldBorder => RoyalUiThemeKit.BrassAntique;
    static Color GoldBright => RoyalUiThemeKit.BrassBright;
    static Color BronzeMuted => RoyalUiThemeKit.BronzeMuted;
    static Color IvoryText => RoyalUiThemeKit.IvoryText;
    static Color WarmMutedText => RoyalUiThemeKit.WarmMutedText;
    static Color ActiveSlotGoldBg => RoyalUiThemeKit.ArcadeGoldCta;
    static Color ReadySlotWoodBg => RoyalUiThemeKit.CrimsonVelvet;
    static Color DisabledSlotBg => RoyalUiThemeKit.LacquerInset;
    static Color ComboActiveBg => new Color(0.84f, 0.25f, 0.12f, 0.98f);
    static readonly Color IvoryDieFace = new Color(0.98f, 0.95f, 0.87f, 1.00f);
    static readonly Color DiePipDark = new Color(0.14f, 0.09f, 0.05f, 1.00f);
    static readonly Color DiePipRed = new Color(0.84f, 0.16f, 0.16f, 1.00f);

    void Awake()
    {
        Instance = this;
        ConfigureHudPopup();
    }

    void Update()
    {
        if (Keyboard.current != null && (
            Keyboard.current.f10Key.wasPressedThisFrame ||
            Keyboard.current.endKey.wasPressedThisFrame))
        {
            TriggerFastMatchFinish();
        }
    }

    void Start()
    {
        ConfigureHudPopup();
        StyleRoomControlPanelIfPresent();
        StyleRollButtonIntoDockIfPresent();
        BgmManager.EnsureCreated().AttachSfxToAllSceneButtons();
    }

    public static Sprite GetRoundedPanelSprite()
    {
        if (roundedPanelSprite == null)
            roundedPanelSprite = RoyalUiThemeKit.GetLacquerPanelSprite();
        return roundedPanelSprite;
    }

    public static Sprite GetRoundedPillSprite()
    {
        if (roundedPillSprite == null)
            roundedPillSprite = RoyalUiThemeKit.GetPillCapsuleSprite();
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
            string youTag = (localPlayerIndex >= 0 && localPlayerIndex == turnIdx) ? " <color=#FFD95A>[BẠN]</color>" : "";
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
        BoardManager board = FindFirstObjectByType<BoardManager>();
        bool hasBounty = board != null && board.activeEvent == PartyEventType.BountyHunt && board.bountyTargetPlayer >= 0;

        for (int i = 0; i < playerCards.Length; i++)
        {
            PlayerCardWidgets card = playerCards[i];
            if (card == null || card.root == null)
                continue;

            bool isCurrent = i == activeIdx;
            bool isHunted = hasBounty && board.bountyTargetPlayer == i;

            if (card.cardBg != null)
                card.cardBg.color = isCurrent ? WoodActiveCardBg : WoodDarkBg;

            if (card.cardOutline != null)
            {
                if (isHunted)
                {
                    card.cardOutline.effectColor = new Color(1.00f, 0.22f, 0.25f, 1f);
                    card.cardOutline.effectDistance = new Vector2(2.4f, -2.4f);
                }
                else
                {
                    card.cardOutline.effectColor = isCurrent ? GoldBright : BronzeMuted;
                    card.cardOutline.effectDistance = isCurrent ? new Vector2(2.2f, -2.2f) : new Vector2(1.2f, -1.2f);
                }
            }

            if (card.activeTurnBadge != null)
            {
                if (isHunted)
                {
                    card.activeTurnBadge.SetActive(true);
                    if (card.badgeBg != null) card.badgeBg.color = RoyalUiThemeKit.ArcadeCrimson;
                    if (card.badgeIcon != null)
                    {
                        card.badgeIcon.sprite = RoyalUiThemeKit.GetIcon(RoyalUiThemeKit.IconKind.Crosshair);
                        card.badgeIcon.color = Color.white;
                        card.badgeIcon.enabled = true;
                    }
                    if (card.badgeText != null)
                    {
                        card.badgeText.text = "TRUY NÃ";
                        card.badgeText.color = Color.white;
                    }
                }
                else if (isCurrent)
                {
                    card.activeTurnBadge.SetActive(true);
                    if (card.badgeBg != null) card.badgeBg.color = GoldBright;
                    if (card.badgeIcon != null)
                    {
                        card.badgeIcon.sprite = RoyalUiThemeKit.GetIcon(RoyalUiThemeKit.IconKind.Lotus);
                        card.badgeIcon.color = RoyalUiThemeKit.DarkGoldInk;
                        card.badgeIcon.enabled = true;
                    }
                    if (card.badgeText != null)
                    {
                        card.badgeText.text = "LƯỢT ĐI";
                        card.badgeText.color = RoyalUiThemeKit.DarkGoldInk;
                    }
                }
                else
                {
                    card.activeTurnBadge.SetActive(false);
                }
            }
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
            dice1QuestionOverlay,
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
            dice2QuestionOverlay,
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
        Image questionOverlay,
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
                outline.effectDistance = new Vector2(2.4f, -2.4f);
            }
        }
        else if (isReady)
        {
            bg.color = ReadySlotWoodBg;
            if (outline != null)
            {
                outline.effectColor = GoldBorder;
                outline.effectDistance = new Vector2(1.6f, -1.6f);
            }
        }
        else
        {
            bg.color = DisabledSlotBg;
            if (outline != null)
            {
                outline.effectColor = BronzeMuted;
                outline.effectDistance = new Vector2(1.1f, -1.1f);
            }
        }

        if (titleLabel != null)
        {
            titleLabel.text = isSelected ? $">> {slotTitle}" : slotTitle;
            titleLabel.color = isSelected ? RoyalUiThemeKit.DarkGoldInk : (isReady ? GoldBright : WarmMutedText);
        }

        if (subLabel != null)
        {
            if (dieValue <= 0)
                subLabel.text = "Chờ gieo";
            else if (isUsed)
                subLabel.text = "Đã dùng";
            else
                subLabel.text = $"{dieValue} bước";

            subLabel.color = isSelected ? RoyalUiThemeKit.DarkGoldInk : (isReady ? IvoryText : new Color(0.84f, 0.78f, 0.68f, 0.88f));
        }

        if (miniDieBox != null)
        {
            bool showPips = dieValue > 0;
            if (showPips)
            {
                miniDieBox.color = !isUsed ? IvoryDieFace : new Color(0.55f, 0.50f, 0.44f, 0.45f);
                if (questionOverlay != null)
                    questionOverlay.enabled = false;
                SetMiniDiePips(pips, dieValue, !isUsed);
            }
            else
            {
                miniDieBox.color = new Color(0.18f, 0.12f, 0.08f, 0.92f);
                if (questionOverlay != null)
                {
                    questionOverlay.enabled = true;
                    questionOverlay.color = new Color(1f, 0.96f, 0.88f, 0.94f);
                }
                SetMiniDiePips(pips, 0, false);
            }
        }
    }

    void StyleBonusSlotCard(bool bonusReady, bool bonusUsed, bool isSelected, bool interactable)
    {
        if (bonusSlotButton == null || bonusSlotBg == null)
            return;

        bonusSlotButton.interactable = interactable;

        if (isSelected || bonusReady)
        {
            bonusSlotBg.color = isSelected ? ComboActiveBg : new Color(0.62f, 0.20f, 0.10f, 0.98f);
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
                bonusSlotOutline.effectColor = BronzeMuted;
                bonusSlotOutline.effectDistance = new Vector2(1.1f, -1.1f);
            }
        }

        if (bonusSwordIcon != null)
        {
            bonusSwordIcon.color = (bonusReady || isSelected)
                ? Color.white
                : new Color(1f, 0.88f, 0.72f, 0.78f);
        }

        if (bonusSlotTitle != null)
        {
            bonusSlotTitle.text = bonusReady ? ">> COMBO +3" : "COMBO +3";
            bonusSlotTitle.color = bonusReady ? GoldBright : IvoryText;
        }

        if (bonusSlotSub != null)
        {
            if (bonusUsed)
                bonusSlotSub.text = "Đã dùng";
            else if (bonusReady)
                bonusSlotSub.text = "Đi +3 bước!";
            else
                bonusSlotSub.text = "Đá địch mở";

            bonusSlotSub.color = bonusReady ? IvoryText : new Color(0.84f, 0.78f, 0.68f, 0.88f);
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
        {
            eventBannerText.text = $"[ {board.lastEventBannerText} ]";
            if (board.activeEvent == PartyEventType.BountyHunt)
                eventBannerText.color = new Color(1.00f, 0.42f, 0.42f, 1f);
            else if (board.activeEvent == PartyEventType.HomeRush)
                eventBannerText.color = new Color(1.00f, 0.62f, 0.18f, 1f);
            else
                eventBannerText.color = GoldBright;
        }

        if (feedText != null)
        {
            feedText.text = string.IsNullOrWhiteSpace(board.lastFeedMessage)
                ? "Mẹo: Đạp Ô Tăng Tốc (+3 bước) | Qua bước 28 (+1đ & thành Tinh Nhuệ)"
                : board.lastFeedMessage;
        }

        if (feedBoxRect != null && leftScoreboardColumn != null)
        {
            float feedY = -48f - (playerCount * 78f) - 10f;
            feedBoxRect.anchoredPosition = new Vector2(0f, feedY);
            leftScoreboardColumn.sizeDelta = new Vector2(356f, Mathf.Abs(feedY) + 56f);
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

            int rank = Mathf.Max(1, playerRank[i]);
            float rankY = -48f - (rank - 1) * 78f;
            if (card.rootRt != null)
                card.rootRt.anchoredPosition = new Vector2(0f, rankY);

            string pName = GetSafeDisplayName(i, PlayerPrefs.GetString("TurnDisplayName_" + i, ""));
            string youBadge = (localPlayerIndex == i) ? " <color=#8AE6FF>[BẠN]</color>" : "";
            bool isHuntedTarget = board.activeEvent == PartyEventType.BountyHunt && board.bountyTargetPlayer == i;

            if (card.crownIcon != null)
                card.crownIcon.enabled = rank == 1;

            if (card.emblemBg != null)
                card.emblemBg.color = rank == 1 ? new Color(0.84f, 0.60f, 0.14f, 0.98f) : GetPlayerColor(i);

            if (card.rankText != null)
            {
                card.rankText.text = $"#{rank}";
                card.rankText.color = rank == 1 ? RoyalUiThemeKit.DarkGoldInk : Color.white;
                card.rankText.rectTransform.anchoredPosition = rank == 1 ? new Vector2(0f, -6f) : Vector2.zero;
            }

            if (card.nameText != null)
                card.nameText.text = $"{pName}{youBadge}";

            if (card.statsText != null)
                card.statsText.text = $"Về đích: <b>{board.playerHomeCounts[i]}</b>   |   Đá hạ: <b>{board.playerKickCounts[i]}</b>";

            if (card.scoreText != null)
                card.scoreText.text = $"{board.playerScores[i]}đ";

            bool isCurrent = i == activeTurnIdx;
            if (card.cardBg != null)
                card.cardBg.color = isCurrent ? WoodActiveCardBg : WoodDarkBg;

            if (card.cardOutline != null)
            {
                if (isHuntedTarget)
                {
                    card.cardOutline.effectColor = new Color(1.00f, 0.22f, 0.25f, 1f);
                    card.cardOutline.effectDistance = new Vector2(2.4f, -2.4f);
                }
                else
                {
                    card.cardOutline.effectColor = isCurrent ? GoldBright : BronzeMuted;
                    card.cardOutline.effectDistance = isCurrent ? new Vector2(2.2f, -2.2f) : new Vector2(1.2f, -1.2f);
                }
            }

            if (card.activeTurnBadge != null)
            {
                if (isHuntedTarget)
                {
                    card.activeTurnBadge.SetActive(true);
                    if (card.badgeBg != null) card.badgeBg.color = RoyalUiThemeKit.ArcadeCrimson;
                    if (card.badgeIcon != null)
                    {
                        card.badgeIcon.sprite = RoyalUiThemeKit.GetIcon(RoyalUiThemeKit.IconKind.Crosshair);
                        card.badgeIcon.color = Color.white;
                        card.badgeIcon.enabled = true;
                    }
                    if (card.badgeText != null)
                    {
                        card.badgeText.text = "TRUY NÃ";
                        card.badgeText.color = Color.white;
                    }
                }
                else if (isCurrent)
                {
                    card.activeTurnBadge.SetActive(true);
                    if (card.badgeBg != null) card.badgeBg.color = GoldBright;
                    if (card.badgeIcon != null)
                    {
                        card.badgeIcon.sprite = RoyalUiThemeKit.GetIcon(RoyalUiThemeKit.IconKind.Lotus);
                        card.badgeIcon.color = RoyalUiThemeKit.DarkGoldInk;
                        card.badgeIcon.enabled = true;
                    }
                    if (card.badgeText != null)
                    {
                        card.badgeText.text = "LƯỢT ĐI";
                        card.badgeText.color = RoyalUiThemeKit.DarkGoldInk;
                    }
                }
                else
                {
                    card.activeTurnBadge.SetActive(false);
                }
            }
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
        RoyalUiThemeKit.BuildHelpGuideButton(canvasRect, new Vector2(1f, 1f), new Vector2(-18f, -84f), 42f);

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
        topCenterBanner.anchoredPosition = new Vector2(20f, -10f);
        topCenterBanner.sizeDelta = new Vector2(704f, 104f);

        RoyalUiThemeKit.ApplyLacquerPanel(bannerObj, WoodDarkBg, GoldBorder, 2.0f, true);
        bannerObj.GetComponent<Image>().raycastTarget = false;

        // Left pill: Match Clock (10:00)
        GameObject clockPill = CreateRoundedSubPanel(
            topCenterBanner,
            "MatchClockPill",
            new Vector2(0f, 1f),
            new Vector2(12f, -8f),
            new Vector2(122f, 34f),
            WoodInnerBg,
            BronzeMuted
        );
        clockPill.GetComponent<Image>().sprite = RoyalUiThemeKit.GetRecessedSlotSprite();
        matchClockText = CreateLabel(
            clockPill.GetComponent<RectTransform>(),
            "MatchClockLabel",
            Vector2.zero,
            new Vector2(122f, 34f),
            14.5f,
            FontStyles.Bold,
            GoldBright,
            TextAlignmentOptions.Center
        );

        // Center ribbon plaque: Active Player Turn (Navy Arcade Ribbon + Brass Rim)
        GameObject turnPill = CreateRoundedSubPanel(
            topCenterBanner,
            "TurnCrestPill",
            new Vector2(0f, 1f),
            new Vector2(142f, -8f),
            new Vector2(468f, 34f),
            RoyalUiThemeKit.NavyRibbon,
            GoldBorder
        );
        turnPill.GetComponent<Image>().sprite = RoyalUiThemeKit.GetRibbonHeaderSprite();
        turnHeaderText = CreateLabel(
            turnPill.GetComponent<RectTransform>(),
            "TurnHeaderLabel",
            Vector2.zero,
            new Vector2(468f, 34f),
            16f,
            FontStyles.Bold,
            IvoryText,
            TextAlignmentOptions.Center
        );
        turnHeaderText.enableAutoSizing = true;
        turnHeaderText.fontSizeMin = 12f;
        turnHeaderText.fontSizeMax = 16f;
        turnHeaderText.textWrappingMode = TextWrappingModes.NoWrap;
        turnHeaderText.overflowMode = TextOverflowModes.Ellipsis;

        // Right medallion pill: Turn Countdown (20s)
        GameObject timerPill = CreateRoundedSubPanel(
            topCenterBanner,
            "TurnTimerPill",
            new Vector2(0f, 1f),
            new Vector2(618f, -8f),
            new Vector2(74f, 34f),
            RoyalUiThemeKit.CrimsonVelvet,
            GoldBright
        );
        timerPill.GetComponent<Image>().sprite = RoyalUiThemeKit.GetPillCapsuleSprite();
        turnCountdownPillBg = timerPill.GetComponent<Image>();
        turnCountdownText = CreateLabel(
            timerPill.GetComponent<RectTransform>(),
            "TurnCountdownLabel",
            Vector2.zero,
            new Vector2(74f, 34f),
            17f,
            FontStyles.Bold,
            GoldBright,
            TextAlignmentOptions.Center
        );

        // Middle ribbon: Event Banner
        eventBannerText = CreateLabel(
            topCenterBanner,
            "EventBannerText",
            new Vector2(14f, -45f),
            new Vector2(676f, 24f),
            13.5f,
            FontStyles.Bold,
            GoldBright,
            TextAlignmentOptions.Center
        );
        eventBannerText.enableAutoSizing = true;
        eventBannerText.fontSizeMin = 11.5f;
        eventBannerText.fontSizeMax = 13.5f;
        eventBannerText.textWrappingMode = TextWrappingModes.NoWrap;

        // Reparent existing messageText into the bottom row of the Royal Banner
        messageText.transform.SetParent(topCenterBanner, false);
        RectTransform msgRt = messageText.rectTransform;
        msgRt.anchorMin = new Vector2(0f, 1f);
        msgRt.anchorMax = new Vector2(0f, 1f);
        msgRt.pivot = new Vector2(0f, 1f);
        msgRt.anchoredPosition = new Vector2(14f, -71f);
        msgRt.sizeDelta = new Vector2(676f, 24f);
        messageText.fontSize = 14f;
        messageText.enableAutoSizing = true;
        messageText.fontSizeMin = 11.5f;
        messageText.fontSizeMax = 14f;
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
        leftScoreboardColumn.sizeDelta = new Vector2(356f, 270f);

        // Header Ribbon Plate
        GameObject headerPlate = CreateRoundedSubPanel(
            leftScoreboardColumn,
            "ScoreboardHeaderPlate",
            new Vector2(0f, 1f),
            new Vector2(0f, 0f),
            new Vector2(356f, 38f),
            RoyalUiThemeKit.CrimsonVelvet,
            GoldBorder
        );
        headerPlate.GetComponent<Image>().sprite = RoyalUiThemeKit.GetRibbonHeaderSprite();
        CreateLabel(
            headerPlate.GetComponent<RectTransform>(),
            "HeaderTitle",
            Vector2.zero,
            new Vector2(356f, 38f),
            15.5f,
            FontStyles.Bold,
            GoldBright,
            TextAlignmentOptions.Center
        ).text = "BẢNG XẾP HẠNG ĐIỂM";

        for (int i = 0; i < 4; i++)
        {
            float yOffset = -48f - i * 78f;
            playerCards[i] = CreateSinglePlayerCard(leftScoreboardColumn, i, new Vector2(0f, yOffset));
        }

        // Parchment Scroll Action Feed Strip at the bottom of the column
        GameObject feedBox = CreateRoundedSubPanel(
            leftScoreboardColumn,
            "RoyalActionFeedBox",
            new Vector2(0f, 1f),
            new Vector2(0f, -214f),
            new Vector2(356f, 52f),
            RoyalUiThemeKit.ParchmentFill,
            new Color(0.56f, 0.36f, 0.14f, 0.96f)
        );
        feedBox.GetComponent<Image>().sprite = RoyalUiThemeKit.GetParchmentScrollSprite();
        feedBoxRect = feedBox.GetComponent<RectTransform>();
        feedText = CreateLabel(
            feedBox.GetComponent<RectTransform>(),
            "ActionFeedText",
            new Vector2(12f, -4f),
            new Vector2(332f, 44f),
            12.5f,
            FontStyles.Bold,
            RoyalUiThemeKit.ParchmentInk,
            TextAlignmentOptions.MidlineLeft
        );
        feedText.enableAutoSizing = true;
        feedText.fontSizeMin = 10.5f;
        feedText.fontSizeMax = 12.5f;
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
            new Vector2(356f, 70f),
            WoodDarkBg,
            BronzeMuted
        );
        w.rootRt = w.root.GetComponent<RectTransform>();
        w.cardBg = w.root.GetComponent<Image>();
        w.cardOutline = w.root.GetComponent<Outline>();

        RectTransform cardRt = w.rootRt;

        // Left Faction Identity Stripe (6px)
        GameObject stripeObj = new GameObject("FactionStripe", typeof(RectTransform), typeof(Image));
        stripeObj.layer = turnText.gameObject.layer;
        stripeObj.transform.SetParent(cardRt, false);
        RectTransform stripeRt = stripeObj.GetComponent<RectTransform>();
        stripeRt.anchorMin = new Vector2(0f, 0.12f);
        stripeRt.anchorMax = new Vector2(0f, 0.88f);
        stripeRt.pivot = new Vector2(0f, 0.5f);
        stripeRt.anchoredPosition = new Vector2(5f, 0f);
        stripeRt.sizeDelta = new Vector2(5f, 0f);
        Image stripeImg = stripeObj.GetComponent<Image>();
        stripeImg.sprite = RoyalUiThemeKit.GetPillCapsuleSprite();
        stripeImg.type = Image.Type.Sliced;
        stripeImg.color = GetPlayerColor(playerIndex);
        stripeImg.raycastTarget = false;

        // Rank Medallion Crest (46x46)
        GameObject emblemObj = CreateRoundedSubPanel(
            cardRt,
            "FactionEmblem",
            new Vector2(0f, 1f),
            new Vector2(14f, -12f),
            new Vector2(46f, 46f),
            GetPlayerColor(playerIndex),
            GoldBorder
        );
        emblemObj.GetComponent<Image>().sprite = RoyalUiThemeKit.GetCircleMedallionSprite();
        emblemObj.GetComponent<Image>().type = Image.Type.Simple;
        w.emblemBg = emblemObj.GetComponent<Image>();

        GameObject crownObj = new GameObject("CrownIcon", typeof(RectTransform), typeof(Image));
        crownObj.layer = turnText.gameObject.layer;
        crownObj.transform.SetParent(emblemObj.transform, false);
        RectTransform crRt = crownObj.GetComponent<RectTransform>();
        crRt.anchorMin = crRt.anchorMax = new Vector2(0.5f, 1f);
        crRt.pivot = new Vector2(0.5f, 1f);
        crRt.anchoredPosition = new Vector2(0f, -2f);
        crRt.sizeDelta = new Vector2(16f, 12f);
        w.crownIcon = crownObj.GetComponent<Image>();
        w.crownIcon.sprite = RoyalUiThemeKit.GetIcon(RoyalUiThemeKit.IconKind.Crown);
        w.crownIcon.preserveAspect = true;
        w.crownIcon.color = RoyalUiThemeKit.DarkGoldInk;
        w.crownIcon.raycastTarget = false;
        w.crownIcon.enabled = false;

        w.rankText = CreateLabel(
            emblemObj.GetComponent<RectTransform>(),
            "RankLabel",
            Vector2.zero,
            new Vector2(46f, 46f),
            15f,
            FontStyles.Bold,
            Color.white,
            TextAlignmentOptions.Center
        );
        w.rankText.text = $"#{playerIndex + 1}";

        // Player Name (fits cleanly beside wider status pill)
        w.nameText = CreateLabel(
            cardRt,
            "PlayerNameLabel",
            new Vector2(66f, -10f),
            new Vector2(134f, 24f),
            14.5f,
            FontStyles.Bold,
            IvoryText,
            TextAlignmentOptions.MidlineLeft
        );
        w.nameText.enableAutoSizing = true;
        w.nameText.fontSizeMin = 12f;
        w.nameText.fontSizeMax = 14.5f;
        w.nameText.textWrappingMode = TextWrappingModes.NoWrap;
        w.nameText.overflowMode = TextOverflowModes.Ellipsis;
        w.nameText.text = GetSafeDisplayName(playerIndex, "");

        // Status Pill (x = 202..276, width = 74f, height = 22f) with Lotus / Crosshair emblem
        w.activeTurnBadge = CreateRoundedSubPanel(
            cardRt,
            "PlayerStatusPill",
            new Vector2(0f, 1f),
            new Vector2(202f, -11f),
            new Vector2(74f, 22f),
            GoldBright,
            GoldBorder
        );
        w.activeTurnBadge.GetComponent<Image>().sprite = RoyalUiThemeKit.GetPillCapsuleSprite();
        w.badgeBg = w.activeTurnBadge.GetComponent<Image>();

        GameObject bIconObj = new GameObject("BadgeIcon", typeof(RectTransform), typeof(Image));
        bIconObj.layer = turnText.gameObject.layer;
        bIconObj.transform.SetParent(w.activeTurnBadge.transform, false);
        RectTransform biRt = bIconObj.GetComponent<RectTransform>();
        biRt.anchorMin = biRt.anchorMax = new Vector2(0f, 0.5f);
        biRt.pivot = new Vector2(0f, 0.5f);
        biRt.anchoredPosition = new Vector2(5f, 0f);
        biRt.sizeDelta = new Vector2(12f, 12f);
        w.badgeIcon = bIconObj.GetComponent<Image>();
        w.badgeIcon.sprite = RoyalUiThemeKit.GetIcon(RoyalUiThemeKit.IconKind.Lotus);
        w.badgeIcon.preserveAspect = true;
        w.badgeIcon.color = RoyalUiThemeKit.DarkGoldInk;
        w.badgeIcon.raycastTarget = false;

        w.badgeText = CreateLabel(
            w.activeTurnBadge.GetComponent<RectTransform>(),
            "BadgeLabel",
            new Vector2(18f, 0f),
            new Vector2(52f, 22f),
            9.5f,
            FontStyles.Bold,
            RoyalUiThemeKit.DarkGoldInk,
            TextAlignmentOptions.Center
        );
        w.badgeText.enableAutoSizing = true;
        w.badgeText.fontSizeMin = 8f;
        w.badgeText.fontSizeMax = 10f;
        w.badgeText.textWrappingMode = TextWrappingModes.NoWrap;
        w.badgeText.text = "LƯỢT ĐI";
        w.activeTurnBadge.SetActive(false);

        // Sub-stats (Home & Kick counts)
        w.statsText = CreateLabel(
            cardRt,
            "PlayerStatsLabel",
            new Vector2(66f, -38f),
            new Vector2(204f, 22f),
            12.5f,
            FontStyles.Normal,
            WarmMutedText,
            TextAlignmentOptions.MidlineLeft
        );
        w.statsText.text = "Về đích: <b>0</b>   |   Đá hạ: <b>0</b>";

        // Right Recessed Score Box (66x48)
        GameObject scoreBox = CreateRoundedSubPanel(
            cardRt,
            "ScoreBox",
            new Vector2(0f, 1f),
            new Vector2(280f, -11f),
            new Vector2(66f, 48f),
            WoodInnerBg,
            GoldBorder
        );
        scoreBox.GetComponent<Image>().sprite = RoyalUiThemeKit.GetRecessedSlotSprite();
        w.scoreText = CreateLabel(
            scoreBox.GetComponent<RectTransform>(),
            "ScoreValueLabel",
            Vector2.zero,
            new Vector2(66f, 48f),
            19.5f,
            FontStyles.Bold,
            GoldBright,
            TextAlignmentOptions.Center
        );
        w.scoreText.text = "0đ";

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
        diceDockPanel.sizeDelta = new Vector2(440f, 174f);

        RoyalUiThemeKit.ApplyLacquerPanel(dockObj, WoodDarkBg, GoldBorder, 2.0f, true);
        dockObj.GetComponent<Image>().raycastTarget = false;

        // Inner Crimson Velvet Dice Tray recess behind slots
        GameObject velvetTray = CreateRoundedSubPanel(
            diceDockPanel,
            "VelvetDiceTray",
            new Vector2(0f, 1f),
            new Vector2(10f, -10f),
            new Vector2(420f, 84f),
            RoyalUiThemeKit.CrimsonVelvet,
            BronzeMuted
        );
        velvetTray.GetComponent<Image>().sprite = RoyalUiThemeKit.GetRecessedSlotSprite();

        // Action Slots Row inside the velvet tray
        GameObject barObject = new GameObject("PartyClashActionSlots");
        barObject.layer = turnText.gameObject.layer;
        barObject.transform.SetParent(diceDockPanel, false);

        actionSlotPanel = barObject.AddComponent<RectTransform>();
        actionSlotPanel.anchorMin = new Vector2(0f, 1f);
        actionSlotPanel.anchorMax = new Vector2(0f, 1f);
        actionSlotPanel.pivot = new Vector2(0f, 1f);
        actionSlotPanel.anchoredPosition = new Vector2(14f, -15f);
        actionSlotPanel.sizeDelta = new Vector2(412f, 74f);

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
            out dice1QuestionOverlay,
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
            out dice2QuestionOverlay,
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
            out bonusSwordIcon,
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
        out Image questionOverlay,
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

        RoyalUiThemeKit.ApplyArcadeButton(btnObj, DisabledSlotBg, BronzeMuted, 1.4f);
        bgImage = btnObj.GetComponent<Image>();
        outline = btnObj.GetComponent<Outline>();

        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = bgImage;
        btn.onClick.AddListener(onClick);
        if (btnObj.GetComponent<RoyalButtonHoverEffect>() == null)
            btnObj.AddComponent<RoyalButtonHoverEffect>();

        // Mini Ivory Die Face on the left (46x46)
        GameObject dieObj = new GameObject("MiniDieFace");
        dieObj.layer = turnText.gameObject.layer;
        dieObj.transform.SetParent(rt, false);

        RectTransform dieRt = dieObj.AddComponent<RectTransform>();
        dieRt.anchorMin = new Vector2(0f, 0.5f);
        dieRt.anchorMax = new Vector2(0f, 0.5f);
        dieRt.pivot = new Vector2(0f, 0.5f);
        dieRt.anchoredPosition = new Vector2(7f, 1f);
        dieRt.sizeDelta = new Vector2(46f, 46f);

        miniDieBox = dieObj.AddComponent<Image>();
        miniDieBox.sprite = RoyalUiThemeKit.GetRecessedSlotSprite();
        miniDieBox.type = Image.Type.Sliced;
        miniDieBox.color = new Color(0.18f, 0.12f, 0.08f, 0.92f);
        miniDieBox.raycastTarget = false;

        // Crisp 3D '?' Die Icon visible when waiting to roll
        GameObject qObj = new GameObject("WaitingDieQuestionIcon", typeof(RectTransform), typeof(Image));
        qObj.layer = turnText.gameObject.layer;
        qObj.transform.SetParent(dieRt, false);
        RectTransform qRt = qObj.GetComponent<RectTransform>();
        qRt.anchorMin = Vector2.zero;
        qRt.anchorMax = Vector2.one;
        qRt.offsetMin = new Vector2(2f, 2f);
        qRt.offsetMax = new Vector2(-2f, -2f);
        questionOverlay = qObj.GetComponent<Image>();
        questionOverlay.sprite = RoyalUiThemeKit.GetIcon(RoyalUiThemeKit.IconKind.DieQuestion);
        questionOverlay.preserveAspect = true;
        questionOverlay.raycastTarget = false;

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
            new Vector2(58f, -12f),
            new Vector2(70f, 24f),
            13.5f,
            FontStyles.Bold,
            GoldBright,
            TextAlignmentOptions.MidlineLeft
        );

        subLabel = CreateLabel(
            rt,
            "SlotSub",
            new Vector2(58f, -38f),
            new Vector2(70f, 24f),
            12.5f,
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
        out Image swordIcon,
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

        RoyalUiThemeKit.ApplyArcadeButton(btnObj, DisabledSlotBg, BronzeMuted, 1.4f);
        bgImage = btnObj.GetComponent<Image>();
        outline = btnObj.GetComponent<Outline>();

        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = bgImage;
        btn.onClick.AddListener(onClick);
        if (btnObj.GetComponent<RoyalButtonHoverEffect>() == null)
            btnObj.AddComponent<RoyalButtonHoverEffect>();

        // Left Flame Sword Crest (38x38)
        GameObject iconObj = new GameObject("FlameSwordCrest", typeof(RectTransform), typeof(Image));
        iconObj.layer = turnText.gameObject.layer;
        iconObj.transform.SetParent(rt, false);
        RectTransform iRt = iconObj.GetComponent<RectTransform>();
        iRt.anchorMin = iRt.anchorMax = new Vector2(0f, 0.5f);
        iRt.pivot = new Vector2(0f, 0.5f);
        iRt.anchoredPosition = new Vector2(6f, 1f);
        iRt.sizeDelta = new Vector2(38f, 38f);
        swordIcon = iconObj.GetComponent<Image>();
        swordIcon.sprite = RoyalUiThemeKit.GetIcon(RoyalUiThemeKit.IconKind.FlameSword);
        swordIcon.preserveAspect = true;
        swordIcon.raycastTarget = false;

        titleLabel = CreateLabel(
            rt,
            "BonusTitle",
            new Vector2(46f, -12f),
            new Vector2(82f, 24f),
            12.5f,
            FontStyles.Bold,
            GoldBright,
            TextAlignmentOptions.MidlineLeft
        );
        titleLabel.enableAutoSizing = true;
        titleLabel.fontSizeMin = 10f;
        titleLabel.fontSizeMax = 12.5f;

        subLabel = CreateLabel(
            rt,
            "BonusSub",
            new Vector2(46f, -38f),
            new Vector2(82f, 24f),
            12f,
            FontStyles.Bold,
            WarmMutedText,
            TextAlignmentOptions.MidlineLeft
        );
        subLabel.enableAutoSizing = true;
        subLabel.fontSizeMin = 9.5f;
        subLabel.fontSizeMax = 12f;

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
            rt.anchoredPosition = new Vector2(-32f, 29f);
            rt.sizeDelta = new Vector2(412f, 52f);
            rt.SetAsLastSibling();
        }

        RoyalUiThemeKit.ApplyArcadeButton(netRollObj, RoyalUiThemeKit.ArcadeGoldCta, RoyalUiThemeKit.BrassBright, 1.8f);
        if (netRollObj.GetComponent<RoyalButtonHoverEffect>() == null)
            netRollObj.AddComponent<RoyalButtonHoverEffect>();

        TMP_Text btnText = netRollObj.GetComponentInChildren<TMP_Text>(true);
        if (btnText != null)
        {
            btnText.text = "GIEO 2 XÚC XẮC";
            btnText.fontSize = 20f;
            btnText.fontStyle = FontStyles.Bold;
            btnText.color = RoyalUiThemeKit.DarkGoldInk;
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
            rt.sizeDelta = new Vector2(186f, 64f);
        }

        RoyalUiThemeKit.ApplyLacquerPanel(roomPanel, WoodDarkBg, GoldBorder, 1.8f, false);

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
                outRt.sizeDelta = new Vector2(162f, 42f);
            }

            RoyalUiThemeKit.ApplyArcadeButton(outBtnTr.gameObject, RoyalUiThemeKit.ArcadeCrimson, GoldBorder, 1.5f);
            if (outBtnTr.gameObject.GetComponent<RoyalButtonHoverEffect>() == null)
                outBtnTr.gameObject.AddComponent<RoyalButtonHoverEffect>();

            TMP_Text outLbl = outBtnTr.GetComponentInChildren<TMP_Text>(true);
            if (outLbl != null)
            {
                outLbl.text = "RỜI PHÒNG";
                outLbl.fontSize = 16f;
                outLbl.fontStyle = FontStyles.Bold;
                outLbl.color = IvoryText;
                outLbl.alignment = TextAlignmentOptions.Center;
            }

            Button outBtn = outBtnTr.GetComponent<Button>();
            if (outBtn != null)
            {
                outBtn.onClick.RemoveAllListeners();
                outBtn.onClick.AddListener(() =>
                {
                    Transform rootTr = transform.root != null ? transform.root : transform;
                    RoyalUiThemeKit.ShowExitConfirmationPopup(rootTr, () =>
                    {
                        if (NetworkRoomControlManager.Instance != null)
                            NetworkRoomControlManager.Instance.ConfirmOutRoom();
                    });
                });
            }
        }

        if (roomPanel.transform.parent != null)
            EnsureDemoFastFinishButton(roomPanel.transform.parent);
    }

    void EnsureDemoFastFinishButton(Transform canvasRoot)
    {
        if (canvasRoot == null || canvasRoot.Find("DemoFastFinishButton") != null)
            return;

        GameObject btnObj = new GameObject("DemoFastFinishButton", typeof(RectTransform), typeof(Image), typeof(Button));
        btnObj.layer = canvasRoot.gameObject.layer;
        btnObj.transform.SetParent(canvasRoot, false);

        RectTransform rt = btnObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-66f, -84f);
        rt.sizeDelta = new Vector2(136f, 42f);

        RoyalUiThemeKit.ApplyArcadeButton(btnObj, RoyalUiThemeKit.ArcadeGoldCta, RoyalUiThemeKit.BrassBright, 1.6f);
        if (btnObj.GetComponent<RoyalButtonHoverEffect>() == null)
            btnObj.AddComponent<RoyalButtonHoverEffect>();

        GameObject txtObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        txtObj.layer = canvasRoot.gameObject.layer;
        txtObj.transform.SetParent(btnObj.transform, false);

        RectTransform txtRt = txtObj.GetComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = Vector2.zero;
        txtRt.offsetMax = Vector2.zero;

        TextMeshProUGUI lbl = txtObj.GetComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null)
            lbl.font = TMP_Settings.defaultFontAsset;
        lbl.text = "DEMO END [F10]";
        lbl.fontSize = 12.5f;
        lbl.fontStyle = FontStyles.Bold;
        lbl.color = RoyalUiThemeKit.DarkGoldInk;
        lbl.alignment = TextAlignmentOptions.Center;
        lbl.raycastTarget = false;

        Button btn = btnObj.GetComponent<Button>();
        btn.onClick.AddListener(TriggerFastMatchFinish);
    }

    public void TriggerFastMatchFinish()
    {
        if (NetworkGameResultManager.Instance == null)
        {
            Debug.LogWarning("[GameplayUI] NetworkGameResultManager.Instance is null, cannot fast finish.");
            return;
        }

        bool isNetworkActive = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        if (!isNetworkActive || NetworkManager.Singleton.IsServer)
        {
            NetworkGameResultManager.Instance.TriggerFastDemoFinishServer();
        }
        else
        {
            NetworkGameResultManager.Instance.RequestFastMatchFinishRpc();
        }

        SetMessage("Mẹo: Đang kích hoạt kết thúc nhanh (Demo)...");
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
        return RoyalUiThemeKit.GetFactionColor(playerIndex);
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
