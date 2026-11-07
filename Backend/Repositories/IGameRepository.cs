using System.Collections.Concurrent;
using CoCaNgua.Server.Models;
using CoCaNgua.Server.Services;
using MongoDB.Driver;

namespace CoCaNgua.Server.Repositories;

public interface IGameRepository
{
    Task<Room> CreateRoomAsync(string hostPlayerId, string code);
    Task<Room?> GetRoomByCodeAsync(string code);
    Task<Room?> GetRoomByIdAsync(string id);
    Task<Room?> JoinRoomAsync(string playerId, string code);
    Task LeaveAllRoomsAsync(string playerId);
    Task<bool> UpdateRoomHeartbeatAsync(string id, string hostPlayerId);
    Task<Match> StartMatchAsync(string hostPlayerId, string roomId, string[] playerIds);
    Task<Match?> GetMatchByIdAsync(string id);
    Task FinishMatchAsync(string matchId, string? winnerPlayerId, int rewardCoins);
    Task RecordRollAsync(DiceRoll roll);
}

public class GameRepository : IGameRepository
{
    private readonly IMongoCollection<Room> _rooms;
    private readonly IMongoCollection<Match> _matches;
    private readonly IMongoCollection<DiceRoll> _rolls;
    private readonly IMongoCollection<User> _users;
    private readonly IUserRepository _userRepo;

    private static readonly ConcurrentDictionary<string, Room> _inMemoryRooms = new();
    private static readonly ConcurrentDictionary<string, Match> _inMemoryMatches = new();
    private static readonly List<DiceRoll> _inMemoryRolls = new();

    public GameRepository(IMongoDatabase database, IUserRepository userRepo)
    {
        _rooms = database.GetCollection<Room>("Rooms");
        _matches = database.GetCollection<Match>("MatchHistories");
        _rolls = database.GetCollection<DiceRoll>("DiceRolls");
        _users = database.GetCollection<User>("users");
        _userRepo = userRepo;
    }

    public async Task<Room> CreateRoomAsync(string hostPlayerId, string code)
    {
        await LeaveAllRoomsAsync(hostPlayerId);
        var room = new Room
        {
            Code = code.Trim().ToUpperInvariant(),
            HostPlayerId = hostPlayerId,
            MemberIds = new List<string> { hostPlayerId },
            Status = "Waiting"
        };

        _inMemoryRooms[room.Id] = room;

        if (DbInitializer.IsMongoConnected)
        {
            try { await _rooms.InsertOneAsync(room); } catch { }
        }
        return room;
    }

    public async Task<Room?> GetRoomByCodeAsync(string code)
    {
        string normCode = code.Trim().ToUpperInvariant();
        if (DbInitializer.IsMongoConnected)
        {
            try { return await _rooms.Find(x => x.Code == normCode && x.Status != "Closed").FirstOrDefaultAsync(); } catch { }
        }
        return _inMemoryRooms.Values.FirstOrDefault(x => x.Code == normCode && x.Status != "Closed");
    }

    public async Task<Room?> GetRoomByIdAsync(string id)
    {
        if (DbInitializer.IsMongoConnected)
        {
            try { return await _rooms.Find(x => x.Id == id).FirstOrDefaultAsync(); } catch { }
        }
        _inMemoryRooms.TryGetValue(id, out var room);
        return room;
    }

    public async Task<Room?> JoinRoomAsync(string playerId, string code)
    {
        await LeaveAllRoomsAsync(playerId);
        var room = await GetRoomByCodeAsync(code);
        if (room == null) return null;

        if (!room.MemberIds.Contains(playerId))
        {
            room.MemberIds.Add(playerId);
            room.LastSeenUtc = DateTime.UtcNow;

            if (DbInitializer.IsMongoConnected)
            {
                try
                {
                    await _rooms.UpdateOneAsync(
                        x => x.Id == room.Id,
                        Builders<Room>.Update.Set(x => x.MemberIds, room.MemberIds).Set(x => x.LastSeenUtc, DateTime.UtcNow)
                    );
                }
                catch { }
            }
        }
        return room;
    }

