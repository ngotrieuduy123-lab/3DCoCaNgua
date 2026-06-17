using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;

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
    private Coroutine highlightRoutine;
    private readonly List<GameObject> outlineObjects = new List<GameObject>();
    private Material outlineMaterial;

    void Awake()
    {
        NormalizeStableState();
        CacheOriginalTransform();
    }

    void Start()
    {
        NormalizeStableState();
        CacheOriginalTransform();
    }

    void NormalizeStableState()
    {
        if (!isInStable)
            return;

        currentIndex = -1;
        homeIndex = -1;
        isInHomePath = false;
        stepsMoved = 0;
        isFinished = false;
    }

    void CacheOriginalTransform()
    {
        stablePosition = transform.position;
        originalScale = transform.localScale;
    }

    void EnsureOriginalTransform()
    {
        if (originalScale == Vector3.zero)
            originalScale = transform.localScale == Vector3.zero ? Vector3.one : transform.localScale;
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
        EnsureOriginalTransform();

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

        SyncStateToClients();
    }

    public void SelectPiece()
    {
        EnsureOriginalTransform();

        if (isMoving || isSelected) return;

        isSelected = true;
        transform.position += Vector3.up * 0.5f;
        transform.localScale = originalScale * 1.15f;
    }

    public void UnselectPiece()
    {
        EnsureOriginalTransform();

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

        SyncStateToClients();
    }

    public void ReturnToStable()
    {
        EnsureOriginalTransform();

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

        SyncStateToClients();
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

        SyncStateToClients();
    }

    void SyncStateToClients()
    {
        if (NetworkManager.Singleton == null ||
            !NetworkManager.Singleton.IsListening ||
            !IsSpawned ||
            !IsServer)
            return;

        SyncStateRpc(
            currentIndex,
            stepsMoved,
            homeIndex,
            isInStable,
            isInHomePath,
            isFinished,
            isMoving,
            isSelected,
            transform.position,
            transform.localScale
        );
    }

    [Rpc(SendTo.ClientsAndHost)]
    void SyncStateRpc(
        int syncedCurrentIndex,
        int syncedStepsMoved,
        int syncedHomeIndex,
        bool syncedIsInStable,
        bool syncedIsInHomePath,
        bool syncedIsFinished,
        bool syncedIsMoving,
        bool syncedIsSelected,
        Vector3 syncedPosition,
        Vector3 syncedScale)
    {
        currentIndex = syncedCurrentIndex;
        stepsMoved = syncedStepsMoved;
        homeIndex = syncedHomeIndex;
        isInStable = syncedIsInStable;
        isInHomePath = syncedIsInHomePath;
        isFinished = syncedIsFinished;
        isMoving = syncedIsMoving;
        isSelected = syncedIsSelected;
        transform.position = syncedPosition;
        transform.localScale = syncedScale;
    }

    public void SetHighlight(bool active)
    {
        EnsureOriginalTransform();

        if (highlightRoutine != null)
        {
            StopCoroutine(highlightRoutine);
            highlightRoutine = null;
        }

        if (!active)
        {
            transform.localScale = isSelected ? originalScale * 1.15f : originalScale;
            SetOutlineVisible(false, 0.35f, 1f);
            return;
        }

        EnsureOutlineObjects();

        if (outlineObjects.Count == 0)
            return;

        highlightRoutine = StartCoroutine(BlinkHighlightRoutine());
    }

    IEnumerator BlinkHighlightRoutine()
    {
        float blinkTime = Random.Range(0f, 1f);

        while (true)
        {
            blinkTime += Time.deltaTime * 6f;
            float pulse = (Mathf.Sin(blinkTime) + 1f) * 0.5f;
            float alpha = Mathf.Lerp(0.28f, 0.9f, pulse);
            float scale = Mathf.Lerp(1.08f, 1.18f, pulse);

            SetOutlineVisible(true, alpha, scale);
            yield return null;
        }
    }

    void EnsureOutlineObjects()
    {
        if (outlineObjects.Count > 0)
            return;

        outlineMaterial = CreateOutlineMaterial();

        if (outlineMaterial == null)
            return;

        MeshFilter[] meshFilters = GetComponentsInChildren<MeshFilter>(true);

        foreach (MeshFilter sourceFilter in meshFilters)
        {
            if (sourceFilter == null ||
                sourceFilter.sharedMesh == null ||
                sourceFilter.gameObject.name.Contains("MoveOutline"))
                continue;

            Renderer sourceRenderer = sourceFilter.GetComponent<Renderer>();

            if (sourceRenderer == null)
                continue;

            GameObject outline = new GameObject("MoveOutline");
            SetIgnoreRaycastLayer(outline);
            outline.transform.SetParent(sourceFilter.transform, false);
            outline.transform.localPosition = Vector3.zero;
            outline.transform.localRotation = Quaternion.identity;
            outline.transform.localScale = Vector3.one * 1.12f;

            MeshFilter outlineFilter = outline.AddComponent<MeshFilter>();
            outlineFilter.sharedMesh = sourceFilter.sharedMesh;

            MeshRenderer outlineRenderer = outline.AddComponent<MeshRenderer>();
            outlineRenderer.sharedMaterial = outlineMaterial;
            outlineRenderer.shadowCastingMode = ShadowCastingMode.Off;
            outlineRenderer.receiveShadows = false;
            outlineRenderer.enabled = false;

            outlineObjects.Add(outline);
        }
    }

    void SetIgnoreRaycastLayer(GameObject target)
    {
        int ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");

        if (ignoreRaycastLayer >= 0 && target != null)
            target.layer = ignoreRaycastLayer;
    }

    Material CreateOutlineMaterial()
    {
        Material material = null;
        Shader shader = FindHighlightShader();

        if (shader != null)
            material = new Material(shader);
        else if (highlightMaterial != null)
            material = new Material(highlightMaterial);
        else if (normalMaterial != null)
            material = new Material(normalMaterial);
        else if (pieceRenderer != null && pieceRenderer.sharedMaterial != null)
            material = new Material(pieceRenderer.sharedMaterial);

        if (material == null)
        {
            Debug.LogWarning("Could not create highlight material for " + name);
            return null;
        }

        ConfigureTransparentMaterial(material);
        SetMaterialColor(material, new Color(1f, 0.92f, 0.12f, 0.45f));
        return material;
    }

    Shader FindHighlightShader()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader != null) return shader;

        shader = Shader.Find("Unlit/Color");
        if (shader != null) return shader;

        shader = Shader.Find("Sprites/Default");
        if (shader != null) return shader;

        shader = Shader.Find("UI/Default");
        if (shader != null) return shader;

        shader = Shader.Find("Hidden/Internal-Colored");
        if (shader != null) return shader;

        return Shader.Find("Standard");
    }

    void ConfigureTransparentMaterial(Material material)
    {
        if (material == null)
            return;

        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);

        if (material.HasProperty("_Mode"))
            material.SetFloat("_Mode", 3f);

        if (material.HasProperty("_SrcBlend"))
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);

        if (material.HasProperty("_DstBlend"))
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);

        if (material.HasProperty("_ZWrite"))
            material.SetInt("_ZWrite", 0);

        material.EnableKeyword("_ALPHABLEND_ON");
        material.renderQueue = (int)RenderQueue.Transparent;
    }

    void SetMaterialColor(Material material, Color color)
    {
        if (material == null)
            return;

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);

        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
    }

    void SetOutlineVisible(bool visible, float alpha, float scale)
    {
        if (outlineMaterial != null)
            SetMaterialColor(outlineMaterial, new Color(1f, 0.92f, 0.12f, alpha));

        foreach (GameObject outline in outlineObjects)
        {
            if (outline == null)
                continue;

            outline.transform.localScale = Vector3.one * scale;

            Renderer outlineRenderer = outline.GetComponent<Renderer>();
            if (outlineRenderer != null)
                outlineRenderer.enabled = visible;
        }
    }
}
