using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TcpBroadcastClient : MonoBehaviour
{
    public static TcpBroadcastClient Instance { get; private set; }

    [Header("TCP Server Settings")]
    [SerializeField] private string serverHost = "127.0.0.1";
    [SerializeField] private int serverPort = 7777;
    [SerializeField] private bool autoConnectOnStart = true;

    private TcpClient tcpClient;
    private NetworkStream networkStream;
    private CancellationTokenSource cts;
    private readonly ConcurrentQueue<string> incomingMessages = new ConcurrentQueue<string>();

    private GameObject bannerObject;
    private TMP_Text bannerText;
    private float bannerHideTime;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        CreateToastBannerUi();
        if (autoConnectOnStart)
        {
            ConnectToServer();
        }
    }

    void OnDestroy()
    {
        Disconnect();
    }

    public void ConnectToServer()
    {
        if (tcpClient != null && tcpClient.Connected)
            return;

        cts?.Cancel();
        cts = new CancellationTokenSource();

        Task.Run(() => ConnectAndListenAsync(cts.Token));
    }

    public void Disconnect()
    {
        cts?.Cancel();
        try { networkStream?.Close(); } catch { }
        try { tcpClient?.Close(); } catch { }
        tcpClient = null;
        networkStream = null;
    }

    private async Task ConnectAndListenAsync(CancellationToken token)
    {
        try
        {
            tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(serverHost, serverPort);
            networkStream = tcpClient.GetStream();
            Debug.Log($"[TcpBroadcastClient] Connected to TCP Server on port {serverPort}!");

            byte[] buffer = new byte[4096];
            StringBuilder sb = new StringBuilder();

            while (!token.IsCancellationRequested && tcpClient.Connected)
            {
                int bytesRead = await networkStream.ReadAsync(buffer, 0, buffer.Length, token);
                if (bytesRead == 0) break;

                string chunk = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                sb.Append(chunk);

                string content = sb.ToString();
                int nl;
                while ((nl = content.IndexOf('\n')) >= 0)
                {
                    string line = content.Substring(0, nl).Trim();
                    content = content.Substring(nl + 1);
                    sb.Clear();
                    sb.Append(content);

                    if (!string.IsNullOrEmpty(line))
                    {
                        incomingMessages.Enqueue(line);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            if (!token.IsCancellationRequested)
            {
                Debug.LogWarning($"[TcpBroadcastClient] Socket error or disconnected: {ex.Message}");
            }
        }
    }

    public bool IsConnected => tcpClient != null && tcpClient.Connected && networkStream != null;

    public void SendGameEventLog(string message, string eventType = "GAMEPLAY")
    {
        if (!IsConnected) return;

        try
        {
            string escaped = message.Replace("\"", "'").Replace("\n", " ").Replace("\r", "");
            string json = $"{{\"action\":\"GAME_LOG\",\"eventType\":\"{eventType}\",\"message\":\"{escaped}\"}}\n";
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            networkStream.Write(bytes, 0, bytes.Length);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[TcpBroadcastClient] Error sending game log: {ex.Message}");
        }
    }

    public void SendPurchase(string itemId, string fishName, double price, string playerId)
    {
        if (tcpClient == null || !tcpClient.Connected || networkStream == null)
        {
            Debug.LogWarning("[TcpBroadcastClient] Cannot send purchase: Not connected to TCP server.");
            return;
        }

        try
        {
            string json = $"{{\"action\":\"PURCHASE\",\"skinKey\":\"{itemId}\",\"skinName\":\"{fishName}\",\"itemId\":\"{itemId}\",\"fishName\":\"{fishName}\",\"price\":{price},\"playerId\":\"{playerId}\"}}\n";
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            networkStream.Write(bytes, 0, bytes.Length);
            Debug.Log($"[TcpBroadcastClient] Sent purchase packet for skin '{fishName}' ({itemId})");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[TcpBroadcastClient] Error sending purchase: {ex.Message}");
        }
    }

    void Update()
    {
        while (incomingMessages.TryDequeue(out string messageJson))
        {
            HandleBroadcastOnMainThread(messageJson);
        }

        if (bannerObject != null && bannerObject.activeSelf)
        {
            if (Time.unscaledTime >= bannerHideTime)
            {
                bannerObject.SetActive(false);
            }
        }
    }

    private void HandleBroadcastOnMainThread(string json)
    {
        Debug.Log($"[TcpBroadcastClient] BROADCAST RECEIVED: {json}");

        string displayMsg = "";
        try
        {
            BroadcastPayload payload = JsonUtility.FromJson<BroadcastPayload>(json);
            if (payload != null && !string.IsNullOrEmpty(payload.message))
            {
                displayMsg = "📢 [THÔNG BÁO] " + payload.message;
            }
        }
        catch
        {
            displayMsg = "📢 " + json;
        }

        if (string.IsNullOrEmpty(displayMsg))
            displayMsg = "📢 " + json;

        ShowBanner(displayMsg);
    }

    public void ShowBanner(string message)
    {
        if (bannerObject == null) CreateToastBannerUi();

        if (bannerText != null)
        {
            bannerText.text = message;
        }

        if (bannerObject != null)
        {
            bannerObject.SetActive(true);
            bannerHideTime = Time.unscaledTime + 5.0f; // Show for 5 seconds
        }
    }

    private void CreateToastBannerUi()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        Transform existing = canvas.transform.Find("GlobalBroadcastBanner");
        if (existing != null)
        {
            bannerObject = existing.gameObject;
            bannerText = bannerObject.GetComponentInChildren<TMP_Text>();
            return;
        }

        bannerObject = new GameObject("GlobalBroadcastBanner");
        bannerObject.transform.SetParent(canvas.transform, false);

        RectTransform rt = bannerObject.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.1f, 0.90f);
        rt.anchorMax = new Vector2(0.9f, 0.98f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image bg = bannerObject.AddComponent<Image>();
        bg.color = new Color(0.1f, 0.12f, 0.2f, 0.92f);

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(bannerObject.transform, false);
        RectTransform textRt = textObj.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(10, 0);
        textRt.offsetMax = new Vector2(-10, 0);

        bannerText = textObj.AddComponent<TextMeshProUGUI>();
        bannerText.fontSize = 20;
        bannerText.alignment = TextAlignmentOptions.Center;
        bannerText.color = new Color(1f, 0.85f, 0.2f, 1f); // Gold / Yellow
        bannerText.text = "";

        bannerObject.SetActive(false);
    }

    [Serializable]
    private class BroadcastPayload
    {
        public string action;
        public string playerId;
        public string skinKey;
        public string skinName;
        public string fishName;
        public double price;
        public string message;
    }
}
