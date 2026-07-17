using ReplicaGuard.Domain.HosterAccounts;

namespace ReplicaGuard.Domain.Tests;

public sealed class AuthIdentityTests
{
    private static SecretSet MakeSecretSet()
    {
        return SecretSet.Create(new[] { Secret.CreateNew(SecretType.Password, new SecretValue(new byte[] { 1, 2, 3 })) });
    }

    [Fact]
    public void create_email_identity_with_value_succeeds()
    {
        var identity = AuthIdentity.CreateNew(IdentityType.Email, "user@example.com", MakeSecretSet());

        Assert.Equal(IdentityType.Email, identity.Type);
        Assert.Equal("user@example.com", identity.Value);
        Assert.Equal(IdentityVerificationStatus.Pending, identity.Status);
    }

    [Fact]
    public void create_username_identity_with_value_succeeds()
    {
        var identity = AuthIdentity.CreateNew(IdentityType.Username, "john_doe", MakeSecretSet());

        Assert.Equal("john_doe", identity.Value);
    }

    [Fact]
    public void create_api_key_identity_with_null_value_succeeds()
    {
        var identity = AuthIdentity.CreateNew(IdentityType.ApiKey, null, MakeSecretSet());

        Assert.Equal(IdentityType.ApiKey, identity.Type);
        Assert.Null(identity.Value);
    }

    [Fact]
    public void create_email_identity_with_null_value_throws()
    {
        Assert.Throws<Exception>(() =>
            AuthIdentity.CreateNew(IdentityType.Email, null, MakeSecretSet()));
    }

    [Fact]
    public void create_api_key_identity_with_value_throws()
    {
        Assert.Throws<Exception>(() =>
            AuthIdentity.CreateNew(IdentityType.ApiKey, "some-key", MakeSecretSet()));
    }

    [Fact]
    public void mark_as_verifying_sets_status()
    {
        var identity = AuthIdentity.CreateNew(IdentityType.Email, "a@b.com", MakeSecretSet());

        identity.MarkAsVerifying();

        Assert.Equal(IdentityVerificationStatus.Verifying, identity.Status);
    }

    [Fact]
    public void mark_as_verified_sets_status()
    {
        var identity = AuthIdentity.CreateNew(IdentityType.Email, "a@b.com", MakeSecretSet());

        identity.MarkAsVerified();

        Assert.Equal(IdentityVerificationStatus.Verified, identity.Status);
    }

    [Fact]
    public void mark_as_rejected_sets_status()
    {
        var identity = AuthIdentity.CreateNew(IdentityType.Email, "a@b.com", MakeSecretSet());

        identity.MarkAsRejected();

        Assert.Equal(IdentityVerificationStatus.Rejected, identity.Status);
    }

    [Fact]
    public void creation_raises_identity_created_domain_event()
    {
        var identity = AuthIdentity.CreateNew(IdentityType.Email, "a@b.com", MakeSecretSet());

        var domainEvent = identity.GetDomainEvents().OfType<IdentityCreatedDomainEvent>().Single();

        Assert.Equal(identity.Id, domainEvent.IdentityId);
    }
}
