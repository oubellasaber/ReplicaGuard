using ReplicaGuard.Domain.HosterAccounts;
using ReplicaGuard.Domain.Hosters;

namespace ReplicaGuard.Domain.Tests;

public sealed class RequirementPathTests
{
    private static AuthIdentity MakeIdentity(IdentityType type, IdentityVerificationStatus status)
    {
        var secretSet = SecretSet.Create(new[] { Secret.CreateNew(SecretType.Password, new SecretValue(new byte[] { 1, 2, 3 })) });
        var identity = AuthIdentity.CreateNew(type, type.RequiresValue() ? "value" : null, secretSet);

        switch (status)
        {
            case IdentityVerificationStatus.Verified:
                identity.MarkAsVerified();
                break;
            case IdentityVerificationStatus.Rejected:
                identity.MarkAsRejected();
                break;
        }

        return identity;
    }

    [Fact]
    public void is_satisfied_when_all_required_types_present()
    {
        var path = new RequirementPath(new[] { IdentityType.Email, IdentityType.Username });
        var identities = new[]
        {
            MakeIdentity(IdentityType.Email, IdentityVerificationStatus.Pending),
            MakeIdentity(IdentityType.Username, IdentityVerificationStatus.Pending)
        };

        Assert.True(path.IsSatisfiedBy(identities));
    }

    [Fact]
    public void is_not_satisfied_when_required_type_missing()
    {
        var path = new RequirementPath(new[] { IdentityType.Email, IdentityType.Username });
        var identities = new[] { MakeIdentity(IdentityType.Email, IdentityVerificationStatus.Pending) };

        Assert.False(path.IsSatisfiedBy(identities));
    }

    [Fact]
    public void is_verified_satisfied_when_all_required_are_verified()
    {
        var path = new RequirementPath(new[] { IdentityType.Email });
        var identities = new[] { MakeIdentity(IdentityType.Email, IdentityVerificationStatus.Verified) };

        Assert.True(path.IsVerifiedSatisfiedBy(identities));
    }

    [Fact]
    public void is_not_verified_satisfied_when_identity_is_pending()
    {
        var path = new RequirementPath(new[] { IdentityType.Email });
        var identities = new[] { MakeIdentity(IdentityType.Email, IdentityVerificationStatus.Pending) };

        Assert.False(path.IsVerifiedSatisfiedBy(identities));
    }

    [Fact]
    public void extra_identities_do_not_prevent_satisfaction()
    {
        var path = new RequirementPath(new[] { IdentityType.Email });
        var identities = new[]
        {
            MakeIdentity(IdentityType.Email, IdentityVerificationStatus.Verified),
            MakeIdentity(IdentityType.ApiKey, IdentityVerificationStatus.Pending)
        };

        Assert.True(path.IsVerifiedSatisfiedBy(identities));
    }
}
