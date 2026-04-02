using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ReplicaGuard.Application.HosterCredentials.AddHosterCredentials;
using ReplicaGuard.Application.HosterCredentials.GetHosterCredentials;
using ReplicaGuard.Application.HosterCredentials.UpdateHosterCredentials;
using ReplicaGuard.Application.Hosters;
using ReplicaGuard.Application.Hosters.ListHosters;
using ReplicaGuard.Core.Abstractions;
using ReplicaGuard.Core.Domain.Credentials;
using ReplicaGuard.Core.Domain.Hoster;
using ReplicaGuard.TestInfrastructure.Fixtures;
using ReplicaGuard.TestInfrastructure.Infrastructure;
using ReplicaGuard.Infrastructure.Persistence;
using HosterCredentialsEntity = ReplicaGuard.Core.Domain.Credentials.HosterCredentials;

namespace ReplicaGuard.Application.IntegrationTests.HosterCredentials;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class HosterCredentialsManagementIntegrationTests
{
    [Fact]
    public async Task AddHosterCredentials_WithValidApiKey_CreatesPendingCredentials()
    {
        // Arrange
        DateTime fixedNow = new(2026, 4, 2, 12, 0, 0, DateTimeKind.Utc);
        await using var harness = await IntegrationHarness.CreateAsync(fixedNow);
        await harness.ResetStateAsync();

        const string apiKey = "api-key-1234567890";
        Guid hosterId;
        Result<AddHosterCredentialsResponse> addResult;

        using (IServiceScope scope = harness.ServiceProvider.CreateScope())
        {
            ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();
            HosterResponse hoster = await GetApiKeyHosterAsync(sender);
            hosterId = hoster.Id;

            // Act
            addResult = await sender.Send(
                new AddHosterCredentialsCommand(hosterId, apiKey, null, null, null),
                CancellationToken.None);
        }

        // Assert
        addResult.IsSuccess.Should().BeTrue();
        addResult.Value.HosterId.Should().Be(hosterId);
        addResult.Value.Status.Should().Be("pending");

        using IServiceScope assertScope = harness.ServiceProvider.CreateScope();
        ApplicationDbContext appDbContext = assertScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        HosterCredentialsEntity? persistedCredentials = await appDbContext.Set<HosterCredentialsEntity>()
            .SingleOrDefaultAsync(credentials =>
                credentials.UserId == IntegrationHarness.CurrentUserId &&
                credentials.HosterId == hosterId);

        persistedCredentials.Should().NotBeNull();
        persistedCredentials!.ApiKey.Should().Be(apiKey);
        persistedCredentials.SyncStatus.Should().Be(CredentialsSyncStatus.Pending);
    }

    [Fact]
    public async Task AddHosterCredentials_WithUnknownHoster_ReturnsHosterNotFoundError()
    {
        // Arrange
        DateTime fixedNow = new(2026, 4, 2, 12, 0, 0, DateTimeKind.Utc);
        await using var harness = await IntegrationHarness.CreateAsync(fixedNow);
        await harness.ResetStateAsync();

        Guid unknownHosterId = Guid.NewGuid();
        Result<AddHosterCredentialsResponse> addResult;

        using (IServiceScope scope = harness.ServiceProvider.CreateScope())
        {
            ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();

            // Act
            addResult = await sender.Send(
                new AddHosterCredentialsCommand(unknownHosterId, "api-key", null, null, null),
                CancellationToken.None);
        }

        // Assert
        addResult.IsFailure.Should().BeTrue();
        addResult.Error.Code.Should().Be(HosterErrors.NotFound(unknownHosterId).Code);
    }