    public async Task LeaveAllRoomsAsync(string playerId)
    {
        var localRooms = _inMemoryRooms.Values.Where(x => x.MemberIds.Contains(playerId) && x.Status != "Closed").ToList();
        foreach (var r in localRooms)
        {
            r.MemberIds.Remove(playerId);
            if (r.HostPlayerId == playerId || r.MemberIds.Count == 0) r.Status = "Closed";
        }

        if (DbInitializer.IsMongoConnected)
        {
            try
            {
                var rooms = await _rooms.Find(x => x.MemberIds.Contains(playerId) && x.Status != "Closed").ToListAsync();
                foreach (var r in rooms)
                {
                    r.MemberIds.Remove(playerId);
                    if (r.HostPlayerId == playerId || r.MemberIds.Count == 0)
                    {
                        await _rooms.UpdateOneAsync(x => x.Id == r.Id, Builders<Room>.Update.Set(x => x.Status, "Closed"));
                    }
                    else
                    {
                        await _rooms.UpdateOneAsync(x => x.Id == r.Id, Builders<Room>.Update.Set(x => x.MemberIds, r.MemberIds));
                    }
                }
            }
            catch { }
        }
    }

    public async Task<bool> UpdateRoomHeartbeatAsync(string id, string hostPlayerId)
    {
        if (_inMemoryRooms.TryGetValue(id, out var r) && r.HostPlayerId == hostPlayerId && r.Status != "Closed")
        {
            r.LastSeenUtc = DateTime.UtcNow;
        }

        if (DbInitializer.IsMongoConnected)
        {
            try
            {
                var res = await _rooms.UpdateOneAsync(
                    x => x.Id == id && x.HostPlayerId == hostPlayerId && x.Status != "Closed",
                    Builders<Room>.Update.Set(x => x.LastSeenUtc, DateTime.UtcNow)
                );
                return res.MatchedCount > 0;
            }
            catch { }
        }
        return r != null;
    }

    public async Task<Match> StartMatchAsync(string hostPlayerId, string roomId, string[] playerIds)
    {
        var participants = new List<Participant>();
        for (int i = 0; i < playerIds.Length; i++)
        {
            var u = await _userRepo.GetByIdAsync(playerIds[i]);
            participants.Add(new Participant
            {
                PlayerId = playerIds[i],
                DisplayName = u?.FullName ?? ("Player " + i),
                Slot = i
            });
        }

        var match = new Match
        {
            RoomId = roomId,
            HostPlayerId = hostPlayerId,
            PlayerCount = playerIds.Length,
            Participants = participants,
            Status = "Playing"
        };

        _inMemoryMatches[match.Id] = match;
        if (_inMemoryRooms.TryGetValue(roomId, out var room))
        {
            room.CurrentMatchId = match.Id;
        }

        if (DbInitializer.IsMongoConnected)
        {
            try
            {
                await _matches.InsertOneAsync(match);
                await _rooms.UpdateOneAsync(x => x.Id == roomId, Builders<Room>.Update.Set(x => x.CurrentMatchId, match.Id));
            }
            catch { }
        }
        return match;
    }

    public async Task<Match?> GetMatchByIdAsync(string id)
    {
        if (DbInitializer.IsMongoConnected)
        {
            try { return await _matches.Find(x => x.Id == id).FirstOrDefaultAsync(); } catch { }
        }
        _inMemoryMatches.TryGetValue(id, out var match);
        return match;
    }

    public async Task FinishMatchAsync(string matchId, string? winnerPlayerId, int rewardCoins)
    {
        if (_inMemoryMatches.TryGetValue(matchId, out var match))
        {
            match.Status = "Finished";
            match.EndedAtUtc = DateTime.UtcNow;
            match.WinnerPlayerId = winnerPlayerId;
            match.RewardCoins = rewardCoins;
        }

        if (!string.IsNullOrEmpty(winnerPlayerId) && rewardCoins > 0)
        {
            var user = await _userRepo.GetByIdAsync(winnerPlayerId);
            if (user != null)
            {
                user.Coins += rewardCoins;
                await _userRepo.UpdateAsync(user);
            }
        }

        if (DbInitializer.IsMongoConnected)
        {
            try
            {
                await _matches.UpdateOneAsync(
                    x => x.Id == matchId,
                    Builders<Match>.Update
                        .Set(x => x.Status, "Finished")
                        .Set(x => x.EndedAtUtc, DateTime.UtcNow)
                        .Set(x => x.WinnerPlayerId, winnerPlayerId)
                        .Set(x => x.RewardCoins, rewardCoins)
                );
            }
            catch { }
        }
    }

    public async Task RecordRollAsync(DiceRoll roll)
    {
        lock (_inMemoryRolls)
        {
            _inMemoryRolls.Add(roll);
        }

        if (DbInitializer.IsMongoConnected)
        {
            try { await _rolls.InsertOneAsync(roll); } catch { }
        }
    }
}
