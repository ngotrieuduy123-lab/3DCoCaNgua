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

        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.LocalClientId == (ulong)playerIndex)
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

        currentPlayerIndex.Value++;

        if (currentPlayerIndex.Value >= playerCount.Value)
            currentPlayerIndex.Value = 0;
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