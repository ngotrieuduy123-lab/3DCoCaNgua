using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class DatabaseManager : MonoBehaviour
{
    public static DatabaseManager Instance;

    [Header("MongoDB")]
    [SerializeField] private string connectionString = "";
    [SerializeField] private string databaseName = "LudoGameDB";
    [SerializeField] private string collectionName = "Players";

    public PlayerData CurrentPlayer { get; private set; }
    public string LastMessage { get; private set; }
    public bool IsConnected => playerCollection != null;

    private IMongoCollection<PlayerData> playerCollection;

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
            EnsureIndexes();

            LastMessage = "Connected to MongoDB.";
            Debug.Log(LastMessage);
        }
        catch (Exception e)
        {
            playerCollection = null;
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
                Coins = 1000,
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
        PlayerPrefs.Save();
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

    void SaveSession(PlayerData player)
    {
        PlayerPrefs.SetString("PlayerId", player.Id);
        PlayerPrefs.SetString("Username", player.Username);
        PlayerPrefs.SetString("DisplayName", player.DisplayName);
        PlayerPrefs.Save();
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
}
