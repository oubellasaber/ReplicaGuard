using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ReplicaGuard.Application.Hosters;
using ReplicaGuard.Application.Hosters.GetHoster;
using ReplicaGuard.Application.Hosters.ListHosters;
using ReplicaGuard.Core.Abstractions;
using ReplicaGuard.Core.Domain.Hoster;
using ReplicaGuard.TestInfrastructure.Fixtures;
using ReplicaGuard.TestInfrastructure.Infrastructure;

namespace ReplicaGuard.Application.IntegrationTests.Hosters;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class HosterManagementIntegrationTests
{
    [Fact]
    public async Task ListHosters_ReturnsSeededHostersWithMappedCredentialRequirements()
    {
        // Arrange
        DateTime fixedNow = new(2026, 4, 2, 12, 0, 0, DateTimeKind.Utc);
        await using var harness = await IntegrationHarness.CreateAsync(fixedNow);
        await harness.ResetStateAsync();

        Result<List<HosterResponse>> listResult;

        using (IServiceScope scope = harness.ServiceProvider.CreateScope())
        {
            ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();

            // Act
            listResult = await sender.Send(new ListHostersQuery(), CancellationToken.None);
        }

        // Assert
        listResult.IsSuccess.Should().BeTrue();
        listResult.Value.Should().NotBeEmpty();
        listResult.Value.Should().OnlyContain(hoster => hoster.Code == hoster.Code.ToLowerInvariant());

        listResult.Value.Select(hoster => hoster.Code)
            .Should()
            .Contain(["pixeldrain", "sendcm"]);

        HosterResponse sendCm = listResult.Value.Single(hoster => hoster.Code == "sendcm");

        sendCm.PrimaryCredentials.Should().ContainSingle().Which.Should().Be("apiKey");
        sendCm.Requirements.Select(requirement => requirement.Feature)
            .Should()
            .Contain(["remoteUpload", "spooledUpload"]);
        sendCm.Requirements.Should().OnlyContain(requirement =>
            requirement.RequiredCredentials.Contains("apiKey"));
    }

    [Fact]
    public async Task GetHoster_WithExistingHosterId_ReturnsMappedHoster()
    {
        // Arrange
        DateTime fixedNow = new(2026, 4, 2, 12, 0, 0, DateTimeKind.Utc);
        await using var harness = await IntegrationHarness.CreateAsync(fixedNow);
        await harness.ResetStateAsync();

        Guid hosterId;

        using (IServiceScope arrangeScope = harness.ServiceProvider.CreateScope())
        {
            ISender sender = arrangeScope.ServiceProvider.GetRequiredService<ISender>();
            HosterResponse hoster = await GetHosterByCodeAsync(sender, "sendcm");
            hosterId = hoster.Id;
        }

        Result<HosterResponse> getResult;

        using (IServiceScope actScope = harness.ServiceProvider.CreateScope())
        {
            ISender sender = actScope.ServiceProvider.GetRequiredService<ISender>();

            // Act
            getResult = await sender.Send(new GetHosterQuery(hosterId), CancellationToken.None);
        }

        // Assert
        getResult.IsSuccess.Should().BeTrue();
        getResult.Value.Id.Should().Be(hosterId);
        getResult.Value.Code.Should().Be("sendcm");
        getResult.Value.PrimaryCredentials.Should().Contain("apiKey");
        getResult.Value.Requirements.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetHoster_WithUnknownId_ReturnsHosterNotFoundError()
    {
        // Arrange
        DateTime fixedNow = new(2026, 4, 2, 12, 0, 0, DateTimeKind.Utc);
        await using var harness = await IntegrationHarness.CreateAsync(fixedNow);
        await harness.ResetStateAsync();

        Guid unknownHosterId = Guid.NewGuid();
        Result<HosterResponse> getResult;

        using (IServiceScope scope = harness.ServiceProvider.CreateScope())
        {
            ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();

            // Act
            getResult = await sender.Send(new GetHosterQuery(unknownHosterId), CancellationToken.None);
        }

        // Assert
        getResult.IsFailure.Should().BeTrue();
        getResult.Error.Code.Should().Be(HosterErrors.NotFound(unknownHosterId).Code);
    }

    private static async Task<HosterResponse> GetHosterByCodeAsync(ISender sender, string expectedCode)
    {
        Result<List<HosterResponse>> listResult = await sender.Send(new ListHostersQuery(), CancellationToken.None);
        listResult.IsSuccess.Should().BeTrue();

        HosterResponse? hoster = listResult.Value.FirstOrDefault(item => item.Code == expectedCode);

        hoster.Should().NotBeNull();
        return hoster!;
    }
}
