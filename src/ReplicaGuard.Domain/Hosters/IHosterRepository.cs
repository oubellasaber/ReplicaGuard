namespace ReplicaGuard.Domain.Hosters;

public interface IHosterRepository
{
    Task<Hoster?> GetByIdAsync(Guid id, CancellationToken ctn);
    Task<List<Hoster>> GetAllAsync(CancellationToken ctn = default);
}
