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
    bool roomStopStarted;

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
            StopRoomServer("Host left. Returning to lobby.", "Host left room");
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
        RequestOutRoom();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    void RequestStopRoomRpc(RpcParams rpcParams = default)
    {
        if (GetPlayerIndex(rpcParams.Receive.SenderClientId) != 0)
            return;

        StopRoomServer("Host left. Returning to lobby.", "Host left room");
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

        if (playerIndex == 0)
        {
            StopRoomServer("Host left. Returning to lobby.", "Host left room");
            return;
        }

        int activeCount = activePlayers.Count;

        if (activeCount < 2)
        {
            StopRoomServer("Not enough players. Returning to lobby.", "Not enough players");
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

    async void StopRoomServer(string message, string endReason)
    {
        if (roomStopStarted)
            return;

        roomStopStarted = true;

        if (DatabaseManager.Instance != null)
        {
            string matchHistoryId = PlayerPrefs.GetString("CurrentMatchHistoryId", "");
            await DatabaseManager.Instance.EndMatchHistory(matchHistoryId, endReason);
            PlayerPrefs.DeleteKey("CurrentMatchHistoryId");
            PlayerPrefs.Save();
        }

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

            SetStatus("Host left. Room is closing.");
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
            stopRoomButton.gameObject.SetActive(false);
            stopRoomButton.interactable = false;
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
        return NetworkPlayerIndexUtility.GetLocalPlayerIndex();
    }

    int GetPlayerIndex(ulong clientId)
    {
        return NetworkPlayerIndexUtility.GetPlayerIndex(clientId);
    }
}
