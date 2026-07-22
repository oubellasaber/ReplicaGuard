using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReplicaGuard.Application.Abstractions.Common;
using ReplicaGuard.Application.Assets.CreateAsset;
using ReplicaGuard.Application.Assets.GetAsset;
using ReplicaGuard.Application.Assets.ListAssets;
using ReplicaGuard.Application.Exceptions;
using ReplicaGuard.Application.HosterAccounts.CreateHosterAccount;
using ReplicaGuard.Domain.Abstractions;
using ReplicaGuard.Domain.HosterAccounts;
using ReplicaGuard.Domain.Hosters;
using ReplicaGuard.Domain.Replication;
using ReplicaGuard.Infrastructure.Persistence;
using ReplicaGuard.TestInfrastructure.Fixtures;
using ReplicaGuard.TestInfrastructure.Infrastructure;

namespace ReplicaGuard.Application.IntegrationTests;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class AssetIntegrationTests
{
    private static readonly HosterCode PreferredHosterCode = HosterCode.Pixeldrain;

    [Fact]
    public async Task create_asset_with_verified_account_creates_asset_and_replica()
    {
        DateTime fixedNow = new(2026, 4, 2, 12, 0, 0, DateTimeKind.Utc);
        await using var harness = await IntegrationHarness.CreateAsync(fixedNow);
        await harness.ResetStateAsync();

        Hoster hoster;
        Guid accountId;
        Result<CreateAssetResponse> createResult;

        using (IServiceScope scope = harness.ServiceProvider.CreateScope())
        {
            ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();

            hoster = await GetSeededHosterAsync(scope.ServiceProvider);
            accountId = await CreateAccountWithVerifiedApiKeyAsync(scope.ServiceProvider, sender, hoster);

            createResult = await sender.Send(
                new CreateAssetCommand("https://example.com/archive.zip", "archive.zip", [accountId]),
                CancellationToken.None);
        }

        createResult.IsSuccess.Should().BeTrue();
        createResult.Value.ReplicaCount.Should().Be(1);

        using IServiceScope assertScope = harness.ServiceProvider.CreateScope();
        IAssetRepository assetRepository = assertScope.ServiceProvider.GetRequiredService<IAssetRepository>();

        Asset? persistedAsset = await assetRepository.GetByIdWithReplicasAsync(
            createResult.Value.AssetId,
            IntegrationHarness.CurrentUserId,
            CancellationToken.None);

        persistedAsset.Should().NotBeNull();
        persistedAsset!.UserId.Should().Be(IntegrationHarness.CurrentUserId);
        persistedAsset.Replicas.Should().ContainSingle(r => r.HosterId == hoster.Id);
    }

    [Fact]
    public async Task create_asset_with_empty_accounts_throws_validation_exception()
    {
        DateTime fixedNow = new(2026, 4, 2, 12, 0, 0, DateTimeKind.Utc);
        await using var harness = await IntegrationHarness.CreateAsync(fixedNow);
        await harness.ResetStateAsync();

        using IServiceScope scope = harness.ServiceProvider.CreateScope();
        ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var createAsset = async () => await sender.Send(
                new CreateAssetCommand("https://example.com/no-creds.zip", "no-creds.zip", []),
                CancellationToken.None);

        await Assert.ThrowsAsync<ValidationException>(createAsset);

    }

    [Fact]
    public async Task create_asset_with_unknown_account_returns_account_not_found()
    {
        DateTime fixedNow = new(2026, 4, 2, 12, 0, 0, DateTimeKind.Utc);
        await using var harness = await IntegrationHarness.CreateAsync(fixedNow);
        await harness.ResetStateAsync();

        Guid unknownAccountId = Guid.NewGuid();
        Result<CreateAssetResponse> createResult;

        using (IServiceScope scope = harness.ServiceProvider.CreateScope())
        {
            ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();

            createResult = await sender.Send(
                new CreateAssetCommand("https://example.com/bad-account.zip", "bad-account.zip", [unknownAccountId]),
                CancellationToken.None);
        }

        createResult.IsFailure.Should().BeTrue();
        createResult.Error.Code.Should().Be(HosterAccountErrors.NotFound(unknownAccountId).Code);
    }

