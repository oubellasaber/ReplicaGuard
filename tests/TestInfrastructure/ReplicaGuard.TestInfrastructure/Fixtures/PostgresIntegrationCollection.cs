using Xunit;

namespace ReplicaGuard.TestInfrastructure.Fixtures;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Usage",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "xUnit discovers collection definitions via reflection")]
/// <summary>
/// Integration tests in this collection share the same physical PostgreSQL database,
/// so they execute sequentially to avoid cross-test interference.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PostgresIntegrationCollection
{
    public const string Name = "PostgresIntegration";
}
