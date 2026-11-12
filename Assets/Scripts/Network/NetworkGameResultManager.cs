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
        bool isNetworkActive = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        if (isNetworkActive && !IsServer)
            return;
        if (isGameOver)
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

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestFastMatchFinishRpc()
    {
        TriggerFastDemoFinishServer();
    }

    public void TriggerFastDemoFinishServer()
    {
        bool isNetworkActive = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        if (isNetworkActive && !IsServer)
            return;
        if (isGameOver)
            return;

        int playerCount = NetworkTurnManager.Instance != null
            ? Mathf.Clamp(NetworkTurnManager.Instance.playerCount.Value, 2, 4)
            : Mathf.Clamp(PlayerPrefs.GetInt("PlayerCount", 2), 2, 4);

        if (boardManager != null)
        {
            bool anyScore = false;
            for (int i = 0; i < playerCount; i++)
            {
                if (boardManager.playerScores[i] > 0)
                {
                    anyScore = true;
                    break;
                }
            }

            if (!anyScore)
            {
                // Demo fallback: assign realistic demo scores so leaderboards display a real winner & rewards
                for (int i = 0; i < playerCount; i++)
                {
                    boardManager.playerScores[i] = (playerCount - i) * 3;
                    boardManager.playerHomeCounts[i] = Mathf.Max(0, 2 - i);
                    boardManager.playerKickCounts[i] = Mathf.Max(0, 1 - i);
                }
            }
        }

        CompletePartyClashMatchServer("Kết thúc nhanh (Bản Demo)");
    }

    public void EndGameFromForfeit(int winnerPlayerIndex, int loserPlayerIndex, string reason = "Đối thủ đã rời trận (Bỏ cuộc)")
    {
        bool isNetworkActive = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        if (isNetworkActive && !IsServer)
            return;
        if (isGameOver || winnerPlayerIndex < 0)
            return;

        int playerCount = NetworkTurnManager.Instance != null
            ? NetworkTurnManager.Instance.playerCount.Value
            : PlayerPrefs.GetInt("PlayerCount", 2);

        finishOrder.Clear();
        finishOrder.Add(winnerPlayerIndex);
        if (loserPlayerIndex >= 0 && !finishOrder.Contains(loserPlayerIndex))
            finishOrder.Add(loserPlayerIndex);

        for (int i = 0; i < playerCount; i++)
        {
            if (!finishOrder.Contains(i))
                finishOrder.Add(i);
        }

        CompleteGame(reason, false);
    }

    public void EndGameFromDisconnect(int winnerPlayerIndex)
    {
        bool isNetworkActive = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        if (isNetworkActive && !IsServer)
            return;
        if (isGameOver || winnerPlayerIndex < 0)
            return;

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

        CompleteGame("Đối thủ đã ngắt kết nối — Bạn chiến thắng!", false);
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
        bool isNetworkActive = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

        if (isNetworkActive && IsSpawned)
        {
            ShowPendingResultClientRpc(rankingSummary);
        }
        else
        {
            if (gameOverUI != null)
                gameOverUI.ShowRanking(rankingSummary);
        }

        string matchId = DatabaseManager.Instance != null ? DatabaseManager.Instance.CurrentMatchId : "";
        int[] rankingToSend = isDrawOrCanceled ? Array.Empty<int>() : finishOrder.ToArray();

        bool saved = DatabaseManager.Instance != null &&
            await DatabaseManager.Instance.FinishMatch(matchId, rankingToSend, reason);

        if (!isNetworkActive || this == null || !IsSpawned)
            return;

        ShowGameOverClientRpc(rankingSummary, winnerPlayerIndex, (!isDrawOrCanceled && saved) ? matchId : "");
    }

    string GetRankingText(bool isDrawOrCanceled, string reason)
    {
        string header = isDrawOrCanceled
            ? $"<align=center><color=#FF7070><b>{reason.ToUpperInvariant()}</b></color></align>\n\n"
            : "<align=center><color=#FFD566><b>--- BẢNG XẾP HẠNG CHUNG CUỘC ---</b></color></align>\n\n";

        string text = header;

        for (int i = 0; i < finishOrder.Count; i++)
        {
            int slot = finishOrder[i];
            string rankBadge;
            if (isDrawOrCanceled)
            {
                rankBadge = "<color=#8E9BB0>•</color>";
            }
            else if (i == 0)
            {
                rankBadge = "<color=#FFD566><b>[#1 QUÁN QUÂN]</b></color>";
            }
            else if (i == 1)
            {
                rankBadge = "<color=#D0D7DE><b>[#2 Á QUÂN]</b></color>";
            }
            else if (i == 2)
            {
                rankBadge = "<color=#CD7F32><b>[#3 HẠNG BA]</b></color>";
            }
            else
            {
                rankBadge = $"<color=#8E9BB0><b>[#{i + 1}]</b></color>";
            }

            string pName = PlayerPrefs.GetString("TurnDisplayName_" + slot, ((PlayerColor)slot).ToString());
            int score = boardManager != null ? boardManager.playerScores[slot] : 0;
            int homes = boardManager != null ? boardManager.playerHomeCounts[slot] : 0;
            int kicks = boardManager != null ? boardManager.playerKickCounts[slot] : 0;

            text += $"{rankBadge} <b>{pName}</b> ({(PlayerColor)slot}) — <color=#FFD566><b>{score}đ</b></color> <size=85%><color=#A8B2C1>[Về chuồng: {homes} | Đá: {kicks}]</color></size>\n";
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
