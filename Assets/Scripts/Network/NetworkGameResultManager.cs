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

            isGameOver = true;

            EndMatchHistory("Game finished");
            ShowGameOverClientRpc(GetRankingText());
        }
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
    void ShowGameOverClientRpc(string ranking)
    {
        gameOverUI.ShowRanking(ranking);
        GameManager.Instance.SetState(GameState.GameOver);
    }
}
