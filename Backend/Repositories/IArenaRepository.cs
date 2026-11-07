using System.Collections.Concurrent;
using CoCaNgua.Server.Models;
using CoCaNgua.Server.Services;
using MongoDB.Driver;

namespace CoCaNgua.Server.Repositories;

public interface IArenaRepository
{
    Task<List<Arena>> GetAllAsync();
    Task<Arena?> GetByIdAsync(string id);
    Task<Arena?> GetByNameAsync(string name);
    Task CreateAsync(Arena arena);
    Task UpdateAsync(Arena arena);
    Task<long> CountAsync();
}

public class ArenaRepository : IArenaRepository
{
    private readonly IMongoCollection<Arena> _collection;
    private static readonly ConcurrentDictionary<string, Arena> _inMemory = new();

    static ArenaRepository()
    {
        var defaultArena = new Arena
        {
            Id = "671000000000000000000010",
            ArenaName = "Đấu trường Cờ Cá Ngựa Hoàng Gia",
            ArenaDescription = "Bàn cờ truyền thống 4 màu kinh điển với chuồng và đường đua tiêu chuẩn",
            Theme = "Classic",
            LastUpdatedUserID = "671000000000000000000001"
        };
        _inMemory[defaultArena.Id] = defaultArena;

        var sciFiArena = new Arena
        {
            Id = "671000000000000000000020",
            ArenaName = "Đấu trường Không Gian Tương Lai",
            ArenaDescription = "Bàn cờ vũ trụ phong cách Cyberpunk với hiệu ứng ánh sáng Neon",
            Theme = "SciFi",
            LastUpdatedUserID = "671000000000000000000001"
        };
        _inMemory[sciFiArena.Id] = sciFiArena;
    }

    public ArenaRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<Arena>("arenas");
    }

    public async Task<List<Arena>> GetAllAsync()
    {
        if (DbInitializer.IsMongoConnected)
        {
            try { return await _collection.Find(_ => true).ToListAsync(); }
            catch { }
        }
        return _inMemory.Values.ToList();
    }

    public async Task<Arena?> GetByIdAsync(string id)
    {
        if (DbInitializer.IsMongoConnected)
        {
            try { return await _collection.Find(x => x.Id == id).FirstOrDefaultAsync(); }
            catch { }
        }
        _inMemory.TryGetValue(id, out var arena);
        return arena;
    }

    public async Task<Arena?> GetByNameAsync(string name)
    {
        if (DbInitializer.IsMongoConnected)
        {
            try { return await _collection.Find(x => x.ArenaName.ToLower() == name.ToLower()).FirstOrDefaultAsync(); }
            catch { }
        }
        return _inMemory.Values.FirstOrDefault(x => string.Equals(x.ArenaName, name, StringComparison.OrdinalIgnoreCase));
    }

    public async Task CreateAsync(Arena arena)
    {
        _inMemory[arena.Id] = arena;
        if (DbInitializer.IsMongoConnected)
        {
            try { await _collection.InsertOneAsync(arena); }
            catch { }
        }
    }

    public async Task UpdateAsync(Arena arena)
    {
        _inMemory[arena.Id] = arena;
        if (DbInitializer.IsMongoConnected)
        {
            try { await _collection.ReplaceOneAsync(x => x.Id == arena.Id, arena); }
            catch { }
        }
    }

    public async Task<long> CountAsync()
    {
        if (DbInitializer.IsMongoConnected)
        {
            try { return await _collection.CountDocumentsAsync(_ => true); }
            catch { }
        }
        return _inMemory.Count;
    }
}
