using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Serialization;

namespace CoCaNgua.Server;

[BsonIgnoreExtraElements]
public class Player
{
    [BsonId, BsonRepresentation(BsonType.ObjectId)] public string Id { get; set; } = ObjectId.GenerateNewId().ToString();
    public string Username { get; set; } = "";
    public string DisplayName { get; set; } = "";
    [JsonIgnore] public string? CredentialHash { get; set; }
    [JsonIgnore] public string? PasswordHash { get; set; }
    [JsonIgnore] public string? PasswordSalt { get; set; }
    [JsonIgnore] public string? Password { get; set; }
    public int Coins { get; set; } = 1000;
    public List<string> OwnedSkinIds { get; set; } = ["default_horse"];
    public string EquippedSkinId { get; set; } = "default_horse";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastLoginUtc { get; set; }
    public DateTime LastSeenUtc { get; set; }
    public bool IsBanned { get; set; }
}

public class PlayerSession
{
    [BsonId] public string Id { get; set; } = "";
    public string PlayerId { get; set; } = "";
    public DateTime ExpiresAtUtc { get; set; }
}

public class Skin
{
    [BsonId] public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int Price { get; set; }
    public bool IsForSale { get; set; } = true;
}

public class Participant
{
    public string PlayerId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int Slot { get; set; }
}

public class Room
{
    [BsonId] public string Id { get; set; } = ObjectId.GenerateNewId().ToString();
    public string Code { get; set; } = "";
    public string HostPlayerId { get; set; } = "";
    public List<string> MemberIds { get; set; } = [];
    public string Status { get; set; } = "Waiting";
    public string? CurrentMatchId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenUtc { get; set; } = DateTime.UtcNow;
}

[BsonIgnoreExtraElements]
public class Match
{
    [BsonId, BsonRepresentation(BsonType.ObjectId)] public string Id { get; set; } = ObjectId.GenerateNewId().ToString();
    public string RoomId { get; set; } = "";
    public string HostPlayerId { get; set; } = "";
    public string HostUsername { get; set; } = "";
    public int PlayerCount { get; set; }
    public List<Participant> Participants { get; set; } = [];
    public List<int> Ranking { get; set; } = [];
    public string? WinnerPlayerId { get; set; }
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAtUtc { get; set; }
    public int? DurationSeconds { get; set; }
    public string Status { get; set; } = "Playing";
    public string EndReason { get; set; } = "";
    public int RewardCoins { get; set; }
    public string Source { get; set; } = "RelayHostReported";
}

public class DiceRoll
{
    [BsonId] public string Id { get; set; } = "";
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

public class LedgerEntry
{
    [BsonId] public string Id { get; set; } = "";
    public string PlayerId { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Reference { get; set; } = "";
    public int Amount { get; set; }
    public int Before { get; set; }
    public int After { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public record Credentials(string Username, string Password, string? DisplayName);
public record SkinRequest(string SkinId, int? ExpectedPrice = null);
public record CreateRoomRequest(string Code);
public record JoinRoomRequest(string Code);
public record StartMatchRequest(string[] PlayerIds);
public record RollRequest(string EventId, int Sequence, int Slot, int Dice1, int Dice2);
public record FinishMatchRequest(int[] Ranking, string Reason);
public record BanRequest(bool IsBanned);
public record EditSkinRequest(string Name, int Price, bool IsForSale);

public class ApiError(int status, string message) : Exception(message)
{
    public int Status { get; } = status;
}
