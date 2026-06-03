using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class PieceController : NetworkBehaviour
{
    public BoardManager board;
    public PlayerColor playerColor;
    public int pieceId;

    public int playerIndex
    {
        get { return (int)playerColor; }
    }

    public int currentIndex = -1;
    public int startIndex = 0;
    public int stepsMoved = 0;
    public int maxLoopSteps = 55;
    public bool isFinished = false;
    public Renderer pieceRenderer;
    public Material normalMaterial;
    public Material highlightMaterial;

    public bool isInStable = true;
    public bool isMoving = false;
    public bool isSelected = false;
    public bool isInHomePath = false;

    public int homeIndex = -1;

    private Vector3 stablePosition;
    private Vector3 originalScale;

    void Start()
    {
        stablePosition = transform.position;
        originalScale = transform.localScale;
    }

    public bool CanSpawn()
    {
        if (!isInStable || isMoving)
            return false;

        PieceController pieceAtStart = board.GetPieceAt(startIndex);

        if (pieceAtStart != null && pieceAtStart.playerIndex == playerIndex)
            return false;

        return true;
    }

    public void SpawnToStart()
    {
        if (!CanSpawn()) return;

        PieceController enemy = board.GetEnemyPieceAt(playerIndex, startIndex);

        if (enemy != null && !board.IsSafeTile(startIndex))
        {
            if (NetworkSoundManager.Instance != null)
            {
                NetworkSoundManager.Instance.PlayKickSoundRpc();
            }
            else
            {
                SoundManager.Instance.PlayKick();
            }

            enemy.ReturnToStable();
        }

        isInStable = false;
        isInHomePath = false;
        currentIndex = startIndex;
        homeIndex = -1;
        stepsMoved = 0;

        transform.position = board.pathPoints[startIndex].position;
        if (NetworkSoundManager.Instance != null)
        {
            NetworkSoundManager.Instance.PlayMoveSoundRpc();
        }
        else
        {
            SoundManager.Instance.PlayMove();
        }
        transform.localScale = originalScale;
        isSelected = false;
    }

    public void SelectPiece()
    {
        if (isMoving || isSelected) return;

        isSelected = true;
        transform.position += Vector3.up * 0.5f;
        transform.localScale = originalScale * 1.15f;
    }

    public void UnselectPiece()
    {
        transform.localScale = originalScale;

        if (isInStable)
            transform.position = stablePosition;
        else if (isInHomePath)
            transform.position = board.GetHomePath(playerIndex)[homeIndex].position;
        else
            transform.position = board.pathPoints[currentIndex].position;

        isSelected = false;
    }

    public bool CanMove(int step)
    {
        if (isInStable) return false;
        if (isFinished) return false;

        Transform[] homePath = board.GetHomePath(playerIndex);

        if (isInHomePath)
            return false;

        if (stepsMoved + step > maxLoopSteps + homePath.Length)
            return false;

        for (int i = 1; i <= step; i++)
        {
            bool willEnterHome = stepsMoved + i > maxLoopSteps;

            if (!willEnterHome)
            {
                int checkIndex = (currentIndex + i) % board.pathPoints.Length;
                PieceController pieceAtTile = board.GetPieceAt(checkIndex);

                if (pieceAtTile != null)
                {
                    bool isTargetTile = i == step;

                    if (pieceAtTile.playerIndex == playerIndex)
                        return false;

                    if (!isTargetTile)
                        return false;

                    if (board.IsSafeTile(checkIndex))
                        return false;
                }
            }
            else
            {
                int stepsToHome = maxLoopSteps - stepsMoved;
                int homeTargetIndex = step - stepsToHome - 1;

                if (homeTargetIndex < 0 || homeTargetIndex >= homePath.Length)
                    return false;

                int finishIndex = board.GetNextFinishHomeIndex(playerIndex);

                if (finishIndex == -1)
                    return false;

                if (homeTargetIndex > finishIndex)
                    return false;

                for (int h = 0; h <= homeTargetIndex; h++)
                {
                    if (board.IsHomeIndexOccupied(playerIndex, h))
                        return false;
                }

                return true;
            }
        }

        return true;
    }

    public void MoveByStep(int step)
    {
        if (!isMoving)
            StartCoroutine(MoveByStepRoutine(step));
    }

    IEnumerator MoveByStepRoutine(int step)
    {
        isMoving = true;

        if (!CanMove(step))
        {
            Debug.Log("cannot move");
            isMoving = false;
            UnselectPiece();
            yield break;
        }

        Transform[] homePath = board.GetHomePath(playerIndex);

        for (int i = 0; i < step; i++)
        {
            if (!isInHomePath)
            {
                if (stepsMoved >= maxLoopSteps)
                {
                    isInHomePath = true;
                    homeIndex = 0;
                }
                else
                {
                    currentIndex = (currentIndex + 1) % board.pathPoints.Length;
                    stepsMoved++;
                }
            }
            else
            {
                homeIndex++;
            }

            Vector3 targetPos;

            if (isInHomePath)
                targetPos = homePath[homeIndex].position;
            else
                targetPos = board.pathPoints[currentIndex].position;
           

            while (Vector3.Distance(transform.position, targetPos) > 0.01f)
            {
                transform.position = Vector3.MoveTowards(
                    transform.position,
                    targetPos,
                    5f * Time.deltaTime
                );

                yield return null;
            }

            transform.position = targetPos;
            if (NetworkSoundManager.Instance != null)
            {
                NetworkSoundManager.Instance.PlayMoveSoundRpc();
            }
            else
            {
                SoundManager.Instance.PlayMove();
            }
            yield return new WaitForSeconds(0.1f);
        }
        if (isInHomePath)
        {
            int finishIndex = board.GetNextFinishHomeIndex(playerIndex);

            if (!isFinished && homeIndex == finishIndex)
            {
                isFinished = true;
                board.AddFinishedPiece(playerIndex);

                Debug.Log("FINISH PIECE: " + name
                    + " player=" + playerIndex
                    + " home=" + (homeIndex + 1)
                    + " count=" + board.playerFinishCount[playerIndex]);
            }
        }
        CheckKickEnemy();
        transform.localScale = originalScale;
        isMoving = false;
        isSelected = false;
    }

    public void ReturnToStable()
    {
        isInStable = true;
        isInHomePath = false;
        currentIndex = -1;
        homeIndex = -1;
        stepsMoved = 0;
        isMoving = false;
        isSelected = false;
        isFinished = false;

        transform.position = stablePosition;
        transform.localScale = originalScale;
    }

    public void CheckKickEnemy()
    {
        if (isInHomePath) return;

        if (board.IsSafeTile(currentIndex))
            return;

        PieceController enemy = board.GetEnemyPieceAt(playerIndex, currentIndex);

        if (enemy != null)
        {
            Debug.Log("kick enemy: " + enemy.name);

            if (NetworkSoundManager.Instance != null)
            {
                NetworkSoundManager.Instance.PlayKickSoundRpc();
            }
            else
            {
                SoundManager.Instance.PlayKick();
            }

            enemy.ReturnToStable();
        }
    }

    public bool CanClimbHome()
    {
        if (!isInHomePath) return false;
        if (isFinished) return false;

        int finishIndex = board.GetNextFinishHomeIndex(playerIndex);
        if (finishIndex == -1) return false;

        int nextIndex = homeIndex + 1;

        if (nextIndex > finishIndex)
            return false;

        if (board.IsHomeIndexOccupied(playerIndex, nextIndex))
            return false;

        return true;
    }

    public void ClimbHomeOneStep()
    {
        if (!CanClimbHome()) return;

        int finishIndex = board.GetNextFinishHomeIndex(playerIndex);

        homeIndex++;
        transform.position = board.GetHomePath(playerIndex)[homeIndex].position;

        if (homeIndex == finishIndex)
        {
            isFinished = true;
            board.AddFinishedPiece(playerIndex);
            if (NetworkSoundManager.Instance != null)
            {
                NetworkSoundManager.Instance.PlayFinishSoundRpc();
            }
            else
            {
                SoundManager.Instance.PlayFinish();
            }
            Debug.Log("FINISH PIECE: " + name
                + " player=" + playerIndex
                + " home=" + (homeIndex + 1)
                + " finishedCount=" + board.playerFinishCount[playerIndex]);
        }
    }

    public void SetHighlight(bool active)
    {
        if (pieceRenderer == null) return;

        pieceRenderer.material = active ? highlightMaterial : normalMaterial;
    }
}