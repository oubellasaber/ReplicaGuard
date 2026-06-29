namespace ReplicaGuard.Domain.HosterAccounts;

public interface IHosterAccountRepository
{
    Task<HosterAccount?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    void Add(HosterAccount account);
    Task<IEnumerable<HosterAccount>> GetAccountsByIds(Guid userId, IEnumerable<Guid> accounts, CancellationToken cancellationToken = default);
    Task<HosterAccount?> GetByIdentityIdAsync(Guid identityId, CancellationToken cancellationToken = default);
}
