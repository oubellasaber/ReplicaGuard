using System.Text.Json;
using System.Text.RegularExpressions;
using MassTransit.Configuration;
using Microsoft.Extensions.Options;
using ReplicaGuard.Core.Abstractions;
using ReplicaGuard.Core.HosterAccounts;
using ReplicaGuard.Core.Hosters;
using ReplicaGuard.Infrastructure.Hosters.Capabilities;

namespace ReplicaGuard.Infrastructure.Hosters.SendCm.CopyFile;
internal sealed class SendCmCopyFileHandler : ICopyFileCapabilityHandler
{
    public HosterCode HosterCode => HosterCode.SendCm;
    public CapabilityCode CapabilityCode => CapabilityCode.CopyFile;

    private readonly HttpClient _httpClient;
    private readonly ISecretEncryptionService _secretEncryptionService;
    private readonly SendCmOptions _options;

    public SendCmCopyFileHandler(
        HttpClient httpClient,
        ISecretEncryptionService secretEncryptionService,
        IOptions<SendCmOptions> options)
    {
        _httpClient = httpClient;
        _secretEncryptionService = secretEncryptionService;
        _options = options.Value;
    }

    public async Task<Result<CopyFileResponse>> HandleAsync(CopyFileRequest input, CancellationToken ct)
    {
        var apiKeyIdentity = input.Account
            .Identities
            .First(id => id.Type == IdentityType.ApiKey);

        if (apiKeyIdentity is null || apiKeyIdentity.Status != IdentityVerificationStatus.Verified)
        {
            throw new InvalidOperationException("The account does not have a verified API key identity.");
        }

        var decryptedApiKey = apiKeyIdentity
            .RevealSecret(SecretType.ApiKeyPair, _secretEncryptionService);

        // build this  https://send.now/api/file/clone?key=1ltghrilhllgrx2b2&file_code=eb58d02u8znz
        var fileCode = ExtractFileCode(input.Url);
        var requestUrl = new Uri($"{_options.ApiBaseUrl}/file/clone?key={decryptedApiKey}&file_code={fileCode}");
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

    private static string ExtractFileCode(Uri url)
    {
        if (url is null)
            throw new ArgumentNullException(nameof(url));

        var match = Regex.Match(
            url.ToString(),
            @"^https?:\/\/[^\/]+\/([A-Za-z0-9]+)\/?$",
            RegexOptions.Compiled
        );

        if (!match.Success)
            throw new ArgumentException(
                "URL must be in the format https://<domain>/{id}",
                nameof(url)
            );

        return match.Groups[1].Value;
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
