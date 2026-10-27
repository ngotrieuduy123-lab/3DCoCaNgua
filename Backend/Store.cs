using Microsoft.AspNetCore.Identity;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace CoCaNgua.Server;

public sealed class Store
{
    public readonly MongoClient Client;
    public readonly IMongoDatabase Database;
    public IMongoCollection<Player> Players => Database.GetCollection<Player>("Players");
    public IMongoCollection<PlayerSession> Sessions => Database.GetCollection<PlayerSession>("BackendSessions");
    public IMongoCollection<Skin> Skins => Database.GetCollection<Skin>("Skins");
    public IMongoCollection<Room> Rooms => Database.GetCollection<Room>("Rooms");
    public IMongoCollection<Match> Matches => Database.GetCollection<Match>("MatchHistories");
    public IMongoCollection<DiceRoll> Rolls => Database.GetCollection<DiceRoll>("DiceRolls");
    public IMongoCollection<LedgerEntry> Ledger => Database.GetCollection<LedgerEntry>("CoinTransactions");
    readonly PasswordHasher<string> hasher = new();

    static Store() => ConventionRegistry.Register("CoCaNguaCamelCase", new ConventionPack { new CamelCaseElementNameConvention() }, t => t.Namespace == typeof(Store).Namespace);

    public Store(IConfiguration config)
    {
        string uri = config["Mongo:ConnectionString"] ?? "";
        if (string.IsNullOrWhiteSpace(uri)) throw new InvalidOperationException("Set Mongo:ConnectionString in appsettings.Local.json or Mongo__ConnectionString.");
        var settings = MongoClientSettings.FromConnectionString(uri);
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(8);
        settings.ConnectTimeout = TimeSpan.FromSeconds(8);
        settings.SocketTimeout = TimeSpan.FromSeconds(15);
        settings.ApplicationName = "CoCaNgua.Backend";
        Client = new MongoClient(settings);
        Database = Client.GetDatabase(config["Mongo:Database"] ?? "LudoGameDB");
    }

    public async Task Initialize()
    {
        await Database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
        await Players.Indexes.CreateOneAsync(new CreateIndexModel<Player>(Builders<Player>.IndexKeys.Ascending(x => x.Username), new CreateIndexOptions { Unique = true }));
        await Sessions.Indexes.CreateOneAsync(new CreateIndexModel<PlayerSession>(Builders<PlayerSession>.IndexKeys.Ascending(x => x.ExpiresAtUtc), new CreateIndexOptions { ExpireAfter = TimeSpan.Zero }));
        await Rooms.Indexes.CreateOneAsync(new CreateIndexModel<Room>(Builders<Room>.IndexKeys.Ascending(x => x.Code)));
        await Matches.Indexes.CreateOneAsync(new CreateIndexModel<Match>(Builders<Match>.IndexKeys.Descending(x => x.StartedAtUtc)));
        await Rolls.Indexes.CreateOneAsync(new CreateIndexModel<DiceRoll>(Builders<DiceRoll>.IndexKeys.Ascending(x => x.MatchId).Descending(x => x.Sequence), new CreateIndexOptions { Unique = true }));
        await Ledger.Indexes.CreateOneAsync(new CreateIndexModel<LedgerEntry>(Builders<LedgerEntry>.IndexKeys.Ascending(x => x.PlayerId).Descending(x => x.CreatedAtUtc)));
        foreach (var skin in new[] {
            new Skin { Id = "default_horse", Name = "Default Horse", Description = "Classic horse piece", Price = 0 },
            new Skin { Id = "gundam_unicorn", Name = "Gundam Unicorn", Description = "Unicorn mobile suit piece", Price = 5000 },
            new Skin { Id = "odin_transformer", Name = "Odin", Description = "Transforms into an aircraft while moving.", Price = 5000 },
            new Skin { Id = "arya", Name = "Arya", Description = "Arya character with walking and left/right turn animations.", Price = 5000 } })
        {
            // Existing prices and availability belong to the administrator.
            await Skins.UpdateOneAsync(x => x.Id == skin.Id, Builders<Skin>.Update
                .SetOnInsert(x => x.Name, skin.Name).SetOnInsert(x => x.Description, skin.Description)
                .SetOnInsert(x => x.Price, skin.Price).SetOnInsert(x => x.IsForSale, true), new UpdateOptions { IsUpsert = true });
        }
    }

