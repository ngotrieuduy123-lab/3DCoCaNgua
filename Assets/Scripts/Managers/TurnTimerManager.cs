using Unity.Netcode;
using UnityEngine;

public class TurnTimerManager : NetworkBehaviour
{
    public static TurnTimerManager Instance;

    public NetworkVariable<int> remainingTime =
        new NetworkVariable<int>(30);

    float timer;

    GameplayUI gameplayUI;

    void Awake()
    {
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        gameplayUI = FindFirstObjectByType<GameplayUI>();

        remainingTime.OnValueChanged += OnTimeChanged;

        if (IsServer)
        {
            ResetTimer();
        }

        OnTimeChanged(0, remainingTime.Value);
    }

    void Update()
    {
        if (!IsServer) return;

        if (GameManager.Instance.IsState(GameState.GameOver))
            return;

        timer += Time.deltaTime;

        if (timer >= 1f)
        {
            timer = 0f;

            remainingTime.Value--;

            if (remainingTime.Value <= 0)
            {
                HandleTimeOut();
            }
        }
    }

    void HandleTimeOut()
    {
        Debug.Log("Time out");

        if (NetworkTurnManager.Instance != null)
        {
            NetworkTurnManager.Instance.NextTurn();
        }

        ResetTimer();
    }

    public void ResetTimer()
    {
        remainingTime.Value = 30;
    }

    void OnTimeChanged(int oldValue, int newValue)
    {
        if (gameplayUI != null)
        {
            gameplayUI.SetTimer(newValue);
        }
    }

    public override void OnNetworkDespawn()
    {
        remainingTime.OnValueChanged -= OnTimeChanged;
    }
}