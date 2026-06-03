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

        int localPlayerIndex =
            NetworkPlayerSlotManager.Instance.GetPlayerIndex(
                NetworkManager.Singleton.LocalClientId
            );

        if (localPlayerIndex == playerIndex)
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

        int nextPlayer = currentPlayerIndex.Value;

        for (int i = 0; i < playerCount.Value; i++)
        {
            nextPlayer++;

            if (nextPlayer >= playerCount.Value)
                nextPlayer = 0;

            bool connected = true;

            if (NetworkPlayerSlotManager.Instance != null)
            {
                connected = NetworkPlayerSlotManager.Instance.IsPlayerConnected(nextPlayer);
            }

            Debug.Log("Check next player " + nextPlayer + " connected=" + connected);

            if (connected)
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