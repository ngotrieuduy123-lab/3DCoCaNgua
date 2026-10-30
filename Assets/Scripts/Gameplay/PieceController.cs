using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;

public class PieceController : NetworkBehaviour
{
    const float StepSettleDelay = 0.03f;
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

    [Header("Party Clash v0.2 Piece State")]
    public bool hasPassedCheckpoint = false;
    public bool hasNemesisMark = false;
    public bool justCashedInThisTurn = false;
    public bool hasSpawnProtection = false;

    public int lastActionPointsAwarded = 0;
    public bool lastActionAwardedScoredKick = false;

    private Vector3 stablePosition;
    private Vector3 originalScale;
    private bool originalTransformCached = false;
    private Coroutine highlightRoutine;
    private Coroutine remoteSkinTransitionRoutine;
    private readonly List<GameObject> outlineObjects = new List<GameObject>();
    private Material outlineMaterial;
    private GameObject statusRingObject;
    private LineRenderer statusRingRenderer;
    private Material statusRingMaterial;
    private GameObject selectionRingObject;
    private LineRenderer selectionRingRenderer;
    private Material selectionRingMaterial;
    private GameObject clickHitboxObject;

    void Awake()
    {
        CacheOriginalTransform();
        NormalizeStableState();
        EnsureClickHitbox();
    }

    void Start()
    {
        CacheOriginalTransform();
        NormalizeStableState();
        EnsureClickHitbox();
        UpdateStatusBadgeRing();
    }

