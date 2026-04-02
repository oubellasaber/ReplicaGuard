using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ReplicaGuard.Application.Abstractions.Data;
using ReplicaGuard.Infrastructure.Identity;
using ReplicaGuard.Infrastructure.Persistence;

namespace ReplicaGuard.TestInfrastructure.Utilities;

internal static class IdentityTestHelper
{
    internal static async Task ExpireRefreshTokenAsync(
        IServiceProvider services,
        string token,
        DateTime expiresAtUtc)
    {
        AppIdentityDbContext identityDb = services.GetRequiredService<AppIdentityDbContext>();
        IIdentityUnitOfWork identityUnitOfWork = services.GetRequiredService<IIdentityUnitOfWork>();

        // Arrange helper: mutate persistence state directly to create an expired-token scenario.
        // This is intentional test setup and not domain behavior under test.
        var tokenEntity = await identityDb.RefreshTokens
            .SingleAsync(rt => rt.Token == token);

        // This helper touches identity data only; exclude app context from the transaction on purpose.
        await identityUnitOfWork.BeginTransactionAsync(includeAppContext: false);

        try
        {
            tokenEntity.ExpiresAtUtc = expiresAtUtc;
            await identityUnitOfWork.SaveChangesAsync();
            await identityUnitOfWork.CommitTransactionAsync();
        }
        catch
        {
            await identityUnitOfWork.RollbackTransactionAsync();
            throw;
        }
    }
}
