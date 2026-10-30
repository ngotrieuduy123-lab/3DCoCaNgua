using Unity.Netcode;
using UnityEngine;

public class TurnTimerManager : NetworkBehaviour
{
    public static TurnTimerManager Instance;

    public NetworkVariable<int> remainingTime =
        new NetworkVariable<int>(DiceRuleUtility.TurnDecisionSeconds);

    public NetworkVariable<int> matchRemainingSeconds =
        new NetworkVariable<int>(DiceRuleUtility.MatchDurationSeconds);

    public NetworkVariable<bool> isFinalRound =
        new NetworkVariable<bool>(false);

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
        matchRemainingSeconds.OnValueChanged += OnMatchTimeChanged;
        isFinalRound.OnValueChanged += OnFinalRoundChanged;

        if (IsServer)
        {
            ResetTimer();
            matchRemainingSeconds.Value = DiceRuleUtility.MatchDurationSeconds;
            isFinalRound.Value = false;
        }

        OnTimeChanged(0, remainingTime.Value);
        OnMatchTimeChanged(0, matchRemainingSeconds.Value);
    }

    void Update()
    {
        if (!IsServer) return;

        if (GameManager.Instance != null && GameManager.Instance.IsState(GameState.GameOver))
            return;

        timer += Time.deltaTime;

        if (timer >= 1f)
        {
            timer -= 1f;

            // 1. Server Match Clock (10 minutes, runs continuously including animations)
            if (matchRemainingSeconds.Value > 0)
            {
                matchRemainingSeconds.Value--;
                if (matchRemainingSeconds.Value <= 0 && !isFinalRound.Value)
                {
                    isFinalRound.Value = true;
                    Debug.Log("Match clock reached 00:00 -> Entering Final Round!");
                }
            }

            // 2. Turn Decision Budget (15 seconds, paused while dice or piece presentation is running)
            bool presentationRunning = GameManager.Instance != null &&
                (GameManager.Instance.IsState(GameState.MovingPiece) ||
                 GameManager.Instance.IsState(GameState.RollingDice));

            if (!presentationRunning)
            {
                remainingTime.Value--;

                if (remainingTime.Value <= 0)
                {
                    HandleTimeOut();
                }
            }
        }
    }

    void HandleTimeOut()
    {
        Debug.Log("Turn decision time out (15s)");

        if (NetworkTurnManager.Instance != null)
        {
            NetworkTurnManager.Instance.NextTurn();
        }

        ResetTimer();
    }

    public void ResetTimer()
    {
        if (IsServer)
            remainingTime.Value = DiceRuleUtility.TurnDecisionSeconds;
    }

    void OnTimeChanged(int oldValue, int newValue)
    {
        if (gameplayUI == null)
            gameplayUI = FindFirstObjectByType<GameplayUI>();

        if (gameplayUI != null)
        {
            gameplayUI.SetTimer(newValue);
        }
    }

    void OnMatchTimeChanged(int oldValue, int newValue)
    {
        if (gameplayUI == null)
            gameplayUI = FindFirstObjectByType<GameplayUI>();

        if (gameplayUI != null)
        {
            gameplayUI.SetMatchTimer(newValue, isFinalRound.Value);
        }
    }

    void OnFinalRoundChanged(bool oldValue, bool newValue)
    {
        if (gameplayUI == null)
            gameplayUI = FindFirstObjectByType<GameplayUI>();

        if (gameplayUI != null)
        {
            gameplayUI.SetMatchTimer(matchRemainingSeconds.Value, newValue);
            if (newValue)
                gameplayUI.SetMessage("Hết 10 phút! Đang hoàn tất vòng lượt cuối cùng!");
        }
    }

    public override void OnNetworkDespawn()
    {
        remainingTime.OnValueChanged -= OnTimeChanged;
        matchRemainingSeconds.OnValueChanged -= OnMatchTimeChanged;
        isFinalRound.OnValueChanged -= OnFinalRoundChanged;
    }
}