    [Fact]
    public async Task get_asset_when_owned_by_current_user_returns_asset_details()
    {
        DateTime fixedNow = new(2026, 4, 2, 12, 0, 0, DateTimeKind.Utc);
        await using var harness = await IntegrationHarness.CreateAsync(fixedNow);
        await harness.ResetStateAsync();

        Guid assetId;

        using (IServiceScope arrangeScope = harness.ServiceProvider.CreateScope())
        {
            ISender sender = arrangeScope.ServiceProvider.GetRequiredService<ISender>();

            Hoster hoster = await GetSeededHosterAsync(arrangeScope.ServiceProvider);
            Guid accountId = await CreateAccountWithVerifiedApiKeyAsync(arrangeScope.ServiceProvider, sender, hoster);

            Result<CreateAssetResponse> createResult = await sender.Send(
                new CreateAssetCommand("https://example.com/owned.zip", "owned.zip", [accountId]),
                CancellationToken.None);

            createResult.IsSuccess.Should().BeTrue();
            assetId = createResult.Value.AssetId;
        }

        Result<GetAssetResponse> getResult;

        using (IServiceScope actScope = harness.ServiceProvider.CreateScope())
        {
            ISender sender = actScope.ServiceProvider.GetRequiredService<ISender>();

            getResult = await sender.Send(new GetAssetQuery(assetId), CancellationToken.None);
        }

        getResult.IsSuccess.Should().BeTrue();
        getResult.Value.Id.Should().Be(assetId);
        getResult.Value.FileName.Should().Be("owned.zip");
        getResult.Value.Replicas.Should().ContainSingle();
    }

    [Fact]
    public async Task get_asset_when_owned_by_different_user_returns_asset_not_found()
    {
        DateTime fixedNow = new(2026, 4, 2, 12, 0, 0, DateTimeKind.Utc);
        await using var harness = await IntegrationHarness.CreateAsync(fixedNow);
        await harness.ResetStateAsync();

        Guid foreignAssetId;

        using (IServiceScope arrangeScope = harness.ServiceProvider.CreateScope())
        {
            Hoster hoster = await GetSeededHosterAsync(arrangeScope.ServiceProvider);
            foreignAssetId = await AddPersistedAssetAsync(
                arrangeScope.ServiceProvider,
                Guid.NewGuid(),
                hoster.Id,
                null,
                "foreign.zip",
                "https://example.com/foreign.zip");
        }

        Result<GetAssetResponse> getResult;

        using (IServiceScope actScope = harness.ServiceProvider.CreateScope())
        {
            ISender sender = actScope.ServiceProvider.GetRequiredService<ISender>();

            getResult = await sender.Send(new GetAssetQuery(foreignAssetId), CancellationToken.None);
        }

        getResult.IsFailure.Should().BeTrue();
        getResult.Error.Code.Should().Be(ReplicationErrors.AssetNotFound(foreignAssetId).Code);
    }

