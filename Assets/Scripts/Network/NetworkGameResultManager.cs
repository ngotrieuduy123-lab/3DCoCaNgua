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

        if (count >= 4)
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

    async void CompleteGame(string reason)
    {
        if (isGameOver)
            return;

        isGameOver = true;
        int playerCount = NetworkTurnManager.Instance != null ? NetworkTurnManager.Instance.playerCount.Value : PlayerPrefs.GetInt("PlayerCount", 2);
        for (int slot = 0; slot < playerCount; slot++)
            if (!finishOrder.Contains(slot)) finishOrder.Add(slot);
        int winnerPlayerIndex = finishOrder.Count > 0 ? finishOrder[0] : -1;
        GameManager.Instance.SetState(GameState.GameOver);
        ShowPendingResultClientRpc(GetRankingText());
        string matchId = DatabaseManager.Instance != null ? DatabaseManager.Instance.CurrentMatchId : "";
        bool saved = DatabaseManager.Instance != null &&
            await DatabaseManager.Instance.FinishMatch(matchId, finishOrder.ToArray(), reason);
        if (this == null || !IsSpawned) return;
        ShowGameOverClientRpc(GetRankingText(), winnerPlayerIndex, saved ? matchId : "");
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
    void ShowPendingResultClientRpc(string ranking)
    {
        gameOverUI.ShowRanking(ranking);
        gameOverUI.AppendMessage("\nSaving result...");
        GameManager.Instance.SetState(GameState.GameOver);
    }

    [Rpc(SendTo.ClientsAndHost)]
    void ShowGameOverClientRpc(string ranking, int winnerPlayerIndex, string rewardId)
    {
        gameOverUI.ShowRanking(ranking);
        GameManager.Instance.SetState(GameState.GameOver);
        if (!string.IsNullOrEmpty(rewardId)) AwardLocalWinner(winnerPlayerIndex, rewardId);
        else gameOverUI.AppendMessage("\nCould not confirm the result with backend. No reward confirmation is available.");
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
