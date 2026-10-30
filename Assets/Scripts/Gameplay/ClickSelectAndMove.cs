using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

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
        if (turnManager != null && turnManager.isGameOver) return;
        if (inputLocked) return;

        bool isNetworkMode =
            Unity.Netcode.NetworkManager.Singleton != null &&
            Unity.Netcode.NetworkManager.Singleton.IsListening;

        if (GameManager.Instance == null) return;

        if (isNetworkMode)
        {
            if (networkDiceManager == null || networkDiceManager.totalValue.Value <= 0) return;
        }
        else
        {
            if (!GameManager.Instance.IsState(GameState.WaitingChoosePiece)) return;
        }

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
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
            else
            {
                TryDeployOfflinePieceOrHintWhenClickMisses();
            }
        }
    }

    PieceController FindClickedPiece(Ray ray)
    {
        bool isNetworkMode =
            Unity.Netcode.NetworkManager.Singleton != null &&
            Unity.Netcode.NetworkManager.Singleton.IsListening;

        int activePlayer = isNetworkMode
            ? NetworkPlayerIndexUtility.GetLocalPlayerIndex()
            : (turnManager != null ? turnManager.currentPlayerIndex : -1);

        TurnActionSlot activeSlot = isNetworkMode
            ? (networkDiceManager != null ? networkDiceManager.selectedActionSlot : TurnActionSlot.None)
            : (diceManager != null ? diceManager.selectedActionSlot : TurnActionSlot.None);

        int activeStep = isNetworkMode
            ? (networkDiceManager != null ? networkDiceManager.GetStepForSlot(activeSlot) : 0)
            : (diceManager != null ? diceManager.GetStepForSlot(activeSlot) : 0);

        RaycastHit[] hits = Physics.RaycastAll(ray, 1000f, ~0, QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        PieceController firstOwnPiece = null;
        PieceController firstAnyPiece = null;

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null)
                continue;

            PieceController piece = hit.collider.GetComponentInParent<PieceController>();
            if (piece == null)
                continue;

            if (firstAnyPiece == null)
                firstAnyPiece = piece;

            if (piece.playerIndex == activePlayer)
            {
                if (firstOwnPiece == null)
                    firstOwnPiece = piece;

                if (boardManager != null &&
                    activeStep > 0 &&
                    boardManager.IsPieceActionValid(piece, activeSlot, activeStep))
                {
                    return piece;
                }
            }
        }

        // Proximity check against vertical character volumes of valid pieces for the active player
        if (boardManager != null && boardManager.allPieces != null && activePlayer >= 0 && activeStep > 0)
        {
            PieceController closestValid = null;
            float bestDist = 0.088f;

            foreach (PieceController piece in boardManager.allPieces)
            {
                if (piece == null ||
                    piece.playerIndex != activePlayer ||
                    piece.isMoving ||
                    !boardManager.IsPieceActionValid(piece, activeSlot, activeStep))
                    continue;

                float dist = DistanceRayToVerticalSegment(
                    ray,
                    piece.transform.position,
                    piece.transform.position + Vector3.up * 0.34f
                );

                if (dist < bestDist)
                {
                    bestDist = dist;
                    closestValid = piece;
                }
            }

            if (closestValid != null)
                return closestValid;
        }

        if (firstOwnPiece != null)
            return firstOwnPiece;

        return firstAnyPiece;
    }

    static float DistanceRayToVerticalSegment(Ray ray, Vector3 segBottom, Vector3 segTop)
    {
        Vector3 u = ray.direction;
        Vector3 v = segTop - segBottom;
        Vector3 w = ray.origin - segBottom;

        float a = Vector3.Dot(u, u);
        float b = Vector3.Dot(u, v);
        float c = Vector3.Dot(v, v);
        float d = Vector3.Dot(u, w);
        float e = Vector3.Dot(v, w);
        float denom = a * c - b * b;

        float sc;
        float tc;

        if (denom < 1e-6f)
        {
            sc = 0f;
            tc = b > c ? d / b : e / c;
        }
        else
        {
            sc = (b * e - c * d) / denom;
            tc = (a * e - b * d) / denom;
        }

        sc = Mathf.Max(0f, sc);
        tc = Mathf.Clamp01(tc);

        Vector3 pointOnRay = ray.origin + u * sc;
        Vector3 pointOnSeg = segBottom + v * tc;
        return Vector3.Distance(pointOnRay, pointOnSeg);
    }

    void TryDeployOfflinePieceOrHintWhenClickMisses()
    {
        if (diceManager == null || boardManager == null || turnManager == null || !diceManager.hasRolled)
            return;

        int currentPlayer = turnManager.currentPlayerIndex;
        TurnActionSlot slot = diceManager.selectedActionSlot;
        int step = diceManager.GetStepForSlot(slot);
        if (slot == TurnActionSlot.None || step <= 0)
            return;

        if (slot != TurnActionSlot.Bonus3 && boardManager.RequiresMandatorySpawn(currentPlayer, step))
        {
            PieceController stablePiece = FindFirstSpawnableStablePiece(currentPlayer, step);
            if (stablePiece != null)
            {
                TryMovePiece(stablePiece);
                return;
            }
        }

        if (gameplayUI != null)
            gameplayUI.SetMessage("Hãy click trực tiếp vào một quân cờ đang phát sáng vòng vàng!");
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

        if (networkDiceManager.totalValue.Value <= 0)
            return;

        int localPlayer = NetworkPlayerIndexUtility.GetLocalPlayerIndex();

        if (localPlayer != NetworkTurnManager.Instance.currentPlayerIndex.Value)
            return;

        if (NetworkRoomControlManager.Instance != null &&
            !NetworkRoomControlManager.Instance.IsPlayerActive(localPlayer))
            return;

        TurnActionSlot slot = networkDiceManager.selectedActionSlot;
        int step = networkDiceManager.GetStepForSlot(slot);
        if (slot == TurnActionSlot.None || slot == TurnActionSlot.Bonus3 || step <= 0)
            return;

        if (!boardManager.RequiresMandatorySpawn(localPlayer, step) &&
            HasValidNonStableNetworkMove(localPlayer, slot, step))
        {
            if (gameplayUI != null)
                gameplayUI.SetMessage("Chọn một quân đang phát sáng");
            return;
        }

        PieceController piece = FindFirstSpawnableStablePiece(localPlayer, step);

        if (piece == null)
            return;

        if (gameplayUI != null)
            gameplayUI.SetMessage("Đang xuất quân xung kích...");

        networkPieceMoveManager.RequestMovePiece(piece.pieceId, slot);
    }

    bool HasValidNonStableNetworkMove(int playerIndex, TurnActionSlot slot, int step)
    {
        foreach (PieceController piece in boardManager.allPieces)
        {
            if (piece == null ||
                piece.playerIndex != playerIndex ||
                piece.isMoving ||
                piece.isInStable)
                continue;

            if (boardManager.IsPieceActionValid(piece, slot, step))
                return true;
        }

        return false;
    }

    PieceController FindFirstSpawnableStablePiece(int playerIndex, int dieValue)
    {
        foreach (PieceController piece in boardManager.allPieces)
        {
            if (piece == null ||
                piece.playerIndex != playerIndex ||
                piece.isMoving ||
                !piece.isInStable)
                continue;

            if (piece.CanSpawn(dieValue))
                return piece;
        }

        return null;
    }

    void TryMovePiece(PieceController piece)
    {
        bool isNetworkMode =
            Unity.Netcode.NetworkManager.Singleton != null &&
            Unity.Netcode.NetworkManager.Singleton.IsListening;

        if (turnManager != null && turnManager.isGameOver) return;
        if (diceManager != null && diceManager.isRolling)
        {
            Debug.Log("dice is rolling");
            return;
        }

        if (isNetworkMode)
        {
            if (networkDiceManager.totalValue.Value <= 0)
            {
                gameplayUI.SetMessage("Gieo xúc xắc trước");
                return;
            }

            int localPlayer = NetworkPlayerIndexUtility.GetLocalPlayerIndex();

            if (NetworkRoomControlManager.Instance != null &&
                !NetworkRoomControlManager.Instance.IsPlayerActive(localPlayer))
            {
                gameplayUI.SetMessage("Bạn không còn trong phòng này.");
                return;
            }

            if (piece.playerIndex != localPlayer)
            {
                gameplayUI.SetMessage("Không phải quân của bạn");
                return;
            }

            if (piece.playerIndex != NetworkTurnManager.Instance.currentPlayerIndex.Value)
            {
                gameplayUI.SetMessage("Chưa tới lượt của bạn");
                return;
            }

            TurnActionSlot netSlot = networkDiceManager.selectedActionSlot;
            int netStep = networkDiceManager.GetStepForSlot(netSlot);

            if (!boardManager.IsPieceActionValid(piece, netSlot, netStep))
            {
                if (netSlot != TurnActionSlot.Bonus3 &&
                    boardManager.RequiresMandatorySpawn(localPlayer, netStep) &&
                    !piece.isInStable)
                {
                    gameplayUI.SetMessage("Bắt buộc chọn quân trong chuồng (cần tối thiểu 2 quân trên bàn)");
                }
                else
                {
                    gameplayUI.SetMessage("Quân này không thể đi với viên đang chọn");
                }
                return;
            }

            networkPieceMoveManager.RequestMovePiece(piece.pieceId, netSlot);
            return;
        }

        // Offline / Local mode
        if (!diceManager.hasRolled || diceManager.totalValue <= 0)
        {
            gameplayUI.SetMessage("Gieo xúc xắc trước");
            return;
        }

        if (!turnManager.IsCurrentPlayer(piece.playerIndex))
        {
            gameplayUI.SetMessage("Chưa tới lượt của bạn");
            return;
        }

        if (piece.isMoving) return;

        TurnActionSlot slot = diceManager.selectedActionSlot;
        int step = diceManager.GetStepForSlot(slot);

        if (!boardManager.IsPieceActionValid(piece, slot, step))
        {
            if (slot != TurnActionSlot.Bonus3 &&
                boardManager.RequiresMandatorySpawn(piece.playerIndex, step) &&
                !piece.isInStable)
            {
                gameplayUI.SetMessage("Bắt buộc chọn quân trong chuồng (cần tối thiểu 2 quân trên bàn)");
            }
            else
            {
                gameplayUI.SetMessage("Quân này không thể đi với viên đang chọn");
            }
            return;
        }

        inputLocked = true;
        GameManager.Instance.SetState(GameState.MovingPiece);
        diceManager.ClearHighlights();

        if (piece.isInStable)
        {
            piece.SpawnAndMoveByDice(step);
        }
        else
        {
            piece.SelectPiece();
            piece.MoveByStep(step);
        }

        StartCoroutine(WaitPieceActionThenConsumeOffline(piece, slot));
    }

    IEnumerator WaitPieceActionThenConsumeOffline(PieceController piece, TurnActionSlot slot)
    {
        float elapsed = 0f;
        while (piece != null && piece.isMoving && elapsed < 6f)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        inputLocked = false;
        diceManager.ConsumeSlotAfterMove(slot, piece != null && piece.lastActionAwardedScoredKick);
    }
}
