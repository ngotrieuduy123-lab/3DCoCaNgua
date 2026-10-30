using System;
using System.Collections.Generic;
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
        // In Party Clash v0.2, matches finish when the 10-minute clock + Final Round completes
    }

    public void CompletePartyClashMatchServer(string reason = "Match finished")
    {
        if (!IsServer || isGameOver)
            return;

        int playerCount = NetworkTurnManager.Instance != null
            ? Mathf.Clamp(NetworkTurnManager.Instance.playerCount.Value, 2, 4)
            : Mathf.Clamp(PlayerPrefs.GetInt("PlayerCount", 2), 2, 4);

        finishOrder.Clear();

        if (boardManager != null)
        {
            finishOrder.AddRange(boardManager.GetSortedRankingOrder(playerCount));
        }
        else
        {
            for (int i = 0; i < playerCount; i++)
                finishOrder.Add(i);
        }

        bool anyScore = false;
        if (boardManager != null)
        {
            for (int i = 0; i < playerCount; i++)
            {
                if (boardManager.playerScores[i] > 0)
                {
                    anyScore = true;
                    break;
                }
            }
        }

        if (!anyScore)
        {
            CompleteGame("Draw (0 points)", true);
        }
        else
        {
            CompleteGame(reason, false);
        }
    }

    public void EndGameFromDisconnect(int winnerPlayerIndex)
    {
        if (!IsServer || isGameOver || winnerPlayerIndex < 0)
            return;

        // Per Section 10 of Party Clash rules: if fewer than 2 players remain, cancel match without win reward
        int playerCount = NetworkTurnManager.Instance != null
            ? NetworkTurnManager.Instance.playerCount.Value
            : PlayerPrefs.GetInt("PlayerCount", 2);

        finishOrder.Clear();
        finishOrder.Add(winnerPlayerIndex);
        for (int i = 0; i < playerCount; i++)
        {
            if (!finishOrder.Contains(i))
                finishOrder.Add(i);
        }

        CompleteGame("Match canceled (not enough players)", true);
    }

    async void CompleteGame(string reason, bool isDrawOrCanceled)
    {
        if (isGameOver)
            return;

        isGameOver = true;
        int playerCount = NetworkTurnManager.Instance != null
            ? NetworkTurnManager.Instance.playerCount.Value
            : PlayerPrefs.GetInt("PlayerCount", 2);

        for (int slot = 0; slot < playerCount; slot++)
        {
            if (!finishOrder.Contains(slot))
                finishOrder.Add(slot);
        }

        int winnerPlayerIndex = (!isDrawOrCanceled && finishOrder.Count > 0) ? finishOrder[0] : -1;
        if (GameManager.Instance != null)
            GameManager.Instance.SetState(GameState.GameOver);

        string rankingSummary = GetRankingText(isDrawOrCanceled, reason);
        ShowPendingResultClientRpc(rankingSummary);

        string matchId = DatabaseManager.Instance != null ? DatabaseManager.Instance.CurrentMatchId : "";
        int[] rankingToSend = isDrawOrCanceled ? Array.Empty<int>() : finishOrder.ToArray();

        bool saved = DatabaseManager.Instance != null &&
            await DatabaseManager.Instance.FinishMatch(matchId, rankingToSend, reason);

        if (this == null || !IsSpawned)
            return;

        ShowGameOverClientRpc(rankingSummary, winnerPlayerIndex, (!isDrawOrCanceled && saved) ? matchId : "");
    }

    string GetRankingText(bool isDrawOrCanceled, string reason)
    {
        string header = isDrawOrCanceled
            ? $"KẾT QUẢ: {reason}\n\n"
            : "BẢNG XẾP HẠNG PARTY CLASH\n\n";

        string text = header;

        for (int i = 0; i < finishOrder.Count; i++)
        {
            int slot = finishOrder[i];
            string rank = isDrawOrCanceled ? "-" : (i == 0 ? "1st" : (i == 1 ? "2nd" : (i == 2 ? "3rd" : "4th")));
            string pName = PlayerPrefs.GetString("TurnDisplayName_" + slot, ((PlayerColor)slot).ToString());
            int score = boardManager != null ? boardManager.playerScores[slot] : 0;
            int homes = boardManager != null ? boardManager.playerHomeCounts[slot] : 0;
            int kicks = boardManager != null ? boardManager.playerKickCounts[slot] : 0;

            text += $"{rank}: {pName} ({(PlayerColor)slot}) — {score}đ [Về nhà: {homes} | Đá: {kicks}]\n";
        }

        return text;
    }

    [Rpc(SendTo.ClientsAndHost)]
    void ShowPendingResultClientRpc(string ranking)
    {
        if (gameOverUI != null)
        {
            gameOverUI.ShowRanking(ranking);
            gameOverUI.AppendMessage("\nĐang lưu kết quả trận đấu...");
        }
        if (GameManager.Instance != null)
            GameManager.Instance.SetState(GameState.GameOver);
    }

    [Rpc(SendTo.ClientsAndHost)]
    void ShowGameOverClientRpc(string ranking, int winnerPlayerIndex, string rewardId)
    {
        if (gameOverUI != null)
            gameOverUI.ShowRanking(ranking);

        if (GameManager.Instance != null)
            GameManager.Instance.SetState(GameState.GameOver);

        if (winnerPlayerIndex < 0)
        {
            if (gameOverUI != null)
                gameOverUI.AppendMessage("\nTrận hòa hoặc bị hủy — không trao thưởng xu.");
            return;
        }

        if (!string.IsNullOrEmpty(rewardId))
            AwardLocalWinner(winnerPlayerIndex, rewardId);
        else if (gameOverUI != null)
            gameOverUI.AppendMessage("\nKhông thể xác nhận phần thưởng với máy chủ.");
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
            gameOverUI.AppendMessage("\nThưởng chiến thắng: +" + DatabaseManager.WinRewardCoins + " coins");
    }
}
