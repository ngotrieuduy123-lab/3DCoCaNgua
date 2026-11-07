using CoCaNgua.Server.Models;

namespace CoCaNgua.Server.Repositories;

public interface IPondRepository
{
    Task<List<Pond>> GetAllAsync();
    Task<Pond?> GetByIdAsync(string id);
    Task<Pond?> GetByNameAsync(string name);
    Task CreateAsync(Pond pond);
    Task UpdateAsync(Pond pond);
    Task<long> CountAsync();
}

public class PondRepository : IPondRepository
{
    private readonly IArenaRepository _arenaRepo;

    public PondRepository(IArenaRepository arenaRepo)
    {
        _arenaRepo = arenaRepo;
    }

    public async Task<List<Pond>> GetAllAsync()
    {
        var arenas = await _arenaRepo.GetAllAsync();
        return arenas.Select(a => new Pond
        {
            Id = a.Id,
            ArenaName = a.ArenaName,
            ArenaDescription = a.ArenaDescription,
            Theme = a.Theme,
            LastUpdatedUserID = a.LastUpdatedUserID
        }).ToList();
    }

    public async Task<Pond?> GetByIdAsync(string id)
    {
        var a = await _arenaRepo.GetByIdAsync(id);
        return a == null ? null : new Pond
        {
            Id = a.Id,
            ArenaName = a.ArenaName,
            ArenaDescription = a.ArenaDescription,
            Theme = a.Theme,
            LastUpdatedUserID = a.LastUpdatedUserID
        };
    }

    public async Task<Pond?> GetByNameAsync(string name)
    {
        var a = await _arenaRepo.GetByNameAsync(name);
        return a == null ? null : new Pond
        {
            Id = a.Id,
            ArenaName = a.ArenaName,
            ArenaDescription = a.ArenaDescription,
            Theme = a.Theme,
            LastUpdatedUserID = a.LastUpdatedUserID
        };
    }

    public Task CreateAsync(Pond pond) => _arenaRepo.CreateAsync(pond);
    public Task UpdateAsync(Pond pond) => _arenaRepo.UpdateAsync(pond);
    public Task<long> CountAsync() => _arenaRepo.CountAsync();
}