    [Fact]
    public async Task AddHosterCredentials_WhenCredentialsAlreadyExist_ReturnsAlreadyExistsError()
    {
        // Arrange
        DateTime fixedNow = new(2026, 4, 2, 12, 0, 0, DateTimeKind.Utc);
        await using var harness = await IntegrationHarness.CreateAsync(fixedNow);
        await harness.ResetStateAsync();

        Result<AddHosterCredentialsResponse> secondAddResult;

        using (IServiceScope scope = harness.ServiceProvider.CreateScope())
        {
            ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();
            HosterResponse hoster = await GetApiKeyHosterAsync(sender);

            Result<AddHosterCredentialsResponse> firstAddResult = await sender.Send(
                new AddHosterCredentialsCommand(hoster.Id, "first-key", null, null, null),
                CancellationToken.None);

            firstAddResult.IsSuccess.Should().BeTrue();

            // Act
            secondAddResult = await sender.Send(
                new AddHosterCredentialsCommand(hoster.Id, "second-key", null, null, null),
                CancellationToken.None);
        }

        // Assert
        secondAddResult.IsFailure.Should().BeTrue();
        secondAddResult.Error.Code.Should().Be(HosterCredentialsErrors.AlreadyExists(string.Empty).Code);
    }

    [Fact]
    public async Task GetHosterCredentials_WhenMissing_ReturnsNotFoundError()
    {
        // Arrange
        DateTime fixedNow = new(2026, 4, 2, 12, 0, 0, DateTimeKind.Utc);
        await using var harness = await IntegrationHarness.CreateAsync(fixedNow);
        await harness.ResetStateAsync();

        Guid hosterId;

        using (IServiceScope arrangeScope = harness.ServiceProvider.CreateScope())
        {
            ISender sender = arrangeScope.ServiceProvider.GetRequiredService<ISender>();
            HosterResponse hoster = await GetApiKeyHosterAsync(sender);
            hosterId = hoster.Id;
        }

        Result<GetHosterCredentialsResponse> getResult;

        using (IServiceScope actScope = harness.ServiceProvider.CreateScope())
        {
            ISender sender = actScope.ServiceProvider.GetRequiredService<ISender>();

            // Act
            getResult = await sender.Send(
                new GetHosterCredentialsQuery(hosterId),
                CancellationToken.None);
        }

        // Assert
        getResult.IsFailure.Should().BeTrue();
        getResult.Error.Code.Should().Be(HosterCredentialsErrors.NotFound(hosterId).Code);
    }

    [Fact]
    public async Task GetHosterCredentials_WhenExisting_ReturnsMaskedApiKey()
    {
        // Arrange
        DateTime fixedNow = new(2026, 4, 2, 12, 0, 0, DateTimeKind.Utc);
        await using var harness = await IntegrationHarness.CreateAsync(fixedNow);
        await harness.ResetStateAsync();

        const string rawApiKey = "api-key-1234567890";
        Guid hosterId;

        using (IServiceScope arrangeScope = harness.ServiceProvider.CreateScope())
        {
            ISender sender = arrangeScope.ServiceProvider.GetRequiredService<ISender>();
            HosterResponse hoster = await GetApiKeyHosterAsync(sender);
            hosterId = hoster.Id;

            Result<AddHosterCredentialsResponse> addResult = await sender.Send(
                new AddHosterCredentialsCommand(hosterId, rawApiKey, null, null, null),
                CancellationToken.None);

            addResult.IsSuccess.Should().BeTrue();
        }

        Result<GetHosterCredentialsResponse> getResult;

        using (IServiceScope actScope = harness.ServiceProvider.CreateScope())
        {
            ISender sender = actScope.ServiceProvider.GetRequiredService<ISender>();

            // Act
            getResult = await sender.Send(
                new GetHosterCredentialsQuery(hosterId),
                CancellationToken.None);
        }

        // Assert
        getResult.IsSuccess.Should().BeTrue();
        getResult.Value.HosterId.Should().Be(hosterId);
        getResult.Value.Status.Should().Be("pending");
        getResult.Value.ApiKey.Should().NotBeNull();
        getResult.Value.ApiKey.Should().NotBe(rawApiKey);
        getResult.Value.ApiKey.Should().StartWith(rawApiKey[..2]);
        getResult.Value.ApiKey.Should().EndWith(rawApiKey[^2..]);
        getResult.Value.ApiKey.Should().HaveLength(rawApiKey.Length);
    }

