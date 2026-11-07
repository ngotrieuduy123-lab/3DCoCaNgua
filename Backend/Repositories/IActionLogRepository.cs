using System.Collections.Concurrent;
using CoCaNgua.Server.Models;
using CoCaNgua.Server.Services;
using MongoDB.Driver;

namespace CoCaNgua.Server.Repositories;

public interface IActionLogRepository
{
    Task LogAsync(string plaintext);
    Task<List<ActionLog>> GetAllAsync(int limit = 100);
}

public class ActionLogRepository : IActionLogRepository
{
    private readonly IMongoCollection<ActionLog> _collection;
    private static readonly ConcurrentQueue<ActionLog> _inMemory = new();

    public ActionLogRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<ActionLog>("action_logs");
    }

    public async Task LogAsync(string plaintext)
    {
        var entry = new ActionLog
        {
            Log = plaintext,
            CreatedAt = DateTime.UtcNow
        };

        _inMemory.Enqueue(entry);
        while (_inMemory.Count > 500 && _inMemory.TryDequeue(out _)) { }

        if (DbInitializer.IsMongoConnected)
        {
            try
            {
                await _collection.InsertOneAsync(entry);
            }
            catch { }
        }
    }

    public async Task<List<ActionLog>> GetAllAsync(int limit = 100)
    {
        if (DbInitializer.IsMongoConnected)
        {
            try
            {
                return await _collection.Find(_ => true)
                    .SortByDescending(x => x.CreatedAt)
                    .Limit(limit)
                    .ToListAsync();
            }
            catch { }
        }
        return _inMemory.Reverse().Take(limit).ToList();
    }
}
