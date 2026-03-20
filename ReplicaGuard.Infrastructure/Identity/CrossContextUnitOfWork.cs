using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ReplicaGuard.Application.Abstractions.Data;
using ReplicaGuard.Infrastructure.Persistence;

namespace ReplicaGuard.Infrastructure.Identity;

public sealed class CrossContextUnitOfWork : IIdentityUnitOfWork, IDisposable
{
    private readonly ApplicationDbContext _appDbContext;
    private readonly AppIdentityDbContext _identityDbContext;
    private IDbContextTransaction? _transaction;
    private bool _includeAppContext;

    public CrossContextUnitOfWork(
        ApplicationDbContext app,
        AppIdentityDbContext identity)
    {
        _appDbContext = app;
        _identityDbContext = identity;
    }

    public async Task BeginTransactionAsync(
        bool includeAppContext = true,
        CancellationToken cancellationToken = default)
    {
        _includeAppContext = includeAppContext;

        // Start transaction on the IdentityDbContext
        _transaction = await _identityDbContext.Database.BeginTransactionAsync(cancellationToken);

        // Only include AppDbContext in the transaction if explicitly requested
        if (_includeAppContext)
        {
            // Get the underlying database connection from IdentityDbContext
            var sharedConnection = _identityDbContext.Database.GetDbConnection();

            // Make AppDbContext use the SAME database connection
            _appDbContext.Database.SetDbConnection(sharedConnection);

            // Make AppDbContext use the SAME transaction that's already started
            await _appDbContext.Database.UseTransactionAsync(
                _transaction.GetDbTransaction(),
                cancellationToken);
        }
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null)
        {
            throw new InvalidOperationException("Transaction has not been started.");
        }

        try
        {
            await _identityDbContext.SaveChangesAsync(cancellationToken);

            // Only save AppDbContext if it was included in the transaction
            if (_includeAppContext)
            {
                // IMPORTANT: SaveChangesAsync on ApplicationDbContext will:
                // 1. Add domain events as outbox messages
                // 2. Clear domain events from entities
                // 3. Save everything within the transaction
                await _appDbContext.SaveChangesAsync(cancellationToken);
            }

            // Commit the transaction
            await _transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await _transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is not null)
        {
            await _transaction.RollbackAsync(cancellationToken);
        }
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Save changes within the transaction (doesn't commit yet)
        int identityResult = await _identityDbContext.SaveChangesAsync(cancellationToken);

        if (_includeAppContext)
        {
            // ApplicationDbContext.SaveChangesAsync will handle outbox messages automatically
            int appResult = await _appDbContext.SaveChangesAsync(cancellationToken);
            return appResult + identityResult;
        }

        return identityResult;
    }

    public void Dispose()
    {
        _transaction?.Dispose();
    }
}
