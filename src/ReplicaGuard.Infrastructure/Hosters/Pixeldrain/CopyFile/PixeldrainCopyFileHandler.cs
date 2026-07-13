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
    private readonly IHosterDefinitionResolver _resolver;
    private readonly PixeldrainOptions _options;

    public HosterCode HosterCode => HosterCode.Pixeldrain;
    public CapabilityCode CapabilityCode => CapabilityCode.CopyFile;

    public PixeldrainCopyFileHandler(
        HttpClient httpClient,
        ISecretEncryptionService secretEncryptionService,
        IHosterDefinitionResolver resolver,
        IOptions<PixeldrainOptions> options)
    {
        _httpClient = httpClient;
        _secretEncryptionService = secretEncryptionService;
        _resolver = resolver;
        _options = options.Value;
    }

    public async Task<Result<CopyFileResponse>> HandleAsync(CopyFileRequest input, CancellationToken ct = default)
    {
        var decryptedApiKeyResult = input.Account.GetApiKey(_secretEncryptionService);

        if (decryptedApiKeyResult.IsFailure)
            return Result.Failure<CopyFileResponse>(decryptedApiKeyResult.Error);

        var decryptedApiKey = decryptedApiKeyResult.Value;

        var hoster = _resolver.Resolve(HosterCode.Pixeldrain);
        var fileCodeResult = hoster.ExtractFileCode(input.Url);

        if (fileCodeResult.IsFailure)
            return Result.Failure<CopyFileResponse>(fileCodeResult.Error);

        var fileCode = fileCodeResult.Value;

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
}
