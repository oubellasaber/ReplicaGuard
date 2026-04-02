namespace ReplicaGuard.Core.Domain.Credentials;

public interface IHosterCredentialsRepository
{
    Task<HosterCredentials?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<HosterCredentials?> FindByUserAndHosterAsync(Guid userId, Guid hosterId, CancellationToken ct);
    void Add(HosterCredentials credentials);
}
