using TMPro;
using UnityEngine;

public class GameplayUI : MonoBehaviour
{
    public TMP_Text turnText;
    public TMP_Text diceText;
    public TMP_Text messageText;
    public TMP_Text winnerText;

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

    public void SetGameOver()
    {
        winnerText.text = "Game Over";
    }

    public void ClearDice()
    {
        diceText.text = "Dice: -";
    }
}