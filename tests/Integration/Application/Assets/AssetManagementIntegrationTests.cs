using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ReplicaGuard.Application.Assets;
using ReplicaGuard.Application.Assets.CreateAsset;
using ReplicaGuard.Application.Assets.GetAsset;
using ReplicaGuard.Application.Assets.ListAssets;
using ReplicaGuard.Core.Abstractions;
using ReplicaGuard.Core.Domain.Credentials;
using ReplicaGuard.Core.Domain.Hoster;
using ReplicaGuard.Core.Domain.Replication;
using ReplicaGuard.TestInfrastructure.Fixtures;
using ReplicaGuard.TestInfrastructure.Infrastructure;
using ReplicaGuard.Infrastructure.Persistence;
using HosterCredentialsEntity = ReplicaGuard.Core.Domain.Credentials.HosterCredentials;
using HosterEntity = ReplicaGuard.Core.Domain.Hoster.Hoster;

namespace ReplicaGuard.Application.IntegrationTests.Assets;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class AssetManagementIntegrationTests
{
    [Fact]
    public async Task CreateAsset_WithSyncedCredentials_CreatesAssetAndReplica()
    {
        // Arrange
        DateTime fixedNow = new(2026, 4, 2, 12, 0, 0, DateTimeKind.Utc);
        await using var harness = await IntegrationHarness.CreateAsync(fixedNow);
        await harness.ResetStateAsync();

        Guid hosterId;
        Result<CreateAssetResponse> createResult;

        using (IServiceScope scope = harness.ServiceProvider.CreateScope())
        {
            ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();

            HosterEntity hoster = await GetSeededHosterAsync(scope.ServiceProvider);
            hosterId = hoster.Id;
            await AddCredentialsAsync(scope.ServiceProvider, IntegrationHarness.CurrentUserId, hoster, markSynced: true);

            // Act
            createResult = await sender.Send(
                new CreateAssetCommand("https://example.com/archive.zip", "archive.zip", [hosterId]),
                CancellationToken.None);
        }

        // Assert
        createResult.IsSuccess.Should().BeTrue();
        createResult.Value.State.Should().Be("created");
        createResult.Value.ReplicaCount.Should().Be(1);

        using IServiceScope assertScope = harness.ServiceProvider.CreateScope();
        IAssetRepository assetRepository = assertScope.ServiceProvider.GetRequiredService<IAssetRepository>();

        Asset? persistedAsset = await assetRepository.GetByIdWithReplicasAsync(
            createResult.Value.AssetId,
            CancellationToken.None);

        persistedAsset.Should().NotBeNull();
        persistedAsset!.UserId.Should().Be(IntegrationHarness.CurrentUserId);
        persistedAsset.Replicas.Should().ContainSingle(r => r.HosterId == hosterId);
    }

    [Fact]
    public async Task CreateAsset_WithoutCredentials_ReturnsMissingCredentialsError()
    {
        // Arrange
        DateTime fixedNow = new(2026, 4, 2, 12, 0, 0, DateTimeKind.Utc);
        await using var harness = await IntegrationHarness.CreateAsync(fixedNow);
        await harness.ResetStateAsync();

        Result<CreateAssetResponse> createResult;
        Guid hosterId;

        using (IServiceScope scope = harness.ServiceProvider.CreateScope())
        {
            ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();

            HosterEntity hoster = await GetSeededHosterAsync(scope.ServiceProvider);
            hosterId = hoster.Id;

            // Act
            createResult = await sender.Send(
                new CreateAssetCommand("https://example.com/no-creds.zip", "no-creds.zip", [hosterId]),
                CancellationToken.None);
        }

        // Assert
        createResult.IsFailure.Should().BeTrue();
        createResult.Error.Code.Should().Be(AssetErrors.MissingCredentials(hosterId).Code);
    }

