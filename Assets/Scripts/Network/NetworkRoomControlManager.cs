using System.Collections;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class NetworkRoomControlManager : NetworkBehaviour
{
    public static NetworkRoomControlManager Instance;

    public Button outRoomButton;
    public Button stopRoomButton;
    public TMP_Text statusText;
    public LoadingOverlay loadingOverlay;
    public BoardManager boardManager;
    public NetworkDiceManager networkDiceManager;

    public NetworkVariable<int> roomOwnerPlayerIndex = new NetworkVariable<int>(0);
    public NetworkList<int> activePlayers;

    bool localReturnedToLobby;

    void Awake()
    {
        Instance = this;
        activePlayers = new NetworkList<int>();
    }

    public override void OnNetworkSpawn()
    {
        activePlayers.OnListChanged += OnActivePlayersChanged;
        roomOwnerPlayerIndex.OnValueChanged += OnRoomOwnerChanged;

        if (IsServer)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            InitializeActivePlayers();
        }

        UpdateLocalControls();
    }

    public override void OnNetworkDespawn()
    {
        activePlayers.OnListChanged -= OnActivePlayersChanged;
        roomOwnerPlayerIndex.OnValueChanged -= OnRoomOwnerChanged;

        if (IsServer && NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
    }

    void InitializeActivePlayers()
    {
        int count = Mathf.Clamp(PlayerPrefs.GetInt("PlayerCount", 2), 2, 4);

        activePlayers.Clear();
        for (int i = 0; i < count; i++)
            activePlayers.Add(i);

        roomOwnerPlayerIndex.Value = GetPlayerIndex(NetworkManager.ServerClientId);
    }

    void OnClientDisconnected(ulong clientId)
    {
        if (!IsServer)
            return;

        HandlePlayerOutServer(clientId, false);
    }

    void OnActivePlayersChanged(NetworkListEvent<int> change)
    {
        UpdateLocalControls();
    }

    void OnRoomOwnerChanged(int oldOwner, int newOwner)
    {
        UpdateLocalControls();
        SetStatus("Room owner: Player " + newOwner);
    }

    public bool IsPlayerActive(int playerIndex)
    {
        if (activePlayers == null)
            return true;

        for (int i = 0; i < activePlayers.Count; i++)
            if (activePlayers[i] == playerIndex)
                return true;

        return false;
    }

    public int ActivePlayerCount => activePlayers != null ? activePlayers.Count : 0;

    public void RequestOutRoom()
    {
        if (localReturnedToLobby || NetworkManager.Singleton == null)
            return;

        if (loadingOverlay != null)
            loadingOverlay.Show("Leaving room...");

        if (IsServer)
        {
            HandlePlayerOutServer(NetworkManager.Singleton.LocalClientId, true);
            return;
        }

        RequestOutRoomRpc();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    void RequestOutRoomRpc(RpcParams rpcParams = default)
    {
        HandlePlayerOutServer(rpcParams.Receive.SenderClientId, true);
    }

    public void RequestStopRoom()
    {
        if (NetworkManager.Singleton == null)
            return;

        int localPlayerIndex = GetLocalPlayerIndex();

        if (localPlayerIndex != roomOwnerPlayerIndex.Value)
        {
            SetStatus("Only the room owner can stop the room.");
            return;
        }

        if (loadingOverlay != null)
            loadingOverlay.Show("Stopping room...");

        if (IsServer)
        {
            StopRoomServer("Room stopped by owner.");
            return;
        }

        RequestStopRoomRpc();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    void RequestStopRoomRpc(RpcParams rpcParams = default)
    {
        if (GetPlayerIndex(rpcParams.Receive.SenderClientId) != roomOwnerPlayerIndex.Value)
            return;

        StopRoomServer("Room stopped by owner.");
    }

    void HandlePlayerOutServer(ulong clientId, bool requestedByPlayer)
    {
        int playerIndex = GetPlayerIndex(clientId);

        if (playerIndex < 0)
            return;

        if (!IsPlayerActive(playerIndex))
            return;

        RemoveActivePlayer(playerIndex);
        SetPlayerPiecesActiveRpc(playerIndex, false);

        if (roomOwnerPlayerIndex.Value == playerIndex)
            TransferOwnerServer();

        int activeCount = activePlayers.Count;

        if (activeCount < 2)
        {
            StopRoomServer("Not enough players. Returning to lobby.");
            return;
        }

        if (NetworkTurnManager.Instance != null &&
            NetworkTurnManager.Instance.currentPlayerIndex.Value == playerIndex)
        {
            if (networkDiceManager != null)
                networkDiceManager.ResetNetworkDice();

            NetworkTurnManager.Instance.NextTurn();
        }

        NotifyPlayerOutRpc(playerIndex, roomOwnerPlayerIndex.Value, activeCount);

        if (requestedByPlayer)
            ReturnLeavingPlayerToLobbyRpc(clientId);
    }

    void RemoveActivePlayer(int playerIndex)
    {
        for (int i = activePlayers.Count - 1; i >= 0; i--)
        {
            if (activePlayers[i] == playerIndex)
                activePlayers.RemoveAt(i);
        }
    }

    void TransferOwnerServer()
    {
        if (activePlayers.Count <= 0)
            return;

        roomOwnerPlayerIndex.Value = activePlayers[0];
    }

    void StopRoomServer(string message)
    {
        StopRoomRpc(message);
    }

    [Rpc(SendTo.ClientsAndHost)]
    void StopRoomRpc(string message)
    {
        if (loadingOverlay != null)
            loadingOverlay.Show(message);

        StartCoroutine(ReturnWholeRoomToLobbyRoutine());
    }

    [Rpc(SendTo.ClientsAndHost)]
    void ReturnLeavingPlayerToLobbyRpc(ulong leavingClientId)
    {
        if (NetworkManager.Singleton == null ||
            NetworkManager.Singleton.LocalClientId != leavingClientId)
            return;

        if (NetworkManager.Singleton.IsServer)
        {
            if (loadingOverlay != null)
                loadingOverlay.Hide();

            SetStatus("You left as a player. Keep this window open so the room can continue.");
            UpdateLocalControls();
            return;
        }

        StartCoroutine(ReturnLocalClientToLobbyRoutine());
    }

    [Rpc(SendTo.ClientsAndHost)]
    void NotifyPlayerOutRpc(int playerIndex, int ownerPlayerIndex, int activeCount)
    {
        SetStatus("Player " + playerIndex + " left. Active players: " + activeCount);
        UpdateLocalControls();
    }

    [Rpc(SendTo.ClientsAndHost)]
    void SetPlayerPiecesActiveRpc(int playerIndex, bool active)
    {
        if (boardManager == null)
            return;

        foreach (PieceController piece in boardManager.allPieces)
        {
            if (piece != null && piece.playerIndex == playerIndex)
                piece.gameObject.SetActive(active);
        }
    }

    IEnumerator ReturnLocalClientToLobbyRoutine()
    {
        localReturnedToLobby = true;
        yield return new WaitForSecondsRealtime(0.35f);

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            NetworkManager.Singleton.Shutdown();

        SceneManager.LoadScene("LobbyScene");
    }

    IEnumerator ReturnWholeRoomToLobbyRoutine()
    {
        localReturnedToLobby = true;
        yield return new WaitForSecondsRealtime(0.45f);

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            NetworkManager.Singleton.Shutdown();

        SceneManager.LoadScene("LobbyScene");
    }

    void UpdateLocalControls()
    {
        if (NetworkManager.Singleton == null)
            return;

        int localPlayerIndex = GetLocalPlayerIndex();
        bool localActive = IsPlayerActive(localPlayerIndex);
        bool isOwner = localPlayerIndex == roomOwnerPlayerIndex.Value;

        if (outRoomButton != null)
            outRoomButton.interactable = localActive && !localReturnedToLobby;

        if (stopRoomButton != null)
        {
            stopRoomButton.gameObject.SetActive(isOwner && localActive && !localReturnedToLobby);
            stopRoomButton.interactable = isOwner && localActive && !localReturnedToLobby;
        }
    }

    void SetStatus(string message)
    {
        Debug.Log(message);

        if (statusText != null)
            statusText.text = message;
    }

    int GetLocalPlayerIndex()
    {
        if (NetworkManager.Singleton == null)
            return -1;

        return GetPlayerIndex(NetworkManager.Singleton.LocalClientId);
    }

    int GetPlayerIndex(ulong clientId)
    {
        if (NetworkPlayerSlotManager.Instance != null)
            return NetworkPlayerSlotManager.Instance.GetPlayerIndex(clientId);

        return clientId <= int.MaxValue ? (int)clientId : -1;
    }
}
