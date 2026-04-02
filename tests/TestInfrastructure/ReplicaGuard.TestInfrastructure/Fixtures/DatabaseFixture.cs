using Npgsql;

namespace ReplicaGuard.TestInfrastructure.Fixtures;

internal sealed class DatabaseFixture
{
    private DatabaseFixture(string connectionString)
    {
        ConnectionString = connectionString;
    }

    internal string ConnectionString { get; }

    internal static async Task<DatabaseFixture> CreateAsync()
    {
        string connectionString = await ResolveIntegrationDatabaseConnectionStringAsync();
        return new DatabaseFixture(connectionString);
    }

    private static async Task<string> ResolveIntegrationDatabaseConnectionStringAsync()
    {
        string baseConnectionString = ResolveBaseConnectionString();
        var testDbBuilder = new NpgsqlConnectionStringBuilder(baseConnectionString);

        if (string.IsNullOrWhiteSpace(testDbBuilder.Database))
        {
            testDbBuilder.Database = "replicaguard_integration_tests";
        }
        else if (!testDbBuilder.Database.Contains("test", StringComparison.OrdinalIgnoreCase))
        {
            testDbBuilder.Database = $"{testDbBuilder.Database}_integration_tests";
        }

        await EnsureDatabaseExistsAsync(baseConnectionString, testDbBuilder.Database);

        return testDbBuilder.ConnectionString;
    }

    private static string ResolveBaseConnectionString()
    {
        string? envConnection = Environment.GetEnvironmentVariable("REPLICAGUARD_TEST_DB_CONNECTION");

        if (!string.IsNullOrWhiteSpace(envConnection))
        {
            return envConnection;
        }

        throw new InvalidOperationException(
            "Missing REPLICAGUARD_TEST_DB_CONNECTION. " +
            "These integration tests run only against a dedicated PostgreSQL test database. " +
            "Example: Host=localhost;Port=5432;Database=replicaguard_test;Username=postgres;Password=postgres");
    }

    private static async Task EnsureDatabaseExistsAsync(string baseConnectionString, string databaseName)
    {
        var adminConnectionBuilder = new NpgsqlConnectionStringBuilder(baseConnectionString)
        {
            Database = "postgres"
        };

        await using var adminConnection = new NpgsqlConnection(adminConnectionBuilder.ConnectionString);
        await adminConnection.OpenAsync();

        await using var existsCommand = new NpgsqlCommand(
            "SELECT 1 FROM pg_database WHERE datname = @databaseName;",
            adminConnection);

        existsCommand.Parameters.AddWithValue("databaseName", databaseName);
        object? existsResult = await existsCommand.ExecuteScalarAsync();

        if (existsResult is not null)
        {
            return;
        }

        string quotedDatabaseName = QuoteIdentifier(databaseName);
        await using var createDatabaseCommand = new NpgsqlCommand(
            $"CREATE DATABASE {quotedDatabaseName};",
            adminConnection);

        await createDatabaseCommand.ExecuteNonQueryAsync();
    }

    private static string QuoteIdentifier(string identifier)
    {
        var commandBuilder = new NpgsqlCommandBuilder();
        return commandBuilder.QuoteIdentifier(identifier);
    }
}
