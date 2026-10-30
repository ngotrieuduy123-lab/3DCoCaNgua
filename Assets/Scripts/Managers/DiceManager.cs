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

    [Header("Party Clash v0.2 Dice Slots")]
    public bool dice1Used;
    public bool dice2Used;
    public bool bonus3Available;
    public bool bonus3Used;
    public bool earnedBonusThisTurn;
    public TurnActionSlot selectedActionSlot = TurnActionSlot.None;

    BoardManager cachedBoard;

    BoardManager GetBoard()
    {
        if (cachedBoard == null)
            cachedBoard = FindFirstObjectByType<BoardManager>();
        return cachedBoard;
    }

    public void RollBothDice()
    {
        if (hasRolled || isRolling) return;
        if (GameManager.Instance != null && !GameManager.Instance.IsState(GameState.WaitingRoll)) return;

        if (GameManager.Instance != null)
            GameManager.Instance.SetState(GameState.RollingDice);

        StartCoroutine(RollBothRoutine());
    }

    IEnumerator RollBothRoutine()
    {
        isRolling = true;
        hasRolled = true;
        dice1Used = false;
        dice2Used = false;
        bonus3Available = false;
        bonus3Used = false;
        earnedBonusThisTurn = false;
        selectedActionSlot = TurnActionSlot.Dice1;

        int firstValue = Random.Range(1, 7);
        int secondValue = Random.Range(1, 7);

        if (SoundManager.Instance != null)
            SoundManager.Instance.PlayDice();

        dice1.RollVisualOnly(firstValue);
        dice2.RollVisualOnly(secondValue);

        DiceRollPresentation presentation = DiceRollPresentation.EnsureCreated();
        yield return presentation.PlayRoll(firstValue, secondValue);

        yield return new WaitUntil(() => !dice1.isRolling && !dice2.isRolling);

        dice1.SetVisualValue(firstValue);
        dice2.SetVisualValue(secondValue);

        totalValue = firstValue + secondValue;

        Debug.Log("Party Clash dice: " + firstValue + " & " + secondValue);
        gameplayUI.SetDice(dice1.value, dice2.value, totalValue);

        isRolling = false;
        EvaluateRemainingActionsOrEndTurn();
    }

    public int GetStepForSlot(TurnActionSlot slot)
    {
        switch (slot)
        {
            case TurnActionSlot.Dice1:
                return !dice1Used && dice1 != null ? dice1.value : 0;
            case TurnActionSlot.Dice2:
                return !dice2Used && dice2 != null ? dice2.value : 0;
            case TurnActionSlot.Bonus3:
                return bonus3Available && !bonus3Used ? DiceRuleUtility.BonusMoveSteps : 0;
            default:
                return 0;
        }
    }

    public int GetSelectedStepValue()
    {
        return GetStepForSlot(selectedActionSlot);
    }

    public void SelectActionSlot(TurnActionSlot slot)
    {
        if (!hasRolled || isRolling)
            return;

        BoardManager board = GetBoard();
        if (board == null || turnManager == null)
            return;

        int currentPlayer = turnManager.currentPlayerIndex;
        int step = GetStepForSlot(slot);

        if (step <= 0 || !board.HasAnyValidPieceForSlot(currentPlayer, slot, step))
        {
            gameplayUI.SetMessage("Viên này không có nước đi hợp lệ");
            return;
        }

        selectedActionSlot = slot;
        UpdateActionSlotsUI(true);
        HighlightValidPieces();
    }

    public void ConsumeSlotAfterMove(TurnActionSlot slot, bool scoredKickOccurred)
    {
        if (slot == TurnActionSlot.Dice1)
            dice1Used = true;
        else if (slot == TurnActionSlot.Dice2)
            dice2Used = true;
        else if (slot == TurnActionSlot.Bonus3)
            bonus3Used = true;

        if (scoredKickOccurred && !earnedBonusThisTurn && slot != TurnActionSlot.Bonus3)
        {
            earnedBonusThisTurn = true;
            bonus3Available = true;
        }

        EvaluateRemainingActionsOrEndTurn();
    }

    public void EvaluateRemainingActionsOrEndTurn()
    {
        BoardManager board = GetBoard();
        if (board == null || turnManager == null)
            return;

        int currentPlayer = turnManager.currentPlayerIndex;

        bool canUseD1 = !dice1Used && dice1 != null && dice1.value > 0 &&
                        board.HasAnyValidPieceForSlot(currentPlayer, TurnActionSlot.Dice1, dice1.value);
        bool canUseD2 = !dice2Used && dice2 != null && dice2.value > 0 &&
                        board.HasAnyValidPieceForSlot(currentPlayer, TurnActionSlot.Dice2, dice2.value);

        if (!canUseD1 && !canUseD2)
        {
            dice1Used = true;
            dice2Used = true;
        }

        bool canUseBonus = dice1Used && dice2Used && bonus3Available && !bonus3Used &&
                           board.HasAnyValidPieceForSlot(currentPlayer, TurnActionSlot.Bonus3, DiceRuleUtility.BonusMoveSteps);

        if (dice1Used && dice2Used && bonus3Available && !canUseBonus)
        {
            bonus3Used = true;
        }

        if (canUseD1 || canUseD2 || canUseBonus)
        {
            if (selectedActionSlot == TurnActionSlot.Dice1 && !canUseD1)
                selectedActionSlot = canUseD2 ? TurnActionSlot.Dice2 : TurnActionSlot.Bonus3;
            else if (selectedActionSlot == TurnActionSlot.Dice2 && !canUseD2)
                selectedActionSlot = canUseD1 ? TurnActionSlot.Dice1 : TurnActionSlot.Bonus3;
            else if (selectedActionSlot == TurnActionSlot.Bonus3 && !canUseBonus)
                selectedActionSlot = canUseD1 ? TurnActionSlot.Dice1 : TurnActionSlot.Dice2;
            else if (selectedActionSlot == TurnActionSlot.None)
                selectedActionSlot = canUseD1 ? TurnActionSlot.Dice1 : (canUseD2 ? TurnActionSlot.Dice2 : TurnActionSlot.Bonus3);

            int remainTotal = (!dice1Used ? dice1.value : 0) + (!dice2Used ? dice2.value : 0) + (canUseBonus ? DiceRuleUtility.BonusMoveSteps : 0);
            totalValue = Mathf.Max(1, remainTotal);

            if (turnManager != null)
                turnManager.RefillDecisionTimer();

            UpdateActionSlotsUI(true);
            HighlightValidPieces();

            int activeStep = GetSelectedStepValue();
            if (selectedActionSlot != TurnActionSlot.Bonus3 && board.RequiresMandatorySpawn(currentPlayer, activeStep))
                gameplayUI.SetMessage("Bắt buộc xuất quân (cần tối thiểu 2 quân trên bàn)");
            else if (selectedActionSlot == TurnActionSlot.Bonus3)
                gameplayUI.SetMessage("Combo Đá! Chọn quân ngoài bàn để đi +3 ô");
            else
                gameplayUI.SetMessage($"Đang chọn {(selectedActionSlot == TurnActionSlot.Dice1 ? "Viên 1" : "Viên 2")} ({activeStep} bước) - Click quân để đi");

            gameplayUI.RefreshPartyClashHud(board);
            GameManager.Instance.SetState(GameState.WaitingChoosePiece);
            return;
        }

        // All actions for this turn are finished or skipped
        gameplayUI.RefreshPartyClashHud(board);
        ResetDice();
        turnManager.NextTurn();
        GameManager.Instance.SetState(GameState.WaitingRoll);
    }

    void UpdateActionSlotsUI(bool canInteract)
    {
        if (gameplayUI == null)
            return;

        int d1 = dice1 != null ? dice1.value : 0;
        int d2 = dice2 != null ? dice2.value : 0;
        gameplayUI.SetActionSlots(
            d1,
            dice1Used,
            d2,
            dice2Used,
            bonus3Available,
            bonus3Used,
            selectedActionSlot,
            canInteract
        );
    }

    public void ResetDice()
    {
        hasRolled = false;
        totalValue = 0;
        isRolling = false;
        dice1Used = false;
        dice2Used = false;
        bonus3Available = false;
        bonus3Used = false;
        earnedBonusThisTurn = false;
        selectedActionSlot = TurnActionSlot.None;
        ClearHighlights();
        UpdateActionSlotsUI(false);
    }

    public bool IsDouble()
    {
        return false; // Extra turns on doubles are disabled in Party Clash v0.2
    }

    public bool IsOneSix()
    {
        return DiceRuleUtility.IsOneSix(dice1.value, dice2.value);
    }

    public bool CanSpawnPiece()
    {
        return selectedActionSlot != TurnActionSlot.Bonus3 && GetSelectedStepValue() > 0;
    }

    public bool CanClimbHome()
    {
        return GetSelectedStepValue() > 0;
    }

    public void ClearHighlights()
    {
        MovePathHighlighter.TryClear();

        if (pieces == null)
            return;

        foreach (PieceController piece in pieces)
        {
            if (piece != null)
                piece.SetHighlight(false);
        }
    }

    public void HighlightValidPieces()
    {
        ClearHighlights();

        BoardManager board = GetBoard();
        if (board == null || turnManager == null || pieces == null)
            return;

        int currentPlayer = turnManager.currentPlayerIndex;
        int step = GetSelectedStepValue();
        if (step <= 0)
            return;

        foreach (PieceController piece in pieces)
        {
            if (piece == null || piece.playerIndex != currentPlayer)
                continue;

            bool canUse = board.IsPieceActionValid(piece, selectedActionSlot, step);
            piece.SetHighlight(canUse);

            if (canUse)
            {
                MovePathHighlighter.Instance.ShowMovePreview(
                    piece,
                    step,
                    selectedActionSlot != TurnActionSlot.Bonus3,
                    true
                );
            }
        }
    }
}
