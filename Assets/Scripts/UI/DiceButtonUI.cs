using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

public class DiceButtonUI : MonoBehaviour
{
    public Button rollButton;
    public CanvasGroup canvasGroup;

    public NetworkDiceManager networkDiceManager;

    void Update()
    {
        if (NetworkManager.Singleton == null ||
            !NetworkManager.Singleton.IsListening ||
            NetworkTurnManager.Instance == null ||
            networkDiceManager == null)
        {
            rollButton.interactable = false;
            SetAlpha(0.4f);
            return;
        }

        int currentPlayer = NetworkTurnManager.Instance.currentPlayerIndex.Value;
        int localPlayer = NetworkPlayerSlotManager.Instance != null
            ? NetworkPlayerSlotManager.Instance.GetPlayerIndex(NetworkManager.Singleton.LocalClientId)
            : (int)NetworkManager.Singleton.LocalClientId;

        bool isMyTurn = localPlayer == currentPlayer;
        bool isActive =
            NetworkRoomControlManager.Instance == null ||
            NetworkRoomControlManager.Instance.IsPlayerActive(localPlayer);
        bool hasNotRolled = networkDiceManager.totalValue.Value <= 0;
        bool gameWaitingRoll = GameManager.Instance.IsState(GameState.WaitingRoll);

        bool canRoll = isActive && isMyTurn && hasNotRolled && gameWaitingRoll;

        rollButton.interactable = canRoll;
        SetAlpha(canRoll ? 1f : 0.4f);
    }

    public void RollDice()
    {
        if (networkDiceManager == null) return;

        networkDiceManager.RequestRollDice();
    }

    void SetAlpha(float alpha)
    {
        if (canvasGroup != null)
            canvasGroup.alpha = alpha;
    }
}
