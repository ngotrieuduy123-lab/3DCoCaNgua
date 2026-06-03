using Unity.Netcode;
using UnityEngine;
using System.Collections;

public class NetworkPieceMoveManager : NetworkBehaviour
{
    public BoardManager boardManager;
    public TurnManager turnManager;
    public NetworkDiceManager networkDiceManager;

    public void RequestMovePiece(int pieceId)
    {
        RequestMovePieceRpc(pieceId);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    void RequestMovePieceRpc(int pieceId, RpcParams rpcParams = default)
    {
        if (GameManager.Instance.IsState(GameState.GameOver))
        {
            return;
        }
        PieceController piece = GetPieceById(pieceId);

        if (piece == null)
        {
            Debug.Log("piece not found");
            return;
        }

        ulong senderClientId = rpcParams.Receive.SenderClientId;
        int senderPlayerIndex = NetworkPlayerSlotManager.Instance.GetPlayerIndex(senderClientId);

        if (senderPlayerIndex == -1)
        {
            Debug.Log("sender has no player slot");
            return;
        }

        if (piece.playerIndex != senderPlayerIndex)
        {
            Debug.Log("client cannot control this color");
            return;
        }

        if (piece.playerIndex != NetworkTurnManager.Instance.currentPlayerIndex.Value)
        {
            Debug.Log("not network current player");
            return;
        }

        int moveStep = networkDiceManager.totalValue.Value;

        if (piece.isInStable)
        {
            if (!networkDiceManager.CanSpawnPiece() || !piece.CanSpawn())
            {
                Debug.Log("server reject spawn");
                return;
            }

            piece.SpawnToStart();
            EndNetworkTurnAfterMove();
            return;
        }

        if (piece.isInHomePath)
        {
            if (!networkDiceManager.CanClimbHome() || !piece.CanClimbHome())
            {
                Debug.Log("server reject climb");
                return;
            }

            piece.ClimbHomeOneStep();
            EndNetworkTurnAfterMove();
            return;
        }

        if (!piece.CanMove(moveStep))
        {
            Debug.Log("server reject move, step=" + moveStep);
            return;
        }

        piece.SelectPiece();
        piece.MoveByStep(moveStep);

        StartCoroutine(WaitMoveThenEndTurn(piece));
    }

    PieceController GetPieceById(int pieceId)
    {
        foreach (PieceController piece in boardManager.allPieces)
        {
            if (piece.pieceId == pieceId)
                return piece;
        }

        return null;
    }

    void EndNetworkTurnAfterMove()
    {
        NetworkGameResultManager.Instance.CheckPlayerFinished(
            NetworkTurnManager.Instance.currentPlayerIndex.Value
        );

        bool extraTurn =
            networkDiceManager.IsDouble() &&
            NetworkTurnManager.Instance.CanGetExtraTurn();

        networkDiceManager.ResetNetworkDice();

        if (extraTurn)
        {
            NetworkTurnManager.Instance.UseExtraTurn();
        }
        else
        {
            NetworkTurnManager.Instance.NextTurn();
        }

        GameManager.Instance.SetState(GameState.WaitingRoll);
    }

    IEnumerator WaitMoveThenEndTurn(PieceController piece)
    {
        yield return new WaitUntil(() => !piece.isMoving);

        piece.CheckKickEnemy();

        EndNetworkTurnAfterMove();
    }

}