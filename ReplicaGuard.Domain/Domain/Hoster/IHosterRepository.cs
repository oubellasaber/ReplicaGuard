namespace ReplicaGuard.Core.Domain.Hoster;

public interface IHosterRepository
{
    Task<Hoster?> GetByIdAsync(Guid id, CancellationToken ctn);
    Task<List<Hoster>> GetAllAsync(CancellationToken ctn = default);
    Task<List<string>> GetAllCodesAsync(CancellationToken ctn = default);
    void Add(Hoster hoster);
}
