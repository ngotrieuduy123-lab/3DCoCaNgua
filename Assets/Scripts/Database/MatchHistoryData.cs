using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

public class MatchHistoryData
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; }

    [BsonElement("hostPlayerId")]
    public string HostPlayerId { get; set; }

    [BsonElement("hostUsername")]
    public string HostUsername { get; set; }

    [BsonElement("playerCount")]
    public int PlayerCount { get; set; }

    [BsonElement("startedAtUtc")]
    public DateTime StartedAtUtc { get; set; }

    [BsonElement("startedAtLocal")]
    public DateTime StartedAtLocal { get; set; }

    [BsonElement("startedAtText")]
    public string StartedAtText { get; set; }

    [BsonElement("endedAtUtc")]
    [BsonIgnoreIfNull]
    public DateTime? EndedAtUtc { get; set; }

    [BsonElement("endedAtLocal")]
    [BsonIgnoreIfNull]
    public DateTime? EndedAtLocal { get; set; }

    [BsonElement("endedAtText")]
    [BsonIgnoreIfNull]
    public string EndedAtText { get; set; }

    [BsonElement("durationSeconds")]
    [BsonIgnoreIfNull]
    public int? DurationSeconds { get; set; }

    [BsonElement("endReason")]
    [BsonIgnoreIfNull]
    public string EndReason { get; set; }

    [BsonElement("status")]
    public string Status { get; set; }
}
