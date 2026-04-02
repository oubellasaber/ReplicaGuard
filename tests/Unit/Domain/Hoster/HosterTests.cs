using FluentAssertions;
using ReplicaGuard.Core.Abstractions;
using ReplicaGuard.Core.Domain.Hoster;
using HosterEntity = ReplicaGuard.Core.Domain.Hoster.Hoster;

namespace ReplicaGuard.Domain.Tests.Hoster;

public class HosterTests
{
    [Fact]
    public void Create_NormalizesCodeAndDisplayName_WhenInputContainsWhitespaceAndLowercase()
    {
        // Arrange
        string rawCode = "  sendcm  ";
        string rawDisplayName = "  SendCM  ";

        // Act
        Result<HosterEntity> result = HosterEntity.Create(rawCode, rawDisplayName, ReplicaGuard.Core.Domain.Hoster.Credentials.ApiKey);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Code.Should().Be("SENDCM");
        result.Value.DisplayName.Should().Be("SendCM");
    }

    [Fact]
    public void AddFeatureRequirement_WhenFeatureAlreadyExists_ReturnsFailure()
    {
        // Arrange
        HosterEntity hoster = HosterEntity.Create("sendcm", "SendCM", ReplicaGuard.Core.Domain.Hoster.Credentials.ApiKey).Value;
        Result firstAddResult = hoster.AddFeatureRequirement(CapabilityCode.RemoteUpload, ReplicaGuard.Core.Domain.Hoster.Credentials.ApiKey);
        firstAddResult.IsSuccess.Should().BeTrue();

        // Act
        Result result = hoster.AddFeatureRequirement(CapabilityCode.RemoteUpload, ReplicaGuard.Core.Domain.Hoster.Credentials.ApiKey);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(HosterErrors.FeatureAlreadyExists(CapabilityCode.RemoteUpload).Code);
    }

    [Fact]
    public void UpdateCredentials_WhenCredentialsBelongToAnotherHoster_ThrowsInvalidOperationException()
    {
        // Arrange
        HosterEntity credentialsOwner = HosterEntity.Create("sendcm", "SendCM", ReplicaGuard.Core.Domain.Hoster.Credentials.ApiKey).Value;
        HosterEntity unrelatedHoster = HosterEntity.Create("rapidgator", "Rapidgator", ReplicaGuard.Core.Domain.Hoster.Credentials.ApiKey).Value;
        var credentials = credentialsOwner.CreateCredentials(Guid.NewGuid(), apiKey: "key-1").Value;

        // Act
        Action act = () => unrelatedHoster.UpdateCredentials(credentials, apiKey: "new-key");

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Credentials do not belong to this hoster.*");
    }
}
