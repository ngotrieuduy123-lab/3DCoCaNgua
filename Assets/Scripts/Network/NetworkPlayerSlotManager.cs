using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

public class NetworkPlayerSlotManager : NetworkBehaviour
{
    public static NetworkPlayerSlotManager Instance;

    private Dictionary<ulong, int> clientToPlayerIndex = new Dictionary<ulong, int>();
    private Dictionary<int, ulong> playerIndexToClient = new Dictionary<int, ulong>();

    public bool[] connectedPlayers = new bool[4];

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            AssignSlot(client.ClientId);
        }
    }

    public override void OnNetworkDespawn()
    {
        if (NetworkManager.Singleton == null) return;

        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    void OnClientConnected(ulong clientId)
    {
        if (!IsServer) return;
        AssignSlot(clientId);
    }

    void OnClientDisconnected(ulong clientId)
    {
        if (!IsServer) return;

        int playerIndex = GetPlayerIndex(clientId);

        if (playerIndex == -1)
        {
            Debug.Log("disconnect but no slot found: " + clientId);
            return;
        }

        connectedPlayers[playerIndex] = false;
        clientToPlayerIndex.Remove(clientId);
        playerIndexToClient.Remove(playerIndex);

        ReturnDisconnectedPlayerPieces(playerIndex);

        Debug.Log("Connected status: Blue=" + connectedPlayers[0]
    + " Red=" + connectedPlayers[1]
    + " Green=" + connectedPlayers[2]
    + " Yellow=" + connectedPlayers[3]);

        Debug.Log("Player disconnected: " + playerIndex);

        if (NetworkDiceManager.Instance != null)
        {
            NetworkDiceManager.Instance.ResetNetworkDice();
        }

        if (NetworkTurnManager.Instance != null &&
            NetworkTurnManager.Instance.currentPlayerIndex.Value == playerIndex)
        {
            NetworkTurnManager.Instance.NextTurn();
        }

        if (GetConnectedCount() <= 1)
        {
            Debug.Log("Only one player left. End game.");

            int winnerPlayerIndex = -1;

            for (int i = 0; i < connectedPlayers.Length; i++)
                if (connectedPlayers[i])
                {
                    winnerPlayerIndex = i;
                    break;
                }

            if (NetworkGameResultManager.Instance != null && winnerPlayerIndex >= 0)
            {
                NetworkGameResultManager.Instance.EndGameFromDisconnect(winnerPlayerIndex);
                return;
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetState(GameState.GameOver);
            }

            if (NetworkTurnManager.Instance != null &&
                NetworkTurnManager.Instance.gameplayUI != null)
            {
                NetworkTurnManager.Instance.gameplayUI.SetGameOver(
                "Opponent disconnected. You win!"
                );
            }
        }
    }

    void AssignSlot(ulong clientId)
    {
        if (clientToPlayerIndex.ContainsKey(clientId))
            return;

        for (int i = 0; i < 4; i++)
        {
            if (!playerIndexToClient.ContainsKey(i) || !connectedPlayers[i])
            {
                clientToPlayerIndex[clientId] = i;
                playerIndexToClient[i] = clientId;
                connectedPlayers[i] = true;

                Debug.Log("Assign client " + clientId + " to player " + i);
                return;
            }
        }

        Debug.Log("Room full");
    }

    public int GetPlayerIndex(ulong clientId)
    {
        if (clientToPlayerIndex.ContainsKey(clientId))
            return clientToPlayerIndex[clientId];

        return -1;
    }

    public ulong GetClientId(int playerIndex)
    {
        if (playerIndexToClient.ContainsKey(playerIndex))
            return playerIndexToClient[playerIndex];

        return ulong.MaxValue;
    }

    public bool IsPlayerConnected(int playerIndex)
    {
        if (playerIndex < 0 || playerIndex >= connectedPlayers.Length)
            return false;

        return connectedPlayers[playerIndex];
    }

    public int GetConnectedCount()
    {
        int count = 0;

        for (int i = 0; i < connectedPlayers.Length; i++)
        {
            if (connectedPlayers[i])
                count++;
        }

        return count;
    }

    void ReturnDisconnectedPlayerPieces(int playerIndex)
    {
        BoardManager boardManager = FindFirstObjectByType<BoardManager>();

        if (boardManager == null)
        {
            Debug.Log("BoardManager not found");
            return;
        }

        foreach (PieceController piece in boardManager.allPieces)
        {
            if (piece.playerIndex == playerIndex)
            {
                piece.ReturnToStable();
            }
        }

        Debug.Log("Returned all pieces of player " + playerIndex + " to stable");
    }
}
