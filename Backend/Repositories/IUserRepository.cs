using System.Collections.Concurrent;
using CoCaNgua.Server.Models;
using CoCaNgua.Server.Services;
using MongoDB.Driver;

namespace CoCaNgua.Server.Repositories;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(string id);
    Task<User?> GetByUsernameAsync(string username);
    Task CreateAsync(User user);
    Task UpdateAsync(User user);
    Task<List<User>> GetAllAsync();
    Task<long> CountAsync();
}

public class UserRepository : IUserRepository
{
    private readonly IMongoCollection<User> _collection;
    private static readonly ConcurrentDictionary<string, User> _inMemory = new(StringComparer.OrdinalIgnoreCase);

    static UserRepository()
    {
        string defaultHash = BCrypt.Net.BCrypt.HashPassword("123456789");
        var admin = new User { Id = "671000000000000000000001", Username = "admin", Password = defaultHash, Role = "ADMIN", FullName = "Administrator", Coins = 100000 };
        var player1 = new User { Id = "671000000000000000000002", Username = "player1", Password = defaultHash, Role = "PLAYER", FullName = "Player One", Coins = 10000 };
        var player2 = new User { Id = "671000000000000000000003", Username = "player2", Password = defaultHash, Role = "PLAYER", FullName = "Player Two", Coins = 10000 };
        _inMemory[admin.Username] = admin;
        _inMemory[player1.Username] = player1;
        _inMemory[player2.Username] = player2;
    }

    public UserRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<User>("users");
    }

    public async Task<User?> GetByIdAsync(string id)
    {
        if (DbInitializer.IsMongoConnected)
        {
            try { return await _collection.Find(x => x.Id == id).FirstOrDefaultAsync(); }
            catch { }
        }
        return _inMemory.Values.FirstOrDefault(x => x.Id == id);
    }

    public async Task<User?> GetByUsernameAsync(string username)
    {
        if (DbInitializer.IsMongoConnected)
        {
            try { return await _collection.Find(x => x.Username.ToLower() == username.ToLower()).FirstOrDefaultAsync(); }
            catch { }
        }
        _inMemory.TryGetValue(username, out var user);
        return user;
    }

    public async Task CreateAsync(User user)
    {
        _inMemory[user.Username] = user;
        if (DbInitializer.IsMongoConnected)
        {
            try { await _collection.InsertOneAsync(user); }
            catch { }
        }
    }

    public async Task UpdateAsync(User user)
    {
        _inMemory[user.Username] = user;
        if (DbInitializer.IsMongoConnected)
        {
            try { await _collection.ReplaceOneAsync(x => x.Id == user.Id, user); }
            catch { }
        }
    }

    public async Task<List<User>> GetAllAsync()
    {
        if (DbInitializer.IsMongoConnected)
        {
            try { return await _collection.Find(_ => true).ToListAsync(); }
            catch { }
        }
        return _inMemory.Values.ToList();
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
