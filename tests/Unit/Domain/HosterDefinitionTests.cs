using ReplicaGuard.Domain.HosterAccounts;
using ReplicaGuard.Domain.Hosters;

namespace ReplicaGuard.Domain.Tests;

public sealed class HosterDefinitionTests
{
    private static readonly FakeEncryptionService Encryption = new();

    // Pixeldrain

    [Fact]
    public void pixeldrain_extract_file_code_from_valid_url()
    {
        var pixeldrain = new Pixeldrain();
        var url = new Uri("https://pixeldrain.com/u/abc123");

        var result = pixeldrain.ExtractFileCode(url);

        Assert.True(result.IsSuccess);
        Assert.Equal("abc123", result.Value);
    }

    [Fact]
    public void pixeldrain_extract_file_code_from_short_domain()
    {
        var pixeldrain = new Pixeldrain();
        var url = new Uri("https://pixeldra.in/u/xyz789");

        var result = pixeldrain.ExtractFileCode(url);

        Assert.True(result.IsSuccess);
        Assert.Equal("xyz789", result.Value);
    }

    [Fact]
    public void pixeldrain_extract_file_code_fails_for_domain_without_u_prefix()
    {
        var pixeldrain = new Pixeldrain();
        var url = new Uri("https://pixeldrain.com/abc123");

        var result = pixeldrain.ExtractFileCode(url);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void pixeldrain_extract_file_code_fails_for_unexpected_host()
    {
        var pixeldrain = new Pixeldrain();
        var url = new Uri("https://example.com/file");

        var result = pixeldrain.ExtractFileCode(url);

        Assert.True(result.IsFailure);
        Assert.Equal(HosterErrors.UnsupportedHosterDomain("example.com").Code, result.Error.Code);
    }

    [Fact]
    public void pixeldrain_extract_file_code_throws_on_null()
    {
        var pixeldrain = new Pixeldrain();

        Assert.Throws<ArgumentNullException>(() => pixeldrain.ExtractFileCode(null!));
    }

    [Fact]
    public void pixeldrain_build_file_url_returns_correct_uri()
    {
        var pixeldrain = new Pixeldrain();

        var result = pixeldrain.BuildFileUrl("abc123");

        Assert.True(result.IsSuccess);
        Assert.Equal("https://pixeldrain.com/u/abc123", result.Value.ToString());
    }

    [Fact]
    public void pixeldrain_build_file_url_with_empty_code_fails()
    {
        var pixeldrain = new Pixeldrain();

        var result = pixeldrain.BuildFileUrl("");

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void pixeldrain_group_for_email_returns_group_with_email_and_username()
    {
        var pixeldrain = new Pixeldrain();

        var group = pixeldrain.GroupFor(IdentityType.Email);

        Assert.NotNull(group);
        Assert.Contains(IdentityType.Email, group.GroupedIdentites);
        Assert.Contains(IdentityType.Username, group.GroupedIdentites);
    }

    [Fact]
    public void pixeldrain_group_for_api_key_returns_api_group()
    {
        var pixeldrain = new Pixeldrain();

        var group = pixeldrain.GroupFor(IdentityType.ApiKey);

        Assert.NotNull(group);
        Assert.Contains(IdentityType.ApiKey, group.GroupedIdentites);
        Assert.DoesNotContain(IdentityType.Email, group.GroupedIdentites);
    }

    // SendCm

    [Fact]
    public void sendcm_extract_file_code_from_valid_url()
    {
        var sendCm = new SendCm();
        var url = new Uri("https://send.cm/filecode123");

        var result = sendCm.ExtractFileCode(url);

        Assert.True(result.IsSuccess);
        Assert.Equal("filecode123", result.Value);
    }

    [Fact]
    public void sendcm_extract_file_code_from_alt_domain()
    {
        var sendCm = new SendCm();
        var url = new Uri("https://send.now/file456");

        var result = sendCm.ExtractFileCode(url);

        Assert.True(result.IsSuccess);
        Assert.Equal("file456", result.Value);
    }

    [Fact]
    public void sendcm_extract_file_code_fails_for_unknown_domain()
    {
        var sendCm = new SendCm();
        var url = new Uri("https://evil.com/file");

        var result = sendCm.ExtractFileCode(url);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void sendcm_build_file_url_returns_correct_uri()
    {
        var sendCm = new SendCm();

        var result = sendCm.BuildFileUrl("code123");

        Assert.True(result.IsSuccess);
        Assert.Equal("https://send.now/code123", result.Value.ToString());
    }

    [Fact]
    public void sendcm_build_file_url_with_empty_code_fails()
    {
        var sendCm = new SendCm();

        var result = sendCm.BuildFileUrl("");

        Assert.True(result.IsFailure);
    }

    // HosterDefinitionBase

    private static HosterAccount CreateAccountWithVerifiedApiKey(Hoster hoster, IHosterDefinition definition)
    {
        var account = HosterAccount.Create(
            definition,
            hoster,
            Guid.NewGuid(),
            "test",
            null,
            new[] { new IdentityPayload.ApiKeyPayload("my-api-key") },
            Encryption).Value;

        account.Identities.Single().MarkAsVerified();

        SetHosterViaReflection(account, hoster);

        return account;
    }

    private static void SetHosterViaReflection(HosterAccount account, Hoster hoster)
    {
        typeof(HosterAccount).GetProperty(nameof(HosterAccount.Hoster))!
            .SetValue(account, hoster);
    }

    [Fact]
    public void validate_primary_credentials_succeeds_with_matching_hoster_and_verified_identities()
    {
        var definition = new Pixeldrain();
        var hoster = new Hoster(HosterCode.Pixeldrain, "Pixeldrain");
        var account = CreateAccountWithVerifiedApiKey(hoster, definition);

        var result = definition.ValidatePrimaryCredentials(account);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void validate_primary_credentials_fails_when_hoster_code_mismatches()
    {
        var definition = new Pixeldrain();
        var hoster = new Hoster(HosterCode.SendCm, "SendCM");
        var account = CreateAccountWithVerifiedApiKey(hoster, definition);

        var result = definition.ValidatePrimaryCredentials(account);

        Assert.True(result.IsFailure);
        Assert.Equal(HosterErrors.AccountDoesNotBelongToHoster(account.HosterCode, account.Id).Code, result.Error.Code);
    }

    [Fact]
    public void validate_primary_credentials_fails_when_required_identity_not_verified()
    {
        var definition = new Pixeldrain();
        var hoster = new Hoster(HosterCode.Pixeldrain, "Pixeldrain");

        var account = HosterAccount.Create(
            definition,
            hoster,
            Guid.NewGuid(),
            "test",
            null,
            new[] { new IdentityPayload.ApiKeyPayload("api-key-value") },
            Encryption).Value;

        SetHosterViaReflection(account, hoster);

        var result = definition.ValidatePrimaryCredentials(account);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void validate_capability_succeeds_when_requirement_satisfied()
    {
        var definition = new Pixeldrain();
        var hoster = new Hoster(HosterCode.Pixeldrain, "Pixeldrain");
        var account = CreateAccountWithVerifiedApiKey(hoster, definition);

        var result = definition.ValidateCapability(account, CapabilityCode.LocalFileUpload);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void validate_capability_fails_when_hoster_code_mismatches()
    {
        var definition = new Pixeldrain();
        var hoster = new Hoster(HosterCode.SendCm, "SendCM");
        var account = CreateAccountWithVerifiedApiKey(hoster, definition);

        var result = definition.ValidateCapability(account, CapabilityCode.LocalFileUpload);

        Assert.True(result.IsFailure);
        Assert.Equal(HosterErrors.AccountDoesNotBelongToHoster(account.HosterCode, account.Id).Code, result.Error.Code);
    }

    [Fact]
    public void validate_capability_fails_for_unsupported_capability()
    {
        var definition = new Pixeldrain();
        var hoster = new Hoster(HosterCode.Pixeldrain, "Pixeldrain");
        var account = CreateAccountWithVerifiedApiKey(hoster, definition);

        var result = definition.ValidateCapability(account, CapabilityCode.RemoteFileUpload);

        Assert.True(result.IsFailure);
        Assert.Equal(HosterErrors.CapabilityNotSupported(account.HosterCode, CapabilityCode.RemoteFileUpload).Code, result.Error.Code);
    }
}
