using Unity.Netcode;
using UnityEngine;

public class NetworkTurnManager : NetworkBehaviour
{
    public static NetworkTurnManager Instance;

    public NetworkVariable<int> currentPlayerIndex = new NetworkVariable<int>(0);

    public GameplayUI gameplayUI;
    public NetworkVariable<int> playerCount = new NetworkVariable<int>(2);

    public bool usedExtraTurn = false;
    public int startingPlayerIndex = 0;
    public int turnsCompletedInCurrentRound = 0;

    private BoardManager cachedBoard;

    void Awake()
    {
        Instance = this;
    }

    BoardManager GetBoard()
    {
        if (cachedBoard == null)
            cachedBoard = FindFirstObjectByType<BoardManager>();
        return cachedBoard;
    }

    public override void OnNetworkSpawn()
    {
        currentPlayerIndex.OnValueChanged += OnTurnChanged;

        if (IsServer)
        {
            int count = Mathf.Clamp(PlayerPrefs.GetInt("PlayerCount", 2), 2, 4);
            SetPlayerCount(count);

            startingPlayerIndex = Random.Range(0, count);
            currentPlayerIndex.Value = startingPlayerIndex;
            turnsCompletedInCurrentRound = 0;

            BoardManager board = GetBoard();
            if (board != null)
            {
                board.InitializePartyClashMatch(count);
                board.OnPlayerTurnStarted(currentPlayerIndex.Value);
            }

            if (NetworkDiceManager.Instance != null)
                NetworkDiceManager.Instance.SyncPartyClashBoardStateServer();

            Debug.Log("Loaded PlayerCount = " + count + ", startingPlayer = " + startingPlayerIndex);
        }

        UpdateTurnUI(currentPlayerIndex.Value);
    }

    void OnTurnChanged(int oldValue, int newValue)
    {
        UpdateTurnUI(newValue);
    }

    void UpdateTurnUI(int playerIndex)
    {
        if (gameplayUI == null)
            return;

        gameplayUI.SetTurn((PlayerColor)playerIndex);
        gameplayUI.SetTurnDisplayName(playerIndex, GetTurnDisplayName(playerIndex));

        int localPlayerIndex = -1;

        if (NetworkManager.Singleton != null)
        {
            localPlayerIndex = NetworkPlayerIndexUtility.GetLocalPlayerIndex();
        }

        gameplayUI.SetLocalPlayer(localPlayerIndex);

        bool playerActive =
            NetworkRoomControlManager.Instance == null ||
            NetworkRoomControlManager.Instance.IsPlayerActive(playerIndex);

        if (localPlayerIndex == playerIndex && playerActive)
        {
            gameplayUI.SetMessage("Lượt của bạn! Bấm Gieo Xúc Xắc");
        }
        else
        {
            gameplayUI.SetMessage("Đang chờ " + GetTurnDisplayName(playerIndex));
        }

        Debug.Log("Network turn: " + (PlayerColor)playerIndex);

        if (NetworkDiceManager.Instance != null)
            NetworkDiceManager.Instance.RefreshHighlights();
    }

    string GetTurnDisplayName(int playerIndex)
    {
        string displayName = PlayerPrefs.GetString("TurnDisplayName_" + playerIndex, "");

        if (string.IsNullOrWhiteSpace(displayName))
            displayName = ((PlayerColor)playerIndex).ToString();

        return displayName;
    }

    public void NextTurn()
    {
        usedExtraTurn = false;
        if (!IsServer) return;

        if (GameManager.Instance != null && GameManager.Instance.IsState(GameState.GameOver))
            return;

        if (NetworkDiceManager.Instance != null)
        {
            NetworkDiceManager.Instance.ResetNetworkDice();
        }

        BoardManager board = GetBoard();
        int count = Mathf.Max(2, playerCount.Value);
        int activeParticipantCount = CountActiveParticipants(count);

        turnsCompletedInCurrentRound++;

        // When every active player in the room has taken 1 turn in this round, we reach the round boundary
        if (turnsCompletedInCurrentRound >= Mathf.Max(1, activeParticipantCount))
        {
            turnsCompletedInCurrentRound = 0;

            if (TurnTimerManager.Instance != null && TurnTimerManager.Instance.isFinalRound.Value)
            {
                if (NetworkGameResultManager.Instance != null)
                    NetworkGameResultManager.Instance.CompletePartyClashMatchServer("Match timer completed");
                return;
            }

            if (board != null)
                board.AdvanceRoundAndEvents(count);
        }

        int nextPlayer = currentPlayerIndex.Value;

        for (int i = 0; i < count; i++)
        {
            nextPlayer = (nextPlayer + 1) % count;

            bool connected = NetworkPlayerSlotManager.Instance == null ||
                NetworkPlayerSlotManager.Instance.IsPlayerConnected(nextPlayer);
            bool active = NetworkRoomControlManager.Instance == null ||
                NetworkRoomControlManager.Instance.IsPlayerActive(nextPlayer);

            if (connected && active)
            {
                currentPlayerIndex.Value = nextPlayer;
                break;
            }
        }

        if (board != null)
            board.OnPlayerTurnStarted(currentPlayerIndex.Value);

        if (NetworkDiceManager.Instance != null)
            NetworkDiceManager.Instance.SyncPartyClashBoardStateServer();

        if (TurnTimerManager.Instance != null)
        {
            TurnTimerManager.Instance.ResetTimer();
        }

        if (GameManager.Instance != null)
            GameManager.Instance.SetState(GameState.WaitingRoll);

        Debug.Log("Next turn = " + currentPlayerIndex.Value);
    }

    int CountActiveParticipants(int count)
    {
        int activeTotal = 0;
        for (int i = 0; i < count; i++)
        {
            bool connected = NetworkPlayerSlotManager.Instance == null ||
                NetworkPlayerSlotManager.Instance.IsPlayerConnected(i);
            bool active = NetworkRoomControlManager.Instance == null ||
                NetworkRoomControlManager.Instance.IsPlayerActive(i);
            if (connected && active)
                activeTotal++;
        }

        return Mathf.Max(1, activeTotal);
    }

    public bool CanGetExtraTurn()
    {
        return false;
    }

    public void UseExtraTurn()
    {
        usedExtraTurn = true;
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
