using FluentAssertions;
using ReplicaGuard.Core.Abstractions;
using ReplicaGuard.Core.Domain.Credentials;
using ReplicaGuard.Core.Domain.Hoster;
using HosterEntity = ReplicaGuard.Core.Domain.Hoster.Hoster;
using HosterCredentialsEntity = ReplicaGuard.Core.Domain.Credentials.HosterCredentials;

namespace ReplicaGuard.Domain.Tests.Credentials;

public class HosterCredentialsTests
{
    [Fact]
    public void CreateCredentials_WhenPrimaryApiKeyIsMissing_ReturnsFailure()
    {
        // Arrange
        HosterEntity hoster = CreateApiKeyHoster();

        // Act
        Result<HosterCredentialsEntity> result = hoster.CreateCredentials(Guid.NewGuid());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(HosterCredentialsErrors.MissingApiKey().Code);
    }

    [Fact]
    public void UpdateCredentials_WithValidApiKeyUpdate_IncrementsVersionAndMarksPending()
    {
        // Arrange
        HosterEntity hoster = CreateApiKeyHoster();
        HosterCredentialsEntity credentials = hoster.CreateCredentials(Guid.NewGuid(), apiKey: "old-key").Value;
        credentials.MarkCredentialAsSynced(credentials.Version).IsSuccess.Should().BeTrue();

        // Act
        Result result = hoster.UpdateCredentials(credentials, apiKey: "  new-key  ");

        // Assert
        result.IsSuccess.Should().BeTrue();
        credentials.Version.Should().Be(2);
        credentials.SyncStatus.Should().Be(CredentialsSyncStatus.Pending);
        credentials.ApiKey.Should().Be("new-key");
        credentials.GetDomainEvents().OfType<HosterCredentialsAreOutOfSync>().Should().ContainSingle(e => e.Version == 2);
    }

    [Fact]
    public void MarkCredentialAsSynced_WithVersionMismatch_ReturnsFailure()
    {
        // Arrange
        HosterEntity hoster = CreateApiKeyHoster();
        HosterCredentialsEntity credentials = hoster.CreateCredentials(Guid.NewGuid(), apiKey: "valid-key").Value;

        // Act
        Result result = credentials.MarkCredentialAsSynced(credentials.Version + 1);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(HosterCredentialsErrors.VersionMismatch().Code);
        credentials.SyncStatus.Should().Be(CredentialsSyncStatus.Pending);
    }

    [Fact]
    public void MarkCredentialAsFailed_WithMatchingVersion_SetsFailedStatus()
    {
        // Arrange
        HosterEntity hoster = CreateApiKeyHoster();
        HosterCredentialsEntity credentials = hoster.CreateCredentials(Guid.NewGuid(), apiKey: "valid-key").Value;

        // Act
        Result result = credentials.MarkCredentialAsFailed(credentials.Version);

        // Assert
        result.IsSuccess.Should().BeTrue();
        credentials.SyncStatus.Should().Be(CredentialsSyncStatus.Failed);
    }

    private static HosterEntity CreateApiKeyHoster()
    {
        Result<HosterEntity> hosterResult = HosterEntity.Create("sendcm", "SendCM", ReplicaGuard.Core.Domain.Hoster.Credentials.ApiKey);
        hosterResult.IsSuccess.Should().BeTrue();
        return hosterResult.Value;
    }
}
