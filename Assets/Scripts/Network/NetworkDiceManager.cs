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

    public override void OnNetworkSpawn()
    {
        dice1Value.OnValueChanged += OnDiceChanged;
        dice2Value.OnValueChanged += OnDiceChanged;
        totalValue.OnValueChanged += OnDiceChanged;

        UpdateDiceUI();
    }

    public void RequestRollDice()
    {
        if (IsServer)
        {
            ulong hostId = NetworkManager.Singleton.LocalClientId;

            if (!CanSenderRoll(hostId))
            {
                Debug.Log("cannot roll now");
                gameplayUI.SetMessage("Not your turn or already rolled");
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
            ShowMessageClientRpc("Not your turn or already rolled");
            return;
        }

        RollDiceServer();
    }

    void RollDiceServer()
    {
        int d1 = Random.Range(1, 7);
        int d2 = Random.Range(1, 7);
        PlayDiceVisualClientRpc(d1, d2);
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
    }

    void UpdateDiceUI()
    {
        gameplayUI.SetDice(dice1Value.Value, dice2Value.Value, totalValue.Value);
    }

    public bool IsDouble()
    {
        return dice1Value.Value == dice2Value.Value;
    }

    public bool IsOneSix()
    {
        return (dice1Value.Value == 1 && dice2Value.Value == 6) ||
               (dice1Value.Value == 6 && dice2Value.Value == 1);
    }

    public bool CanSpawnPiece()
    {
        return IsDouble() || IsOneSix();
    }

    public bool CanClimbHome()
    {
        return IsDouble() || IsOneSix();
    }
    public void ResetNetworkDice()
    {
        dice1Value.Value = 0;
        dice2Value.Value = 0;
        totalValue.Value = 0;
    }

    void CheckAutoSkipTurnNetwork()
    {
        int currentPlayer = NetworkTurnManager.Instance.currentPlayerIndex.Value;
        bool hasValidMove = false;

        foreach (PieceController piece in boardManager.allPieces)
        {
            if (piece.playerIndex != currentPlayer)
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

        if ((int)senderClientId != currentPlayer)
            return false;

        if (totalValue.Value > 0)
            return false;

        return true;
    }

    [Rpc(SendTo.ClientsAndHost)]
    void ShowMessageClientRpc(string message)
    {
        gameplayUI.SetMessage(message);
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
    IEnumerator SkipTurnAfterDelay()
    {
        yield return new WaitForSeconds(1.5f);

        ResetNetworkDice();

        NetworkTurnManager.Instance.NextTurn();

        GameManager.Instance.SetState(GameState.WaitingRoll);
    }
}