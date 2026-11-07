using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Serialization;

namespace CoCaNgua.Server.Models;

[BsonIgnoreExtraElements]
public class User
{
    [BsonId, BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    public string Username { get; set; } = "";
    public string Password { get; set; } = ""; // BCrypt hashed
    public string Role { get; set; } = "PLAYER"; // "ADMIN" | "PLAYER"
    public string FullName { get; set; } = "";

    // Game Specific Properties
    public int Coins { get; set; } = 1000;
    public List<string> OwnedSkinIds { get; set; } = new() { "default_horse" };
    public string EquippedSkinId { get; set; } = "default_horse";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastLoginUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenUtc { get; set; } = DateTime.UtcNow;
    public bool IsBanned { get; set; } = false;
}

[BsonIgnoreExtraElements]
public class Arena
{
    [BsonId, BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    public string ArenaName { get; set; } = "";
    public string? ArenaDescription { get; set; }
    public string Theme { get; set; } = "Classic";

    [BsonRepresentation(BsonType.ObjectId)]
    public string LastUpdatedUserID { get; set; } = "";
}

// Pond alias for compatibility
[BsonIgnoreExtraElements]
public class Pond : Arena
{
    public string PondName { get => ArenaName; set => ArenaName = value; }
    public string? PondDescription { get => ArenaDescription; set => ArenaDescription = value; }
}

[BsonIgnoreExtraElements]
public class HorseSkin
{
    [BsonId, BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    public string SkinKey { get; set; } = ""; // e.g. "default_horse", "gundam_unicorn", "odin_transformer", "arya"
    public string SkinName { get; set; } = "";
    public string? SkinDescription { get; set; }
    public double Price { get; set; }
    public int Amount { get; set; } = 999;

    [BsonRepresentation(BsonType.ObjectId)]
    public string ArenaId { get; set; } = "";

    public bool IsAvailable { get; set; } = true;
}

// Fish alias for compatibility
[BsonIgnoreExtraElements]
public class Fish : HorseSkin
{
    public string FishName { get => SkinName; set => SkinName = value; }
    public string? FishDescription { get => SkinDescription; set => SkinDescription = value; }
    public string PondId { get => ArenaId; set => ArenaId = value; }
}

[BsonIgnoreExtraElements]
public class PurchaseLog
{
    [BsonId, BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    public string PlayerId { get; set; } = ""; // Username of user
    public string ItemId { get; set; } = "";
    public string SkinKey { get; set; } = "";
    public string SkinName { get; set; } = "";
    public string FishName { get => SkinName; set => SkinName = value; }
    public double Price { get; set; }
    public string Status { get; set; } = "RECEIVED"; // "RECEIVED" | "BROADCASTED"
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

[BsonIgnoreExtraElements]
public class ActionLog
{
    [BsonId, BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    // Plaintext string containing all details in one line/block without separate columns
    public string Log { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

[BsonIgnoreExtraElements]
public class Participant
{
    public string PlayerId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int Slot { get; set; }
}

[BsonIgnoreExtraElements]
public class Room
{
    [BsonId, BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    public string Code { get; set; } = "";
    public string HostPlayerId { get; set; } = "";
    public List<string> MemberIds { get; set; } = new();
    public string Status { get; set; } = "Waiting";
    public string? CurrentMatchId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenUtc { get; set; } = DateTime.UtcNow;
}

[BsonIgnoreExtraElements]
public class Match
{
    [BsonId, BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    public string RoomId { get; set; } = "";
    public string HostPlayerId { get; set; } = "";
    public string HostUsername { get; set; } = "";
    public int PlayerCount { get; set; }
    public List<Participant> Participants { get; set; } = new();
    public List<int> Ranking { get; set; } = new();
    public string? WinnerPlayerId { get; set; }
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAtUtc { get; set; }
    public int? DurationSeconds { get; set; }
    public string Status { get; set; } = "Playing";
    public string EndReason { get; set; } = "";
    public int RewardCoins { get; set; }
    public string Source { get; set; } = "RelayHostReported";
}

[BsonIgnoreExtraElements]
public class DiceRoll
{
    [BsonId, BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    public string MatchId { get; set; } = "";
    public string PlayerId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int Slot { get; set; }
    public int Sequence { get; set; }
    public int Dice1 { get; set; }
    public int Dice2 { get; set; }
    public int Total { get; set; }
    public DateTime RolledAtUtc { get; set; } = DateTime.UtcNow;
}
