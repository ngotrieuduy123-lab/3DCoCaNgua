using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public GameState currentState = GameState.WaitingRoll;

    void Awake()
    {
        Instance = this;
    }

    public void SetState(GameState newState)
    {
        currentState = newState;
        Debug.Log("Game State: " + currentState);
    }

    public bool IsState(GameState state)
    {
        return currentState == state;
    }
}