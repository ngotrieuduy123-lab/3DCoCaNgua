using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

public class PlayerData
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; }

    [BsonElement("username")]
    public string Username { get; set; }

    [BsonElement("password")]
    [BsonIgnoreIfNull]
    public string Password { get; set; }

    [BsonElement("passwordHash")]
    [BsonIgnoreIfNull]
    public string PasswordHash { get; set; }

    [BsonElement("passwordSalt")]
    [BsonIgnoreIfNull]
    public string PasswordSalt { get; set; }

    [BsonElement("displayName")]
    public string DisplayName { get; set; }

    [BsonElement("coins")]
    public int Coins { get; set; }

    [BsonElement("createdAtUtc")]
    public DateTime CreatedAtUtc { get; set; }

    [BsonElement("lastLoginUtc")]
    public DateTime LastLoginUtc { get; set; }
}