    [Fact]
    public async Task UpdateHosterCredentials_WhenMissing_ReturnsNotFoundError()
    {
        // Arrange
        DateTime fixedNow = new(2026, 4, 2, 12, 0, 0, DateTimeKind.Utc);
        await using var harness = await IntegrationHarness.CreateAsync(fixedNow);
        await harness.ResetStateAsync();

        Guid hosterId;

        using (IServiceScope arrangeScope = harness.ServiceProvider.CreateScope())
        {
            ISender sender = arrangeScope.ServiceProvider.GetRequiredService<ISender>();
            HosterResponse hoster = await GetApiKeyHosterAsync(sender);
            hosterId = hoster.Id;
        }

        Result updateResult;

        using (IServiceScope actScope = harness.ServiceProvider.CreateScope())
        {
            ISender sender = actScope.ServiceProvider.GetRequiredService<ISender>();

            // Act
            updateResult = await sender.Send(
                new UpdateHosterCredentialsCommand(hosterId, "updated-key", null, null, null),
                CancellationToken.None);
        }

        // Assert
        updateResult.IsFailure.Should().BeTrue();
        updateResult.Error.Code.Should().Be(HosterCredentialsErrors.NotFound(hosterId).Code);
    }

    [Fact]
    public async Task UpdateHosterCredentials_WhenExisting_UpdatesStoredApiKey()
    {
        // Arrange
        DateTime fixedNow = new(2026, 4, 2, 12, 0, 0, DateTimeKind.Utc);
        await using var harness = await IntegrationHarness.CreateAsync(fixedNow);
        await harness.ResetStateAsync();

        const string updatedApiKey = "updated-key-9876543210";
        Guid hosterId;

        using (IServiceScope arrangeScope = harness.ServiceProvider.CreateScope())
        {
            ISender sender = arrangeScope.ServiceProvider.GetRequiredService<ISender>();
            HosterResponse hoster = await GetApiKeyHosterAsync(sender);
            hosterId = hoster.Id;

            Result<AddHosterCredentialsResponse> addResult = await sender.Send(
                new AddHosterCredentialsCommand(hosterId, "initial-key", null, null, null),
                CancellationToken.None);

            addResult.IsSuccess.Should().BeTrue();
        }

        Result updateResult;

        using (IServiceScope actScope = harness.ServiceProvider.CreateScope())
        {
            ISender sender = actScope.ServiceProvider.GetRequiredService<ISender>();

            // Act
            updateResult = await sender.Send(
                new UpdateHosterCredentialsCommand(hosterId, updatedApiKey, null, null, null),
                CancellationToken.None);
        }

        // Assert
        updateResult.IsSuccess.Should().BeTrue();

        using IServiceScope assertScope = harness.ServiceProvider.CreateScope();
        ApplicationDbContext appDbContext = assertScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        HosterCredentialsEntity? persistedCredentials = await appDbContext.Set<HosterCredentialsEntity>()
            .SingleOrDefaultAsync(credentials =>
                credentials.UserId == IntegrationHarness.CurrentUserId &&
                credentials.HosterId == hosterId);

        persistedCredentials.Should().NotBeNull();
        persistedCredentials!.ApiKey.Should().Be(updatedApiKey);
        persistedCredentials.SyncStatus.Should().Be(CredentialsSyncStatus.Pending);
        persistedCredentials.Version.Should().Be((uint)2);
        persistedCredentials.UpdatedAtUtc.Should().NotBeNull();
    }

    private static async Task<HosterResponse> GetApiKeyHosterAsync(ISender sender)
    {
        Result<List<HosterResponse>> listResult = await sender.Send(new ListHostersQuery(), CancellationToken.None);
        listResult.IsSuccess.Should().BeTrue();

        HosterResponse? hoster = listResult.Value.FirstOrDefault(item =>
            item.PrimaryCredentials.Contains("apiKey"));

        hoster.Should().NotBeNull();
        return hoster!;
    }
}
