using System.Collections;
using UnityEngine;

public class DiceManager : MonoBehaviour
{
    public Dice dice1;
    public Dice dice2;

    public int totalValue;
    public bool hasRolled;
    public bool isRolling;

    public GameplayUI gameplayUI;
    public TurnManager turnManager;
    public PieceController[] pieces;

    public void RollBothDice()
    {
        if (hasRolled || isRolling) return;
        if (!GameManager.Instance.IsState(GameState.WaitingRoll)) return;

        GameManager.Instance.SetState(GameState.RollingDice);

        StartCoroutine(RollBothRoutine());
    }

    IEnumerator RollBothRoutine()
    {
        isRolling = true;
        hasRolled = true;

        dice1.Roll();
        dice2.Roll();

        yield return new WaitUntil(() => !dice1.isRolling && !dice2.isRolling);

        totalValue = dice1.value + dice2.value;

        Debug.Log("total dice: " + totalValue);
        gameplayUI.SetDice(dice1.value, dice2.value, totalValue);

        CheckAutoSkipTurn();

        if (hasRolled)
        {
            HighlightValidPieces();
            GameManager.Instance.SetState(GameState.WaitingChoosePiece);
        }

        isRolling = false;
    }
    void CheckAutoSkipTurn()
    {
        bool hasValidMove = false;

        foreach (PieceController piece in pieces)
        {
            if (piece.playerIndex != turnManager.currentPlayerIndex)
                continue;

            if (piece.isFinished)
                continue;

            if (piece.isInStable)
            {
                if (CanSpawnPiece() && piece.CanSpawn())
                {
                    hasValidMove = true;
                    break;
                }
            }
            else if (piece.isInHomePath)
            {
                if (CanClimbHome() && piece.CanClimbHome())
                {
                    hasValidMove = true;
                    break;
                }
            }
            else
            {
                if (piece.CanMove(totalValue))
                {
                    hasValidMove = true;
                    break;
                }
            }
        }

        if (!hasValidMove)
        {
            Debug.Log("no valid move, skip turn");

            gameplayUI.SetMessage("No valid move, skip turn");

            ResetDice();
            turnManager.NextTurn();

            GameManager.Instance.SetState(GameState.WaitingRoll);
        }
    }

    public void ResetDice()
    {
        hasRolled = false;
        totalValue = 0;
        isRolling = false;
        ClearHighlights();
    }

    public bool IsDouble()
    {
        return dice1.value == dice2.value;
    }

    public bool IsOneSix()
    {
        return (dice1.value == 1 && dice2.value == 6) ||
               (dice1.value == 6 && dice2.value == 1);
    }

    public bool CanSpawnPiece()
    {
        return IsDouble() || IsOneSix();
    }

    public bool CanClimbHome()
    {
        return IsDouble() || IsOneSix();
    }
    public void ClearHighlights()
    {
        foreach (PieceController piece in pieces)
        {
            piece.SetHighlight(false);
        }
    }

    public void HighlightValidPieces()
    {
        ClearHighlights();

        foreach (PieceController piece in pieces)
        {
            if (piece.playerIndex != turnManager.currentPlayerIndex)
                continue;

            if (piece.isFinished)
                continue;

            bool canUse = false;

            if (piece.isInStable)
                canUse = CanSpawnPiece() && piece.CanSpawn();
            else if (piece.isInHomePath)
                canUse = CanClimbHome() && piece.CanClimbHome();
            else
                canUse = piece.CanMove(totalValue);

            piece.SetHighlight(canUse);
        }
    }
}