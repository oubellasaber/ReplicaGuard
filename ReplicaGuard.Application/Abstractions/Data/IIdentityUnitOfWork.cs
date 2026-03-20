namespace ReplicaGuard.Application.Abstractions.Data;

public interface IIdentityUnitOfWork
{
    Task BeginTransactionAsync(bool includeAppContext = true, CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
