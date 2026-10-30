using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class NetworkDiceManager : NetworkBehaviour
{
    public NetworkVariable<int> dice1Value = new NetworkVariable<int>(0);
    public NetworkVariable<int> dice2Value = new NetworkVariable<int>(0);
    public NetworkVariable<int> totalValue = new NetworkVariable<int>(0);

    public NetworkVariable<bool> dice1Used = new NetworkVariable<bool>(false);
    public NetworkVariable<bool> dice2Used = new NetworkVariable<bool>(false);
    public NetworkVariable<bool> bonus3Available = new NetworkVariable<bool>(false);
    public NetworkVariable<bool> bonus3Used = new NetworkVariable<bool>(false);

    public TurnActionSlot selectedActionSlot = TurnActionSlot.None;
    public bool earnedBonusThisTurn = false;

    public GameplayUI gameplayUI;
    public BoardManager boardManager;
    public Dice visualDice1;
    public Dice visualDice2;

    public static NetworkDiceManager Instance;

    void Awake()
    {
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        dice1Value.OnValueChanged += OnDiceChanged;
        dice2Value.OnValueChanged += OnDiceChanged;
        totalValue.OnValueChanged += OnDiceChanged;
        dice1Used.OnValueChanged += OnSlotFlagsChanged;
        dice2Used.OnValueChanged += OnSlotFlagsChanged;
        bonus3Available.OnValueChanged += OnSlotFlagsChanged;
        bonus3Used.OnValueChanged += OnSlotFlagsChanged;

        UpdateDiceUI();
        if (boardManager != null && gameplayUI != null)
            gameplayUI.RefreshPartyClashHud(boardManager);
    }

    public override void OnNetworkDespawn()
    {
        dice1Value.OnValueChanged -= OnDiceChanged;
        dice2Value.OnValueChanged -= OnDiceChanged;
        totalValue.OnValueChanged -= OnDiceChanged;
        dice1Used.OnValueChanged -= OnSlotFlagsChanged;
        dice2Used.OnValueChanged -= OnSlotFlagsChanged;
        bonus3Available.OnValueChanged -= OnSlotFlagsChanged;
        bonus3Used.OnValueChanged -= OnSlotFlagsChanged;

        ClearHighlights();
    }

    public void RequestRollDice()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
        {
            DiceManager localDice = FindFirstObjectByType<DiceManager>();
            if (localDice != null)
                localDice.RollBothDice();
            return;
        }

        if (GameManager.Instance != null && GameManager.Instance.IsState(GameState.GameOver))
        {
            gameplayUI.SetMessage("Game over");
            return;
        }

        if (IsServer)
        {
            ulong hostId = NetworkManager.Singleton.LocalClientId;

            if (!CanSenderRoll(hostId))
            {
                Debug.Log("cannot roll now");
                gameplayUI.SetMessage(GetRollRejectMessage(hostId));
                return;
            }

            RollDiceServer();
        }
        else
        {
            RollDiceServerRpc();
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    void RollDiceServerRpc(RpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;

        if (!CanSenderRoll(senderClientId))
        {
            Debug.Log("client cannot roll now");
            ShowMessageToClientRpc(GetRollRejectMessage(senderClientId), senderClientId);
            return;
        }

        RollDiceServer();
    }

    void RollDiceServer()
    {
        int d1 = Random.Range(1, 7);
        int d2 = Random.Range(1, 7);

        if (DatabaseManager.Instance != null && NetworkTurnManager.Instance != null)
            DatabaseManager.Instance.RecordDiceRoll(NetworkTurnManager.Instance.currentPlayerIndex.Value, d1, d2);

        PlayDiceVisualClientRpc(d1, d2);

        if (NetworkSoundManager.Instance != null)
            NetworkSoundManager.Instance.PlayDiceSoundRpc();
        else
            PlayDiceSoundClientRpc();

        earnedBonusThisTurn = false;
        dice1Used.Value = false;
        dice2Used.Value = false;
        bonus3Available.Value = false;
        bonus3Used.Value = false;

        dice1Value.Value = d1;
        dice2Value.Value = d2;
        totalValue.Value = d1 + d2;

        Debug.Log("Network Party Clash dice: " + d1 + " & " + d2);
        EvaluateRemainingActionsServer(true);
    }

    void OnDiceChanged(int oldValue, int newValue)
    {
        EnsureValidLocalSlotSelection();
        UpdateDiceUI();
        RefreshHighlights();
    }

    void OnSlotFlagsChanged(bool oldValue, bool newValue)
    {
        EnsureValidLocalSlotSelection();
        UpdateDiceUI();
        RefreshHighlights();
    }

    public int GetStepForSlot(TurnActionSlot slot)
    {
        switch (slot)
        {
            case TurnActionSlot.Dice1:
                return !dice1Used.Value ? dice1Value.Value : 0;
            case TurnActionSlot.Dice2:
                return !dice2Used.Value ? dice2Value.Value : 0;
            case TurnActionSlot.Bonus3:
                return bonus3Available.Value && !bonus3Used.Value ? DiceRuleUtility.BonusMoveSteps : 0;
            default:
                return 0;
        }
    }

    public int GetSelectedStepValue()
    {
        EnsureValidLocalSlotSelection();
        return GetStepForSlot(selectedActionSlot);
    }

    public bool IsSlotPlayableForPlayer(int playerIndex, TurnActionSlot slot)
    {
        if (boardManager == null)
            return false;

        if (slot == TurnActionSlot.Bonus3 && (!dice1Used.Value || !dice2Used.Value))
            return false;

        int step = GetStepForSlot(slot);
        return step > 0 && boardManager.HasAnyValidPieceForSlot(playerIndex, slot, step);
    }

    void EnsureValidLocalSlotSelection()
    {
        if (totalValue.Value <= 0 || NetworkTurnManager.Instance == null)
        {
            selectedActionSlot = TurnActionSlot.None;
            return;
        }

        int currentPlayer = NetworkTurnManager.Instance.currentPlayerIndex.Value;
        if (IsSlotPlayableForPlayer(currentPlayer, selectedActionSlot))
            return;

        if (IsSlotPlayableForPlayer(currentPlayer, TurnActionSlot.Dice1))
            selectedActionSlot = TurnActionSlot.Dice1;
        else if (IsSlotPlayableForPlayer(currentPlayer, TurnActionSlot.Dice2))
            selectedActionSlot = TurnActionSlot.Dice2;
        else if (IsSlotPlayableForPlayer(currentPlayer, TurnActionSlot.Bonus3))
            selectedActionSlot = TurnActionSlot.Bonus3;
        else
            selectedActionSlot = TurnActionSlot.None;
    }

    public void SelectActionSlot(TurnActionSlot slot)
    {
        if (totalValue.Value <= 0 || NetworkTurnManager.Instance == null)
            return;

        int localPlayer = NetworkPlayerIndexUtility.GetLocalPlayerIndex();
        int currentPlayer = NetworkTurnManager.Instance.currentPlayerIndex.Value;

        if (localPlayer != currentPlayer)
        {
            gameplayUI.SetMessage("Chưa tới lượt của bạn");
            return;
        }

        if (!IsSlotPlayableForPlayer(currentPlayer, slot))
        {
            gameplayUI.SetMessage("Hành động này không có nước đi hợp lệ");
            return;
        }

        selectedActionSlot = slot;
        UpdateDiceUI();
        RefreshHighlights();

        int step = GetStepForSlot(slot);
        if (slot != TurnActionSlot.Bonus3 && boardManager != null && boardManager.RequiresMandatorySpawn(currentPlayer, step))
            gameplayUI.SetMessage("Bắt buộc xuất quân (cần tối thiểu 2 quân trên bàn)");
        else if (slot == TurnActionSlot.Bonus3)
            gameplayUI.SetMessage("Combo Đá! Chọn quân ngoài bàn để đi +3 ô");
        else
            gameplayUI.SetMessage($"Đang chọn {(slot == TurnActionSlot.Dice1 ? "Viên 1" : "Viên 2")} ({step} bước)");
    }

    public void ConsumeSlotAfterMoveServer(TurnActionSlot slot, bool scoredKickOccurred)
    {
        if (!IsServer)
            return;

        if (slot == TurnActionSlot.Dice1)
            dice1Used.Value = true;
        else if (slot == TurnActionSlot.Dice2)
            dice2Used.Value = true;
        else if (slot == TurnActionSlot.Bonus3)
            bonus3Used.Value = true;

        if (scoredKickOccurred && !earnedBonusThisTurn && slot != TurnActionSlot.Bonus3)
        {
            earnedBonusThisTurn = true;
            bonus3Available.Value = true;
        }

        SyncPartyClashBoardStateServer();
        EvaluateRemainingActionsServer(false);
    }

    public void EvaluateRemainingActionsServer(bool isAfterRoll)
    {
        if (!IsServer || boardManager == null || NetworkTurnManager.Instance == null)
            return;

        int currentPlayer = NetworkTurnManager.Instance.currentPlayerIndex.Value;

        bool canUseD1 = !dice1Used.Value && dice1Value.Value > 0 &&
                        boardManager.HasAnyValidPieceForSlot(currentPlayer, TurnActionSlot.Dice1, dice1Value.Value);
        bool canUseD2 = !dice2Used.Value && dice2Value.Value > 0 &&
                        boardManager.HasAnyValidPieceForSlot(currentPlayer, TurnActionSlot.Dice2, dice2Value.Value);

        if (!canUseD1 && !canUseD2)
        {
            dice1Used.Value = true;
            dice2Used.Value = true;
        }

        bool canUseBonus = dice1Used.Value && dice2Used.Value && bonus3Available.Value && !bonus3Used.Value &&
                           boardManager.HasAnyValidPieceForSlot(currentPlayer, TurnActionSlot.Bonus3, DiceRuleUtility.BonusMoveSteps);

        if (dice1Used.Value && dice2Used.Value && bonus3Available.Value && !canUseBonus)
        {
            bonus3Used.Value = true;
        }

        if (canUseD1 || canUseD2 || canUseBonus)
        {
            int remainSum = (!dice1Used.Value ? dice1Value.Value : 0) +
                            (!dice2Used.Value ? dice2Value.Value : 0) +
                            (canUseBonus ? DiceRuleUtility.BonusMoveSteps : 0);
            totalValue.Value = Mathf.Max(1, remainSum);

            TurnActionSlot nextSlot = canUseD1
                ? TurnActionSlot.Dice1
                : (canUseD2 ? TurnActionSlot.Dice2 : TurnActionSlot.Bonus3);

            GameManager.Instance.SetState(GameState.WaitingChoosePiece);
            NotifyActiveSlotClientRpc((int)nextSlot);
            return;
        }

        if (isAfterRoll)
        {
            Debug.Log("network no valid move on roll, skip turn");
            ShowMessageClientRpc("Không có nước đi hợp lệ. Chuyển lượt...");
            ClearHighlights();
            StartCoroutine(SkipTurnAfterDelay());
        }
        else
        {
            ResetNetworkDice();
            NetworkTurnManager.Instance.NextTurn();
            GameManager.Instance.SetState(GameState.WaitingRoll);
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    void NotifyActiveSlotClientRpc(int suggestedSlotInt)
    {
        TurnActionSlot suggested = (TurnActionSlot)suggestedSlotInt;
        if (NetworkTurnManager.Instance != null &&
            IsSlotPlayableForPlayer(NetworkTurnManager.Instance.currentPlayerIndex.Value, suggested))
        {
            selectedActionSlot = suggested;
        }
        else
        {
            EnsureValidLocalSlotSelection();
        }

        UpdateDiceUI();
        RefreshHighlights();

        if (NetworkTurnManager.Instance != null)
        {
            int currentPlayer = NetworkTurnManager.Instance.currentPlayerIndex.Value;
            int localPlayer = NetworkPlayerIndexUtility.GetLocalPlayerIndex();
            if (localPlayer == currentPlayer && selectedActionSlot != TurnActionSlot.None)
            {
                int step = GetStepForSlot(selectedActionSlot);
                if (selectedActionSlot != TurnActionSlot.Bonus3 &&
                    boardManager != null &&
                    boardManager.RequiresMandatorySpawn(currentPlayer, step))
                {
                    gameplayUI.SetMessage("Bắt buộc xuất quân (cần tối thiểu 2 quân trên bàn)");
                }
                else if (selectedActionSlot == TurnActionSlot.Bonus3)
                {
                    gameplayUI.SetMessage("Combo Đá! Chọn quân ngoài bàn để đi +3 ô");
                }
                else
                {
                    gameplayUI.SetMessage($"Chọn quân cho {(selectedActionSlot == TurnActionSlot.Dice1 ? "Viên 1" : "Viên 2")} ({step} bước)");
                }
            }
        }
    }

    public void SyncPartyClashBoardStateServer()
    {
        if (!IsServer || boardManager == null)
            return;

        SyncPartyClashBoardStateClientRpc(
            boardManager.playerScores[0],
            boardManager.playerScores[1],
            boardManager.playerScores[2],
            boardManager.playerScores[3],
            boardManager.playerHomeCounts[0],
            boardManager.playerHomeCounts[1],
            boardManager.playerHomeCounts[2],
            boardManager.playerHomeCounts[3],
            boardManager.playerKickCounts[0],
            boardManager.playerKickCounts[1],
            boardManager.playerKickCounts[2],
            boardManager.playerKickCounts[3],
            boardManager.currentRound,
            (int)boardManager.activeEvent,
            (int)boardManager.upcomingEvent,
            boardManager.bountyTargetPlayer,
            boardManager.lastEventBannerText ?? "",
            boardManager.lastFeedMessage ?? ""
        );
    }

    [Rpc(SendTo.ClientsAndHost)]
    void SyncPartyClashBoardStateClientRpc(
        int s0, int s1, int s2, int s3,
        int h0, int h1, int h2, int h3,
        int k0, int k1, int k2, int k3,
        int round,
        int activeEvt,
        int upcomingEvt,
        int bountyTarget,
        string bannerText,
        string feedMsg)
    {
        if (boardManager == null)
            return;

        boardManager.playerScores[0] = s0;
        boardManager.playerScores[1] = s1;
        boardManager.playerScores[2] = s2;
        boardManager.playerScores[3] = s3;

        boardManager.playerHomeCounts[0] = h0;
        boardManager.playerHomeCounts[1] = h1;
        boardManager.playerHomeCounts[2] = h2;
        boardManager.playerHomeCounts[3] = h3;

        boardManager.playerFinishCount[0] = h0;
        boardManager.playerFinishCount[1] = h1;
        boardManager.playerFinishCount[2] = h2;
        boardManager.playerFinishCount[3] = h3;

        boardManager.playerKickCounts[0] = k0;
        boardManager.playerKickCounts[1] = k1;
        boardManager.playerKickCounts[2] = k2;
        boardManager.playerKickCounts[3] = k3;

        boardManager.currentRound = round;
        boardManager.activeEvent = (PartyEventType)activeEvt;
        boardManager.upcomingEvent = (PartyEventType)upcomingEvt;
        boardManager.bountyTargetPlayer = bountyTarget;
        boardManager.lastEventBannerText = bannerText;
        boardManager.lastFeedMessage = feedMsg;

        if (gameplayUI != null)
            gameplayUI.RefreshPartyClashHud(boardManager);
    }

    void UpdateDiceUI()
    {
        if (gameplayUI == null)
            return;

        gameplayUI.SetDice(dice1Value.Value, dice2Value.Value, totalValue.Value);

        int localPlayer = NetworkPlayerIndexUtility.GetLocalPlayerIndex();
        int currentPlayer = NetworkTurnManager.Instance != null ? NetworkTurnManager.Instance.currentPlayerIndex.Value : -1;
        bool canInteract = localPlayer == currentPlayer && totalValue.Value > 0;

        gameplayUI.SetActionSlots(
            dice1Value.Value,
            dice1Used.Value,
            dice2Value.Value,
            dice2Used.Value,
            bonus3Available.Value,
            bonus3Used.Value,
            selectedActionSlot,
            canInteract
        );

        if (boardManager != null)
            gameplayUI.RefreshPartyClashHud(boardManager);
    }

    public bool IsDouble()
    {
        return false; // Extra turns on doubles are disabled in Party Clash v0.2
    }

    public bool IsOneSix()
    {
        return DiceRuleUtility.IsOneSix(dice1Value.Value, dice2Value.Value);
    }

    public bool CanSpawnPiece()
    {
        return selectedActionSlot != TurnActionSlot.Bonus3 && GetSelectedStepValue() > 0;
    }

    public bool CanClimbHome()
    {
        return GetSelectedStepValue() > 0;
    }

    public void ResetNetworkDice()
    {
        if (IsServer)
        {
            dice1Value.Value = 0;
            dice2Value.Value = 0;
            totalValue.Value = 0;
            dice1Used.Value = false;
            dice2Used.Value = false;
            bonus3Available.Value = false;
            bonus3Used.Value = false;
            earnedBonusThisTurn = false;
        }

        selectedActionSlot = TurnActionSlot.None;
        ClearHighlights();
        UpdateDiceUI();
    }

    bool CanSenderRoll(ulong senderClientId)
    {
        if (GameManager.Instance == null || GameManager.Instance.IsState(GameState.GameOver)) return false;
        int currentPlayer = NetworkTurnManager.Instance.currentPlayerIndex.Value;

        int senderPlayerIndex = NetworkPlayerIndexUtility.GetPlayerIndex(senderClientId);

        if (NetworkRoomControlManager.Instance != null &&
            !NetworkRoomControlManager.Instance.IsPlayerActive(senderPlayerIndex))
            return false;

        if (senderPlayerIndex != currentPlayer)
            return false;

        if (totalValue.Value > 0)
            return false;

        return true;
    }

    string GetRollRejectMessage(ulong senderClientId)
    {
        int currentPlayer = NetworkTurnManager.Instance.currentPlayerIndex.Value;
        int senderPlayerIndex = NetworkPlayerIndexUtility.GetPlayerIndex(senderClientId);

        if (NetworkRoomControlManager.Instance != null &&
            !NetworkRoomControlManager.Instance.IsPlayerActive(senderPlayerIndex))
            return "You are no longer in this room.";

        if (senderPlayerIndex != currentPlayer)
            return "Not your turn";

        if (totalValue.Value > 0)
            return "Choose a highlighted piece";

        return "Cannot roll now";
    }

    [Rpc(SendTo.ClientsAndHost)]
    void ShowMessageClientRpc(string message)
    {
        gameplayUI.SetMessage(message);
    }

    [Rpc(SendTo.ClientsAndHost)]
    void ShowMessageToClientRpc(string message, ulong targetClientId)
    {
        if (NetworkManager.Singleton == null ||
            NetworkManager.Singleton.LocalClientId != targetClientId)
            return;

        gameplayUI.SetMessage(message);
    }

    [Rpc(SendTo.ClientsAndHost)]
    void PlayDiceSoundClientRpc()
    {
        if (SoundManager.Instance != null)
            SoundManager.Instance.PlayDice();
    }

    [Rpc(SendTo.ClientsAndHost)]
    void PlayDiceVisualClientRpc(int d1, int d2)
    {
        StartCoroutine(PlayDiceVisualRoutine(d1, d2));
    }

    IEnumerator PlayDiceVisualRoutine(int d1, int d2)
    {
        visualDice1.RollVisualOnly(d1);
        visualDice2.RollVisualOnly(d2);

        DiceRollPresentation presentation = DiceRollPresentation.EnsureCreated();
        yield return presentation.PlayRoll(d1, d2);

        yield return new WaitUntil(() => !visualDice1.isRolling && !visualDice2.isRolling);

        visualDice1.SetVisualValue(d1);
        visualDice2.SetVisualValue(d2);

        UpdateDiceUI();
    }

    public void RefreshHighlights()
    {
        ClearHighlights();

        if (boardManager == null ||
            NetworkTurnManager.Instance == null ||
            totalValue.Value <= 0)
            return;

        int currentPlayer = NetworkTurnManager.Instance.currentPlayerIndex.Value;
        int localPlayer = NetworkPlayerIndexUtility.GetLocalPlayerIndex();

        if (localPlayer != currentPlayer)
            return;

        if (NetworkRoomControlManager.Instance != null &&
            !NetworkRoomControlManager.Instance.IsPlayerActive(localPlayer))
            return;

        EnsureValidLocalSlotSelection();
        int step = GetStepForSlot(selectedActionSlot);
        if (step <= 0)
            return;

        foreach (PieceController piece in boardManager.allPieces)
        {
            if (piece == null || piece.playerIndex != currentPlayer)
                continue;

            bool canUse = boardManager.IsPieceActionValid(piece, selectedActionSlot, step);
            piece.SetHighlight(canUse);

            if (canUse)
            {
                MovePathHighlighter.Instance.ShowMovePreview(
                    piece,
                    step,
                    selectedActionSlot != TurnActionSlot.Bonus3,
                    true
                );
            }
        }
    }

    public void ClearHighlights()
    {
        MovePathHighlighter.TryClear();

        if (boardManager == null || boardManager.allPieces == null)
            return;

        foreach (PieceController piece in boardManager.allPieces)
        {
            if (piece != null)
                piece.SetHighlight(false);
        }
    }

    IEnumerator SkipTurnAfterDelay()
    {
        yield return new WaitForSeconds(1.2f);

        ResetNetworkDice();

        NetworkTurnManager.Instance.NextTurn();

        GameManager.Instance.SetState(GameState.WaitingRoll);
    }
}
