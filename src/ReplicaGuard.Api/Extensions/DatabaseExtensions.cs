using Microsoft.EntityFrameworkCore;
using ReplicaGuard.Infrastructure.Identity;
using ReplicaGuard.Infrastructure.Persistence;

namespace ReplicaGuard.Api.Extensions;

public static class DatabaseExtensions
{
    public static async Task ApplyMigrationsAsync(this WebApplication app)
    {
        await app.ApplyMigrationsAsync<ApplicationDbContext>();
        await app.ApplyMigrationsAsync<AppIdentityDbContext>();
    }

    private static async Task ApplyMigrationsAsync<TDbContext>(this WebApplication app)
    where TDbContext : DbContext
    {
        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        await using TDbContext dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();

        try
        {
            await dbContext.Database.MigrateAsync();
            app.Logger.LogInformation("Database migrations for {DbContext} applied successfully", typeof(TDbContext).Name);
        }
        catch (Exception ex)
        {
            app.Logger.LogError(ex, "An error occurred while applying database migrations for {DbContext}", typeof(TDbContext).Name);
            throw;
        }
    }
}
