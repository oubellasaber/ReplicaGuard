using Microsoft.Extensions.Options;
using ReplicaGuard.Core.Abstractions;
using ReplicaGuard.Core.Capabilities;
using ReplicaGuard.Core.HosterAccounts;
using ReplicaGuard.Core.Hosters;

namespace ReplicaGuard.Infrastructure.Hosters.Pixeldrain.IdentityVerification;

internal class PixeldrainIdentityVerificationHandler : IIdentityVerificationHandler
{
    private readonly HttpClient _httpClient;
    private readonly PixeldrainOptions _options;
    private readonly ISecretEncryptionService _secretEncryptionService;

    public HosterCode HosterCode => HosterCode.Pixeldrain;
    public CapabilityCode CapabilityCode => CapabilityCode.IdentityVerification;

    public PixeldrainIdentityVerificationHandler(
        IHttpClientFactory factory,
        IOptions<PixeldrainOptions> options,
        ISecretEncryptionService secretEncryptionService)
    {
        _httpClient = factory.CreateClient(HosterCode.Pixeldrain.ToFriendlyString());
        _options = options.Value;
        _secretEncryptionService = secretEncryptionService;
    }

    // TODO: when handling the identity verification, we should handle all the identity types supported by the hoster.
    public async Task<Result> HandleAsync(IdentityVerificationRequest input, CancellationToken ct = default)
    {
        var decryptedApiKey = input.Identity
            .RevealSecret(SecretType.ApiKeyPair, _secretEncryptionService);

        var req = new HttpRequestMessage(HttpMethod.Head, _options.UserInfoEndpoint)
        {
            Headers = { Authorization = PixeldrainBasicAuthenticationHeaderFactory.Create(decryptedApiKey) }
        };

        var res = await _httpClient.SendAsync(req, ct);

        return res.IsSuccessStatusCode ?
            Result.Success() :
            Result.Failure(IdentityVerificationErrors.InvalidApiKey(HosterCode));
    }
}
