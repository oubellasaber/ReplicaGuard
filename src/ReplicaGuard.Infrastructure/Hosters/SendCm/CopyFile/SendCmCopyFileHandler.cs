using System.Text.Json;
using Microsoft.Extensions.Options;
using ReplicaGuard.Domain.Abstractions;
using ReplicaGuard.Domain.Capabilities;
using ReplicaGuard.Domain.HosterAccounts;
using ReplicaGuard.Domain.Hosters;

namespace ReplicaGuard.Infrastructure.Hosters.SendCm.CopyFile;

internal sealed class SendCmCopyFileHandler : ICopyFileCapabilityHandler
{
    public HosterCode HosterCode => HosterCode.SendCm;
    public CapabilityCode CapabilityCode => CapabilityCode.CopyFile;

    private readonly HttpClient _httpClient;
    private readonly ISecretEncryptionService _secretEncryptionService;
    private readonly IHosterDefinitionResolver _resolver;
    private readonly SendCmOptions _options;

    public SendCmCopyFileHandler(
        HttpClient httpClient,
        ISecretEncryptionService secretEncryptionService,
        IHosterDefinitionResolver resolver,
        IOptions<SendCmOptions> options)
    {
        _httpClient = httpClient;
        _secretEncryptionService = secretEncryptionService;
        _resolver = resolver;
        _options = options.Value;
    }

    public async Task<Result<CopyFileResponse>> HandleAsync(CopyFileRequest input, CancellationToken ct)
    {
        var decryptedApiKeyResult = input.Account.GetApiKey(_secretEncryptionService);

        if (decryptedApiKeyResult.IsFailure)
            return Result.Failure<CopyFileResponse>(decryptedApiKeyResult.Error);

        var decryptedApiKey = decryptedApiKeyResult.Value;

        // build this  https://send.now/api/file/clone?key=1ltghrilhllgrx2b2&file_code=eb58d02u8znz
        var hoster = _resolver.Resolve(HosterCode.SendCm);
        var fileCodeResult = hoster.ExtractFileCode(input.Url);

        if (fileCodeResult.IsFailure)
            return Result.Failure<CopyFileResponse>(fileCodeResult.Error);

        var fileCode = fileCodeResult.Value;
        var requestUrl = new Uri($"{_options.ApiBaseUrl}/api/file/clone?key={decryptedApiKey}&file_code={fileCode}");
        var response = await _httpClient.GetAsync(requestUrl, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        var json = JsonDocument.Parse(body);
        var status = json.RootElement.GetProperty("status").GetInt32();

        if (status == 200)
        {
            var result = json.RootElement.GetProperty("result");
            var newFileCode = result.GetProperty("filecode").GetString() ?? throw new InvalidOperationException("The response did not contain a valid file code.");
            return Result.Success(new CopyFileResponse(newFileCode));
        }
        else if (status == 403)
        {
            return Result.Failure<CopyFileResponse>(SendCmCopyFileErrors.ValidApiKeyIsRequired());
        }
        else if (status == 404)
        {
            return Result.Failure<CopyFileResponse>(SendCmCopyFileErrors.FileWithCodeNotFound(fileCode));
        }
        else
        {
            var msg = json.RootElement.GetProperty("msg").GetString();
            return Result.Failure<CopyFileResponse>(SendCmCopyFileErrors.UnknownError(status));
        }
    }
}

public static class SendCmCopyFileErrors
{
    public static Error InvalidUrl(string url) =>
        new Error("Hoster.SendCm.FileCopy.InvalidUrl", "The provided URL is invalid.")
            .WithDetail($"'{url}' is not a valid SendCm URL.")
            .WithType(ErrorType.InvalidInput)
            .AsPermanent();

    public static Error ValidApiKeyIsRequired() =>
        new Error("Hoster.SendCm.FileCopy.ValidApiKeyIsRequired", "A valid api key is required for this operation.")
            .WithType(ErrorType.Unauthorized)
            .AsPermanent();

    public static Error FileWithCodeNotFound(string fileCode) =>
        new Error("Hoster.SendCm.FileCopy.FileNotFound", "The specified file was not found on SendCm.")
            .WithDetail($"No file with code '{fileCode}' was found on SendCm.")
            .WithType(ErrorType.NotFound)
            .AsPermanent();

    public static Error UnknownError(int statusCode) =>
        new Error("Hoster.SendCm.FileCopy.Unknown", "An unknown error occurred during file copy.")
            .WithMetadata("StatusCode", statusCode)
            .WithType(ErrorType.Failure);
}
