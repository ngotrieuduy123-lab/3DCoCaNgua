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
        if (localReturnedToLobby)
            return;

        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas != null)
        {
            RoyalUiThemeKit.ShowExitConfirmationPopup(canvas.transform, ConfirmOutRoom);
            return;
        }

        ConfirmOutRoom();
    }

    public void ConfirmOutRoom()
    {
        if (localReturnedToLobby)
            return;

        if (loadingOverlay != null)
            loadingOverlay.Show("Đang rời phòng...");

        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || !IsSpawned)
        {
            StartCoroutine(ReturnLocalClientToLobbyRoutine());
            return;
        }

        if (IsServer)
        {
            HandlePlayerOutServer(NetworkManager.ServerClientId, true);
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
        if (boardManager != null && boardManager.allPieces != null)
        {
            foreach (PieceController piece in boardManager.allPieces)
            {
                if (piece != null && piece.playerIndex == playerIndex)
                    piece.ReturnToStable(false);
            }
        }
        SetPlayerPiecesActiveRpc(playerIndex, false);

        int activeCount = activePlayers.Count;

        // CASE 1: Host is the leaving player
        if (playerIndex == 0)
        {
            if (activeCount == 1)
            {
                // In a 2-player match: Host surrenders -> the other remaining player wins!
                int remainingWinner = activePlayers[0];
                if (NetworkGameResultManager.Instance != null)
                {
                    NetworkGameResultManager.Instance.EndGameFromForfeit(remainingWinner, 0, "Chủ phòng đã rời trận (Bỏ cuộc)");
                    StartCoroutine(HostDelayLeaveRoutine(1.2f));
                    return;
                }
            }

            StopRoomServer("Chủ phòng đã rời trận đấu. Đang trở về sảnh.", "Host left room");
            return;
        }

        // CASE 2: Client is leaving and only 1 player remains (2-player match or last survivor)
        if (activeCount <= 1)
        {
            int winnerIndex = activeCount == 1 ? activePlayers[0] : 0;

            if (requestedByPlayer)
                ReturnLeavingPlayerToLobbyRpc(clientId);

            if (NetworkGameResultManager.Instance != null)
            {
                NetworkGameResultManager.Instance.EndGameFromForfeit(winnerIndex, playerIndex, "Đối thủ đã rời trận (Bỏ cuộc)");
                return;
            }

            StopRoomServer("Không còn đủ người chơi. Đang trở về sảnh.", "Not enough players");
            return;
        }

        // CASE 3: 3+ players -> Match continues normally!
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

    IEnumerator HostDelayLeaveRoutine(float delaySeconds)
    {
        yield return new WaitForSecondsRealtime(delaySeconds);
        StartCoroutine(ReturnWholeRoomToLobbyRoutine());
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
        string pName = PlayerPrefs.GetString("TurnDisplayName_" + playerIndex, ((PlayerColor)playerIndex).ToString());
        string msg = $"{pName} ({(PlayerColor)playerIndex}) đã rời trận! Trận đấu tiếp tục với {activeCount} kỳ thủ.";
        SetStatus(msg);
        UpdateLocalControls();
        if (GameplayUI.Instance != null)
        {
            GameplayUI.Instance.SetMessage("Thông báo: " + msg);
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    void SetPlayerPiecesActiveRpc(int playerIndex, bool active)
    {
        if (boardManager == null || boardManager.allPieces == null)
            return;

        foreach (PieceController piece in boardManager.allPieces)
        {
            if (piece != null && piece.playerIndex == playerIndex)
            {
                if (!active)
                    piece.ReturnToStable(false);
                piece.gameObject.SetActive(active);
            }
        }
    }

    IEnumerator ReturnLocalClientToLobbyRoutine()
    {
        if (DatabaseManager.Instance != null) _ = DatabaseManager.Instance.LeaveBackendRoom();
        localReturnedToLobby = true;
        yield return new WaitForSecondsRealtime(0.35f);

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            NetworkManager.Singleton.Shutdown();

        SceneManager.LoadScene("LobbyScene");
    }

    IEnumerator ReturnWholeRoomToLobbyRoutine()
    {
        if (DatabaseManager.Instance != null) _ = DatabaseManager.Instance.LeaveBackendRoom();
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
