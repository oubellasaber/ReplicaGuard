namespace ReplicaGuard.Domain.HosterAccounts;

public interface IHosterAccountRepository
{
    Task<HosterAccount?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<HosterAccount?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    void Add(HosterAccount account);
    Task<IEnumerable<HosterAccount>> GetAccountsByIds(Guid userId, IEnumerable<Guid> accounts, CancellationToken cancellationToken = default);
    Task<HosterAccount?> GetByIdentityId(Guid identityId, CancellationToken cancellationToken = default);
    Task<IEnumerable<HosterAccount>> GetAccounts(Guid userId, CancellationToken cancellationToken = default);
}