    public static string TokenHash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    public async Task<Player?> Authenticate(string token)
    {
        if (token.Length != 64) return null;
        var session = await Sessions.Find(x => x.Id == TokenHash(token) && x.ExpiresAtUtc > DateTime.UtcNow).FirstOrDefaultAsync();
        if (session == null) return null;
        return await Players.Find(x => x.Id == session.PlayerId && !x.IsBanned).FirstOrDefaultAsync();
    }

    public async Task<object> Login(Credentials request, bool register)
    {
        string username = (request.Username ?? "").Trim().ToLowerInvariant();
        string password = request.Password ?? "";
        if ((register ? !Regex.IsMatch(username, @"^[a-z0-9_.-]{3,32}$") : username.Length is < 3 or > 128 || username.Any(char.IsControl)) || password.Length < 6 || password.Length > 128)
            throw new ApiError(400, "Username: 3–32 letters/digits/._-; password: 6–128 characters.");
        Player? player;
        if (register)
        {
            string name = string.IsNullOrWhiteSpace(request.DisplayName) ? username : request.DisplayName.Trim();
            if (name.Length > 24 || name.Any(char.IsControl)) throw new ApiError(400, "Display name must contain at most 24 characters.");
            player = new Player { Username = username, DisplayName = name, CredentialHash = hasher.HashPassword(username, password), LastLoginUtc = DateTime.UtcNow, LastSeenUtc = DateTime.UtcNow };
            try
            {
                await Transaction(async session => {
                    await Players.InsertOneAsync(session, player);
                    await Ledger.InsertOneAsync(session, new LedgerEntry { Id = "signup:" + player.Id, PlayerId = player.Id, Kind = "Signup", Amount = 1000, Before = 0, After = 1000 });
                    return true;
                });
            }
            catch (MongoWriteException e) when (e.WriteError.Category == ServerErrorCategory.DuplicateKey) { throw new ApiError(409, "Username already exists."); }
        }
        else
        {
            player = await Players.Find(x => x.Username == username).FirstOrDefaultAsync();
            if (player == null || !VerifyPassword(player, password)) throw new ApiError(401, "Wrong username or password.");
            if (player.IsBanned) throw new ApiError(403, "Account is locked.");
            player.LastLoginUtc = DateTime.UtcNow;
            player.LastSeenUtc = DateTime.UtcNow;
            var update = Builders<Player>.Update.Set(x => x.LastLoginUtc, player.LastLoginUtc).Set(x => x.LastSeenUtc, player.LastSeenUtc)
                .Set(x => x.CredentialHash, hasher.HashPassword(username, password)).Unset(x => x.Password).Unset(x => x.PasswordHash).Unset(x => x.PasswordSalt)
                .AddToSet(x => x.OwnedSkinIds, "default_horse");
            if (await Skins.Find(x => x.Id == player.EquippedSkinId).FirstOrDefaultAsync() == null)
            {
                player.EquippedSkinId = "default_horse";
                update = update.Set(x => x.EquippedSkinId, "default_horse");
            }
            await Players.UpdateOneAsync(x => x.Id == player.Id, update);
            player = await Players.Find(x => x.Id == player.Id).FirstAsync();
        }
        string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        await Sessions.InsertOneAsync(new PlayerSession { Id = TokenHash(token), PlayerId = player.Id, ExpiresAtUtc = DateTime.UtcNow.AddHours(12) });
        return new { token, player = View(player), message = register ? "Register success." : "Login success." };
    }

