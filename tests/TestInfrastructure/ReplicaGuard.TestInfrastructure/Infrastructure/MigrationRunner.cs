using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ReplicaGuard.Infrastructure.Identity;
using ReplicaGuard.Infrastructure.Persistence;

namespace ReplicaGuard.TestInfrastructure.Infrastructure;

internal static class MigrationRunner
{
    internal static async Task ApplyAsync(IServiceProvider serviceProvider)
    {
        using IServiceScope scope = serviceProvider.CreateScope();
        ApplicationDbContext appDb = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        AppIdentityDbContext identityDb = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();

        await appDb.Database.MigrateAsync();
        await identityDb.Database.MigrateAsync();
    }
}
