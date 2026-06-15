using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using ReplicaGuard.Core.Abstractions;
using ReplicaGuard.Core.Capabilities;
using ReplicaGuard.Core.HosterAccounts;
using ReplicaGuard.Core.Hosters;

namespace ReplicaGuard.Infrastructure.Hosters.SendCm.IdentityVerification;

internal class SendCmIdentityVerificationHandler : IIdentityVerificationHandler
{
    private readonly HttpClient _httpClient;
    private readonly SendCmOptions _options;
    private readonly ISecretEncryptionService _secretEncryptionService;
    
    public HosterCode HosterCode => HosterCode.SendCm;
    public CapabilityCode CapabilityCode => CapabilityCode.IdentityVerification;

    public SendCmIdentityVerificationHandler(
        IHttpClientFactory factory,
        IOptions<SendCmOptions> options,
        ISecretEncryptionService secretEncryptionService)
    {
        _httpClient = factory.CreateClient(HosterCode.SendCm.ToFriendlyString());
        _options = options.Value;
        _secretEncryptionService = secretEncryptionService;
    }


    public async Task<Result> HandleAsync(IdentityVerificationRequest input, CancellationToken ct = default)
    {
        var decryptedApiKey = input.identity
            .RevealSecret(SecretType.ApiKeyPair, _secretEncryptionService);

        string url = QueryHelpers.AddQueryString(_options.UserInfoEndpoint, "key", decryptedApiKey);

        using var response = await _httpClient.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode)
            return Result.Failure(IdentityVerificationErrors.InvalidApiKey(HosterCode));

        await using Stream stream = await response.Content.ReadAsStreamAsync(ct);
        using JsonDocument doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        if (!doc.RootElement.TryGetProperty("status", out JsonElement statusElement))
            return Result.Failure(IdentityVerificationErrors.InvalidApiKey(HosterCode));

        int status = statusElement.GetInt32();
        if (status == 403)
            return Result.Failure(IdentityVerificationErrors.InvalidApiKey(HosterCode));

        return Result.Success();
    }
}
