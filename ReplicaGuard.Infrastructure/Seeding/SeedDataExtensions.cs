using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ReplicaGuard.Infrastructure.Seeding;

public static class SeedDataExtensions
{
    public static async Task SeedDataAsync(this IApplicationBuilder app)
    {
        using var scope = app.ApplicationServices.CreateScope();

        var seeder = scope.ServiceProvider.GetRequiredService<AppSeeder>();

        // Run seeding, passing environment info
        await seeder.SeedAsync();
    }
}