    bool VerifyPassword(Player player, string password)
    {
        try
        {
            if (!string.IsNullOrEmpty(player.CredentialHash)) return hasher.VerifyHashedPassword(player.Username, player.CredentialHash, password) != PasswordVerificationResult.Failed;
            if (!string.IsNullOrEmpty(player.PasswordHash) && !string.IsNullOrEmpty(player.PasswordSalt))
            {
                using var hmac = new HMACSHA256(Convert.FromBase64String(player.PasswordSalt));
                return CryptographicOperations.FixedTimeEquals(hmac.ComputeHash(Encoding.UTF8.GetBytes(password)), Convert.FromBase64String(player.PasswordHash));
            }
            return player.Password != null && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(password), Encoding.UTF8.GetBytes(player.Password));
        }
        catch (FormatException) { return false; }
    }

    public static object View(Player p) => new { p.Id, p.Username, p.DisplayName, p.Coins, p.OwnedSkinIds, p.EquippedSkinId, p.CreatedAtUtc, p.LastLoginUtc, p.LastSeenUtc, p.IsBanned };

    public async Task<T> Transaction<T>(Func<IClientSessionHandle, Task<T>> action)
    {
        using var session = await Client.StartSessionAsync();
        return await session.WithTransactionAsync(async (s, _) => await action(s));
    }

    public async Task<Player> Purchase(Player player, string skinId, int? expectedPrice)
    {
        var skin = await Skins.Find(x => x.Id == skinId && x.IsForSale).FirstOrDefaultAsync() ?? throw new ApiError(404, "Skin is not for sale.");
        if (expectedPrice == null || expectedPrice.Value != skin.Price) throw new ApiError(409, "Skin price changed. Reopen the shop before purchasing.");
        return await Transaction(async session => {
            var updated = await Players.FindOneAndUpdateAsync(session,
                Builders<Player>.Filter.Eq(x => x.Id, player.Id) & Builders<Player>.Filter.Gte(x => x.Coins, skin.Price) & Builders<Player>.Filter.Ne("ownedSkinIds", skinId) & Builders<Player>.Filter.Ne(x => x.IsBanned, true),
                Builders<Player>.Update.Inc(x => x.Coins, -skin.Price).AddToSet(x => x.OwnedSkinIds, skinId),
                new FindOneAndUpdateOptions<Player> { ReturnDocument = ReturnDocument.After });
            if (updated == null) throw new ApiError(409, "Skin already owned or insufficient coins.");
            await Ledger.InsertOneAsync(session, new LedgerEntry { Id = "purchase:" + player.Id + ":" + skinId, PlayerId = player.Id, Kind = "SkinPurchase", Reference = skinId, Amount = -skin.Price, Before = updated.Coins + skin.Price, After = updated.Coins });
            return updated;
        });
    }

    public async Task<Player> Equip(Player player, string skinId)
    {
        if (await Skins.Find(x => x.Id == skinId).FirstOrDefaultAsync() == null) throw new ApiError(404, "Unknown skin.");
        return await Players.FindOneAndUpdateAsync(x => x.Id == player.Id && x.OwnedSkinIds.Contains(skinId) && !x.IsBanned,
            Builders<Player>.Update.Set(x => x.EquippedSkinId, skinId), new FindOneAndUpdateOptions<Player> { ReturnDocument = ReturnDocument.After }) ?? throw new ApiError(403, "You do not own this skin.");
    }

    public async Task<Room> CreateRoom(Player player, string code)
    {
        code = (code ?? "").Trim().ToUpperInvariant();
        if (!Regex.IsMatch(code, "^[A-Z0-9]{4,12}$")) throw new ApiError(400, "Invalid Relay code.");
        await LeaveAllRooms(player.Id);
        var room = new Room { Code = code, HostPlayerId = player.Id, MemberIds = [player.Id] };
        await Rooms.InsertOneAsync(room);
        return room;
    }

    public async Task<Room> JoinRoom(Player player, string code)
    {
        code = (code ?? "").Trim().ToUpperInvariant();
        var room = await Rooms.Find(x => x.Code == code && x.Status == "Waiting" && x.LastSeenUtc > DateTime.UtcNow.AddSeconds(-90)).SortByDescending(x => x.CreatedAtUtc).FirstOrDefaultAsync()
            ?? throw new ApiError(404, "Room is unavailable or host is offline.");
        if (room.MemberIds.Contains(player.Id)) return room;
        await LeaveAllRooms(player.Id);
        var filter = Builders<Room>.Filter.Eq(x => x.Id, room.Id) & Builders<Room>.Filter.Eq(x => x.Status, "Waiting") & Builders<Room>.Filter.SizeLt(x => x.MemberIds, 4);
        return await Rooms.FindOneAndUpdateAsync(filter, Builders<Room>.Update.AddToSet(x => x.MemberIds, player.Id), new FindOneAndUpdateOptions<Room> { ReturnDocument = ReturnDocument.After })
            ?? throw new ApiError(409, "Room is full or has already started.");
    }

    public async Task LeaveAllRooms(string playerId)
    {
        var hosted = await Rooms.Find(x => x.HostPlayerId == playerId && x.Status != "Closed").ToListAsync();
        await Rooms.UpdateManyAsync(x => x.HostPlayerId == playerId && x.Status != "Closed", Builders<Room>.Update.Set(x => x.Status, "Closed"));
        foreach (var room in hosted)
            if (!string.IsNullOrEmpty(room.CurrentMatchId))
                await Matches.UpdateOneAsync(x => x.Id == room.CurrentMatchId && x.Status == "Playing", Builders<Match>.Update.Set(x => x.Status, "Aborted").Set(x => x.EndReason, "Host left or account locked").Set(x => x.EndedAtUtc, DateTime.UtcNow));
        await Rooms.UpdateManyAsync(x => x.MemberIds.Contains(playerId) && x.Status == "Waiting", Builders<Room>.Update.Pull(x => x.MemberIds, playerId));
    }

    public async Task<Match> StartMatch(Player host, string roomId, string[] ids)
    {
        if (ids == null || ids.Length < 2 || ids.Length > 4 || ids.Distinct().Count() != ids.Length || !ids.Contains(host.Id) || ids.Any(id => !ObjectId.TryParse(id, out _))) throw new ApiError(400, "Match needs 2–4 distinct authenticated players including host.");
        return await Transaction(async session => {
            var room = await Rooms.Find(session, x => x.Id == roomId && x.HostPlayerId == host.Id && x.LastSeenUtc > DateTime.UtcNow.AddSeconds(-90)).FirstOrDefaultAsync() ?? throw new ApiError(403, "Only an active room host can start a match.");
            if (room.Status == "Playing" && room.CurrentMatchId != null) return await Matches.Find(session, x => x.Id == room.CurrentMatchId).FirstAsync();
            if (room.Status != "Waiting" || ids.Any(id => !room.MemberIds.Contains(id))) throw new ApiError(409, "Every participant must join this room through the backend.");
            var members = await Players.Find(session, x => ids.Contains(x.Id) && !x.IsBanned).ToListAsync();
            if (members.Count != ids.Length) throw new ApiError(409, "A participant is unavailable.");
            var match = new Match { RoomId = roomId, HostPlayerId = host.Id, HostUsername = host.Username, PlayerCount = ids.Length,
                Participants = ids.Select((id, slot) => new Participant { PlayerId = id, Slot = slot, DisplayName = members.Single(p => p.Id == id).DisplayName }).ToList() };
            await Matches.InsertOneAsync(session, match);
            await Rooms.UpdateOneAsync(session, x => x.Id == roomId, Builders<Room>.Update.Set(x => x.Status, "Playing").Set(x => x.CurrentMatchId, match.Id));
            return match;
        });
    }

    public async Task RecordRoll(Player host, string matchId, RollRequest request)
    {
        if (!ObjectId.TryParse(matchId, out _) || !Guid.TryParse(request.EventId, out _) || request.Sequence < 1 || request.Dice1 is < 1 or > 6 || request.Dice2 is < 1 or > 6) throw new ApiError(400, "Invalid dice event.");
        try
        {
            await Transaction(async session => {
            var match = await Matches.Find(session, x => x.Id == matchId && x.HostPlayerId == host.Id).FirstOrDefaultAsync() ?? throw new ApiError(403, "Only the match host can report dice.");
            string eventId = matchId + ":" + request.EventId;
            var existing = await Rolls.Find(session, x => x.Id == eventId).FirstOrDefaultAsync();
            if (existing != null)
            {
                if (existing.Sequence != request.Sequence || existing.Slot != request.Slot || existing.Dice1 != request.Dice1 || existing.Dice2 != request.Dice2) throw new ApiError(409, "Dice event cannot be changed.");
                return true;
            }
            if (match.Status != "Playing") throw new ApiError(409, "Match already ended.");
            var member = match.Participants.SingleOrDefault(x => x.Slot == request.Slot) ?? throw new ApiError(400, "Invalid player slot.");
            await Rolls.InsertOneAsync(session, new DiceRoll { Id = eventId, MatchId = matchId, PlayerId = member.PlayerId, DisplayName = member.DisplayName, Slot = member.Slot,
                Sequence = request.Sequence, Dice1 = request.Dice1, Dice2 = request.Dice2, Total = request.Dice1 + request.Dice2 });
            // Serialize writes against match completion: a roll cannot commit after an ended match.
            await Matches.UpdateOneAsync(session, x => x.Id == matchId, Builders<Match>.Update.Set("lastRollSequence", request.Sequence));
            return true;
            });
        }
        catch (MongoWriteException e) when (e.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            // A timed-out HTTP upload may still commit before its retry reaches us.
            var existing = await Rolls.Find(x => x.Id == matchId + ":" + request.EventId).FirstOrDefaultAsync();
            if (existing == null || existing.Sequence != request.Sequence || existing.Slot != request.Slot || existing.Dice1 != request.Dice1 || existing.Dice2 != request.Dice2)
                throw new ApiError(409, "Dice sequence/event already exists with different values.");
        }
    }

    public async Task<Match> FinishMatch(Player host, string matchId, FinishMatchRequest request)
    {
        if (!ObjectId.TryParse(matchId, out _) || request.Ranking == null || (request.Reason ?? "").Length > 120) throw new ApiError(400, "Invalid match result.");
        return await Transaction(async session => {
            var match = await Matches.Find(session, x => x.Id == matchId && x.HostPlayerId == host.Id).FirstOrDefaultAsync() ?? throw new ApiError(403, "Only the match host can finish it.");
            if (match.Status != "Playing") return match;
            bool completed = request.Ranking.Length > 0;
            if (completed && (request.Ranking.Length != match.PlayerCount || request.Ranking.Distinct().Count() != match.PlayerCount || request.Ranking.Any(i => i < 0 || i >= match.PlayerCount))) throw new ApiError(400, "Ranking must contain every participant exactly once.");
            match.Status = completed ? "Ended" : "Aborted";
            match.EndedAtUtc = DateTime.UtcNow;
            match.DurationSeconds = Math.Max(0, (int)(match.EndedAtUtc.Value - match.StartedAtUtc).TotalSeconds);
            match.EndReason = request.Reason ?? "";
            match.Ranking = request.Ranking.ToList();
            if (completed)
            {
                match.WinnerPlayerId = match.Participants.Single(x => x.Slot == request.Ranking[0]).PlayerId;
                var winner = await Players.FindOneAndUpdateAsync(session, Builders<Player>.Filter.Eq(x => x.Id, match.WinnerPlayerId) & Builders<Player>.Filter.Ne(x => x.IsBanned, true),
                    Builders<Player>.Update.Inc(x => x.Coins, 1000), new FindOneAndUpdateOptions<Player> { ReturnDocument = ReturnDocument.After });
                if (winner != null)
                {
                    match.RewardCoins = 1000;
                    await Ledger.InsertOneAsync(session, new LedgerEntry { Id = "win:" + matchId, PlayerId = winner.Id, Kind = "MatchWin", Reference = matchId, Amount = 1000, Before = winner.Coins - 1000, After = winner.Coins });
                }
            }
            await Matches.ReplaceOneAsync(session, x => x.Id == matchId, match);
            await Rooms.UpdateOneAsync(session, x => x.Id == match.RoomId, Builders<Room>.Update.Set(x => x.Status, "Closed"));
            return match;
        });
    }
}
