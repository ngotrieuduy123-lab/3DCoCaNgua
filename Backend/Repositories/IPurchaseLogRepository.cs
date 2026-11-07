using System.Collections.Concurrent;
using CoCaNgua.Server.Models;
using CoCaNgua.Server.Services;
using MongoDB.Driver;

namespace CoCaNgua.Server.Repositories;

public interface IPurchaseLogRepository
{
    Task CreateAsync(PurchaseLog log);
    Task<PurchaseLog?> GetByIdAsync(string id);
    Task UpdateStatusAsync(string id, string status);
    Task<List<PurchaseLog>> GetAllAsync();
}

public class PurchaseLogRepository : IPurchaseLogRepository
{
    private readonly IMongoCollection<PurchaseLog> _collection;
    private static readonly ConcurrentDictionary<string, PurchaseLog> _inMemory = new();

    public PurchaseLogRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<PurchaseLog>("purchaselogs");
    }

    public async Task CreateAsync(PurchaseLog log)
    {
        _inMemory[log.Id] = log;
        if (DbInitializer.IsMongoConnected)
        {
            try { await _collection.InsertOneAsync(log); }
            catch { }
        }
    }

    public async Task<PurchaseLog?> GetByIdAsync(string id)
    {
        if (DbInitializer.IsMongoConnected)
        {
            try { return await _collection.Find(x => x.Id == id).FirstOrDefaultAsync(); }
            catch { }
        }
        _inMemory.TryGetValue(id, out var log);
        return log;
    }

    public async Task UpdateStatusAsync(string id, string status)
    {
        if (_inMemory.TryGetValue(id, out var log))
        {
            log.Status = status;
        }

        if (DbInitializer.IsMongoConnected)
        {
            try
            {
                await _collection.UpdateOneAsync(
                    x => x.Id == id,
                    Builders<PurchaseLog>.Update.Set(x => x.Status, status)
                );
            }
            catch { }
        }
    }

    public async Task<List<PurchaseLog>> GetAllAsync()
    {
        if (DbInitializer.IsMongoConnected)
        {
            try { return await _collection.Find(_ => true).SortByDescending(x => x.CreatedAt).ToListAsync(); }
            catch { }
        }
        return _inMemory.Values.OrderByDescending(x => x.CreatedAt).ToList();
    }
}
