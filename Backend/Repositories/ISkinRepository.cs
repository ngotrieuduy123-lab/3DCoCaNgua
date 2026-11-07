using System.Collections.Concurrent;
using CoCaNgua.Server.Models;
using CoCaNgua.Server.Services;
using MongoDB.Driver;

namespace CoCaNgua.Server.Repositories;

public interface ISkinRepository
{
    Task<List<HorseSkin>> GetAllAsync();
    Task<HorseSkin?> GetByIdAsync(string id);
    Task<HorseSkin?> GetByNameAsync(string name);
    Task<HorseSkin?> GetBySkinKeyAsync(string skinKey);
    Task CreateAsync(HorseSkin skin);
    Task UpdateAsync(HorseSkin skin);
    Task<long> CountAsync();
}

public class SkinRepository : ISkinRepository
{
    private readonly IMongoCollection<HorseSkin> _collection;
    private static readonly ConcurrentDictionary<string, HorseSkin> _inMemory = new(StringComparer.OrdinalIgnoreCase);

    static SkinRepository()
    {
        var skins = new[]
        {
            new HorseSkin
            {
                Id = "671000000000000000000101",
                SkinKey = "default_horse",
                SkinName = "Ngựa Gỗ Cổ Điển",
                SkinDescription = "Quân cờ ngựa gỗ tiêu chuẩn được trang bị mặc định",
                Price = 0,
                Amount = 999,
                IsAvailable = true,
                ArenaId = "671000000000000000000010"
            },
            new HorseSkin
            {
                Id = "671000000000000000000102",
                SkinKey = "gundam_unicorn",
                SkinName = "Gundam Unicorn",
                SkinDescription = "Chiến giáp Mobile Suit Unicorn với hiệu ứng ánh sáng phát quang",
                Price = 5000,
                Amount = 999,
                IsAvailable = true,
                ArenaId = "671000000000000000000010"
            },
            new HorseSkin
            {
                Id = "671000000000000000000103",
                SkinKey = "odin_transformer",
                SkinName = "Odin Biến Hình",
                SkinDescription = "Chiến thần Odin có thể biến hình thành phi thuyền phản lực khi bay",
                Price = 5000,
                Amount = 999,
                IsAvailable = true,
                ArenaId = "671000000000000000000020"
            },
            new HorseSkin
            {
                Id = "671000000000000000000104",
                SkinKey = "arya",
                SkinName = "Hiệp Sĩ Arya",
                SkinDescription = "Nữ chiến binh Arya dũng cảm với hoạt ảnh di chuyển linh hoạt",
                Price = 5000,
                Amount = 999,
                IsAvailable = true,
                ArenaId = "671000000000000000000010"
            }
        };
        foreach (var s in skins) _inMemory[s.Id] = s;
    }

    public SkinRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<HorseSkin>("skins");
    }

    public async Task<List<HorseSkin>> GetAllAsync()
    {
        if (DbInitializer.IsMongoConnected)
        {
            try { return await _collection.Find(_ => true).ToListAsync(); }
            catch { }
        }
        return _inMemory.Values.ToList();
    }

    public async Task<HorseSkin?> GetByIdAsync(string id)
    {
        if (DbInitializer.IsMongoConnected)
        {
            try { return await _collection.Find(x => x.Id == id).FirstOrDefaultAsync(); }
            catch { }
        }
        _inMemory.TryGetValue(id, out var skin);
        return skin;
    }

    public async Task<HorseSkin?> GetByNameAsync(string name)
    {
        if (DbInitializer.IsMongoConnected)
        {
            try { return await _collection.Find(x => x.SkinName.ToLower() == name.ToLower()).FirstOrDefaultAsync(); }
            catch { }
        }
        return _inMemory.Values.FirstOrDefault(x => string.Equals(x.SkinName, name, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<HorseSkin?> GetBySkinKeyAsync(string skinKey)
    {
        if (DbInitializer.IsMongoConnected)
        {
            try { return await _collection.Find(x => x.SkinKey == skinKey).FirstOrDefaultAsync(); }
            catch { }
        }
        return _inMemory.Values.FirstOrDefault(x =>
            string.Equals(x.SkinKey, skinKey, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(x.Id, skinKey, StringComparison.OrdinalIgnoreCase));
    }

    public async Task CreateAsync(HorseSkin skin)
    {
        _inMemory[skin.Id] = skin;
        if (DbInitializer.IsMongoConnected)
        {
            try { await _collection.InsertOneAsync(skin); }
            catch { }
        }
    }

    public async Task UpdateAsync(HorseSkin skin)
    {
        _inMemory[skin.Id] = skin;
        if (DbInitializer.IsMongoConnected)
        {
            try { await _collection.ReplaceOneAsync(x => x.Id == skin.Id, skin); }
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