    void EnsureClickHitbox()
    {
        if (clickHitboxObject != null)
            return;

        Transform existing = transform.Find("PieceClickHitbox");
        if (existing != null)
        {
            clickHitboxObject = existing.gameObject;
            return;
        }

        clickHitboxObject = new GameObject("PieceClickHitbox");
        clickHitboxObject.layer = gameObject.layer;
        clickHitboxObject.transform.SetParent(transform, false);
        clickHitboxObject.transform.position = transform.position;
        clickHitboxObject.transform.rotation = Quaternion.identity;

        float invScale = 1f / Mathf.Max(0.001f, transform.lossyScale.x);
        clickHitboxObject.transform.localScale = Vector3.one * invScale;

        BoxCollider box = clickHitboxObject.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, 0.17f, 0f);
        box.size = new Vector3(0.105f, 0.36f, 0.105f);
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
        hasPassedCheckpoint = false;
        hasNemesisMark = false;
        hasSpawnProtection = false;
    }

    void CacheOriginalTransform()
    {
        if (originalTransformCached)
            return;

        stablePosition = transform.position;
        originalScale = transform.localScale == Vector3.zero ? Vector3.one : transform.localScale;
        originalTransformCached = true;
    }

    void EnsureOriginalTransform()
    {
        if (!originalTransformCached)
            CacheOriginalTransform();

        if (originalScale == Vector3.zero)
            originalScale = transform.localScale == Vector3.zero ? Vector3.one : transform.localScale;
    }

    public void PlaceOnBoardAtOffset(int offsetFromStart, bool grantSpawnProtection)
    {
        EnsureOriginalTransform();

        if (board == null || board.pathPoints == null || board.pathPoints.Length == 0)
            return;

        offsetFromStart = Mathf.Clamp(offsetFromStart, 0, maxLoopSteps);
        isInStable = false;
        isInHomePath = false;
        isFinished = false;
        isMoving = false;
        isSelected = false;
        homeIndex = -1;
        stepsMoved = offsetFromStart;
        currentIndex = (startIndex + offsetFromStart) % board.pathPoints.Length;
        hasPassedCheckpoint = stepsMoved >= DiceRuleUtility.CheckpointStep;
        hasNemesisMark = false;
        justCashedInThisTurn = false;
        hasSpawnProtection = grantSpawnProtection;

        transform.position = board.pathPoints[currentIndex].position;
        transform.localScale = originalScale;

        UpdateStatusBadgeRing();
        SyncStateToClients();
    }

    public bool CanSpawn()
    {
        return CanSpawn(1);
    }

    public bool CanSpawn(int dieValue)
    {
        if (!isInStable || isMoving || justCashedInThisTurn)
            return false;

        if (!DiceRuleUtility.CanDieSpawnPiece(dieValue))
            return false;

        if (board == null || board.pathPoints == null || board.pathPoints.Length == 0)
            return false;

        int initialSteps = Mathf.Max(0, dieValue - 1);
        int landingIndex = (startIndex + initialSteps) % board.pathPoints.Length;

        if (board.HasOwnPieceAt(playerIndex, landingIndex))
            return false;

        return true;
    }

    public void SpawnToStart()
    {
        SpawnAndMoveByDice(1);
    }

    public void SpawnAndMoveByDice(int dieValue)
    {
        if (!isMoving)
            StartCoroutine(SpawnAndMoveRoutine(dieValue));
    }

    IEnumerator SpawnAndMoveRoutine(int dieValue)
    {
        EnsureOriginalTransform();
        lastActionPointsAwarded = 0;
        lastActionAwardedScoredKick = false;

        if (!CanSpawn(dieValue))
        {
            isMoving = false;
            UnselectPiece();
            yield break;
        }

        isMoving = true;
        SetHighlight(false);
        UnselectPiece();

        isInStable = false;
        isInHomePath = false;
        isFinished = false;
        currentIndex = startIndex;
        homeIndex = -1;
        stepsMoved = 0;
        hasPassedCheckpoint = false;
        hasNemesisMark = false;
        hasSpawnProtection = false;

        transform.position = board.pathPoints[startIndex].position;
        transform.localScale = originalScale;
        PlayMoveSound();

        int extraSteps = Mathf.Max(0, dieValue - 1);

        if (extraSteps == 0)
        {
            // Spawn with die = 1 lands at startIndex: pushes enemy to stable without awarding points or combo
            PieceController enemyAtStart = board.GetEnemyPieceAt(playerIndex, startIndex);
            if (enemyAtStart != null)
            {
                PlayKickSound();
                enemyAtStart.ReturnToStable(false);
                board.lastFeedMessage = $"{(PlayerColor)playerIndex} xuất quân đẩy {(PlayerColor)enemyAtStart.playerIndex} về chuồng (0đ)";
            }

            yield return new WaitForSeconds(StepSettleDelay);
            isMoving = false;
            isSelected = false;
            UpdateStatusBadgeRing();
            SyncStateToClients();
            yield break;
        }

        yield return new WaitForSeconds(StepSettleDelay);
        yield return WalkMainTrackAndResolveRoutine(extraSteps);
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
            transform.position = board.GetHomePath(playerIndex)[Mathf.Clamp(homeIndex, 0, board.GetHomePath(playerIndex).Length - 1)].position;
        else if (currentIndex >= 0 && currentIndex < board.pathPoints.Length)
            transform.position = board.pathPoints[currentIndex].position;

        isSelected = false;
    }

    public bool CanMove(int step)
    {
        if (isInStable || isMoving || step <= 0)
            return false;

        if (board == null || board.pathPoints == null || board.pathPoints.Length == 0)
            return false;

        // If the move reaches or exceeds maxLoopSteps, the piece enters Home Cash-in Zone and immediately scores + returns to stable
        if (stepsMoved + step > maxLoopSteps)
            return true;

        int landingIndex = (currentIndex + step) % board.pathPoints.Length;
        if (board.HasOwnPieceAt(playerIndex, landingIndex))
            return false;

        return true;
    }

    public void MoveByStep(int step)
    {
        if (!isMoving)
            StartCoroutine(MoveByStepRoutine(step));
    }

    IEnumerator MoveByStepRoutine(int step)
    {
        lastActionPointsAwarded = 0;
        lastActionAwardedScoredKick = false;

        if (!CanMove(step))
        {
            Debug.Log("cannot move");
            isMoving = false;
            UnselectPiece();
            yield break;
        }

        isMoving = true;
        hasSpawnProtection = false;
        SetHighlight(false);
        UnselectPiece();

        yield return WalkMainTrackAndResolveRoutine(step);
    }

    IEnumerator WalkMainTrackAndResolveRoutine(int step)
    {
        PieceSkinApplicator skinApplicator = GetComponent<PieceSkinApplicator>();
        float movementSpeed = skinApplicator != null ? Mathf.Max(6.5f, skinApplicator.MovementSpeed) : 6.5f;

        BroadcastSkinMovementBegin();

        if (skinApplicator != null)
            yield return skinApplicator.PrepareMovement();

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

                    if (stepsMoved >= DiceRuleUtility.CheckpointStep && !hasPassedCheckpoint)
                    {
                        hasPassedCheckpoint = true;
                        lastActionPointsAwarded += board.AwardCheckpointScore(playerIndex);
                    }
                }
            }
            else
            {
                if (homeIndex < homePath.Length - 1)
                {
                    homeIndex++;
                }
                else
                {
                    // Reached the deepest home tile (tile 6); discard excess steps
                    break;
                }
            }

            Vector3 targetPos = isInHomePath
                ? homePath[Mathf.Clamp(homeIndex, 0, homePath.Length - 1)].position
                : board.pathPoints[currentIndex].position;

            yield return StepToPositionRoutine(skinApplicator, targetPos, movementSpeed);
        }

        // Check Dash Tile (+3 steps) if piece stopped on a Dash Tile on the main loop
        if (!isInHomePath && board.IsDashTile(currentIndex))
        {
            int dashSteps = DiceRuleUtility.DashTileSteps;
            if (stepsMoved + dashSteps <= maxLoopSteps)
            {
                int dashTargetIndex = (currentIndex + dashSteps) % board.pathPoints.Length;
                if (!board.HasOwnPieceAt(playerIndex, dashTargetIndex))
                {
                    board.lastFeedMessage = $"{(PlayerColor)playerIndex} đạp Ô Tăng Tốc (+{dashSteps} bước)!";

                    for (int d = 0; d < dashSteps; d++)
                    {
                        currentIndex = (currentIndex + 1) % board.pathPoints.Length;
                        stepsMoved++;

                        if (stepsMoved >= DiceRuleUtility.CheckpointStep && !hasPassedCheckpoint)
                        {
                            hasPassedCheckpoint = true;
                            lastActionPointsAwarded += board.AwardCheckpointScore(playerIndex);
                        }

                        Vector3 dashPos = board.pathPoints[currentIndex].position;
                        yield return StepToPositionRoutine(skinApplicator, dashPos, movementSpeed * 1.25f);
                    }
                }
            }
        }

        if (skinApplicator != null)
        {
            BroadcastSkinMovementEnd();
            skinApplicator.SetWalking(false);
            yield return skinApplicator.FinishMovement();
        }

        if (isInHomePath)
        {
            PlayFinishSound();
            int cashInPts = board.AwardHomeCashInScore(playerIndex, homeIndex);
            lastActionPointsAwarded += cashInPts;

            SyncStateToClients();
            yield return new WaitForSeconds(0.22f);

            // Instant Cash-in: return piece to stable for reuse on next turn
            ReturnToStable(true);
            yield break;
        }

        ResolveLandingKick();
        transform.localScale = originalScale;
        isMoving = false;
        isSelected = false;

        UpdateStatusBadgeRing();
        SyncStateToClients();
    }

    IEnumerator StepToPositionRoutine(PieceSkinApplicator skinApplicator, Vector3 targetPos, float movementSpeed)
    {
        if (skinApplicator != null)
        {
            BroadcastSkinDirection(targetPos - transform.position);
            yield return skinApplicator.TurnTowards(targetPos - transform.position);
            skinApplicator.SetWalking(true);
        }

        while (Vector3.Distance(transform.position, targetPos) > 0.01f)
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                targetPos,
                movementSpeed * Time.deltaTime
            );

            yield return null;
        }

        transform.position = targetPos;
        PlayMoveSound();
        yield return new WaitForSeconds(StepSettleDelay);
    }

    void ResolveLandingKick()
    {
        if (isInHomePath || isInStable || board == null)
            return;

        PieceController enemy = board.GetEnemyPieceAt(playerIndex, currentIndex);
        if (enemy == null)
            return;

        Debug.Log("kick enemy: " + enemy.name);
        PlayKickSound();

        int kickPoints = board.AwardKickScore(playerIndex, enemy);
        lastActionPointsAwarded += kickPoints;

        enemy.ReturnToStable(false);

        if (kickPoints > 0)
        {
            hasNemesisMark = true;
            lastActionAwardedScoredKick = true;
        }
    }

    void PlayMoveSound()
    {
        if (NetworkSoundManager.Instance != null)
            NetworkSoundManager.Instance.PlayMoveSoundRpc();
        else if (SoundManager.Instance != null)
            SoundManager.Instance.PlayMove();
    }

    void PlayKickSound()
    {
        if (NetworkSoundManager.Instance != null)
            NetworkSoundManager.Instance.PlayKickSoundRpc();
        else if (SoundManager.Instance != null)
            SoundManager.Instance.PlayKick();
    }

    void PlayFinishSound()
    {
        if (NetworkSoundManager.Instance != null)
            NetworkSoundManager.Instance.PlayFinishSoundRpc();
        else if (SoundManager.Instance != null)
            SoundManager.Instance.PlayFinish();
    }

    void BroadcastSkinMovementBegin()
    {
        if (CanBroadcastSkinVisuals())
            BeginSkinMovementRpc();
    }

    void BroadcastSkinDirection(Vector3 direction)
    {
        if (CanBroadcastSkinVisuals())
            UpdateSkinDirectionRpc(direction);
    }

    void BroadcastSkinMovementEnd()
    {
        if (CanBroadcastSkinVisuals())
            EndSkinMovementRpc();
    }

    bool CanBroadcastSkinVisuals()
    {
        return NetworkManager.Singleton != null &&
               NetworkManager.Singleton.IsListening &&
               IsSpawned &&
               IsServer;
    }

    [Rpc(SendTo.ClientsAndHost)]
    void BeginSkinMovementRpc()
    {
        if (IsServer)
            return;

        SetHighlight(false);

        if (remoteSkinTransitionRoutine != null)
            StopCoroutine(remoteSkinTransitionRoutine);

        remoteSkinTransitionRoutine = StartCoroutine(RemotePrepareSkinMovement());
    }

    [Rpc(SendTo.ClientsAndHost)]
    void UpdateSkinDirectionRpc(Vector3 direction)
    {
        if (!IsServer)
            StartCoroutine(RemoteUpdateSkinDirection(direction));
    }

    [Rpc(SendTo.ClientsAndHost)]
    void EndSkinMovementRpc()
    {
        if (IsServer)
            return;

        if (remoteSkinTransitionRoutine != null)
            StopCoroutine(remoteSkinTransitionRoutine);

        remoteSkinTransitionRoutine = StartCoroutine(RemoteFinishSkinMovement());
    }

    IEnumerator RemotePrepareSkinMovement()
    {
        PieceSkinApplicator skinApplicator = GetComponent<PieceSkinApplicator>();
        if (skinApplicator != null)
            yield return skinApplicator.PrepareMovement();

        remoteSkinTransitionRoutine = null;
    }

    IEnumerator RemoteUpdateSkinDirection(Vector3 direction)
    {
        PieceSkinApplicator skinApplicator = GetComponent<PieceSkinApplicator>();
        if (skinApplicator == null)
            yield break;

        yield return skinApplicator.TurnTowards(direction);
        skinApplicator.SetWalking(true);
    }

    IEnumerator RemoteFinishSkinMovement()
    {
        PieceSkinApplicator skinApplicator = GetComponent<PieceSkinApplicator>();
        if (skinApplicator != null)
        {
            skinApplicator.SetWalking(false);
            yield return skinApplicator.FinishMovement();
        }

        remoteSkinTransitionRoutine = null;
    }

    public void ReturnToStable(bool cashedInThisTurn = false)
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
        hasPassedCheckpoint = false;
        hasNemesisMark = false;
        hasSpawnProtection = false;
        justCashedInThisTurn = cashedInThisTurn;

        PieceSkinApplicator skinApplicator = GetComponent<PieceSkinApplicator>();
        if (skinApplicator != null)
            skinApplicator.ResetMotion();

        transform.position = stablePosition;
        transform.localScale = originalScale;

        UpdateStatusBadgeRing();
        SyncStateToClients();
    }

    // Kept as a safe no-op for backward compatibility; kicks are resolved once in WalkMainTrackAndResolveRoutine
    public void CheckKickEnemy()
    {
    }

    public bool CanClimbHome()
    {
        return false;
    }

    public void ClimbHomeOneStep()
    {
    }

    public void RefreshStatusVisualsAndSync()
    {
        UpdateStatusBadgeRing();
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
            hasPassedCheckpoint,
            hasNemesisMark,
            justCashedInThisTurn,
            hasSpawnProtection,
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
        bool syncedHasPassedCheckpoint,
        bool syncedHasNemesisMark,
        bool syncedJustCashedIn,
        bool syncedHasSpawnProtection,
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
        hasPassedCheckpoint = syncedHasPassedCheckpoint;
        hasNemesisMark = syncedHasNemesisMark;
        justCashedInThisTurn = syncedJustCashedIn;
        hasSpawnProtection = syncedHasSpawnProtection;
        transform.position = syncedPosition;
        transform.localScale = syncedScale;

        UpdateStatusBadgeRing();
    }

    void UpdateStatusBadgeRing()
    {
        bool showRing = !isInStable && (hasNemesisMark || hasPassedCheckpoint);

        if (!showRing)
        {
            if (statusRingRenderer != null)
                statusRingRenderer.enabled = false;
            return;
        }

        EnsureStatusRing();
        if (statusRingRenderer == null || statusRingMaterial == null)
            return;

        Color ringColor = hasNemesisMark
            ? new Color(1f, 0.15f, 0.2f, 0.92f)   // Crimson Red for Nemesis Mark (+1 Revenge Bonus)
            : new Color(1f, 0.72f, 0.1f, 0.88f);  // Gold/Amber for Elite Piece (3-point Bounty)

        SetMaterialColor(statusRingMaterial, ringColor);
        statusRingRenderer.enabled = true;
    }

    void EnsureStatusRing()
    {
        if (statusRingObject != null)
            return;

        Shader shader = FindHighlightShader();
        if (shader == null)
            return;

        statusRingMaterial = new Material(shader);
        ConfigureTransparentMaterial(statusRingMaterial);

        statusRingObject = new GameObject("PieceStatusRing");
        SetIgnoreRaycastLayer(statusRingObject);
        statusRingObject.transform.SetParent(transform, false);
        statusRingObject.transform.position = transform.position + new Vector3(0f, 0.016f, 0f);
        statusRingObject.transform.rotation = Quaternion.identity;
        float invScale = 1f / Mathf.Max(0.001f, transform.lossyScale.x);
        statusRingObject.transform.localScale = Vector3.one * invScale;

        statusRingRenderer = statusRingObject.AddComponent<LineRenderer>();
        statusRingRenderer.useWorldSpace = false;
        statusRingRenderer.loop = true;
        statusRingRenderer.positionCount = 28;
        statusRingRenderer.widthMultiplier = 0.008f;
        statusRingRenderer.sharedMaterial = statusRingMaterial;
        statusRingRenderer.shadowCastingMode = ShadowCastingMode.Off;
        statusRingRenderer.receiveShadows = false;

        float r = 0.044f;
        for (int i = 0; i < 28; i++)
        {
            float angle = (Mathf.PI * 2f * i) / 28f;
            statusRingRenderer.SetPosition(i, new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r));
        }
    }

    void EnsureSelectionRing()
    {
        if (selectionRingObject != null)
            return;

        Shader shader = FindHighlightShader();
        if (shader == null)
            return;

        selectionRingMaterial = new Material(shader);
        ConfigureTransparentMaterial(selectionRingMaterial);
        SetMaterialColor(selectionRingMaterial, new Color(1f, 0.92f, 0.12f, 0.9f));

        selectionRingObject = new GameObject("PieceSelectionRing");
        SetIgnoreRaycastLayer(selectionRingObject);
        selectionRingObject.transform.SetParent(transform, false);
        selectionRingObject.transform.position = transform.position + new Vector3(0f, 0.012f, 0f);
        selectionRingObject.transform.rotation = Quaternion.identity;
        float invScale = 1f / Mathf.Max(0.001f, transform.lossyScale.x);
        selectionRingObject.transform.localScale = Vector3.one * invScale;

        selectionRingRenderer = selectionRingObject.AddComponent<LineRenderer>();
        selectionRingRenderer.useWorldSpace = false;
        selectionRingRenderer.loop = true;
        selectionRingRenderer.positionCount = 32;
        selectionRingRenderer.widthMultiplier = 0.011f;
        selectionRingRenderer.sharedMaterial = selectionRingMaterial;
        selectionRingRenderer.shadowCastingMode = ShadowCastingMode.Off;
        selectionRingRenderer.receiveShadows = false;
        selectionRingRenderer.enabled = false;

        float r = 0.052f;
        for (int i = 0; i < 32; i++)
        {
            float angle = (Mathf.PI * 2f * i) / 32f;
            selectionRingRenderer.SetPosition(i, new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r));
        }
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
        EnsureSelectionRing();

        if (outlineObjects.Count == 0 && selectionRingRenderer == null)
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
            float alpha = Mathf.Lerp(0.38f, 0.95f, pulse);
            float scale = Mathf.Lerp(1.04f, 1.14f, pulse);

            transform.localScale = originalScale * Mathf.Lerp(1f, 1.07f, pulse);
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

        if (selectionRingRenderer != null)
        {
            selectionRingRenderer.enabled = visible;
            if (selectionRingMaterial != null)
                SetMaterialColor(selectionRingMaterial, new Color(1f, 0.92f, 0.12f, Mathf.Clamp01(alpha + 0.12f)));
        }

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
