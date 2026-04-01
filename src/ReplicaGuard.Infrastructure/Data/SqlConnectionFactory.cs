using System.Data;
using Npgsql;
using ReplicaGuard.Application.Abstractions.Data;

namespace ReplicaGuard.Infrastructure.Data;

internal sealed class SqlConnectionFactory : ISqlConnectionFactory
{
    private readonly string _connectionString;

    public SqlConnectionFactory(string connectionString) => _connectionString = connectionString;

    public IDbConnection CreateConnection()
    {
        var connection = new NpgsqlConnection(_connectionString);
        connection.Open();

        using var cmd = new NpgsqlCommand("SET search_path TO syncarr, public", connection);
        cmd.ExecuteNonQuery();

        return connection;
    }
}