    [Fact]
    public async Task CreateAsset_WithPendingCredentials_ReturnsCredentialsNotSyncedError()
    {
        // Arrange
        DateTime fixedNow = new(2026, 4, 2, 12, 0, 0, DateTimeKind.Utc);
        await using var harness = await IntegrationHarness.CreateAsync(fixedNow);
        await harness.ResetStateAsync();

        Result<CreateAssetResponse> createResult;
        Guid hosterId;

        using (IServiceScope scope = harness.ServiceProvider.CreateScope())
        {
            ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();

            HosterEntity hoster = await GetSeededHosterAsync(scope.ServiceProvider);
            hosterId = hoster.Id;
            await AddCredentialsAsync(scope.ServiceProvider, IntegrationHarness.CurrentUserId, hoster, markSynced: false);

            // Act
            createResult = await sender.Send(
                new CreateAssetCommand("https://example.com/pending-creds.zip", "pending-creds.zip", [hosterId]),
                CancellationToken.None);
        }

        // Assert
        createResult.IsFailure.Should().BeTrue();
        createResult.Error.Code.Should().Be(AssetErrors.CredentialsNotSynced(hosterId).Code);
    }

    [Fact]
    public async Task GetAsset_WhenOwnedByCurrentUser_ReturnsAssetDetails()
    {
        // Arrange
        DateTime fixedNow = new(2026, 4, 2, 12, 0, 0, DateTimeKind.Utc);
        await using var harness = await IntegrationHarness.CreateAsync(fixedNow);
        await harness.ResetStateAsync();

        Guid assetId;

        using (IServiceScope arrangeScope = harness.ServiceProvider.CreateScope())
        {
            ISender sender = arrangeScope.ServiceProvider.GetRequiredService<ISender>();

            HosterEntity hoster = await GetSeededHosterAsync(arrangeScope.ServiceProvider);
            await AddCredentialsAsync(arrangeScope.ServiceProvider, IntegrationHarness.CurrentUserId, hoster, markSynced: true);

            Result<CreateAssetResponse> createResult = await sender.Send(
                new CreateAssetCommand("https://example.com/owned.zip", "owned.zip", [hoster.Id]),
                CancellationToken.None);

            createResult.IsSuccess.Should().BeTrue();
            assetId = createResult.Value.AssetId;
        }

        Result<GetAssetResponse> getResult;

        using (IServiceScope actScope = harness.ServiceProvider.CreateScope())
        {
            ISender sender = actScope.ServiceProvider.GetRequiredService<ISender>();

            // Act
            getResult = await sender.Send(
                new GetAssetQuery(assetId),
                CancellationToken.None);
        }

        // Assert
        getResult.IsSuccess.Should().BeTrue();
        getResult.Value.Id.Should().Be(assetId);
        getResult.Value.FileName.Should().Be("owned.zip");
        getResult.Value.Replicas.Should().ContainSingle();
    }

    [Fact]
    public async Task GetAsset_WhenOwnedByDifferentUser_ReturnsAssetNotFoundError()
    {
        // Arrange
        DateTime fixedNow = new(2026, 4, 2, 12, 0, 0, DateTimeKind.Utc);
        await using var harness = await IntegrationHarness.CreateAsync(fixedNow);
        await harness.ResetStateAsync();

        Guid foreignAssetId;

        using (IServiceScope arrangeScope = harness.ServiceProvider.CreateScope())
        {
            HosterEntity hoster = await GetSeededHosterAsync(arrangeScope.ServiceProvider);
            foreignAssetId = await AddPersistedAssetAsync(
                arrangeScope.ServiceProvider,
                Guid.NewGuid(),
                hoster.Id,
                "foreign.zip",
                "https://example.com/foreign.zip");
        }

        Result<GetAssetResponse> getResult;

        using (IServiceScope actScope = harness.ServiceProvider.CreateScope())
        {
            ISender sender = actScope.ServiceProvider.GetRequiredService<ISender>();

            // Act
            getResult = await sender.Send(
                new GetAssetQuery(foreignAssetId),
                CancellationToken.None);
        }

        // Assert
        getResult.IsFailure.Should().BeTrue();
        getResult.Error.Code.Should().Be(ReplicationErrors.AssetNotFound(foreignAssetId).Code);
    }

