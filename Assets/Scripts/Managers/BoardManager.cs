using UnityEngine;

public class BoardManager : MonoBehaviour
{
    public Transform[] pathPoints;
    public PieceController[] allPieces;
    public Transform[] redHomePath;
    public Transform[] blueHomePath;
    public Transform[] greenHomePath;
    public Transform[] yellowHomePath;
    public BoardTile[] tiles;
    public int[] playerFinishCount = new int[4];

    void Start()
    {
        ApplyPlayerSkins();
    }

    void ApplyPlayerSkins()
    {
        foreach (PieceController piece in allPieces)
        {
            if (piece == null)
                continue;

            PieceSkinApplicator applicator = piece.GetComponent<PieceSkinApplicator>();

            if (applicator == null)
                applicator = piece.gameObject.AddComponent<PieceSkinApplicator>();

            applicator.Initialize(piece);
        }
    }

    public bool HasOwnPieceAt(int playerIndex, int tileIndex)
    {
        foreach (PieceController piece in allPieces)
        {
            if (piece.playerIndex == playerIndex &&
                !piece.isInStable &&
                !piece.isInHomePath &&
                piece.currentIndex == tileIndex)
            {
                return true;
            }
        }

        return false;
    }

    public bool HasOwnPieceBetween(int playerIndex, int fromIndex, int targetIndex)
    {
        foreach (PieceController piece in allPieces)
        {
            if (piece.playerIndex == playerIndex &&
                !piece.isInStable &&
                piece.currentIndex > fromIndex &&
                piece.currentIndex <= targetIndex)
            {
                return true;
            }
        }

        return false;
    }

    public PieceController GetPieceAt(int tileIndex)
    {
        foreach (PieceController piece in allPieces)
        {
            if (!piece.isInStable &&
                !piece.isInHomePath &&
                piece.currentIndex == tileIndex)
            {
                return piece;
            }
        }

        return null;
    }

    public Transform[] GetHomePath(int playerIndex)
    {
        if (playerIndex == 0) return blueHomePath;
        if (playerIndex == 1) return redHomePath;
        if (playerIndex == 2) return greenHomePath;
        if (playerIndex == 3) return yellowHomePath;

        return null;
    }
    public bool IsSafeTile(int tileIndex)
    {
        return tiles[tileIndex].isSafeTile;
    }

    public PieceController GetEnemyPieceAt(int playerIndex, int tileIndex)
    {
        foreach (PieceController piece in allPieces)
        {
            if (!piece.isInStable &&
                !piece.isInHomePath &&
                piece.playerIndex != playerIndex &&
                piece.currentIndex == tileIndex)
            {
                return piece;
            }
        }

        return null;
    }

    public int GetNextFinishHomeIndex(int playerIndex)
    {
        int count = playerFinishCount[playerIndex];

        if (count == 0) return 5; // ô 6
        if (count == 1) return 4; // ô 5
        if (count == 2) return 3; // ô 4
        if (count == 3) return 2; // ô 3

        return -1;
    }

    public void AddFinishedPiece(int playerIndex)
    {
        playerFinishCount[playerIndex]++;
    }

    public bool IsHomeIndexOccupied(int playerIndex, int homeIndex)
    {
        foreach (PieceController piece in allPieces)
        {
            if (piece.playerIndex == playerIndex &&
                piece.isInHomePath &&
                piece.homeIndex == homeIndex)
            {
                return true;
            }
        }

        return false;
    }
}
