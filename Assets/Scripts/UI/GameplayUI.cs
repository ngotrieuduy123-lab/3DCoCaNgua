using TMPro;
using UnityEngine;

public class GameplayUI : MonoBehaviour
{
    public TMP_Text turnText;
    public TMP_Text diceText;
    public TMP_Text messageText;
    public TMP_Text winnerText;
    public GameObject gameOverPanel;
    public TMP_Text gameOverText;
    public TMP_Text timerText;

    public void SetTurn(PlayerColor color)
    {
        turnText.text = "Player " + color + " turn";
    }

    public void SetDice(int dice1, int dice2, int total)
    {
        diceText.text = "Dice: " + dice1 + " + " + dice2 + " = " + total;
    }

    public void SetMessage(string message)
    {
        messageText.text = message;
    }

    public void SetWinner(int playerIndex)
    {
        winnerText.text = "Player " + playerIndex + " finished";
    }

    public void SetGameOver(string message = "Game Over")
    {
        if (gameOverPanel != null)
            gameOverPanel.SetActive(true);

        if (gameOverText != null)
            gameOverText.text = message;

        SetMessage(message);
    }

    public void ClearDice()
    {
        diceText.text = "Dice: -";
    }
    public void SetTimer(int value)
    {
        if (timerText != null)
        {
            timerText.text = "Time: " + value;
        }
    }
}