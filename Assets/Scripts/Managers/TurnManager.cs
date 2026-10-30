using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance;

    public int playerCount = 2;
    public int currentPlayerIndex = 0;
    public bool usedExtraTurn = false;
    public bool[] playerFinished;
    public bool isGameOver = false;
    public GameplayUI gameplayUI;
    public GameOverUI gameOverUI;
    public List<int> finishOrder = new List<int>();

    private int startingPlayerIndex = 0;
    private float offlineMatchRemaining = DiceRuleUtility.MatchDurationSeconds;
    private float offlineTurnRemaining = DiceRuleUtility.TurnDecisionSeconds;
    private float secAccumulator = 0f;
    private bool isFinalRound = false;
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

    void Start()
    {
        playerCount = Mathf.Clamp(PlayerPrefs.GetInt("PlayerCount", 2), 2, 4);
        playerFinished = new bool[playerCount];

        // Randomize starting player in offline mode
        bool isNet = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        if (!isNet)
        {
            startingPlayerIndex = Random.Range(0, playerCount);
            currentPlayerIndex = startingPlayerIndex;
        }

        BoardManager board = GetBoard();
        if (board != null)
            board.OnPlayerTurnStarted(currentPlayerIndex);

        if (gameplayUI != null)
        {
            gameplayUI.SetLocalPlayer(currentPlayerIndex);
            gameplayUI.SetTurn((PlayerColor)currentPlayerIndex);
            gameplayUI.SetTurnDisplayName(currentPlayerIndex, GetTurnDisplayName(currentPlayerIndex));
            gameplayUI.SetMatchTimer(Mathf.CeilToInt(offlineMatchRemaining), false);
            gameplayUI.SetTimer(Mathf.CeilToInt(offlineTurnRemaining));
            if (board != null)
                gameplayUI.RefreshPartyClashHud(board, Mathf.CeilToInt(offlineMatchRemaining), false);
        }

        Debug.Log("Player " + currentPlayerIndex + " turn");
    }

    void Update()
    {
        bool isNet = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        if (isNet || isGameOver)
            return;

        secAccumulator += Time.deltaTime;
        if (secAccumulator < 1f)
            return;

        secAccumulator -= 1f;

        if (offlineMatchRemaining > 0f)
        {
            offlineMatchRemaining = Mathf.Max(0f, offlineMatchRemaining - 1f);
            if (offlineMatchRemaining <= 0f && !isFinalRound)
            {
                isFinalRound = true;
                if (gameplayUI != null)
                    gameplayUI.SetMessage("Hết 10 phút! Hoàn tất vòng lượt cuối cùng!");
            }
        }

        bool pausedForAnimation = GameManager.Instance != null &&
            (GameManager.Instance.IsState(GameState.MovingPiece) ||
             GameManager.Instance.IsState(GameState.RollingDice));

        if (!pausedForAnimation)
        {
            offlineTurnRemaining = Mathf.Max(0f, offlineTurnRemaining - 1f);
            if (offlineTurnRemaining <= 0f)
            {
                DiceManager dm = FindFirstObjectByType<DiceManager>();
                if (dm != null) dm.ResetDice();
                NextTurn();
                if (GameManager.Instance != null)
                    GameManager.Instance.SetState(GameState.WaitingRoll);
                return;
            }
        }

        if (gameplayUI != null)
        {
            gameplayUI.SetMatchTimer(Mathf.CeilToInt(offlineMatchRemaining), isFinalRound);
            gameplayUI.SetTimer(Mathf.CeilToInt(offlineTurnRemaining));
        }
    }

    public void RefillDecisionTimer()
    {
        offlineTurnRemaining = DiceRuleUtility.TurnDecisionSeconds;
        if (gameplayUI != null)
            gameplayUI.SetTimer(Mathf.CeilToInt(offlineTurnRemaining));
    }

    public void NextTurn()
    {
        if (isGameOver) return;

        currentPlayerIndex = (currentPlayerIndex + 1) % playerCount;
        usedExtraTurn = false;
        offlineTurnRemaining = DiceRuleUtility.TurnDecisionSeconds;

        BoardManager board = GetBoard();

        // Check if we completed a full round back to startingPlayerIndex
        if (currentPlayerIndex == startingPlayerIndex)
        {
            if (isFinalRound)
            {
                EndPartyClashMatchOffline();
                return;
            }

            if (board != null)
                board.AdvanceRoundAndEvents(playerCount);
        }

        if (board != null)
            board.OnPlayerTurnStarted(currentPlayerIndex);

        Debug.Log("Player " + currentPlayerIndex + " turn");
        if (gameplayUI != null)
        {
            gameplayUI.SetLocalPlayer(currentPlayerIndex);
            gameplayUI.SetTurn((PlayerColor)currentPlayerIndex);
            gameplayUI.SetTurnDisplayName(currentPlayerIndex, GetTurnDisplayName(currentPlayerIndex));
            gameplayUI.ClearDice();
            gameplayUI.SetMessage("");
            gameplayUI.SetTimer(Mathf.CeilToInt(offlineTurnRemaining));
            if (board != null)
                gameplayUI.RefreshPartyClashHud(board, Mathf.CeilToInt(offlineMatchRemaining), isFinalRound);
        }
    }

    void EndPartyClashMatchOffline()
    {
        isGameOver = true;
        if (GameManager.Instance != null)
            GameManager.Instance.SetState(GameState.GameOver);

        BoardManager board = GetBoard();
        finishOrder.Clear();
        if (board != null)
            finishOrder.AddRange(board.GetSortedRankingOrder(playerCount));
        else
        {
            for (int i = 0; i < playerCount; i++)
                finishOrder.Add(i);
        }

        if (gameplayUI != null)
            gameplayUI.SetGameOver("Kết thúc trận Party Clash!");

        if (gameOverUI != null)
            gameOverUI.ShowRanking(GetRankingText());
    }

    public bool IsCurrentPlayer(int playerIndex)
    {
        return playerIndex == currentPlayerIndex;
    }

    public bool CanGetExtraTurn()
    {
        return false;
    }

    public void UseExtraTurn()
    {
        usedExtraTurn = true;
    }

    public void SetPlayerFinished(int playerIndex)
    {
        // In Party Clash v0.2, match ends by round clock rather than 4 fixed pieces
    }

    string GetPlayerName(PlayerColor color)
    {
        return color.ToString();
    }

    string GetTurnDisplayName(int playerIndex)
    {
        string displayName = PlayerPrefs.GetString("TurnDisplayName_" + playerIndex, "");

        if (string.IsNullOrWhiteSpace(displayName))
            displayName = ((PlayerColor)playerIndex).ToString();

        return displayName;
    }

    string GetRankingText()
    {
        BoardManager board = GetBoard();
        string text = "";

        for (int i = 0; i < finishOrder.Count; i++)
        {
            int p = finishOrder[i];
            string rank = i == 0 ? "1st" : (i == 1 ? "2nd" : (i == 2 ? "3rd" : "4th"));
            int score = board != null ? board.playerScores[p] : 0;
            int homes = board != null ? board.playerHomeCounts[p] : 0;
            int kicks = board != null ? board.playerKickCounts[p] : 0;

            text += $"{rank}: {GetTurnDisplayName(p)} ({GetPlayerName((PlayerColor)p)}) — {score}đ [Nhà:{homes} | Đá:{kicks}]\n";
        }

        return text;
    }
}
