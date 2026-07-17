using ReplicaGuard.Domain.HosterAccounts;
using ReplicaGuard.Domain.Hosters;

namespace ReplicaGuard.Domain.Tests;

public sealed class HosterAccountTests
{
    private static readonly FakeEncryptionService Encryption = new();
    private static readonly Hoster PixeldrainHoster = new(HosterCode.Pixeldrain, "Pixeldrain");
    private static readonly IHosterDefinition PixeldrainDef = new Pixeldrain();

    [Fact]
    public void create_account_with_api_key_identity_succeeds()
    {
        var result = HosterAccount.Create(
            PixeldrainDef,
            PixeldrainHoster,
            Guid.NewGuid(),
            "My Account",
            null,
            new[] { new IdentityPayload.ApiKeyPayload("px-api-key") },
            Encryption);

        Assert.True(result.IsSuccess);
        var account = result.Value;
        Assert.Equal("My Account", account.Alias);
        Assert.Single(account.Identities);
        Assert.Equal(IdentityType.ApiKey, account.Identities[0].Type);
    }

    [Fact]
    public void create_account_with_email_identity_succeeds()
    {
        var result = HosterAccount.Create(
            PixeldrainDef,
            PixeldrainHoster,
            Guid.NewGuid(),
            "Email Account",
            "my description",
            new[] { new IdentityPayload.ApiKeyPayload("px-api-key") },
            Encryption);

        Assert.True(result.IsSuccess);
        var account = result.Value;
        Assert.Equal("Email Account", account.Alias);
        Assert.Equal("my description", account.Description);
        Assert.Single(account.Identities);
        Assert.Equal(IdentityType.ApiKey, account.Identities[0].Type);
    }

    [Fact]
    public void create_account_without_identities_fails()
    {
        var result = HosterAccount.Create(
            PixeldrainDef,
            PixeldrainHoster,
            Guid.NewGuid(),
            "Empty",
            null,
            Enumerable.Empty<IdentityPayload>(),
            Encryption);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void get_api_key_returns_key_when_verified()
    {
        var account = HosterAccount.Create(
            PixeldrainDef,
            PixeldrainHoster,
            Guid.NewGuid(),
            "Test",
            null,
            new[] { new IdentityPayload.ApiKeyPayload("secret-key") },
            Encryption).Value;

        account.Identities.Single().MarkAsVerified();

        var result = account.GetApiKey(Encryption);

        Assert.True(result.IsSuccess);
        Assert.Equal("secret-key", result.Value);
    }

    [Fact]
    public void get_api_key_fails_when_not_verified()
    {
        var account = HosterAccount.Create(
            PixeldrainDef,
            PixeldrainHoster,
            Guid.NewGuid(),
            "Test",
            null,
            new[] { new IdentityPayload.ApiKeyPayload("secret-key") },
            Encryption).Value;

        var result = account.GetApiKey(Encryption);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthIdentityErrors.IdentityNotVerified(account.Id, account.Identities.Single().Id).Code, result.Error.Code);
    }
}
