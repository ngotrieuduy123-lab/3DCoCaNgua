using System.Collections.Generic;
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
        return pts;
    }

    public int AwardKickScore(int attackerIndex, PieceController victimPiece)
    {
        if (attackerIndex < 0 || attackerIndex >= 4 || victimPiece == null)
            return 0;

        if (victimPiece.hasSpawnProtection)
        {
            lastFeedMessage = $"{(PlayerColor)attackerIndex} đẩy {(PlayerColor)victimPiece.playerIndex} khỏi mốc khởi động (0đ)";
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

        Material dashMat = new Material(shader);
        Color cyanGlow = new Color(0.15f, 0.95f, 0.95f, 0.85f);
        if (dashMat.HasProperty("_BaseColor")) dashMat.SetColor("_BaseColor", cyanGlow);
        if (dashMat.HasProperty("_Color")) dashMat.SetColor("_Color", cyanGlow);

        for (int i = 0; i < pathPoints.Length; i++)
        {
            if (!IsDashTile(i) || pathPoints[i] == null)
                continue;

            GameObject ringObj = new GameObject($"DashTileRing_{i}");
            int ignoreLayer = LayerMask.NameToLayer("Ignore Raycast");
            if (ignoreLayer >= 0) ringObj.layer = ignoreLayer;

            ringObj.transform.SetParent(transform, false);
            ringObj.transform.position = pathPoints[i].position + new Vector3(0.012f, 0.06f, -0.024f);

            LineRenderer lr = ringObj.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.positionCount = 32;
            lr.widthMultiplier = 0.006f;
            lr.sharedMaterial = dashMat;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;

            float radius = 0.042f;
            for (int seg = 0; seg < 32; seg++)
            {
                float angle = (Mathf.PI * 2f * seg) / 32f;
                lr.SetPosition(seg, new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
            }

            dashTileMarkers.Add(ringObj);
        }
    }
}
