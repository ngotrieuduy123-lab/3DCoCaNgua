using CoCaNgua.Server.Models;

namespace CoCaNgua.Server.Repositories;

public interface IFishRepository
{
    Task<List<Fish>> GetAllAsync();
    Task<Fish?> GetByIdAsync(string id);
    Task<Fish?> GetByNameAsync(string name);
    Task<Fish?> GetBySkinKeyAsync(string skinKey);
    Task CreateAsync(Fish fish);
    Task UpdateAsync(Fish fish);
    Task<long> CountAsync();
}

public class FishRepository : IFishRepository
{
    private readonly ISkinRepository _skinRepo;

    public FishRepository(ISkinRepository skinRepo)
    {
        _skinRepo = skinRepo;
    }

    public async Task<List<Fish>> GetAllAsync()
    {
        var skins = await _skinRepo.GetAllAsync();
        return skins.Select(s => new Fish
        {
            Id = s.Id,
            SkinKey = s.SkinKey,
            SkinName = s.SkinName,
            SkinDescription = s.SkinDescription,
            Price = s.Price,
            Amount = s.Amount,
            ArenaId = s.ArenaId,
            IsAvailable = s.IsAvailable
        }).ToList();
    }

    public async Task<Fish?> GetByIdAsync(string id)
    {
        var s = await _skinRepo.GetByIdAsync(id);
        return s == null ? null : new Fish
        {
            Id = s.Id,
            SkinKey = s.SkinKey,
            SkinName = s.SkinName,
            SkinDescription = s.SkinDescription,
            Price = s.Price,
            Amount = s.Amount,
            ArenaId = s.ArenaId,
            IsAvailable = s.IsAvailable
        };
    }

    public async Task<Fish?> GetByNameAsync(string name)
    {
        var s = await _skinRepo.GetByNameAsync(name);
        return s == null ? null : new Fish
        {
            Id = s.Id,
            SkinKey = s.SkinKey,
            SkinName = s.SkinName,
            SkinDescription = s.SkinDescription,
            Price = s.Price,
            Amount = s.Amount,
            ArenaId = s.ArenaId,
            IsAvailable = s.IsAvailable
        };
    }

    public async Task<Fish?> GetBySkinKeyAsync(string skinKey)
    {
        var s = await _skinRepo.GetBySkinKeyAsync(skinKey);
        return s == null ? null : new Fish
        {
            Id = s.Id,
            SkinKey = s.SkinKey,
            SkinName = s.SkinName,
            SkinDescription = s.SkinDescription,
            Price = s.Price,
            Amount = s.Amount,
            ArenaId = s.ArenaId,
            IsAvailable = s.IsAvailable
        };
    }

    public Task CreateAsync(Fish fish) => _skinRepo.CreateAsync(fish);
    public Task UpdateAsync(Fish fish) => _skinRepo.UpdateAsync(fish);
    public Task<long> CountAsync() => _skinRepo.CountAsync();
}
