using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Unity.Netcode;
using UnityEngine.SceneManagement;

// Keep the existing scene component; all persistence is owned by ASP.NET Core.
public class DatabaseManager : MonoBehaviour
{
    public const int StartingCoins = 1000;
    public const int WinRewardCoins = 1000;
    public static DatabaseManager Instance;
    [Header("Backend API")]
    [SerializeField] private string backendUrl = "http://127.0.0.1:5080";
    public PlayerData CurrentPlayer { get; private set; }
    public string LastMessage { get; private set; }
    public bool IsConnected { get; private set; }
    public string CurrentRoomId { get; private set; } = "";
    public string CurrentMatchId { get; private set; } = "";
    public bool IsRoomHost { get; private set; }
    public BackendSkin[] AvailableSkins { get; private set; } = Array.Empty<BackendSkin>();
    string token = "";
    bool destroyed;
    float nextHeartbeat;
    bool heartbeatBusy;
    int rollSequence;
    Task rollQueue = Task.CompletedTask;
    int pendingRolls;
    readonly HashSet<UnityWebRequest> activeRequests = new HashSet<UnityWebRequest>();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        string configured = Environment.GetEnvironmentVariable("COCANGUA_BACKEND_URL");
        if (!string.IsNullOrWhiteSpace(configured)) backendUrl = configured;
        backendUrl = backendUrl.TrimEnd('/');
        PlayerPrefs.DeleteKey("CurrentMatchHistoryId");
        ConnectToDatabase();
    }

    void OnDestroy()
    {
        if (Instance != this) return;
        destroyed = true;
        foreach (UnityWebRequest request in new List<UnityWebRequest>(activeRequests)) request.Abort();
        Instance = null;
    }

    void Update()
    {
        if (CurrentPlayer == null || heartbeatBusy || Time.unscaledTime < nextHeartbeat) return;
        nextHeartbeat = Time.unscaledTime + 20f;
        _ = Heartbeat();
    }

    async Task Heartbeat()
    {
        heartbeatBusy = true;
        try
        {
            await Send("/api/game/heartbeat", "POST", "{}");
            if (IsRoomHost && !string.IsNullOrEmpty(CurrentRoomId)) await Send("/api/game/rooms/" + CurrentRoomId + "/heartbeat", "POST", "{}");
        }
        catch (Exception e) { if (!destroyed) Debug.LogWarning(e.Message); }
        finally { heartbeatBusy = false; }
    }

    public async void ConnectToDatabase()
    {
        try { await Send("/health", "GET"); IsConnected = true; LastMessage = "Backend connected."; }
        catch (Exception e) { IsConnected = false; LastMessage = e.Message; }
    }

    public Task<AuthResult> RegisterPlayerDetailed(string username, string password, string displayName) => Authenticate("register", username, password, displayName);
    public Task<AuthResult> LoginPlayerDetailed(string username, string password) => Authenticate("login", username, password, "");
    async Task<AuthResult> Authenticate(string action, string username, string password, string displayName)
    {
        try
        {
            var response = await Send("/api/auth/" + action, "POST", JsonUtility.ToJson(new Credentials { username = username, password = password, displayName = displayName }));
            token = response.token;
            ApplyPlayer(response.player);
            await RefreshSkins();
            nextHeartbeat = 0;
            LastMessage = response.message;
            return new AuthResult(true, LastMessage, CurrentPlayer);
        }
        catch (Exception e) { LastMessage = e.Message; return new AuthResult(false, LastMessage, null); }
    }

    public async Task<bool> RegisterPlayer(string username, string password, string displayName) => (await RegisterPlayerDetailed(username, password, displayName)).Success;
    public async Task<PlayerData> LoginPlayer(string username, string password) => (await LoginPlayerDetailed(username, password)).Player;
    public async void Logout() { await LogoutAsync(); }
    public async Task LogoutAsync()
    {
        try { if (!string.IsNullOrEmpty(token)) await Send("/api/game/logout", "POST", "{}"); }
        catch (Exception e) { if (!destroyed) Debug.LogWarning(e.Message); }
        finally
        {
            token = ""; CurrentPlayer = null; CurrentRoomId = ""; CurrentMatchId = ""; IsRoomHost = false;
            foreach (string key in new[] { "PlayerId", "Username", "DisplayName", "Coins", "EquippedSkinId", "CurrentMatchHistoryId" }) PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }
    }

    public async Task<bool> RefreshPlayer()
    {
        try { ApplyPlayer((await Send("/api/game/me", "GET")).player); return true; }
        catch (Exception e) { LastMessage = e.Message; return false; }
    }
    public async Task RefreshSkins() => AvailableSkins = (await Send("/api/game/skins", "GET")).skins ?? Array.Empty<BackendSkin>();
    public Task<ShopResult> PurchaseSkin(string skinId) => ChangeSkin("purchase", skinId);
    public Task<ShopResult> EquipSkin(string skinId) => ChangeSkin("equip", skinId);
    async Task<ShopResult> ChangeSkin(string action, string skinId)
    {
        try
        {
            BackendSkin sale = Array.Find(AvailableSkins, skin => skin.id == skinId);
            var response = await Send("/api/game/skins/" + action, "POST", JsonUtility.ToJson(new SkinBody { skinId = skinId, expectedPrice = sale != null ? sale.price : -1 }));
            ApplyPlayer(response.player);
            return new ShopResult(true, response.message, CurrentPlayer.Coins);
        }
        catch (Exception e) { LastMessage = e.Message; return new ShopResult(false, e.Message, CurrentPlayer != null ? CurrentPlayer.Coins : 0); }
    }
    public bool CurrentPlayerOwnsSkin(string skinId) => CurrentPlayer != null && (skinId == SkinCatalog.DefaultSkinId || CurrentPlayer.OwnedSkinIds.Contains(skinId));

    public async Task<bool> RegisterRoom(string relayCode, bool host)
    {
        try
        {
            var response = await Send(host ? "/api/game/rooms" : "/api/game/rooms/join", "POST", JsonUtility.ToJson(new RoomBody { code = relayCode }));
            CurrentRoomId = response.room.id; IsRoomHost = host; CurrentMatchId = ""; nextHeartbeat = 0;
            return true;
        }
        catch (Exception e) { LastMessage = e.Message; return false; }
    }

    public async Task LeaveBackendRoom()
    {
        string matchId = CurrentMatchId;
        bool host = IsRoomHost;
        CurrentRoomId = ""; CurrentMatchId = ""; IsRoomHost = false;
        try
        {
            if (host && !string.IsNullOrEmpty(matchId)) await EndMatchHistory(matchId, "Host left room");
            if (!string.IsNullOrEmpty(token)) await Send("/api/game/rooms/leave", "POST", "{}");
        }
        catch (Exception e) { LastMessage = e.Message; }
    }

    public async Task<string> BeginMatchHistory(string[] playerIds)
    {
        try
        {
            await rollQueue;
            var response = await Send("/api/game/rooms/" + CurrentRoomId + "/matches", "POST", JsonUtility.ToJson(new StartBody { playerIds = playerIds }));
            CurrentMatchId = response.match.id; rollSequence = 0;
            PlayerPrefs.SetString("CurrentMatchHistoryId", CurrentMatchId);
            return CurrentMatchId;
        }
        catch (Exception e) { LastMessage = e.Message; return ""; }
    }

    public void RecordDiceRoll(int slot, int dice1, int dice2)
    {
        if (!IsRoomHost || string.IsNullOrEmpty(CurrentMatchId)) return;
        if (pendingRolls >= 100) { Debug.LogWarning("Dice telemetry queue is full. This roll was not recorded."); return; }
        string matchId = CurrentMatchId;
        var body = new RollBody { eventId = Guid.NewGuid().ToString(), sequence = ++rollSequence, slot = slot, dice1 = dice1, dice2 = dice2 };
        pendingRolls++;
        rollQueue = SendRollAfter(rollQueue, matchId, JsonUtility.ToJson(body));
    }
    async Task SendRollAfter(Task previous, string matchId, string body)
    {
        try
        {
            await previous;
            for (int attempt = 0; attempt < 3 && !destroyed; attempt++)
            {
                if (CurrentMatchId != matchId) return;
                try { await Send("/api/game/matches/" + matchId + "/rolls", "POST", body); return; }
                catch (Exception) { if (attempt < 2) await Task.Delay(500 * (attempt + 1)); }
            }
            if (!destroyed) Debug.LogWarning("Could not save a dice roll after 3 attempts. Check backend connectivity.");
        }
        finally { pendingRolls--; }
    }

    public Task<bool> EndMatchHistory(string matchId, string reason) => FinishMatch(matchId, Array.Empty<int>(), reason);
    public async Task<bool> FinishMatch(string matchId, int[] ranking, string reason)
    {
        if (string.IsNullOrEmpty(matchId)) return false;
        try
        {
            Task drained = await Task.WhenAny(rollQueue, Task.Delay(5000));
            if (drained != rollQueue && !destroyed) Debug.LogWarning("Some dice telemetry is still pending; finishing the match without delaying the results screen.");
            var response = await Send("/api/game/matches/" + matchId + "/finish", "POST", JsonUtility.ToJson(new FinishBody { ranking = ranking, reason = reason }));
            await RefreshPlayer();
            if (CurrentMatchId == matchId) { CurrentMatchId = ""; CurrentRoomId = ""; IsRoomHost = false; }
            PlayerPrefs.DeleteKey("CurrentMatchHistoryId");
            bool confirmed = ranking.Length == 0 || (response.match != null && response.match.status == "Ended" && response.match.rewardCoins > 0);
            if (!confirmed) LastMessage = "The backend did not confirm a winner reward for this match.";
            return confirmed;
        }
        catch (Exception e) { LastMessage = e.Message; return false; }
    }

    // The finish endpoint commits rewards; clients can only refresh their balance.
    public async Task<ShopResult> AwardWinCoins(string rewardId, int amount = WinRewardCoins)
    {
        bool refreshed = await RefreshPlayer();
        return new ShopResult(refreshed, refreshed ? "Balance refreshed." : LastMessage, CurrentPlayer != null ? CurrentPlayer.Coins : 0);
    }

    async Task<Response> Send(string path, string method, string body = null)
    {
        if (destroyed) throw new InvalidOperationException("Backend client has closed.");
        if (!Uri.TryCreate(backendUrl, UriKind.Absolute, out Uri address) || (address.Scheme != "https" && !(address.Scheme == "http" && address.IsLoopback)))
            throw new InvalidOperationException("Backend URL must use HTTPS, except localhost development.");
        using (var request = new UnityWebRequest(backendUrl + path, method))
        {
            string requestToken = token;
            bool protectedRequest = path.StartsWith("/api/game/", StringComparison.Ordinal);
            request.downloadHandler = new DownloadHandlerBuffer(); request.timeout = 12;
            if (body != null) { request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body)); request.SetRequestHeader("Content-Type", "application/json"); }
            if (!string.IsNullOrEmpty(requestToken) && protectedRequest) request.SetRequestHeader("Authorization", "Bearer " + requestToken);
            activeRequests.Add(request);
            try
            {
                var completion = new TaskCompletionSource<bool>();
                var operation = request.SendWebRequest();
                operation.completed += _ => completion.TrySetResult(true);
                if (operation.isDone) completion.TrySetResult(true);
                await completion.Task;
                if (destroyed) throw new InvalidOperationException("Backend client has closed.");
                if (protectedRequest && token != requestToken) throw new InvalidOperationException("Session changed. Please log in again.");
                Response response = null;
                try { response = JsonUtility.FromJson<Response>(request.downloadHandler.text); } catch (ArgumentException) { }
                if (request.result != UnityWebRequest.Result.Success)
                {
                    IsConnected = request.responseCode > 0;
                    if (request.responseCode == 401 && path.StartsWith("/api/game/", StringComparison.Ordinal))
                    {
                        token = ""; CurrentPlayer = null; CurrentRoomId = ""; CurrentMatchId = ""; IsRoomHost = false;
                        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) NetworkManager.Singleton.Shutdown();
                        if (SceneManager.GetActiveScene().name != "AuthScene") SceneManager.LoadScene("AuthScene");
                    }
                    throw new InvalidOperationException(response != null && !string.IsNullOrEmpty(response.message) ? response.message : "Cannot reach backend. Start the server and check Backend URL.");
                }
                IsConnected = true;
                return response ?? new Response();
            }
            finally { activeRequests.Remove(request); }
        }
    }

    void ApplyPlayer(PlayerDto p)
    {
        if (p == null) throw new InvalidOperationException("Backend returned no player profile.");
        CurrentPlayer = new PlayerData { Id = p.id, Username = p.username, DisplayName = p.displayName, Coins = p.coins, OwnedSkinIds = new List<string>(p.ownedSkinIds ?? Array.Empty<string>()), EquippedSkinId = p.equippedSkinId };
        PlayerPrefs.SetString("PlayerId", p.id); PlayerPrefs.SetString("Username", p.username); PlayerPrefs.SetString("DisplayName", p.displayName);
        PlayerPrefs.SetInt("Coins", p.coins); PlayerPrefs.SetString("EquippedSkinId", p.equippedSkinId);
        PlayerPrefs.SetString("PlayerSkin_" + PlayerPrefs.GetInt("LocalPlayerIndex", 0), p.equippedSkinId);
        PlayerPrefs.Save();
    }

    [Serializable] class Credentials { public string username; public string password; public string displayName; }
    [Serializable] class SkinBody { public string skinId; public int expectedPrice; }
    [Serializable] class RoomBody { public string code; }
    [Serializable] class StartBody { public string[] playerIds; }
    [Serializable] class FinishBody { public int[] ranking; public string reason; }
    [Serializable] class RollBody { public string eventId; public int sequence; public int slot; public int dice1; public int dice2; }
    [Serializable] class Response { public string token = ""; public string message = ""; public PlayerDto player = null; public RoomDto room = null; public MatchDto match = null; public BackendSkin[] skins = null; }
    [Serializable] class RoomDto { public string id = ""; }
    [Serializable] class MatchDto { public string id = ""; public string status = ""; public int rewardCoins = 0; }
    [Serializable] class PlayerDto { public string id = ""; public string username = ""; public string displayName = ""; public int coins = 0; public string[] ownedSkinIds = null; public string equippedSkinId = ""; }
    [Serializable] public class BackendSkin { public string id; public string name; public string description; public int price; public bool isForSale; }
    public readonly struct AuthResult
    {
        public readonly bool Success; public readonly string Message; public readonly PlayerData Player;
        public AuthResult(bool success, string message, PlayerData player) { Success = success; Message = message; Player = player; }
    }
    public readonly struct ShopResult
    {
        public readonly bool Success; public readonly string Message; public readonly int Coins;
        public ShopResult(bool success, string message, int coins) { Success = success; Message = message; Coins = coins; }
    }
}
