using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class NetworkPieceMoveManager : NetworkBehaviour
{
    public BoardManager boardManager;
    public TurnManager turnManager;
    public NetworkDiceManager networkDiceManager;

    private bool isResolvingMove = false;

    public bool IsResolvingMove => isResolvingMove;

    public void RequestMovePiece(int pieceId)
    {
        TurnActionSlot slot = networkDiceManager != null
            ? networkDiceManager.selectedActionSlot
            : TurnActionSlot.Dice1;
        RequestMovePiece(pieceId, slot);
    }

    public void RequestMovePiece(int pieceId, TurnActionSlot slot)
    {
        RequestMovePieceRpc(pieceId, (int)slot);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    void RequestMovePieceRpc(int pieceId, int actionSlotInt, RpcParams rpcParams = default)
    {
        if (isResolvingMove)
        {
            Debug.Log("server reject move: another action is currently resolving");
            return;
        }

        if (GameManager.Instance != null && GameManager.Instance.IsState(GameState.GameOver))
            return;

        PieceController piece = GetPieceById(pieceId);
        if (piece == null || piece.isMoving)
        {
            Debug.Log("piece not found or already moving");
            return;
        }

        ulong senderClientId = rpcParams.Receive.SenderClientId;
        int senderPlayerIndex = NetworkPlayerIndexUtility.GetPlayerIndex(senderClientId);

        if (senderPlayerIndex == -1 || piece.playerIndex != senderPlayerIndex)
        {
            Debug.Log("client cannot control this piece");
            return;
        }

        if (NetworkRoomControlManager.Instance != null &&
            !NetworkRoomControlManager.Instance.IsPlayerActive(senderPlayerIndex))
        {
            Debug.Log("inactive player cannot move");
            return;
        }

        if (NetworkTurnManager.Instance == null ||
            piece.playerIndex != NetworkTurnManager.Instance.currentPlayerIndex.Value)
        {
            Debug.Log("not network current player");
            return;
        }

        TurnActionSlot slot = (TurnActionSlot)actionSlotInt;
        if (slot == TurnActionSlot.None)
        {
            if (networkDiceManager.IsSlotPlayableForPlayer(senderPlayerIndex, TurnActionSlot.Dice1))
                slot = TurnActionSlot.Dice1;
            else if (networkDiceManager.IsSlotPlayableForPlayer(senderPlayerIndex, TurnActionSlot.Dice2))
                slot = TurnActionSlot.Dice2;
            else if (networkDiceManager.IsSlotPlayableForPlayer(senderPlayerIndex, TurnActionSlot.Bonus3))
                slot = TurnActionSlot.Bonus3;
        }

        int moveStep = networkDiceManager.GetStepForSlot(slot);
        if (moveStep <= 0 || !boardManager.IsPieceActionValid(piece, slot, moveStep))
        {
            Debug.Log("server reject action: slot=" + slot + " step=" + moveStep);
            return;
        }

        isResolvingMove = true;
        if (GameManager.Instance != null)
            GameManager.Instance.SetState(GameState.MovingPiece);

        if (piece.isInStable)
        {
            piece.SpawnAndMoveByDice(moveStep);
        }
        else
        {
            piece.SelectPiece();
            piece.MoveByStep(moveStep);
        }

        StartCoroutine(WaitMoveThenConsumeSlot(piece, slot));
    }

    PieceController GetPieceById(int pieceId)
    {
        if (boardManager == null || boardManager.allPieces == null)
            return null;

        foreach (PieceController piece in boardManager.allPieces)
        {
            if (piece != null && piece.pieceId == pieceId)
                return piece;
        }

        return null;
    }

    IEnumerator WaitMoveThenConsumeSlot(PieceController piece, TurnActionSlot slot)
    {
        float elapsed = 0f;
        while (piece != null && piece.isMoving && elapsed < 6f)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        isResolvingMove = false;
        bool scoredKick = piece != null && piece.lastActionAwardedScoredKick;
        networkDiceManager.ConsumeSlotAfterMoveServer(slot, scoredKick);
    }
}
