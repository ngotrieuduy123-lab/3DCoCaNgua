using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class DiceButtonUI : MonoBehaviour
{
    public Button rollButton;
    public CanvasGroup canvasGroup;

    public NetworkDiceManager networkDiceManager;

    private DiceManager localDiceManager;
    private Image buttonImage;
    private Outline buttonOutline;
    private TMP_Text buttonLabel;
    private RoyalButtonHoverEffect hoverEffect;

    void Awake()
    {
        CacheVisuals();
    }

    void CacheVisuals()
    {
        if (rollButton == null)
            return;

        if (buttonImage == null)
            buttonImage = rollButton.GetComponent<Image>();

        if (buttonOutline == null)
            buttonOutline = rollButton.GetComponent<Outline>();

        if (buttonLabel == null)
            buttonLabel = rollButton.GetComponentInChildren<TMP_Text>(true);

        if (hoverEffect == null)
            hoverEffect = rollButton.GetComponent<RoyalButtonHoverEffect>();
    }

    void Update()
    {
        if (rollButton == null)
            return;

        CacheVisuals();

        bool isNetworkMode =
            NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsListening;

        if (!isNetworkMode)
        {
            if (localDiceManager == null)
                localDiceManager = FindFirstObjectByType<DiceManager>();

            bool gameWaitingRollLocal = GameManager.Instance != null && GameManager.Instance.IsState(GameState.WaitingRoll);
            bool hasNotRolledLocal = localDiceManager != null && !localDiceManager.hasRolled && !localDiceManager.isRolling;
            bool canRollLocal = gameWaitingRollLocal && hasNotRolledLocal;

            rollButton.interactable = canRollLocal;
            ApplyButtonStateVisuals(
                canRollLocal,
                localDiceManager != null && localDiceManager.isRolling
                    ? "ĐANG LẮC XÚC XẮC..."
                    : (canRollLocal ? "GIEO 2 XÚC XẮC" : "CHỌN VIÊN & CLICK QUÂN")
            );
            return;
        }

        if (NetworkTurnManager.Instance == null || networkDiceManager == null)
        {
            rollButton.interactable = false;
            ApplyButtonStateVisuals(false, "ĐANG KẾT NỐI...");
            return;
        }

        int currentPlayer = NetworkTurnManager.Instance.currentPlayerIndex.Value;
        int localPlayer = NetworkPlayerIndexUtility.GetLocalPlayerIndex();

        bool isMyTurn = localPlayer == currentPlayer;
        bool isActive =
            NetworkRoomControlManager.Instance == null ||
            NetworkRoomControlManager.Instance.IsPlayerActive(localPlayer);
        bool hasNotRolled = networkDiceManager.totalValue.Value <= 0;
        bool gameWaitingRoll = GameManager.Instance != null && GameManager.Instance.IsState(GameState.WaitingRoll);

        bool canRoll = isActive && isMyTurn && hasNotRolled && gameWaitingRoll;

        rollButton.interactable = canRoll;
        string labelText = canRoll
            ? "GIEO 2 XÚC XẮC"
            : (isMyTurn ? "CHỌN VIÊN & CLICK QUÂN" : "CHỜ LƯỢT ĐỐI THỦ...");
        ApplyButtonStateVisuals(canRoll, labelText);
    }

    void ApplyButtonStateVisuals(bool canRoll, string text)
    {
        SetAlpha(canRoll ? 1f : 0.74f);

        if (buttonImage != null)
        {
            buttonImage.color = canRoll
                ? RoyalUiThemeKit.ArcadeGoldCta
                : RoyalUiThemeKit.ArcadeDisabled;
        }

        if (buttonOutline != null)
        {
            if (canRoll)
            {
                float glow = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4.5f);
                buttonOutline.effectColor = Color.Lerp(RoyalUiThemeKit.BrassAntique, RoyalUiThemeKit.BrassBright, glow);
            }
            else
            {
                buttonOutline.effectColor = RoyalUiThemeKit.BronzeMuted;
            }
        }

        if (buttonLabel != null)
        {
            buttonLabel.text = text;
            buttonLabel.color = canRoll
                ? RoyalUiThemeKit.DarkGoldInk
                : RoyalUiThemeKit.WarmMutedText;
        }

        if (hoverEffect != null)
            hoverEffect.SetIdlePulse(canRoll);
    }

    public void RollDice()
    {
        bool isNetworkMode =
            NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsListening;

        if (!isNetworkMode)
        {
            if (localDiceManager == null)
                localDiceManager = FindFirstObjectByType<DiceManager>();

            if (localDiceManager != null)
                localDiceManager.RollBothDice();
            return;
        }

        if (networkDiceManager == null) return;

        networkDiceManager.RequestRollDice();
    }

    void SetAlpha(float alpha)
    {
        if (canvasGroup != null)
            canvasGroup.alpha = alpha;
    }
}
