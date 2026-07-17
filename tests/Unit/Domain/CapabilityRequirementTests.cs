using ReplicaGuard.Domain.Hosters;
using ReplicaGuard.Domain.HosterAccounts;

namespace ReplicaGuard.Domain.Tests;

public sealed class CapabilityRequirementTests
{
    private static AuthIdentity MakeIdentity(IdentityType type)
    {
        var secretSet = SecretSet.Create(new[] { Secret.CreateNew(SecretType.Password, new SecretValue(new byte[] { 1, 2, 3 })) });
        return AuthIdentity.CreateNew(type, type.RequiresValue() ? "value" : null, secretSet);
    }

    [Fact]
    public void satisfied_when_one_path_matches()
    {
        var requirement = new CapabilityRequirement(
            CapabilityCode.LocalFileUpload,
            new[]
            {
                new RequirementPath(new[] { IdentityType.Email, IdentityType.Username }),
                new RequirementPath(new[] { IdentityType.ApiKey })
            });

        var identities = new[] { MakeIdentity(IdentityType.ApiKey) };

        Assert.True(requirement.IsSatisfiedBy(identities));
    }

    [Fact]
    public void not_satisfied_when_no_path_matches()
    {
        var requirement = new CapabilityRequirement(
            CapabilityCode.LocalFileUpload,
            new[]
            {
                new RequirementPath(new[] { IdentityType.Email, IdentityType.Username }),
                new RequirementPath(new[] { IdentityType.ApiKey })
            });

        var identities = new[] { MakeIdentity(IdentityType.Email) };

        Assert.False(requirement.IsSatisfiedBy(identities));
    }

    [Fact]
    public void satisfied_requires_all_identities_within_a_path()
    {
        var requirement = new CapabilityRequirement(
            CapabilityCode.LocalFileUpload,
            new[] { new RequirementPath(new[] { IdentityType.Email, IdentityType.Username }) });

        var identities = new[] { MakeIdentity(IdentityType.Email) };

        Assert.False(requirement.IsSatisfiedBy(identities));
    }
}
