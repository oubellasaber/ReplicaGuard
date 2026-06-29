using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using ReplicaGuard.Domain.Abstractions;
using ReplicaGuard.Domain.Capabilities;
using ReplicaGuard.Domain.HosterAccounts;
using ReplicaGuard.Domain.Hosters;

namespace ReplicaGuard.Infrastructure.Hosters.Pixeldrain.CopyFile;

internal class PixeldrainCopyFileHandler : ICopyFileCapabilityHandler
{
    private const string AuthCookieName = "pd_auth_key";
    private const string AuthRequiredErrorValue = "authentication_required";
    private const string FileNotFoundErrorValue = "not_found";

    private readonly HttpClient _httpClient;
    private readonly ISecretEncryptionService _secretEncryptionService;
    private readonly PixeldrainOptions _options;

    public HosterCode HosterCode => HosterCode.Pixeldrain;
    public CapabilityCode CapabilityCode => CapabilityCode.CopyFile;

    public PixeldrainCopyFileHandler(
        HttpClient httpClient,
        ISecretEncryptionService secretEncryptionService,
        IOptions<PixeldrainOptions> options)
    {
        _httpClient = httpClient;
        _secretEncryptionService = secretEncryptionService;
        _options = options.Value;
    }

    public async Task<Result<CopyFileResponse>> HandleAsync(CopyFileRequest input, CancellationToken ct = default)
    {
        var apiKeyIdentity = input.Account
            .Identities
            .First(id => id.Type == IdentityType.ApiKey);

        var decryptedApiKey = apiKeyIdentity
            .RevealSecret(SecretType.ApiKeyPair, _secretEncryptionService);

        var fileCode = ExtractFileCode(input.Url);
        
        if (string.IsNullOrEmpty(fileCode)) {
            return Result.Failure<CopyFileResponse>(PixeldrainFileCopyErrors.FileWithCodeNotFound(fileCode));
        }

        var request = new HttpRequestMessage(HttpMethod.Post, _options.FileUploadEndpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "grab_file", fileCode }
            }),
            Headers =
            {
                { "Cookie", $"{AuthCookieName}={decryptedApiKey}" }
            }
        };

        var response = await _httpClient.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        var copiedFileJson = JsonDocument.Parse(body);
        var copiedFileCode = copiedFileJson.RootElement.GetProperty("id").GetString();

        if (string.IsNullOrEmpty(copiedFileCode))
        {
            var errorValue = copiedFileJson.RootElement.GetProperty("value").GetString();
            if (errorValue == AuthRequiredErrorValue)
                return Result.Failure<CopyFileResponse>(PixeldrainFileCopyErrors.ValidApiKeyIsRequired());
            if (errorValue == FileNotFoundErrorValue)
                return Result.Failure<CopyFileResponse>(PixeldrainFileCopyErrors.FileWithCodeNotFound(fileCode));
            return Result.Failure<CopyFileResponse>(PixeldrainFileCopyErrors.UnknownError((int)response.StatusCode, body));
        }

        return Result.Success(new CopyFileResponse(copiedFileCode));
    }

    private static string ExtractFileCode(Uri url)
    {
        if (url is null)
            throw new ArgumentNullException(nameof(url));

        var match = Regex.Match(
            url.ToString(),
            @"^https?:\/\/[^\/]+\/u\/([A-Za-z0-9]+)\/?$",
            RegexOptions.Compiled
        );

        if (!match.Success)
            throw new ArgumentException(
                "URL must be in the format https://<domain>/u/{id}",
                nameof(url)
            );

        return match.Groups[1].Value;
    }
}
