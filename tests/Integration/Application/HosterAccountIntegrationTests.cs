using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ReplicaGuard.Application.HosterAccounts.CreateHosterAccount;
using ReplicaGuard.Application.HosterAccounts.GetHosterAccount;
using ReplicaGuard.Application.Hosters;
using ReplicaGuard.Application.Hosters.ListHosters;
using ReplicaGuard.Domain.Abstractions;
using ReplicaGuard.Domain.HosterAccounts;
using ReplicaGuard.Domain.Hosters;
using ReplicaGuard.TestInfrastructure.Fixtures;
using ReplicaGuard.TestInfrastructure.Infrastructure;

namespace ReplicaGuard.Application.IntegrationTests;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class HosterAccountIntegrationTests
{
    [Fact]
    public async Task create_account_with_api_key_identity_returns_account()
    {
        DateTime fixedNow = new(2026, 4, 2, 12, 0, 0, DateTimeKind.Utc);
        await using var harness = await IntegrationHarness.CreateAsync(fixedNow);
        await harness.ResetStateAsync();

        Result<CreateHosterAccountResponse> createResult;

        using (IServiceScope scope = harness.ServiceProvider.CreateScope())
        {
            ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();
            Guid hosterId = await GetAnyHosterIdAsync(sender);

            createResult = await sender.Send(
                new CreateHosterAccountCommand(
                    hosterId,
                    "my-pixel-account",
                    "My Pixeldrain account for xyz",
                    [
                        new IdentityDto(
                            IdentityType.ApiKey,
                            null,
                            new Dictionary<SecretType, string> { { SecretType.ApiKeyPair, "px-api-key-123" } })
                    ]),
                CancellationToken.None);
        }

        createResult.IsSuccess.Should().BeTrue();
        createResult.Value.HosterAccountId.Should().NotBeEmpty();
        createResult.Value.Alias.Should().Be("my-pixel-account");
        createResult.Value.TotalIdentities.Should().Be(1);
    }

    [Fact]
    public async Task create_account_with_unknown_hoster_returns_not_found()
    {
        DateTime fixedNow = new(2026, 4, 2, 12, 0, 0, DateTimeKind.Utc);
        await using var harness = await IntegrationHarness.CreateAsync(fixedNow);
        await harness.ResetStateAsync();

        Guid unknownHosterId = Guid.NewGuid();
        Result<CreateHosterAccountResponse> createResult;

        using (IServiceScope scope = harness.ServiceProvider.CreateScope())
        {
            ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();

            createResult = await sender.Send(
                new CreateHosterAccountCommand(
                    unknownHosterId,
                    "ghost-account",
                    null,
                    [
                        new IdentityDto(
                            IdentityType.ApiKey,
                            null,
                            new Dictionary<SecretType, string> { { SecretType.ApiKeyPair, "fake-key" } })
                    ]),
                CancellationToken.None);
        }

        createResult.IsFailure.Should().BeTrue();
        createResult.Error.Code.Should().Be(HosterErrors.NotFound(unknownHosterId).Code);
    }

    [Fact]
    public async Task get_account_when_exists_returns_account_with_identities()
    {
        DateTime fixedNow = new(2026, 4, 2, 12, 0, 0, DateTimeKind.Utc);
        await using var harness = await IntegrationHarness.CreateAsync(fixedNow);
        await harness.ResetStateAsync();

        Guid accountId;

        using (IServiceScope arrangeScope = harness.ServiceProvider.CreateScope())
        {
            ISender sender = arrangeScope.ServiceProvider.GetRequiredService<ISender>();
            Guid hosterId = await GetAnyHosterIdAsync(sender);

            Result<CreateHosterAccountResponse> createResult = await sender.Send(
                new CreateHosterAccountCommand(
                    hosterId,
                    "get-test-account",
                    null,
                    [
                        new IdentityDto(
                            IdentityType.ApiKey,
                            null,
                            new Dictionary<SecretType, string> { { SecretType.ApiKeyPair, "get-test-key" } })
                    ]),
                CancellationToken.None);

            createResult.IsSuccess.Should().BeTrue();
            accountId = createResult.Value.HosterAccountId;
        }

        Result<GetHosterAccountResponse> getResult;

        using (IServiceScope actScope = harness.ServiceProvider.CreateScope())
        {
            ISender sender = actScope.ServiceProvider.GetRequiredService<ISender>();

            getResult = await sender.Send(
                new GetHosterAccountQuery(accountId),
                CancellationToken.None);
        }

        getResult.IsSuccess.Should().BeTrue();
        getResult.Value.HosterAccountId.Should().Be(accountId);
        getResult.Value.Identities.Should().ContainSingle(i => i.Type == IdentityType.ApiKey);
        getResult.Value.Identities.Single().Status.Should().Be(IdentityVerificationStatus.Pending);
    }

    [Fact]
    public async Task get_account_when_missing_returns_not_found()
    {
        DateTime fixedNow = new(2026, 4, 2, 12, 0, 0, DateTimeKind.Utc);
        await using var harness = await IntegrationHarness.CreateAsync(fixedNow);
        await harness.ResetStateAsync();

        Guid missingId = Guid.NewGuid();
        Result<GetHosterAccountResponse> getResult;

        using (IServiceScope scope = harness.ServiceProvider.CreateScope())
        {
            ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();

            getResult = await sender.Send(
                new GetHosterAccountQuery(missingId),
                CancellationToken.None);
        }

        getResult.IsFailure.Should().BeTrue();
        getResult.Error.Code.Should().Be(HosterAccountErrors.NotFound(missingId).Code);
    }

    private static async Task<Guid> GetAnyHosterIdAsync(ISender sender)
    {
        Result<List<HosterResponse>> listResult = await sender.Send(
            new ListHostersQuery(), CancellationToken.None);

        listResult.IsSuccess.Should().BeTrue();
        return listResult.Value.First().Id;
    }
}
