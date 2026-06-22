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

        int firstValue = Random.Range(1, 7);
        int secondValue = Random.Range(1, 7);

        if (SoundManager.Instance != null)
            SoundManager.Instance.PlayDice();

        DiceRollPresentation presentation = DiceRollPresentation.EnsureCreated();
        yield return presentation.PlayRoll(firstValue, secondValue);

        dice1.RollVisualOnly(firstValue);
        dice2.RollVisualOnly(secondValue);

        yield return new WaitUntil(() => !dice1.isRolling && !dice2.isRolling);

        dice1.SetVisualValue(firstValue);
        dice2.SetVisualValue(secondValue);

        totalValue = firstValue + secondValue;

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
        return DiceRuleUtility.IsDouble(dice1.value, dice2.value);
    }

    public bool IsOneSix()
    {
        return DiceRuleUtility.IsOneSix(dice1.value, dice2.value);
    }

    public bool CanSpawnPiece()
    {
        return DiceRuleUtility.CanEnterBoardOrClimb(dice1.value, dice2.value);
    }

    public bool CanClimbHome()
    {
        return DiceRuleUtility.CanEnterBoardOrClimb(dice1.value, dice2.value);
    }
    public void ClearHighlights()
    {
        MovePathHighlighter.TryClear();

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

            if (canUse)
            {
                MovePathHighlighter.Instance.ShowMovePreview(
                    piece,
                    totalValue,
                    CanSpawnPiece(),
                    CanClimbHome()
                );
            }
        }
    }
}
