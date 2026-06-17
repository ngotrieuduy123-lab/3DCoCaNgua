using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System;

public class ClickSelectAndMove : MonoBehaviour
{
    public Camera mainCamera;
    public TurnManager turnManager;
    public DiceManager diceManager;
    public BoardManager boardManager;
    public bool inputLocked = false;

    public NetworkDiceManager networkDiceManager;
    public NetworkPieceMoveManager networkPieceMoveManager;
    public GameplayUI gameplayUI;
    void Update()
    {
        if (turnManager.isGameOver) return;
        if (inputLocked) return;
        bool isNetworkMode =
    Unity.Netcode.NetworkManager.Singleton != null &&
    Unity.Netcode.NetworkManager.Singleton.IsListening;

        if (GameManager.Instance == null) return;

        if (isNetworkMode)
        {
            if (networkDiceManager.totalValue.Value <= 0) return;
        }
        else
        {
            if (!GameManager.Instance.IsState(GameState.WaitingChoosePiece)) return;
        }

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (IsPointerOverBlockingUi())
                return;

            Ray ray = mainCamera.ScreenPointToRay(
                Mouse.current.position.ReadValue()
            );

            PieceController piece = FindClickedPiece(ray);

            if (piece != null)
            {
                TryMovePiece(piece);
            }
            else if (isNetworkMode)
            {
                TryDeployStablePieceWhenClickMisses();
            }
        }
    }

    PieceController FindClickedPiece(Ray ray)
    {
        RaycastHit[] hits = Physics.RaycastAll(ray, 1000f, ~0, QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null)
                continue;

            PieceController piece = hit.collider.GetComponentInParent<PieceController>();

            if (piece != null)
                return piece;
        }

        return null;
    }

    bool IsPointerOverBlockingUi()
    {
        if (EventSystem.current == null || Mouse.current == null)
            return false;

        PointerEventData pointerData = new PointerEventData(EventSystem.current)
        {
            position = Mouse.current.position.ReadValue()
        };

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);

        foreach (RaycastResult result in results)
        {
            GameObject target = result.gameObject;

            if (target == null)
                continue;

            if (target.GetComponentInParent<Selectable>() != null ||
                target.GetComponentInParent<TMP_InputField>() != null)
                return true;
        }

        return false;
    }

    void TryDeployStablePieceWhenClickMisses()
    {
        if (networkDiceManager == null ||
            networkPieceMoveManager == null ||
            boardManager == null ||
            NetworkTurnManager.Instance == null)
            return;

        if (networkDiceManager.totalValue.Value <= 0 ||
            !networkDiceManager.CanSpawnPiece())
            return;

        int localPlayer = NetworkPlayerIndexUtility.GetLocalPlayerIndex();

        if (localPlayer != NetworkTurnManager.Instance.currentPlayerIndex.Value)
            return;

        if (NetworkRoomControlManager.Instance != null &&
            !NetworkRoomControlManager.Instance.IsPlayerActive(localPlayer))
            return;

        if (HasValidNonStableNetworkMove(localPlayer))
        {
            if (gameplayUI != null)
                gameplayUI.SetMessage("Choose a highlighted piece");
            return;
        }

        PieceController piece = FindFirstSpawnableStablePiece(localPlayer);

        if (piece == null)
            return;

        if (gameplayUI != null)
            gameplayUI.SetMessage("Deploying piece...");

        networkPieceMoveManager.RequestMovePiece(piece.pieceId);
    }

    bool HasValidNonStableNetworkMove(int playerIndex)
    {
        foreach (PieceController piece in boardManager.allPieces)
        {
            if (piece == null ||
                piece.playerIndex != playerIndex ||
                piece.isFinished ||
                piece.isMoving ||
                piece.isInStable)
                continue;

            if (piece.isInHomePath)
            {
                if (networkDiceManager.CanClimbHome() && piece.CanClimbHome())
                    return true;
            }
            else if (piece.CanMove(networkDiceManager.totalValue.Value))
            {
                return true;
            }
        }

        return false;
    }

    PieceController FindFirstSpawnableStablePiece(int playerIndex)
    {
        foreach (PieceController piece in boardManager.allPieces)
        {
            if (piece == null ||
                piece.playerIndex != playerIndex ||
                piece.isFinished ||
                piece.isMoving ||
                !piece.isInStable)
                continue;

            if (piece.CanSpawn())
                return piece;
        }

        return null;
    }

    void TryMovePiece(PieceController piece)
    {
        bool isNetworkMode =
    Unity.Netcode.NetworkManager.Singleton != null &&
    Unity.Netcode.NetworkManager.Singleton.IsListening;
        if (turnManager.isGameOver) return;
        if (diceManager.isRolling)
        {
            Debug.Log("dice is rolling");
            return;
        }

        if (isNetworkMode)
        {
            if (networkDiceManager.totalValue.Value <= 0)
            {
                Debug.Log("network roll dice first");
                gameplayUI.SetMessage("Roll dice first");
                return;
            }
        }
        else
        {
            if (!diceManager.hasRolled || diceManager.totalValue <= 0)
            {
                Debug.Log("roll dice first");
                gameplayUI.SetMessage("Roll dice first");
                return;
            }
        }

        if (isNetworkMode)
        {
            int localPlayer = NetworkPlayerIndexUtility.GetLocalPlayerIndex();

            if (NetworkRoomControlManager.Instance != null &&
                !NetworkRoomControlManager.Instance.IsPlayerActive(localPlayer))
            {
                gameplayUI.SetMessage("You are no longer in this room.");
                return;
            }

            if (piece.playerIndex != localPlayer)
            {
                Debug.Log("not your piece");
                gameplayUI.SetMessage("Not your piece");
                return;
            }

            if (piece.playerIndex != NetworkTurnManager.Instance.currentPlayerIndex.Value)
            {
                Debug.Log("not network turn");
                gameplayUI.SetMessage("Not your turn");
                return;
            }
        }
        else
        {
            if (!turnManager.IsCurrentPlayer(piece.playerIndex))
            {
                Debug.Log("not your turn");
                gameplayUI.SetMessage("Not your turn");
                return;
            }
        }

        if (isNetworkMode)
        {
            networkPieceMoveManager.RequestMovePiece(piece.pieceId);
            return;
        }

        if (piece.isMoving) return;

        if (piece.isInStable)
        {
            if (diceManager.CanSpawnPiece())
            {
                if (!piece.CanSpawn())
                {
                    Debug.Log("cannot spawn this piece, choose another piece");
                    return;
                }

                inputLocked = true;
                GameManager.Instance.SetState(GameState.MovingPiece);

                piece.SpawnToStart();

                inputLocked = false;

                EndTurnAfterMove();
            }
            else
            {
                Debug.Log("cannot spawn this piece");
                return;
            }

            return;
        }
        if (piece.isInHomePath)
        {
            if (!diceManager.CanClimbHome())
            {
                Debug.Log("need double or 1-6 to climb home");
                return;
            }

            if (!piece.CanClimbHome())
            {
                Debug.Log("this piece cannot climb home");
                return;
            }

            inputLocked = true;
            GameManager.Instance.SetState(GameState.MovingPiece);

            piece.ClimbHomeOneStep();
            CheckPlayerFinished(piece.playerIndex);

            inputLocked = false;

            EndTurnAfterMove();
            return;
        }

        if (!piece.CanMove(diceManager.totalValue))
        {
            Debug.Log("this piece cannot move, choose another piece");
            gameplayUI.SetMessage("Cannot move this piece");
            return;
        }

        inputLocked = true;
        GameManager.Instance.SetState(GameState.MovingPiece);


        piece.SelectPiece();
        piece.MoveByStep(diceManager.totalValue);

        StartCoroutine(WaitMoveThenEndTurn(piece));
    }

    void EndTurnAfterMove()
    {
        if (turnManager.isGameOver)
        {
            diceManager.ResetDice();
            GameManager.Instance.SetState(GameState.GameOver);
            Debug.Log("game ended");
            return;
        }

        bool extraTurn = diceManager.IsDouble() && turnManager.CanGetExtraTurn();

        diceManager.ResetDice();

        if (extraTurn)
        {
            turnManager.UseExtraTurn();
            GameManager.Instance.SetState(GameState.WaitingRoll);
        }
        else
        {
            turnManager.NextTurn();
            GameManager.Instance.SetState(GameState.WaitingRoll);
        }
    }

    void CheckPlayerFinished(int playerIndex)
    {
        int count = 0;

        foreach (PieceController piece in boardManager.allPieces)
        {
            if (piece.playerIndex == playerIndex && piece.isFinished)
                count++;
        }

        Debug.Log("CHECK PLAYER FINISH: player=" + playerIndex + " count=" + count);

        if (count >= 4)
        {
            Debug.Log("PLAYER FINISHED GAME: player=" + playerIndex);
            turnManager.SetPlayerFinished(playerIndex);
        }
    }

    IEnumerator WaitMoveThenEndTurn(PieceController piece)
    {
        yield return new WaitUntil(() => !piece.isMoving);

        inputLocked = false;

        CheckPlayerFinished(piece.playerIndex);
        EndTurnAfterMove();
    }
}