    [Fact]
    public async Task list_assets_returns_only_current_user_assets()
    {
        DateTime fixedNow = new(2026, 4, 2, 12, 0, 0, DateTimeKind.Utc);
        await using var harness = await IntegrationHarness.CreateAsync(fixedNow);
        await harness.ResetStateAsync();

        Guid currentUserAssetId;
        Guid otherUserAssetId;

        using (IServiceScope arrangeScope = harness.ServiceProvider.CreateScope())
        {
            Hoster hoster = await GetSeededHosterAsync(arrangeScope.ServiceProvider);

            currentUserAssetId = await AddPersistedAssetAsync(
                arrangeScope.ServiceProvider,
                IntegrationHarness.CurrentUserId,
                hoster.Id,
                null,
                "mine.zip",
                "https://example.com/mine.zip");

            otherUserAssetId = await AddPersistedAssetAsync(
                arrangeScope.ServiceProvider,
                Guid.NewGuid(),
                hoster.Id,
                null,
                "other.zip",
                "https://example.com/other.zip");
        }

        Result<PagedList<AssetSummaryResponse>> listResult;

        using (IServiceScope actScope = harness.ServiceProvider.CreateScope())
        {
            ISender sender = actScope.ServiceProvider.GetRequiredService<ISender>();

            listResult = await sender.Send(new ListAssetsQuery(new ResourceParameters { PageSize = 2 }), CancellationToken.None);
        }

        listResult.IsSuccess.Should().BeTrue();
        listResult.Value.Items.Should().Contain(asset => asset.Id == currentUserAssetId);
        listResult.Value.Items.Should().NotContain(asset => asset.Id == otherUserAssetId);
    }

    [Fact]
    public async Task get_asset_includes_replica_details()
    {
        DateTime fixedNow = new(2026, 4, 2, 12, 0, 0, DateTimeKind.Utc);
        await using var harness = await IntegrationHarness.CreateAsync(fixedNow);
        await harness.ResetStateAsync();

        Guid assetId;

        using (IServiceScope arrangeScope = harness.ServiceProvider.CreateScope())
        {
            ISender sender = arrangeScope.ServiceProvider.GetRequiredService<ISender>();

            Hoster hoster = await GetSeededHosterAsync(arrangeScope.ServiceProvider);
            Guid accountId = await CreateAccountWithVerifiedApiKeyAsync(arrangeScope.ServiceProvider, sender, hoster);

            Result<CreateAssetResponse> createResult = await sender.Send(
                new CreateAssetCommand("https://example.com/details.zip", "details.zip", [accountId]),
                CancellationToken.None);

            createResult.IsSuccess.Should().BeTrue();
            assetId = createResult.Value.AssetId;
        }

        Result<GetAssetResponse> getResult;

        using (IServiceScope actScope = harness.ServiceProvider.CreateScope())
        {
            ISender sender = actScope.ServiceProvider.GetRequiredService<ISender>();

            getResult = await sender.Send(new GetAssetQuery(assetId), CancellationToken.None);
        }

        getResult.IsSuccess.Should().BeTrue();
        ReplicaResponse replica = getResult.Value.Replicas.Single();
        replica.Status.Should().Be("pending");
        replica.Id.Should().NotBeEmpty();
        replica.HosterId.Should().NotBeEmpty();
        replica.Link.Should().BeNull();
    }

    private static async Task<Hoster> GetSeededHosterAsync(IServiceProvider services)
    {
        var logger = services.GetService<ILogger<AssetIntegrationTests>>();
        var appDbContext = services.GetRequiredService<ApplicationDbContext>();

        Hoster? anySeededHoster = await appDbContext.Set<Hoster>()
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (anySeededHoster is null)
            throw new InvalidOperationException("No seeded hosters found, check seeding logic.");

        Hoster? preferred = await appDbContext.Set<Hoster>()
            .AsNoTracking()
            .SingleOrDefaultAsync(h => h.Code == PreferredHosterCode);

        if (preferred is null)
        {
            var available = await appDbContext.Set<Hoster>()
                .AsNoTracking()
                .OrderBy(h => h.Code)
                .Select(h => h.Code.ToString())
                .ToListAsync();

            throw new InvalidOperationException(
                $"Seeded hoster '{PreferredHosterCode}' not found. Available: {string.Join(", ", available)}");
        }

        return preferred;
    }