    [Fact]
    public async Task ListAssets_ReturnsOnlyCurrentUserAssets()
    {
        // Arrange
        DateTime fixedNow = new(2026, 4, 2, 12, 0, 0, DateTimeKind.Utc);
        await using var harness = await IntegrationHarness.CreateAsync(fixedNow);
        await harness.ResetStateAsync();

        Guid currentUserAssetId;
        Guid otherUserAssetId;

        using (IServiceScope arrangeScope = harness.ServiceProvider.CreateScope())
        {
            HosterEntity hoster = await GetSeededHosterAsync(arrangeScope.ServiceProvider);

            currentUserAssetId = await AddPersistedAssetAsync(
                arrangeScope.ServiceProvider,
                IntegrationHarness.CurrentUserId,
                hoster.Id,
                "mine.zip",
                "https://example.com/mine.zip");

            otherUserAssetId = await AddPersistedAssetAsync(
                arrangeScope.ServiceProvider,
                Guid.NewGuid(),
                hoster.Id,
                "other.zip",
                "https://example.com/other.zip");
        }

        Result<List<AssetSummaryResponse>> listResult;

        using (IServiceScope actScope = harness.ServiceProvider.CreateScope())
        {
            ISender sender = actScope.ServiceProvider.GetRequiredService<ISender>();

            // Act
            listResult = await sender.Send(new ListAssetsQuery(), CancellationToken.None);
        }

        // Assert
        listResult.IsSuccess.Should().BeTrue();
        listResult.Value.Should().Contain(asset => asset.Id == currentUserAssetId);
        listResult.Value.Should().NotContain(asset => asset.Id == otherUserAssetId);
    }

    private static async Task<HosterEntity> GetSeededHosterAsync(IServiceProvider services)
    {
        ApplicationDbContext appDbContext = services.GetRequiredService<ApplicationDbContext>();

        return await appDbContext.Set<HosterEntity>()
            .OrderBy(hoster => hoster.Code)
            .FirstAsync();
    }

    private static async Task AddCredentialsAsync(
        IServiceProvider services,
        Guid userId,
        HosterEntity hoster,
        bool markSynced)
    {
        ApplicationDbContext appDbContext = services.GetRequiredService<ApplicationDbContext>();

        Result<HosterCredentialsEntity> credentialsResult = hoster.CreateCredentials(userId, apiKey: "integration-api-key");
        credentialsResult.IsSuccess.Should().BeTrue();

        HosterCredentialsEntity credentials = credentialsResult.Value;

        if (markSynced)
        {
            Result syncResult = credentials.MarkCredentialAsSynced(credentials.Version);
            syncResult.IsSuccess.Should().BeTrue();
        }

        appDbContext.Set<HosterCredentialsEntity>().Add(credentials);
        await appDbContext.SaveChangesAsync();
    }

    private static async Task<Guid> AddPersistedAssetAsync(
        IServiceProvider services,
        Guid userId,
        Guid hosterId,
        string fileName,
        string source)
    {
        ApplicationDbContext appDbContext = services.GetRequiredService<ApplicationDbContext>();

        Result<FileName> fileNameResult = FileName.Create(fileName);
        fileNameResult.IsSuccess.Should().BeTrue();

        Result<Asset> assetResult = Asset.CreateFromRemoteUrl(userId, source, fileNameResult.Value);
        assetResult.IsSuccess.Should().BeTrue();

        Asset asset = assetResult.Value;

        Result<Replica> replicaResult = asset.AddReplica(hosterId);
        replicaResult.IsSuccess.Should().BeTrue();

        appDbContext.Set<Asset>().Add(asset);
        await appDbContext.SaveChangesAsync();

        return asset.Id;
    }
}
