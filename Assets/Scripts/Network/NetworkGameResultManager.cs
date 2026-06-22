using System.Collections.Generic;
using System;
using Unity.Netcode;
using UnityEngine;

public class NetworkGameResultManager : NetworkBehaviour
{
    public static NetworkGameResultManager Instance;

    public BoardManager boardManager;
    public GameOverUI gameOverUI;

    public List<int> finishOrder = new List<int>();
    public bool isGameOver = false;

    void Awake()
    {
        Instance = this;
    }

    public void CheckPlayerFinished(int playerIndex)
    {
        if (!IsServer) return;
        if (isGameOver) return;

        int count = 0;

        foreach (PieceController piece in boardManager.allPieces)
        {
            if (piece.playerIndex == playerIndex && piece.isFinished)
                count++;
        }

        if (count >= 1)
        {
            AddFinishedPlayer(playerIndex);
        }
    }

    void AddFinishedPlayer(int playerIndex)
    {
        if (finishOrder.Contains(playerIndex)) return;

        finishOrder.Add(playerIndex);

        int activeCount = 0;
        int lastPlayer = -1;

        for (int i = 0; i < NetworkTurnManager.Instance.playerCount.Value; i++)
        {
            if (NetworkRoomControlManager.Instance != null &&
                !NetworkRoomControlManager.Instance.IsPlayerActive(i))
                continue;

            if (!finishOrder.Contains(i))
            {
                activeCount++;
                lastPlayer = i;
            }
        }

        if (activeCount <= 1)
        {
            if (lastPlayer != -1)
                finishOrder.Add(lastPlayer);

            CompleteGame("Game finished");
        }
    }

    public void EndGameFromDisconnect(int winnerPlayerIndex)
    {
        if (!IsServer || isGameOver || winnerPlayerIndex < 0)
            return;

        if (!finishOrder.Contains(winnerPlayerIndex))
            finishOrder.Insert(0, winnerPlayerIndex);

        int playerCount = NetworkTurnManager.Instance != null
            ? NetworkTurnManager.Instance.playerCount.Value
            : PlayerPrefs.GetInt("PlayerCount", 2);

        for (int i = 0; i < playerCount; i++)
            if (!finishOrder.Contains(i))
                finishOrder.Add(i);

        CompleteGame("Opponent disconnected");
    }

    void CompleteGame(string reason)
    {
        if (isGameOver)
            return;

        isGameOver = true;
        int winnerPlayerIndex = finishOrder.Count > 0 ? finishOrder[0] : -1;
        string rewardId = Guid.NewGuid().ToString("N");

        EndMatchHistory(reason);
        ShowGameOverClientRpc(GetRankingText(), winnerPlayerIndex, rewardId);
    }

    async void EndMatchHistory(string reason)
    {
        if (DatabaseManager.Instance == null)
            return;

        string matchHistoryId = PlayerPrefs.GetString("CurrentMatchHistoryId", "");

        if (string.IsNullOrWhiteSpace(matchHistoryId))
            return;

        await DatabaseManager.Instance.EndMatchHistory(matchHistoryId, reason);
        PlayerPrefs.DeleteKey("CurrentMatchHistoryId");
        PlayerPrefs.Save();
    }

    string GetRankingText()
    {
        string text = "";

        for (int i = 0; i < finishOrder.Count; i++)
        {
            string rank = i == finishOrder.Count - 1 ? "Lose" : (i + 1).ToString();

            text += rank + ": " + ((PlayerColor)finishOrder[i]).ToString() + "\n";
        }

        return text;
    }

    [Rpc(SendTo.ClientsAndHost)]
    void ShowGameOverClientRpc(string ranking, int winnerPlayerIndex, string rewardId)
    {
        gameOverUI.ShowRanking(ranking);
        GameManager.Instance.SetState(GameState.GameOver);
        AwardLocalWinner(winnerPlayerIndex, rewardId);
    }

    async void AwardLocalWinner(int winnerPlayerIndex, string rewardId)
    {
        if (NetworkPlayerIndexUtility.GetLocalPlayerIndex() != winnerPlayerIndex ||
            DatabaseManager.Instance == null)
            return;

        DatabaseManager.ShopResult result = await DatabaseManager.Instance.AwardWinCoins(
            rewardId,
            DatabaseManager.WinRewardCoins
        );

        if (result.Success && gameOverUI != null)
            gameOverUI.AppendMessage("\nVictory reward: +" + DatabaseManager.WinRewardCoins + " coins");
    }
}
