using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;

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

    [Header("Party Clash v0.2 Runtime State")]
    public int[] playerScores = new int[4];
    public int[] playerHomeCounts = new int[4];
    public int[] playerKickCounts = new int[4];

    public int currentRound = 1;
    public PartyEventType activeEvent = PartyEventType.None;
    public PartyEventType upcomingEvent = PartyEventType.None;
    public int bountyTargetPlayer = -1;
    public bool[] bountyClaimedThisRound = new bool[4];
    public bool bountyTargetWasKickedThisRound = false;
    public bool[] homeRushUsedThisRound = new bool[4];
    public string lastEventBannerText = "";
    public string lastFeedMessage = "";

    private PartyEventType nextScheduledEventType = PartyEventType.BountyHunt;
    private readonly List<GameObject> dashTileMarkers = new List<GameObject>();

    void Start()
    {
        ApplyPlayerSkins();
        CreateDashTileVisuals();
        VietnameseFloorAtmosphere.EnsureCreated();
        AnimeKickCutscene.EnsureCreated();

        int count = Mathf.Clamp(PlayerPrefs.GetInt("PlayerCount", 2), 2, 4);
        InitializePartyClashMatch(count);
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

    public void InitializePartyClashMatch(int playerCount)
    {
        playerCount = Mathf.Clamp(playerCount, 2, 4);

        for (int i = 0; i < 4; i++)
        {
            playerFinishCount[i] = 0;
            playerScores[i] = 0;
            playerHomeCounts[i] = 0;
            playerKickCounts[i] = 0;
            bountyClaimedThisRound[i] = false;
            homeRushUsedThisRound[i] = false;
        }

        currentRound = 1;
        activeEvent = PartyEventType.None;
        upcomingEvent = PartyEventType.None;
        bountyTargetPlayer = -1;
        bountyTargetWasKickedThisRound = false;
        nextScheduledEventType = PartyEventType.BountyHunt;
        UpdateEventBannerText(playerCount);

        for (int p = 0; p < 4; p++)
        {
            List<PieceController> playerPieces = GetPiecesForPlayer(p);
            playerPieces.Sort((a, b) => a.pieceId.CompareTo(b.pieceId));

            bool participating = p < playerCount && IsPlayerParticipating(p);

            for (int idx = 0; idx < playerPieces.Count; idx++)
            {
                PieceController piece = playerPieces[idx];
                if (piece == null)
                    continue;

                piece.gameObject.SetActive(participating);

                if (!participating)
                {
                    piece.ReturnToStable(false);
                    continue;
                }

                if (idx == 0)
                {
                    // Piece 0 starts at +0 with Turn-1 spawn protection until it moves
                    piece.PlaceOnBoardAtOffset(0, true);
                }
                else if (idx == 1)
                {
                    // Piece 1 starts at +3 on the common track
                    piece.PlaceOnBoardAtOffset(DiceRuleUtility.InitialFrontPieceOffset, false);
                }
                else
                {
                    piece.ReturnToStable(false);
                }
            }
        }
    }

    public List<PieceController> GetPiecesForPlayer(int playerIndex)
    {
        List<PieceController> list = new List<PieceController>();
        if (allPieces == null)
            return list;

        foreach (PieceController piece in allPieces)
        {
            if (piece != null && piece.playerIndex == playerIndex)
                list.Add(piece);
        }

        return list;
    }

    public bool IsPlayerParticipating(int playerIndex)
    {
        if (playerIndex < 0 || playerIndex >= 4)
            return false;

        int maxPlayers = PlayerPrefs.GetInt("PlayerCount", 2);
        if (NetworkTurnManager.Instance != null && NetworkTurnManager.Instance.IsSpawned)
            maxPlayers = NetworkTurnManager.Instance.playerCount.Value;
        else if (TurnManager.Instance != null)
            maxPlayers = TurnManager.Instance.playerCount;

        if (playerIndex >= maxPlayers)
            return false;

        if (NetworkRoomControlManager.Instance != null &&
            NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsListening)
        {
            if (!NetworkRoomControlManager.Instance.IsPlayerActive(playerIndex))
                return false;
        }

        if (NetworkPlayerSlotManager.Instance != null &&
            NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsListening &&
            NetworkManager.Singleton.IsServer)
        {
            if (!NetworkPlayerSlotManager.Instance.IsPlayerConnected(playerIndex))
                return false;
        }

        return true;
    }

    public void OnPlayerTurnStarted(int playerIndex)
    {
        if (allPieces == null)
            return;

        foreach (PieceController piece in allPieces)
        {
            if (piece == null || piece.playerIndex != playerIndex)
                continue;

            // Pieces that cashed in on the previous turn can now be deployed again.
            // Nemesis mark expires once a full round has passed back to the owner's turn.
            bool changed = piece.justCashedInThisTurn || piece.hasNemesisMark;
            piece.justCashedInThisTurn = false;
            piece.hasNemesisMark = false;

            if (changed)
                piece.RefreshStatusVisualsAndSync();
        }
    }

    public int CountBoardPieces(int playerIndex)
    {
        int count = 0;
        if (allPieces == null)
            return 0;

        foreach (PieceController piece in allPieces)
        {
            if (piece != null &&
                piece.gameObject.activeInHierarchy &&
                piece.playerIndex == playerIndex &&
                !piece.isInStable &&
                !piece.isInHomePath)
            {
                count++;
            }
        }

        return count;
    }

    public bool HasSpawnablePiece(int playerIndex, int dieValue)
    {
        if (!DiceRuleUtility.CanDieSpawnPiece(dieValue) || allPieces == null)
            return false;

        foreach (PieceController piece in allPieces)
        {
            if (piece != null &&
                piece.gameObject.activeInHierarchy &&
                piece.playerIndex == playerIndex &&
                piece.isInStable &&
                piece.CanSpawn(dieValue))
            {
                return true;
            }
        }

        return false;
    }

    public bool RequiresMandatorySpawn(int playerIndex, int dieValue)
    {
        if (!DiceRuleUtility.CanDieSpawnPiece(dieValue))
            return false;

        if (CountBoardPieces(playerIndex) >= DiceRuleUtility.MinBoardPresence)
            return false;

        return HasSpawnablePiece(playerIndex, dieValue);
    }

    public bool IsPieceActionValid(PieceController piece, TurnActionSlot slot, int stepValue)
    {
        if (piece == null ||
            !piece.gameObject.activeInHierarchy ||
            piece.isMoving ||
            slot == TurnActionSlot.None ||
            stepValue <= 0)
            return false;

        if (!IsPlayerParticipating(piece.playerIndex))
            return false;

        if (slot == TurnActionSlot.Bonus3)
        {
            if (piece.isInStable)
                return false;

            return piece.CanMove(DiceRuleUtility.BonusMoveSteps);
        }

        bool mustSpawn = RequiresMandatorySpawn(piece.playerIndex, stepValue);

        if (piece.isInStable)
        {
            return piece.CanSpawn(stepValue);
        }

        if (mustSpawn)
        {
            return false;
        }

        return piece.CanMove(stepValue);
    }

    public bool HasAnyValidPieceForSlot(int playerIndex, TurnActionSlot slot, int stepValue)
    {
        if (slot == TurnActionSlot.None || stepValue <= 0 || allPieces == null)
            return false;

        foreach (PieceController piece in allPieces)
        {
            if (piece != null &&
                piece.playerIndex == playerIndex &&
                IsPieceActionValid(piece, slot, stepValue))
            {
                return true;
            }
        }

        return false;
    }

    public bool HasOwnPieceAt(int playerIndex, int tileIndex)
    {
        if (allPieces == null)
            return false;

        foreach (PieceController piece in allPieces)
        {
            if (piece != null &&
                piece.gameObject.activeInHierarchy &&
                IsPlayerParticipating(piece.playerIndex) &&
                piece.playerIndex == playerIndex &&
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
        if (allPieces == null)
            return false;

        foreach (PieceController piece in allPieces)
        {
            if (piece != null &&
                piece.gameObject.activeInHierarchy &&
                piece.playerIndex == playerIndex &&
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
        if (allPieces == null)
            return null;

        foreach (PieceController piece in allPieces)
        {
            if (piece != null &&
                piece.gameObject.activeInHierarchy &&
                IsPlayerParticipating(piece.playerIndex) &&
                !piece.isInStable &&
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

    // In Party Clash v0.2, there are no safe tiles on the common loop; former safe tiles act as Dash Tiles (+3).
    public bool IsSafeTile(int tileIndex)
    {
        return false;
    }

    public bool IsDashTile(int tileIndex)
    {
        if (tileIndex < 0 || pathPoints == null || tileIndex >= pathPoints.Length)
            return false;

        if (DiceRuleUtility.IsDefaultDashTileIndex(tileIndex))
            return true;

        if (tiles != null && tileIndex < tiles.Length && tiles[tileIndex] != null)
            return tiles[tileIndex].isSafeTile;

        return false;
    }

    public PieceController GetEnemyPieceAt(int playerIndex, int tileIndex)
    {
        if (allPieces == null)
            return null;

        foreach (PieceController piece in allPieces)
        {
            if (piece != null &&
                piece.gameObject.activeInHierarchy &&
                IsPlayerParticipating(piece.playerIndex) &&
                !piece.isInStable &&
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
        return 5;
    }

    public void AddFinishedPiece(int playerIndex)
    {
        if (playerIndex < 0 || playerIndex >= 4)
            return;

        playerFinishCount[playerIndex]++;
        playerHomeCounts[playerIndex] = playerFinishCount[playerIndex];
    }

    public int AwardCheckpointScore(int playerIndex)
    {
        if (playerIndex < 0 || playerIndex >= 4)
            return 0;

        int pts = activeEvent == PartyEventType.HomeRush
            ? DiceRuleUtility.CheckpointPoints * 2
            : DiceRuleUtility.CheckpointPoints;

        playerScores[playerIndex] += pts;
        lastFeedMessage = $"{(PlayerColor)playerIndex} qua mốc giữa bàn (+{pts}đ)";
        GameLogBridge.Send($"[CHECKPOINT ELITE] Quân của {(PlayerColor)playerIndex} vượt mốc bước 28 -> Tiến hóa Tinh Nhuệ (+{pts}đ | Tổng: {playerScores[playerIndex]}đ)", "CHECKPOINT");
        return pts;
    }

    public int AwardHomeCashInScore(int playerIndex, int homeIndex)
    {
        if (playerIndex < 0 || playerIndex >= 4)
            return 0;

        int pts = DiceRuleUtility.GetHomeCashInPoints(homeIndex);
        bool rushBonus = false;

        if (activeEvent == PartyEventType.HomeRush && !homeRushUsedThisRound[playerIndex])
        {
            homeRushUsedThisRound[playerIndex] = true;
            pts += DiceRuleUtility.HomeRushBonusPoints;
            rushBonus = true;
        }

        playerScores[playerIndex] += pts;
        playerHomeCounts[playerIndex]++;
        playerFinishCount[playerIndex] = playerHomeCounts[playerIndex];

        string tierLabel = homeIndex >= 3 ? "Về Đích Sâu" : "Về Đích Nông";
        string bonusLabel = rushBonus ? " + Nước Rút!" : "";
        lastFeedMessage = $"{(PlayerColor)playerIndex} {tierLabel}{bonusLabel} (+{pts}đ)";
        GameLogBridge.Send($"[VỀ ĐÍCH THU ĐIỂM] {(PlayerColor)playerIndex} {tierLabel}{bonusLabel} tại ô đích #{homeIndex + 1} -> +{pts}đ (Tổng: {playerScores[playerIndex]}đ, Quân tái sử dụng về chuồng)", "HOME_CASHIN");
        return pts;
    }

    public int AwardKickScore(int attackerIndex, PieceController victimPiece)
    {
        if (attackerIndex < 0 || attackerIndex >= 4 || victimPiece == null)
            return 0;

        if (victimPiece.hasSpawnProtection)
        {
            lastFeedMessage = $"{(PlayerColor)attackerIndex} đẩy {(PlayerColor)victimPiece.playerIndex} khỏi mốc khởi động (0đ)";
            GameLogBridge.Send($"[ĐẨY LÙI KHIÊN ĐẦU TRẬN] {(PlayerColor)attackerIndex} đẩy quân có khiên của {(PlayerColor)victimPiece.playerIndex} về chuồng (0đ)", "SHIELD_PUSH");
            return 0;
        }

        int basePts = victimPiece.hasPassedCheckpoint
            ? DiceRuleUtility.EliteKickPoints
            : DiceRuleUtility.RookieKickPoints;

        int revengePts = victimPiece.hasNemesisMark
            ? DiceRuleUtility.RevengeBonusPoints
            : 0;

        int bountyHuntPts = 0;
        if (activeEvent == PartyEventType.BountyHunt &&
            victimPiece.playerIndex == bountyTargetPlayer &&
            attackerIndex != bountyTargetPlayer)
        {
            bountyTargetWasKickedThisRound = true;
            if (!bountyClaimedThisRound[attackerIndex])
            {
                bountyClaimedThisRound[attackerIndex] = true;
                bountyHuntPts = DiceRuleUtility.BountyHuntBonusPoints;
            }
        }

        int totalPts = basePts + revengePts + bountyHuntPts;
        playerScores[attackerIndex] += totalPts;
        playerKickCounts[attackerIndex]++;

        string detail = victimPiece.hasPassedCheckpoint ? "Tinh Nhuệ" : "Tân Binh";
        if (revengePts > 0) detail += "+Phục Hận";
        if (bountyHuntPts > 0) detail += "+Truy Nã";

        lastFeedMessage = $"{(PlayerColor)attackerIndex} đá {(PlayerColor)victimPiece.playerIndex} [{detail}] (+{totalPts}đ)";
        GameLogBridge.Send($"[ĐÁ QUÂN - BOUNTY] {(PlayerColor)attackerIndex} đá hạ quân {(PlayerColor)victimPiece.playerIndex} [{detail}] -> +{totalPts}đ & Mở khóa Combo +3! (Tổng: {playerScores[attackerIndex]}đ)", "KICK_SCORE");
        return totalPts;
    }

    public void AdvanceRoundAndEvents(int playerCount)
    {
        playerCount = Mathf.Clamp(playerCount, 2, 4);

        // If Bounty Hunt just finished and the target survived without being kicked, grant +2 Survival points
        if (activeEvent == PartyEventType.BountyHunt &&
            bountyTargetPlayer >= 0 &&
            bountyTargetPlayer < 4 &&
            IsPlayerParticipating(bountyTargetPlayer) &&
            !bountyTargetWasKickedThisRound)
        {
            playerScores[bountyTargetPlayer] += DiceRuleUtility.BountyHuntBonusPoints;
            lastFeedMessage = $"{(PlayerColor)bountyTargetPlayer} sống sót qua vòng Truy Nã (+{DiceRuleUtility.BountyHuntBonusPoints}đ)!";
        }

        currentRound++;

        for (int i = 0; i < 4; i++)
        {
            bountyClaimedThisRound[i] = false;
            homeRushUsedThisRound[i] = false;
        }
        bountyTargetWasKickedThisRound = false;

        int normalRoundsPerCycle = playerCount >= 4 ? 2 : 3;
        int cycleLength = normalRoundsPerCycle + 1;
        int posInCycle = (currentRound - 1) % cycleLength;

        if (posInCycle == normalRoundsPerCycle)
        {
            // Activate the announced event for this round
            activeEvent = upcomingEvent != PartyEventType.None ? upcomingEvent : nextScheduledEventType;
            upcomingEvent = PartyEventType.None;

            if (activeEvent == PartyEventType.BountyHunt)
            {
                if (bountyTargetPlayer < 0 || !IsPlayerParticipating(bountyTargetPlayer) || playerScores[bountyTargetPlayer] <= 0)
                {
                    bountyTargetPlayer = FindLeadingPlayerWithScore(playerCount);
                }

                if (bountyTargetPlayer < 0)
                {
                    // Fallback to Home Rush if nobody has scored yet
                    activeEvent = PartyEventType.HomeRush;
                }
            }

            nextScheduledEventType = activeEvent == PartyEventType.BountyHunt
                ? PartyEventType.HomeRush
                : PartyEventType.BountyHunt;
        }
        else if (posInCycle == normalRoundsPerCycle - 1)
        {
            // Warning round (1 full round before activation)
            activeEvent = PartyEventType.None;
            upcomingEvent = nextScheduledEventType;

            if (upcomingEvent == PartyEventType.BountyHunt)
            {
                bountyTargetPlayer = FindLeadingPlayerWithScore(playerCount);
                if (bountyTargetPlayer < 0)
                    upcomingEvent = PartyEventType.HomeRush;
            }
            else
            {
                bountyTargetPlayer = -1;
            }
        }
        else
        {
            activeEvent = PartyEventType.None;
            upcomingEvent = PartyEventType.None;
            bountyTargetPlayer = -1;
        }

        UpdateEventBannerText(playerCount);
        if (activeEvent != PartyEventType.None)
            GameLogBridge.Send($"[BIẾN CỐ TOÀN BÀN] {lastEventBannerText}", "GLOBAL_EVENT");
    }

    int FindLeadingPlayerWithScore(int playerCount)
    {
        int bestPlayer = -1;
        int bestScore = 0;

        for (int i = 0; i < playerCount; i++)
        {
            if (!IsPlayerParticipating(i))
                continue;

            if (playerScores[i] > bestScore)
            {
                bestScore = playerScores[i];
                bestPlayer = i;
            }
            else if (bestScore > 0 && playerScores[i] == bestScore && bestPlayer >= 0)
            {
                if (ComparePlayersForTieBreak(i, bestPlayer) < 0)
                    bestPlayer = i;
            }
        }

        return bestPlayer;
    }

    // Returns < 0 if playerA ranks higher than playerB
    public int ComparePlayersForTieBreak(int playerA, int playerB)
    {
        bool activeA = IsPlayerParticipating(playerA);
        bool activeB = IsPlayerParticipating(playerB);
        if (activeA != activeB)
            return activeA ? -1 : 1;

        if (playerScores[playerA] != playerScores[playerB])
            return playerScores[playerB].CompareTo(playerScores[playerA]);

        if (playerHomeCounts[playerA] != playerHomeCounts[playerB])
            return playerHomeCounts[playerB].CompareTo(playerHomeCounts[playerA]);

        if (playerKickCounts[playerA] != playerKickCounts[playerB])
            return playerKickCounts[playerB].CompareTo(playerKickCounts[playerA]);

        // Later seat index wins final tie-breaker to offset Turn-1 advantage
        return playerB.CompareTo(playerA);
    }

    public int[] GetSortedRankingOrder(int playerCount)
    {
        playerCount = Mathf.Clamp(playerCount, 2, 4);
        List<int> order = new List<int>(playerCount);
        for (int i = 0; i < playerCount; i++)
            order.Add(i);

        order.Sort(ComparePlayersForTieBreak);
        return order.ToArray();
    }

    public void UpdateEventBannerText(int playerCount)
    {
        if (activeEvent == PartyEventType.BountyHunt && bountyTargetPlayer >= 0)
        {
            lastEventBannerText = $"Vòng {currentRound} | BIẾN CỐ: TRUY NÃ {(PlayerColor)bountyTargetPlayer} (Đá +2đ / Sống sót +2đ)";
        }
        else if (activeEvent == PartyEventType.HomeRush)
        {
            lastEventBannerText = $"Vòng {currentRound} | BIẾN CỐ: NƯỚC RÚT (Về nhà đầu +2đ, Checkpoint x2)";
        }
        else if (upcomingEvent == PartyEventType.BountyHunt && bountyTargetPlayer >= 0)
        {
            lastEventBannerText = $"Vòng {currentRound} | Sắp tới (Vòng {currentRound + 1}): TRUY NÃ {(PlayerColor)bountyTargetPlayer}";
        }
        else if (upcomingEvent == PartyEventType.HomeRush)
        {
            lastEventBannerText = $"Vòng {currentRound} | Sắp tới (Vòng {currentRound + 1}): NƯỚC RÚT (+2đ Về nhà)";
        }
        else
        {
            lastEventBannerText = $"Vòng {currentRound} | Đấu Tính Điểm Party Clash";
        }
    }

    public bool IsHomeIndexOccupied(int playerIndex, int homeIndex)
    {
        // In Party Clash v0.2, pieces immediately cash in at home and return to stable, so home is never blocked.
        return false;
    }

    void CreateDashTileVisuals()
    {
        foreach (GameObject oldMarker in dashTileMarkers)
        {
            if (oldMarker != null)
                Destroy(oldMarker);
        }
        dashTileMarkers.Clear();

        if (pathPoints == null)
            return;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
            ?? Shader.Find("Unlit/Color")
            ?? Shader.Find("Sprites/Default");

        if (shader == null)
            return;

        int ignoreLayer = LayerMask.NameToLayer("Ignore Raycast");
        float invParentScale = 1f / Mathf.Max(0.001f, transform.lossyScale.x);

        Material cyanMat = CreateUnlitMarkerMaterial(shader, new Color(0.15f, 0.96f, 0.98f, 0.92f));
        Material checkpointMat = CreateUnlitMarkerMaterial(shader, new Color(1f, 0.78f, 0.16f, 0.85f));
        Material shallowHomeMat = CreateUnlitMarkerMaterial(shader, new Color(0.24f, 0.95f, 0.56f, 0.72f));
        Material deepHomeMat = CreateUnlitMarkerMaterial(shader, new Color(1f, 0.76f, 0.14f, 0.92f));

        // 1. 4 Dash Tiles (+3 steps) at indices 7, 21, 35, 49
        for (int i = 0; i < pathPoints.Length; i++)
        {
            if (!IsDashTile(i) || pathPoints[i] == null)
                continue;

            Vector3 tilePos = pathPoints[i].position + new Vector3(0f, 0.007f, 0f);
            GameObject dashRing = CreatePolygonRingMarker(
                $"DashTileOuter_{i}",
                tilePos,
                invParentScale,
                32,
                0.044f,
                0.0065f,
                cyanMat,
                ignoreLayer
            );
            dashTileMarkers.Add(dashRing);

            GameObject dashDiamond = CreatePolygonRingMarker(
                $"DashTileInner_{i}",
                tilePos + new Vector3(0f, 0.002f, 0f),
                invParentScale,
                4,
                0.026f,
                0.005f,
                cyanMat,
                ignoreLayer
            );
            dashTileMarkers.Add(dashDiamond);

            GameObject dashLabel = CreateFlatTileLabel(
                $"DashTileLabel_{i}",
                tilePos + new Vector3(0f, 0.008f, 0f),
                invParentScale,
                "+3",
                new Color(0.20f, 0.98f, 1f, 0.96f),
                0.82f,
                ignoreLayer
            );
            dashTileMarkers.Add(dashLabel);
        }

        // 2. 4 Start / Checkpoint (Step 28) Gates at indices 0, 14, 28, 42
        int[] gateIndices = { 0, 14, 28, 42 };
        foreach (int gateIdx in gateIndices)
        {
            if (gateIdx < 0 || gateIdx >= pathPoints.Length || pathPoints[gateIdx] == null)
                continue;

            Vector3 gatePos = pathPoints[gateIdx].position + new Vector3(0f, 0.006f, 0f);
            GameObject gateRing = CreatePolygonRingMarker(
                $"CheckpointGate_{gateIdx}",
                gatePos,
                invParentScale,
                8,
                0.047f,
                0.0055f,
                checkpointMat,
                ignoreLayer
            );
            dashTileMarkers.Add(gateRing);
        }

        // 3. Home Cash-in Path Tiers: Shallow (0..2 = +4đ) vs Deep (3..5 = +6đ)
        for (int p = 0; p < 4; p++)
        {
            Transform[] homePath = GetHomePath(p);
            if (homePath == null)
                continue;

            for (int h = 0; h < homePath.Length; h++)
            {
                if (homePath[h] == null)
                    continue;

                bool isDeep = h >= 3;
                Vector3 homePos = homePath[h].position + new Vector3(0f, 0.006f, 0f);
                GameObject homeRing = CreatePolygonRingMarker(
                    $"HomeTier_{p}_{h}",
                    homePos,
                    invParentScale,
                    isDeep ? 24 : 16,
                    isDeep ? 0.040f : 0.034f,
                    isDeep ? 0.006f : 0.0042f,
                    isDeep ? deepHomeMat : shallowHomeMat,
                    ignoreLayer
                );
                dashTileMarkers.Add(homeRing);

                if (h == 0 || h == 3)
                {
                    GameObject tierBadge = CreateFlatTileLabel(
                        $"HomeTierLabel_{p}_{h}",
                        homePos + new Vector3(0f, 0.007f, 0f),
                        invParentScale,
                        isDeep ? "6đ" : "4đ",
                        isDeep ? new Color(1f, 0.85f, 0.20f, 0.96f) : new Color(0.35f, 1f, 0.62f, 0.92f),
                        0.72f,
                        ignoreLayer
                    );
                    dashTileMarkers.Add(tierBadge);
                }
            }
        }
    }

    Material CreateUnlitMarkerMaterial(Shader shader, Color color)
    {
        Material mat = new Material(shader);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        return mat;
    }

    GameObject CreatePolygonRingMarker(
        string markerName,
        Vector3 worldPosition,
        float invParentScale,
        int segments,
        float radius,
        float lineWidth,
        Material sharedMat,
        int ignoreLayer)
    {
        GameObject ringObj = new GameObject(markerName);
        if (ignoreLayer >= 0)
            ringObj.layer = ignoreLayer;

        ringObj.transform.SetParent(transform, false);
        ringObj.transform.position = worldPosition;
        ringObj.transform.rotation = Quaternion.identity;
        ringObj.transform.localScale = Vector3.one * invParentScale;

        LineRenderer lr = ringObj.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = true;
        lr.positionCount = segments;
        lr.widthMultiplier = lineWidth;
        lr.sharedMaterial = sharedMat;
        lr.shadowCastingMode = ShadowCastingMode.Off;
        lr.receiveShadows = false;

        for (int seg = 0; seg < segments; seg++)
        {
            float angle = (Mathf.PI * 2f * seg) / segments;
            lr.SetPosition(seg, new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
        }

        return ringObj;
    }

    GameObject CreateFlatTileLabel(
        string labelName,
        Vector3 worldPosition,
        float invParentScale,
        string content,
        Color color,
        float fontSize,
        int ignoreLayer)
    {
        GameObject go = new GameObject(labelName);
        if (ignoreLayer >= 0)
            go.layer = ignoreLayer;

        go.transform.SetParent(transform, false);
        go.transform.position = worldPosition;
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        go.transform.localScale = Vector3.one * (invParentScale * 0.08f);

        TextMeshPro tmp = go.AddComponent<TextMeshPro>();
        tmp.text = content;
        tmp.fontSize = fontSize;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.color = color;
        tmp.outlineWidth = 0.2f;
        tmp.outlineColor = new Color32(10, 12, 18, 235);

        MeshRenderer mr = go.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.sortingOrder = 10;
        }

        return go;
    }
}
