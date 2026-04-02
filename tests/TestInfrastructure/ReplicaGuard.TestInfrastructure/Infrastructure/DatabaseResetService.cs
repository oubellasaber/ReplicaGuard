using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ReplicaGuard.Infrastructure.Identity;
using ReplicaGuard.Infrastructure.Persistence;
using ReplicaGuard.Infrastructure.Seeding;

namespace ReplicaGuard.TestInfrastructure.Infrastructure;

internal sealed class DatabaseResetService
{
    private readonly IServiceProvider _serviceProvider;

    internal DatabaseResetService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    internal async Task ResetStateAsync()
    {
        await CleanDatabaseAsync();
        await SeedReferenceDataAsync();
    }

    private async Task CleanDatabaseAsync()
    {
        using IServiceScope scope = _serviceProvider.CreateScope();
        AppIdentityDbContext identityDb = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();

        // We use one physical PostgreSQL database with multiple schemas, so truncating across
        // application/identity/transport can run on the same connection.
        bool openedHere = false;

        if (identityDb.Database.GetDbConnection().State == ConnectionState.Closed)
        {
            await identityDb.Database.OpenConnectionAsync();
            openedHere = true;
        }

        try
        {
            NpgsqlConnection connection = (NpgsqlConnection)identityDb.Database.GetDbConnection();

            const string tableQuery = """
                SELECT quote_ident(table_schema) || '.' || quote_ident(table_name)
                FROM information_schema.tables
                WHERE table_type = 'BASE TABLE'
                  AND table_schema = ANY(@schemas)
                  AND table_name <> '__EFMigrationsHistory';
                """;

            await using var listTablesCommand = new NpgsqlCommand(tableQuery, connection);
            listTablesCommand.Parameters.AddWithValue("schemas", new[]
            {
                Schemas.Application,
                Schemas.Identity,
                Schemas.Transport,
            });

            var tables = new List<string>();

            await using (var reader = await listTablesCommand.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    tables.Add(reader.GetString(0));
                }
            }

            if (tables.Count == 0)
            {
                return;
            }

            string truncateCommandText = $"TRUNCATE TABLE {string.Join(", ", tables)} RESTART IDENTITY CASCADE;";
            await using var truncateCommand = new NpgsqlCommand(truncateCommandText, connection);
            await truncateCommand.ExecuteNonQueryAsync();
        }
        finally
        {
            if (openedHere)
            {
                await identityDb.Database.CloseConnectionAsync();
            }
        }
    }

    private async Task SeedReferenceDataAsync()
    {
        using IServiceScope scope = _serviceProvider.CreateScope();
        AppSeeder seeder = scope.ServiceProvider.GetRequiredService<AppSeeder>();
        await seeder.SeedAsync();
    }
}
