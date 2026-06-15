namespace ReplicaGuard.Core.Hosters;

public interface IHosterRepository
{
    Task<Hoster?> GetByIdAsync(HosterCode id, CancellationToken ctn);
    Task<List<Hoster>> GetAllAsync(CancellationToken ctn = default);
    //void Add(Hoster hoster);
}