    private static async Task<Guid> CreateAccountWithVerifiedApiKeyAsync(
        IServiceProvider services,
        ISender sender,
        Hoster hoster)
    {
        var createResult = await sender.Send(
            new CreateHosterAccountCommand(
                hoster.Id,
                "test-account",
                null,
                [
                    new IdentityDto(
                        IdentityType.ApiKey,
                        null,
                        new Dictionary<SecretType, string> { { SecretType.ApiKeyPair, "test-api-key" } })
                ]),
            CancellationToken.None);

        createResult.IsSuccess.Should().BeTrue();
        Guid accountId = createResult.Value.HosterAccountId;

        // Mark the api key identity as verified directly (bypasses real API call)
        var appDbContext = services.GetRequiredService<ApplicationDbContext>();
        var account = await appDbContext.Set<HosterAccount>()
            .Include(a => a.Identities)
            .SingleAsync(a => a.Id == accountId);

        var apiKeyIdentity = account.Identities.Single(i => i.Type == IdentityType.ApiKey);
        apiKeyIdentity.MarkAsVerified();
        await appDbContext.SaveChangesAsync();

        return accountId;
    }

    [Fact]
    public async Task create_asset_with_unverified_identity_returns_capability_error()
    {
        DateTime fixedNow = new(2026, 4, 2, 12, 0, 0, DateTimeKind.Utc);
        await using var harness = await IntegrationHarness.CreateAsync(fixedNow);
        await harness.ResetStateAsync();

        Result<CreateAssetResponse> createResult;

        using (IServiceScope scope = harness.ServiceProvider.CreateScope())
        {
            ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();
            Hoster hoster = await GetSeededHosterAsync(scope.ServiceProvider);

            Result<CreateHosterAccountResponse> accountResult = await sender.Send(
                new CreateHosterAccountCommand(
                    hoster.Id, "unverified", null,
                    [new IdentityDto(IdentityType.ApiKey, null, new() { { SecretType.ApiKeyPair, "unverified-key" } })]),
                CancellationToken.None);

            accountResult.IsSuccess.Should().BeTrue();

            createResult = await sender.Send(
                new CreateAssetCommand("https://example.com/unverified.zip", "unverified.zip", [accountResult.Value.HosterAccountId]),
                CancellationToken.None);
        }

        createResult.IsFailure.Should().BeTrue();
    }

    [Fact(Skip = "Temporarily disabling until CreateAccountWithVerifiedApiKeyAsync is configured to create hoster accounts for diffirent hosters.")]
    public async Task create_asset_with_multiple_accounts_creates_multiple_replicas()
    {
        DateTime fixedNow = new(2026, 4, 2, 12, 0, 0, DateTimeKind.Utc);
        await using var harness = await IntegrationHarness.CreateAsync(fixedNow);
        await harness.ResetStateAsync();

        Result<CreateAssetResponse> createResult;

        using (IServiceScope scope = harness.ServiceProvider.CreateScope())
        {
            ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();
            Hoster hoster = await GetSeededHosterAsync(scope.ServiceProvider);

            Guid account1 = await CreateAccountWithVerifiedApiKeyAsync(scope.ServiceProvider, sender, hoster);
            Guid account2 = await CreateAccountWithVerifiedApiKeyAsync(scope.ServiceProvider, sender, hoster);

            createResult = await sender.Send(
                new CreateAssetCommand("https://example.com/multi.zip", "multi.zip", [account1, account2]),
                CancellationToken.None);
        }

        createResult.IsSuccess.Should().BeTrue();
        createResult.Value.ReplicaCount.Should().Be(2);
    }

    private static async Task<Guid> AddPersistedAssetAsync(
        IServiceProvider services,
        Guid userId,
        Guid hosterId,
        Guid? accountId,
        string fileName,
        string source)
    {
        var appDbContext = services.GetRequiredService<ApplicationDbContext>();

        Result<FileName> fileNameResult = FileName.Create(fileName);
        fileNameResult.IsSuccess.Should().BeTrue();

        Result<Asset> assetResult = Asset.CreateFromRemoteUrl(
            userId, source, fileNameResult.Value, [(hosterId, accountId)]);

        assetResult.IsSuccess.Should().BeTrue();

        appDbContext.Set<Asset>().Add(assetResult.Value);
        await appDbContext.SaveChangesAsync();

        return assetResult.Value.Id;
    }
}
