using Unity.Netcode;
using UnityEngine;
using System.Collections;

public class NetworkDiceManager : NetworkBehaviour
{
    public NetworkVariable<int> dice1Value = new NetworkVariable<int>(0);
    public NetworkVariable<int> dice2Value = new NetworkVariable<int>(0);
    public NetworkVariable<int> totalValue = new NetworkVariable<int>(0);

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

        UpdateDiceUI();
    }

    public override void OnNetworkDespawn()
    {
        dice1Value.OnValueChanged -= OnDiceChanged;
        dice2Value.OnValueChanged -= OnDiceChanged;
        totalValue.OnValueChanged -= OnDiceChanged;

        ClearHighlights();
    }

    public void RequestRollDice()
    {
        if (GameManager.Instance.IsState(GameState.GameOver))
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
        PlayDiceVisualClientRpc(d1, d2);
        if (NetworkSoundManager.Instance != null)
        {
            NetworkSoundManager.Instance.PlayDiceSoundRpc();
        }
        else
        {
            PlayDiceSoundClientRpc();
        }

        dice1Value.Value = d1;
        dice2Value.Value = d2;
        totalValue.Value = d1 + d2;

        Debug.Log("Network dice: " + d1 + " + " + d2 + " = " + totalValue.Value);
        ShowMessageClientRpc("Dice rolled");
        CheckAutoSkipTurnNetwork();
    }

    void OnDiceChanged(int oldValue, int newValue)
    {
        UpdateDiceUI();
        RefreshHighlights();
    }

    void UpdateDiceUI()
    {
        gameplayUI.SetDice(dice1Value.Value, dice2Value.Value, totalValue.Value);
    }

    public bool IsDouble()
    {
        return DiceRuleUtility.IsDouble(dice1Value.Value, dice2Value.Value);
    }

    public bool IsOneSix()
    {
        return DiceRuleUtility.IsOneSix(dice1Value.Value, dice2Value.Value);
    }

    public bool CanSpawnPiece()
    {
        return DiceRuleUtility.CanEnterBoardOrClimb(dice1Value.Value, dice2Value.Value);
    }

    public bool CanClimbHome()
    {
        return DiceRuleUtility.CanEnterBoardOrClimb(dice1Value.Value, dice2Value.Value);
    }
    public void ResetNetworkDice()
    {
        dice1Value.Value = 0;
        dice2Value.Value = 0;
        totalValue.Value = 0;
        ClearHighlights();
    }

    void CheckAutoSkipTurnNetwork()
    {
        int currentPlayer = NetworkTurnManager.Instance.currentPlayerIndex.Value;
        bool hasValidMove = false;

        foreach (PieceController piece in boardManager.allPieces)
        {
            if (piece.playerIndex != currentPlayer)
                continue;

            if (NetworkRoomControlManager.Instance != null &&
                !NetworkRoomControlManager.Instance.IsPlayerActive(piece.playerIndex))
                continue;

            if (piece.isFinished)
                continue;

            if (piece.isInStable)
            {
                if (CanSpawnPiece() && piece.CanSpawn())
                {
                    hasValidMove = true;
                    break;
                }
            }
            else if (piece.isInHomePath)
            {
                if (CanClimbHome() && piece.CanClimbHome())
                {
                    hasValidMove = true;
                    break;
                }
            }
            else
            {
                if (piece.CanMove(totalValue.Value))
                {
                    hasValidMove = true;
                    break;
                }
            }
        }

        if (!hasValidMove)
        {
            Debug.Log("network no valid move, skip turn");

            ShowMessageClientRpc("No valid move. Skip turn...");
            ClearHighlights();

            StartCoroutine(SkipTurnAfterDelay());
        }
        else
        {
            GameManager.Instance.SetState(GameState.WaitingChoosePiece);
        }
    }

    bool CanSenderRoll(ulong senderClientId)
    {
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
        visualDice1.RollVisualOnly();
        visualDice2.RollVisualOnly();

        yield return new WaitForSeconds(0.2f);

        visualDice1.SetVisualValue(d1);
        visualDice2.SetVisualValue(d2);

        gameplayUI.SetDice(d1, d2, d1 + d2);
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

        foreach (PieceController piece in boardManager.allPieces)
        {
            if (piece == null ||
                piece.playerIndex != currentPlayer ||
                piece.isFinished)
                continue;

            bool canUse = false;

            if (piece.isInStable)
                canUse = CanSpawnPiece() && piece.CanSpawn();
            else if (piece.isInHomePath)
                canUse = CanClimbHome() && piece.CanClimbHome();
            else
                canUse = piece.CanMove(totalValue.Value);

            piece.SetHighlight(canUse);

            if (canUse)
            {
                MovePathHighlighter.Instance.ShowMovePreview(
                    piece,
                    totalValue.Value,
                    CanSpawnPiece(),
                    CanClimbHome()
                );
            }
        }
    }

    public void ClearHighlights()
    {
        MovePathHighlighter.TryClear();

        if (boardManager == null)
            return;

        foreach (PieceController piece in boardManager.allPieces)
        {
            if (piece != null)
                piece.SetHighlight(false);
        }
    }

    IEnumerator SkipTurnAfterDelay()
    {
        yield return new WaitForSeconds(1.5f);

        ResetNetworkDice();

        NetworkTurnManager.Instance.NextTurn();

        GameManager.Instance.SetState(GameState.WaitingRoll);
    }
}
