using UnityEngine;

public static class GameLogBridge
{
    public static void Send(string message, string eventType = "GAMEPLAY")
    {
        Debug.Log($"[GameLog] {message}");

        if (TcpBroadcastClient.Instance != null && TcpBroadcastClient.Instance.IsConnected)
        {
            TcpBroadcastClient.Instance.SendGameEventLog(message, eventType);
        }
        else if (DatabaseManager.Instance != null)
        {
            DatabaseManager.Instance.SendGameLog(message, eventType);
        }
    }
}
