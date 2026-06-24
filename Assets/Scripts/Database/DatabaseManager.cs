using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class DatabaseManager : MonoBehaviour
{
    public const int StartingCoins = 1000;
    public const int WinRewardCoins = 1000;

    public static DatabaseManager Instance;

    [Header("MongoDB")]
    [SerializeField] private string connectionString = "";
    [SerializeField] private string databaseName = "LudoGameDB";
    [SerializeField] private string collectionName = "Players";
    [SerializeField] private string matchHistoryCollectionName = "MatchHistories";

    public PlayerData CurrentPlayer { get; private set; }
    public string LastMessage { get; private set; }
    public bool IsConnected => playerCollection != null;

    private IMongoCollection<PlayerData> playerCollection;
    private IMongoCollection<MatchHistoryData> matchHistoryCollection;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            ConnectToDatabase();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void ConnectToDatabase()
    {
        try
        {
            string resolvedConnectionString = ResolveConnectionString();

            if (string.IsNullOrWhiteSpace(resolvedConnectionString))
            {
                LastMessage = "MongoDB connection string is empty.";
                Debug.LogWarning(LastMessage);
                return;
            }

            MongoClient client = new MongoClient(resolvedConnectionString);
            IMongoDatabase database = client.GetDatabase(databaseName);
            playerCollection = database.GetCollection<PlayerData>(collectionName);
            matchHistoryCollection = database.GetCollection<MatchHistoryData>(matchHistoryCollectionName);
            EnsureIndexes();

            LastMessage = "Connected to MongoDB.";
            Debug.Log(LastMessage);
        }
        catch (Exception e)
        {
            playerCollection = null;
            matchHistoryCollection = null;
            LastMessage = "MongoDB connection failed: " + e.Message;
            Debug.LogError(LastMessage);
        }
    }

    public async Task<AuthResult> RegisterPlayerDetailed(string username, string password, string displayName)
    {
        username = NormalizeUsername(username);
        displayName = string.IsNullOrWhiteSpace(displayName) ? username : displayName.Trim();

        if (!ValidateCredentials(username, password, out string validationMessage))
            return Fail(validationMessage);

        if (!EnsureCollectionReady(out string databaseMessage))
            return Fail(databaseMessage);

        try
        {
            PlayerData existingPlayer = await playerCollection
                .Find(p => p.Username == username)
                .FirstOrDefaultAsync();

            if (existingPlayer != null)
                return Fail("Username already exists.");

            CreatePasswordHash(password, out string salt, out string hash);

            PlayerData newPlayer = new PlayerData
            {
                Id = ObjectId.GenerateNewId().ToString(),
                Username = username,
                DisplayName = displayName,
                PasswordSalt = salt,
                PasswordHash = hash,
                Password = null,
                Coins = StartingCoins,
                OwnedSkinIds = new List<string> { SkinCatalog.DefaultSkinId },
                EquippedSkinId = SkinCatalog.DefaultSkinId,
                RewardedMatchIds = new List<string>(),
                CreatedAtUtc = DateTime.UtcNow,
                LastLoginUtc = DateTime.UtcNow
            };

            await playerCollection.InsertOneAsync(newPlayer);

            CurrentPlayer = newPlayer;
            SaveSession(newPlayer);

            return Success("Register success.", newPlayer);
        }
        catch (MongoWriteException e) when (e.WriteError != null && e.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            return Fail("Username already exists.");
        }
        catch (Exception e)
        {
            return Fail("Register failed: " + e.Message);
        }
    }

    public async Task<string> BeginMatchHistory(int playerCount)
    {
        if (!EnsureMatchHistoryCollectionReady(out string databaseMessage))
        {
            Debug.LogWarning(databaseMessage);
            return "";
        }

        try
        {
            DateTime utcNow = DateTime.UtcNow;
            DateTime localNow = DateTime.Now;

            MatchHistoryData matchHistory = new MatchHistoryData
            {
                Id = ObjectId.GenerateNewId().ToString(),
                HostPlayerId = CurrentPlayer != null ? CurrentPlayer.Id : PlayerPrefs.GetString("PlayerId", ""),
                HostUsername = CurrentPlayer != null ? CurrentPlayer.Username : PlayerPrefs.GetString("Username", ""),
                PlayerCount = playerCount,
                StartedAtUtc = utcNow,
                StartedAtLocal = localNow,
                StartedAtText = FormatLocalTime(localNow),
                Status = "Playing"
            };

            await matchHistoryCollection.InsertOneAsync(matchHistory);

            LastMessage = "Match history started.";
            Debug.Log(LastMessage + " Id=" + matchHistory.Id);
            return matchHistory.Id;
        }
        catch (Exception e)
        {
            Debug.LogWarning("Begin match history failed: " + e.Message);
            return "";
        }
    }

    public async Task<bool> EndMatchHistory(string matchHistoryId, string endReason)
    {
        if (string.IsNullOrWhiteSpace(matchHistoryId))
            return false;

        if (!EnsureMatchHistoryCollectionReady(out string databaseMessage))
        {
            Debug.LogWarning(databaseMessage);
            return false;
        }

        try
        {
            MatchHistoryData matchHistory = await matchHistoryCollection
                .Find(m => m.Id == matchHistoryId)
                .FirstOrDefaultAsync();

            if (matchHistory == null)
            {
                Debug.LogWarning("Match history not found: " + matchHistoryId);
                return false;
            }

            if (matchHistory.EndedAtUtc.HasValue)
                return true;

            DateTime utcNow = DateTime.UtcNow;
            DateTime localNow = DateTime.Now;
            int durationSeconds = Mathf.Max(
                0,
                Mathf.RoundToInt((float)(utcNow - matchHistory.StartedAtUtc).TotalSeconds)
            );

            UpdateDefinition<MatchHistoryData> update = Builders<MatchHistoryData>.Update
                .Set(m => m.EndedAtUtc, utcNow)
                .Set(m => m.EndedAtLocal, localNow)
                .Set(m => m.EndedAtText, FormatLocalTime(localNow))
                .Set(m => m.DurationSeconds, durationSeconds)
                .Set(m => m.EndReason, endReason)
                .Set(m => m.Status, "Ended");

            await matchHistoryCollection.UpdateOneAsync(m => m.Id == matchHistoryId, update);

            LastMessage = "Match history ended.";
            Debug.Log(LastMessage + " Id=" + matchHistoryId);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning("End match history failed: " + e.Message);
            return false;
        }
    }

    public async Task<AuthResult> LoginPlayerDetailed(string username, string password)
    {
        username = NormalizeUsername(username);

        if (!ValidateCredentials(username, password, out string validationMessage))
            return Fail(validationMessage);

        if (!EnsureCollectionReady(out string databaseMessage))
            return Fail(databaseMessage);

        try
        {
            PlayerData player = await playerCollection
                .Find(p => p.Username == username)
                .FirstOrDefaultAsync();

            if (player == null)
                return Fail("Wrong username or password.");

            if (!VerifyPassword(player, password))
                return Fail("Wrong username or password.");

            await MarkLoginAndMigrateLegacyPassword(player, password);
            await EnsurePlayerEconomyDefaults(player);

            CurrentPlayer = player;
            SaveSession(player);

            return Success("Login success.", player);
        }
        catch (Exception e)
        {
            return Fail("Login failed: " + e.Message);
        }
    }

    public async Task<bool> RegisterPlayer(string username, string password, string displayName)
    {
        AuthResult result = await RegisterPlayerDetailed(username, password, displayName);
        return result.Success;
    }

    public async Task<PlayerData> LoginPlayer(string username, string password)
    {
        AuthResult result = await LoginPlayerDetailed(username, password);
        return result.Player;
    }

    public void Logout()
    {
        CurrentPlayer = null;
        PlayerPrefs.DeleteKey("PlayerId");
        PlayerPrefs.DeleteKey("Username");
        PlayerPrefs.DeleteKey("DisplayName");
        PlayerPrefs.DeleteKey("Coins");
        PlayerPrefs.DeleteKey("EquippedSkinId");
        PlayerPrefs.Save();
    }

    public async Task<ShopResult> PurchaseSkin(string skinId)
    {
        if (CurrentPlayer == null)
            return ShopFail("Please log in first.");

        skinId = string.IsNullOrWhiteSpace(skinId) ? "" : skinId.Trim();

        SkinCatalog catalog = SkinCatalog.Load();
        SkinDefinition skin = catalog != null ? catalog.Get(skinId) : null;

        if (skin == null || skin.price < 0)
            return ShopFail("Invalid skin.");

        int price = skin.price;

        if (OwnsSkin(CurrentPlayer, skinId))
            return ShopFail("You already own this skin.");

        if (!EnsureCollectionReady(out string databaseMessage))
            return ShopFail(databaseMessage);

        try
        {
            FilterDefinition<PlayerData> filter = Builders<PlayerData>.Filter.And(
                Builders<PlayerData>.Filter.Eq(p => p.Id, CurrentPlayer.Id),
                Builders<PlayerData>.Filter.Gte(p => p.Coins, price),
                Builders<PlayerData>.Filter.Ne("ownedSkinIds", skinId)
            );

            UpdateDefinition<PlayerData> update = Builders<PlayerData>.Update
                .Inc(p => p.Coins, -price)
                .AddToSet(p => p.OwnedSkinIds, skinId);

            PlayerData updated = await playerCollection.FindOneAndUpdateAsync(
                filter,
                update,
                new FindOneAndUpdateOptions<PlayerData> { ReturnDocument = ReturnDocument.After }
            );

            if (updated == null)
                return ShopFail(CurrentPlayer.Coins < price ? "Not enough coins." : "Purchase could not be completed.");

            CurrentPlayer = updated;
            SaveSession(updated);
            return ShopSuccess("Purchased successfully.");
        }
        catch (Exception e)
        {
            return ShopFail("Purchase failed: " + e.Message);
        }
    }

public async Task<ShopResult> EquipSkin(string skinId)
    {
        if (CurrentPlayer == null)
            return ShopFail("Please log in first.");

        skinId = string.IsNullOrWhiteSpace(skinId) ? SkinCatalog.DefaultSkinId : skinId.Trim();

        if (!OwnsSkin(CurrentPlayer, skinId))
            return ShopFail("Purchase this skin before equipping it.");

        if (!EnsureCollectionReady(out string databaseMessage))
            return ShopFail(databaseMessage);

        try
        {
            await playerCollection.UpdateOneAsync(
                p => p.Id == CurrentPlayer.Id,
                Builders<PlayerData>.Update.Set(p => p.EquippedSkinId, skinId)
            );

            CurrentPlayer.EquippedSkinId = skinId;
            SaveSession(CurrentPlayer);
            CacheEquippedSkinForGame(skinId);
            return ShopSuccess("Skin equipped.");
        }
        catch (Exception e)
        {
            return ShopFail("Equip failed: " + e.Message);
        }
    }

    public async Task<ShopResult> AwardWinCoins(string rewardId, int amount = WinRewardCoins)
    {
        if (CurrentPlayer == null || string.IsNullOrWhiteSpace(rewardId) || amount <= 0)
            return ShopFail("Win reward is unavailable.");

        if (!EnsureCollectionReady(out string databaseMessage))
            return ShopFail(databaseMessage);

        try
        {
            FilterDefinition<PlayerData> filter = Builders<PlayerData>.Filter.And(
                Builders<PlayerData>.Filter.Eq(p => p.Id, CurrentPlayer.Id),
                Builders<PlayerData>.Filter.Ne("rewardedMatchIds", rewardId)
            );

            UpdateDefinition<PlayerData> update = Builders<PlayerData>.Update
                .Inc(p => p.Coins, amount)
                .AddToSet(p => p.RewardedMatchIds, rewardId);

            PlayerData updated = await playerCollection.FindOneAndUpdateAsync(
                filter,
                update,
                new FindOneAndUpdateOptions<PlayerData> { ReturnDocument = ReturnDocument.After }
            );

            if (updated == null)
                return ShopFail("This match reward was already claimed.");

            CurrentPlayer = updated;
            SaveSession(updated);
            return ShopSuccess("Victory reward: +" + amount + " coins.");
        }
        catch (Exception e)
        {
            return ShopFail("Reward failed: " + e.Message);
        }
    }

    public bool CurrentPlayerOwnsSkin(string skinId)
    {
        return CurrentPlayer != null && OwnsSkin(CurrentPlayer, skinId);
    }

    string ResolveConnectionString()
    {
        if (!string.IsNullOrWhiteSpace(connectionString))
            return connectionString.Trim();

        string envConnection = Environment.GetEnvironmentVariable("MONGODB_CONNECTION_STRING");
        return string.IsNullOrWhiteSpace(envConnection) ? "" : envConnection.Trim();
    }

    void EnsureIndexes()
    {
        try
        {
            IndexKeysDefinition<PlayerData> keys = Builders<PlayerData>.IndexKeys.Ascending(p => p.Username);
            CreateIndexOptions options = new CreateIndexOptions { Unique = true };
            CreateIndexModel<PlayerData> model = new CreateIndexModel<PlayerData>(keys, options);
            playerCollection.Indexes.CreateOne(model);

            if (matchHistoryCollection != null)
            {
                IndexKeysDefinition<MatchHistoryData> startedAtKeys =
                    Builders<MatchHistoryData>.IndexKeys.Descending(m => m.StartedAtUtc);
                matchHistoryCollection.Indexes.CreateOne(new CreateIndexModel<MatchHistoryData>(startedAtKeys));
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("MongoDB username index was not created: " + e.Message);
        }
    }

    bool EnsureCollectionReady(out string message)
    {
        if (playerCollection != null)
        {
            message = "";
            return true;
        }

        ConnectToDatabase();

        if (playerCollection != null)
        {
            message = "";
            return true;
        }

        message = LastMessage;
        return false;
    }

    bool EnsureMatchHistoryCollectionReady(out string message)
    {
        if (matchHistoryCollection != null)
        {
            message = "";
            return true;
        }

        ConnectToDatabase();

        if (matchHistoryCollection != null)
        {
            message = "";
            return true;
        }

        message = LastMessage;
        return false;
    }

    static string FormatLocalTime(DateTime value)
    {
        return value.ToString("dd/MM/yyyy HH:mm:ss");
    }

    static bool ValidateCredentials(string username, string password, out string message)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            message = "Username is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            message = "Password is required.";
            return false;
        }

        if (username.Length < 3)
        {
            message = "Username must be at least 3 characters.";
            return false;
        }

        if (password.Length < 6)
        {
            message = "Password must be at least 6 characters.";
            return false;
        }

        message = "";
        return true;
    }

    static string NormalizeUsername(string username)
    {
        return string.IsNullOrWhiteSpace(username) ? "" : username.Trim().ToLowerInvariant();
    }

    static void CreatePasswordHash(string password, out string salt, out string hash)
    {
        byte[] saltBytes = new byte[16];

        using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(saltBytes);
        }

        salt = Convert.ToBase64String(saltBytes);
        hash = HashPassword(password, salt);
    }

    static string HashPassword(string password, string salt)
    {
        byte[] saltBytes = Convert.FromBase64String(salt);

        using (HMACSHA256 hmac = new HMACSHA256(saltBytes))
        {
            byte[] hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(hashBytes);
        }
    }

    static bool VerifyPassword(PlayerData player, string password)
    {
        if (!string.IsNullOrEmpty(player.PasswordHash) && !string.IsNullOrEmpty(player.PasswordSalt))
            return HashPassword(password, player.PasswordSalt) == player.PasswordHash;

        return !string.IsNullOrEmpty(player.Password) && player.Password == password;
    }

    async Task MarkLoginAndMigrateLegacyPassword(PlayerData player, string password)
    {
        player.LastLoginUtc = DateTime.UtcNow;

        UpdateDefinition<PlayerData> update = Builders<PlayerData>.Update
            .Set(p => p.LastLoginUtc, player.LastLoginUtc);

        if (string.IsNullOrEmpty(player.PasswordHash) || string.IsNullOrEmpty(player.PasswordSalt))
        {
            CreatePasswordHash(password, out string salt, out string hash);

            player.PasswordSalt = salt;
            player.PasswordHash = hash;
            player.Password = null;

            update = update
                .Set(p => p.PasswordSalt, salt)
                .Set(p => p.PasswordHash, hash)
                .Unset(p => p.Password);
        }

        await playerCollection.UpdateOneAsync(p => p.Id == player.Id, update);
    }

    async Task EnsurePlayerEconomyDefaults(PlayerData player)
    {
        bool hasDefaultSkin = OwnsSkin(player, SkinCatalog.DefaultSkinId);
        bool hasEquippedSkin = !string.IsNullOrWhiteSpace(player.EquippedSkinId);

        if (hasDefaultSkin && hasEquippedSkin)
            return;

        UpdateDefinition<PlayerData> update = Builders<PlayerData>.Update
            .AddToSet(p => p.OwnedSkinIds, SkinCatalog.DefaultSkinId);

        if (!hasEquippedSkin)
            update = update.Set(p => p.EquippedSkinId, SkinCatalog.DefaultSkinId);

        await playerCollection.UpdateOneAsync(p => p.Id == player.Id, update);

        if (player.OwnedSkinIds == null)
            player.OwnedSkinIds = new List<string>();

        if (!player.OwnedSkinIds.Contains(SkinCatalog.DefaultSkinId))
            player.OwnedSkinIds.Add(SkinCatalog.DefaultSkinId);

        if (!hasEquippedSkin)
            player.EquippedSkinId = SkinCatalog.DefaultSkinId;
    }

    static bool OwnsSkin(PlayerData player, string skinId)
    {
        if (skinId == SkinCatalog.DefaultSkinId)
            return true;

        return player.OwnedSkinIds != null && player.OwnedSkinIds.Contains(skinId);
    }

    void SaveSession(PlayerData player)
    {
        PlayerPrefs.SetString("PlayerId", player.Id);
        PlayerPrefs.SetString("Username", player.Username);
        PlayerPrefs.SetString("DisplayName", player.DisplayName);
        PlayerPrefs.SetInt("Coins", player.Coins);
        PlayerPrefs.SetString(
            "EquippedSkinId",
            string.IsNullOrWhiteSpace(player.EquippedSkinId)
                ? SkinCatalog.DefaultSkinId
                : player.EquippedSkinId
        );
        PlayerPrefs.Save();
    }

    ShopResult ShopSuccess(string message)
    {
        LastMessage = message;
        Debug.Log(message);
        return new ShopResult(true, message, CurrentPlayer != null ? CurrentPlayer.Coins : 0);
    }

    ShopResult ShopFail(string message)
    {
        LastMessage = message;
        Debug.LogWarning(message);
        return new ShopResult(false, message, CurrentPlayer != null ? CurrentPlayer.Coins : 0);
    }

    AuthResult Success(string message, PlayerData player)
    {
        LastMessage = message;
        Debug.Log(message);
        return new AuthResult(true, message, player);
    }

    AuthResult Fail(string message)
    {
        LastMessage = message;
        Debug.LogWarning(message);
        return new AuthResult(false, message, null);
    }

    public readonly struct AuthResult
    {
        public readonly bool Success;
        public readonly string Message;
        public readonly PlayerData Player;

        public AuthResult(bool success, string message, PlayerData player)
        {
            Success = success;
            Message = message;
            Player = player;
        }
    }

    public readonly struct ShopResult
    {
        public readonly bool Success;
        public readonly string Message;
        public readonly int Coins;

        public ShopResult(bool success, string message, int coins)
        {
            Success = success;
            Message = message;
            Coins = coins;
        }
    }


void CacheEquippedSkinForGame(string skinId)
    {
        skinId = string.IsNullOrWhiteSpace(skinId) ? SkinCatalog.DefaultSkinId : skinId.Trim();

        PlayerPrefs.SetString("EquippedSkinId", skinId);

        int localPlayerIndex = PlayerPrefs.HasKey("LocalPlayerIndex")
            ? PlayerPrefs.GetInt("LocalPlayerIndex")
            : 0;

        if (localPlayerIndex < 0 || localPlayerIndex > 3)
            localPlayerIndex = 0;

        PlayerPrefs.SetString("PlayerSkin_" + localPlayerIndex, skinId);
        PlayerPrefs.Save();
    }
}
