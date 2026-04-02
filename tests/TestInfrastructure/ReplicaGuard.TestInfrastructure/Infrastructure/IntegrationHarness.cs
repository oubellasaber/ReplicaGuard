using Microsoft.Extensions.DependencyInjection;
using ReplicaGuard.TestInfrastructure.Fixtures;

namespace ReplicaGuard.TestInfrastructure.Infrastructure;

internal sealed class IntegrationHarness : IAsyncDisposable
{
    internal const int RefreshTokenExpirationInDays = 7;
    internal static readonly Guid CurrentUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    internal const string CurrentIdentityId = "integration-test-identity-id";

    private readonly ServiceProvider _serviceProvider;
    private readonly DatabaseResetService _databaseResetService;

    private IntegrationHarness(ServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _databaseResetService = new DatabaseResetService(serviceProvider);
    }

    internal IServiceProvider ServiceProvider => _serviceProvider;

    internal static async Task<IntegrationHarness> CreateAsync(DateTime utcNow)
    {
        DatabaseFixture databaseFixture = await DatabaseFixture.CreateAsync();
        ServiceProvider serviceProvider = ServiceProviderFactory.Create(
            databaseFixture.ConnectionString,
            utcNow,
            RefreshTokenExpirationInDays,
            CurrentUserId,
            CurrentIdentityId);

        // Migrations are idempotent via EF history tables; run once per harness creation.
        await MigrationRunner.ApplyAsync(serviceProvider);

        return new IntegrationHarness(serviceProvider);
    }

    internal Task ResetStateAsync()
    {
        return _databaseResetService.ResetStateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
    }
}
