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
    private TMP_Text buttonLabel;

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

        if (buttonLabel == null)
            buttonLabel = rollButton.GetComponentInChildren<TMP_Text>(true);
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
        SetAlpha(canRoll ? 1f : 0.72f);

        if (buttonImage != null)
        {
            buttonImage.color = canRoll
                ? new Color(0.88f, 0.64f, 0.16f, 1f)
                : new Color(0.22f, 0.17f, 0.12f, 0.92f);
        }

        if (buttonLabel != null)
        {
            buttonLabel.text = text;
            buttonLabel.color = canRoll
                ? new Color(0.14f, 0.09f, 0.03f, 1f)
                : new Color(0.84f, 0.78f, 0.68f, 0.90f);
        }
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
