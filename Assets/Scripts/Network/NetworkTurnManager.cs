using Unity.Netcode;
using UnityEngine;

public class NetworkTurnManager : NetworkBehaviour
{
    public static NetworkTurnManager Instance;

    public NetworkVariable<int> currentPlayerIndex = new NetworkVariable<int>(0);

    public GameplayUI gameplayUI;
    public NetworkVariable<int> playerCount = new NetworkVariable<int>(2);

    public bool usedExtraTurn = false;

    void Awake()
    {
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        currentPlayerIndex.OnValueChanged += OnTurnChanged;

        if (IsServer)
        {
            int count = PlayerPrefs.GetInt("PlayerCount", 2);

            SetPlayerCount(count);

            Debug.Log("Loaded PlayerCount = " + count);
        }

        UpdateTurnUI(currentPlayerIndex.Value);
    }

    void OnTurnChanged(int oldValue, int newValue)
    {
        UpdateTurnUI(newValue);
    }

    void UpdateTurnUI(int playerIndex)
    {
        gameplayUI.SetTurn((PlayerColor)playerIndex);

        int localPlayerIndex = -1;

        if (NetworkManager.Singleton != null)
        {
            localPlayerIndex = NetworkPlayerSlotManager.Instance != null
                ? NetworkPlayerSlotManager.Instance.GetPlayerIndex(NetworkManager.Singleton.LocalClientId)
                : (int)NetworkManager.Singleton.LocalClientId;
        }

        bool playerActive =
            NetworkRoomControlManager.Instance == null ||
            NetworkRoomControlManager.Instance.IsPlayerActive(playerIndex);

        if (localPlayerIndex == playerIndex && playerActive)
        {
            gameplayUI.SetMessage("Your turn");
        }
        else
        {
            gameplayUI.SetMessage("Waiting for " + (PlayerColor)playerIndex);
        }

        Debug.Log("Network turn: " + (PlayerColor)playerIndex);
    }

    public void NextTurn()
    {
        usedExtraTurn = false;
        if (!IsServer) return;

        if (NetworkDiceManager.Instance != null)
        {
            NetworkDiceManager.Instance.ResetNetworkDice();
        }

        int count = Mathf.Max(1, playerCount.Value);
        int nextPlayer = currentPlayerIndex.Value;

        for (int i = 0; i < count; i++)
        {
            nextPlayer++;

            if (nextPlayer >= count)
                nextPlayer = 0;

            bool connected = NetworkPlayerSlotManager.Instance == null ||
                NetworkPlayerSlotManager.Instance.IsPlayerConnected(nextPlayer);
            bool active = NetworkRoomControlManager.Instance == null ||
                NetworkRoomControlManager.Instance.IsPlayerActive(nextPlayer);

            Debug.Log("Check next player " + nextPlayer + " connected=" + connected + " active=" + active);

            if (connected && active)
            {
                currentPlayerIndex.Value = nextPlayer;
                break;
            }
        }
        if (TurnTimerManager.Instance != null)
        {
            TurnTimerManager.Instance.ResetTimer();
        }

        GameManager.Instance.SetState(GameState.WaitingRoll);

        Debug.Log("Next turn after skip = " + currentPlayerIndex.Value);
    }
    public bool CanGetExtraTurn()
    {
        return !usedExtraTurn;
    }

    public void UseExtraTurn()
    {
        usedExtraTurn = true;
        Debug.Log("network extra turn");
    }

    public void ResetExtraTurn()
    {
        usedExtraTurn = false;
    }
    public void SetPlayerCount(int count)
    {
        if (!IsServer) return;

        playerCount.Value = count;

        Debug.Log("Network player count: " + count);
    }

    public override void OnNetworkDespawn()
    {
        currentPlayerIndex.OnValueChanged -= OnTurnChanged;
    }
}
