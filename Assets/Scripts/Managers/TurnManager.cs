using UnityEngine;
using System.Collections.Generic;

public class TurnManager : MonoBehaviour
{
    public int playerCount = 2;
    public int currentPlayerIndex = 0;
    public bool usedExtraTurn = false;
    public bool[] playerFinished;
    public bool isGameOver = false;
    public GameplayUI gameplayUI;
    public GameOverUI gameOverUI;
    public List<int> finishOrder = new List<int>();
    void Start()
    {
        playerCount = PlayerPrefs.GetInt("PlayerCount", 2);

        playerFinished = new bool[playerCount];

        gameplayUI.SetTurn((PlayerColor)currentPlayerIndex);

        Debug.Log("Player " + currentPlayerIndex + " turn");
    }

    public void NextTurn()
    {
        if (isGameOver) return;

        do
        {
            currentPlayerIndex++;

            if (currentPlayerIndex >= playerCount)
                currentPlayerIndex = 0;

        } while (playerFinished[currentPlayerIndex]);

        usedExtraTurn = false;

        Debug.Log("Player " + currentPlayerIndex + " turn");
        gameplayUI.SetTurn((PlayerColor)currentPlayerIndex);
        gameplayUI.ClearDice();
        gameplayUI.SetMessage("");
    }

    public bool IsCurrentPlayer(int playerIndex)
    {
        return playerIndex == currentPlayerIndex;
    }

    public bool CanGetExtraTurn()
    {
        return !usedExtraTurn;
    }

    public void UseExtraTurn()
    {
        usedExtraTurn = true;
        Debug.Log("extra turn");

        gameplayUI.SetMessage("Extra turn");
        gameplayUI.ClearDice();
        gameplayUI.SetTurn((PlayerColor)currentPlayerIndex);
    }

    public void SetPlayerFinished(int playerIndex)
    {
        if (playerFinished[playerIndex]) return;

        playerFinished[playerIndex] = true;
        finishOrder.Add(playerIndex);

        int activeCount = 0;
        int lastPlayer = -1;

        for (int i = 0; i < playerCount; i++)
        {
            if (!playerFinished[i])
            {
                activeCount++;
                lastPlayer = i;
            }
        }

        if (activeCount <= 1)
        {
            isGameOver = true;

            if (lastPlayer != -1)
            {
                finishOrder.Add(lastPlayer);
            }

            Debug.Log("game over");
            gameplayUI.SetGameOver();
            gameOverUI.ShowRanking(GetRankingText());
        }
    }
    string GetPlayerName(PlayerColor color)
    {
        return color.ToString();
    }

    string GetRankingText()
    {
        string text = "";

        for (int i = 0; i < finishOrder.Count; i++)
        {
            string rank = "";

            if (i == 0) rank = "1st";
            else if (i == 1) rank = "2nd";
            else if (i == 2) rank = "3rd";
            else rank = "Lose";

            text += rank + ": " + GetPlayerName((PlayerColor)finishOrder[i]) + "\n";
        }

        return text;
    }
}