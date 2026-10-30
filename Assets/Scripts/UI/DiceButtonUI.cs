using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class DiceButtonUI : MonoBehaviour
{
    public Button rollButton;
    public CanvasGroup canvasGroup;

    public NetworkDiceManager networkDiceManager;

    private DiceManager localDiceManager;

    void Update()
    {
        if (rollButton == null)
            return;

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
            SetAlpha(canRollLocal ? 1f : 0.4f);
            return;
        }

        if (NetworkTurnManager.Instance == null || networkDiceManager == null)
        {
            rollButton.interactable = false;
            SetAlpha(0.4f);
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
        SetAlpha(canRoll ? 1f : 0.4f);
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
