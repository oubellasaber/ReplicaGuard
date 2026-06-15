using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReplicaGuard.Core.Abstractions;
using ReplicaGuard.Core.Capabilities;
using ReplicaGuard.Core.HosterAccounts;
using ReplicaGuard.Core.Hosters;

namespace ReplicaGuard.Infrastructure.Hosters.Pixeldrain.Upload;

internal sealed class PixeldrainLocalFileUploadHandler : ILocalFileUploadHandler
{
    private readonly HttpClient _uploadClient;
    private readonly PixeldrainOptions _options;
    private readonly ISecretEncryptionService _secretEncryptionService;
    private readonly ILogger<PixeldrainLocalFileUploadHandler> _logger;

    public HosterCode HosterCode => HosterCode.Pixeldrain;
    public CapabilityCode CapabilityCode => CapabilityCode.LocalFileUpload;

    public PixeldrainLocalFileUploadHandler(
        IHttpClientFactory factory,
        IOptions<PixeldrainOptions> options,
        ISecretEncryptionService secretEncryptionService,
        ILogger<PixeldrainLocalFileUploadHandler> logger)
    {
        _uploadClient = factory.CreateClient("FileUploadingHttpClient");
        _options = options.Value;
        _secretEncryptionService = secretEncryptionService;
        _logger = logger;
    }

    public async Task<Result<LocalFileUploadResponse>> HandleAsync(LocalFileUploadRequest input, CancellationToken ct = default)
    {
        var (account, fileName, source) = input;

        // Find later how to validate that this account can be used for upload (e.g. check if API key is present and valid)
        // This is just to ensure we don't proceed with invalid data and to provide early feedback in case of misconfiguration
        // This could be implemented  as a cross-cutting concern or a decorator in the future if we find that multiple handlers need similar validation logic
        ArgumentNullException.ThrowIfNull(account);
        ArgumentException.ThrowIfNullOrEmpty(fileName);
        ArgumentNullException.ThrowIfNull(source);

        var decryptedApiKey = account
            .GetAuthIdentity(IdentityType.ApiKey)
            .RevealSecret(SecretType.ApiKeyPair, _secretEncryptionService);

        if (!File.Exists(source.FilePath))
        {
            return Result.Failure<LocalFileUploadResponse>(HosterErrors.LocalFileNotFound(source.FilePath));
        }

        try
        {
            await using var fileStream = new FileStream(source.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var content = new StreamContent(fileStream);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            var escapedFileName = Uri.EscapeDataString(fileName);
            var fileUploadUrl = $"{_options.FileUploadEndpoint.TrimEnd('/')}/{escapedFileName}";
            using var req = new HttpRequestMessage(HttpMethod.Put, fileUploadUrl)
            {
                Content = content,
                Headers = { Authorization = PixeldrainBasicAuthenticationHeaderFactory.Create(decryptedApiKey) }
            };

            // Using the upload client that has a larger timeout configuration
            using var res = await _uploadClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);

            var parseResult = await PixeldrainUploadResponseParser.ParseAsync(res, ct);

            if (parseResult.IsFailure)
                return Result.Failure<LocalFileUploadResponse>(parseResult.Error);

            var fileId = parseResult.Value;
            var fileUrl = new Uri($"{_options.FileUrlBase}/{fileId}");

            return Result.Success(new LocalFileUploadResponse(
                FileId: fileId,
                FileUrl: fileUrl,
                FileName: fileName,
                SizeBytes: fileStream.Length
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error during local file upload to hoster {HosterCode} for file {FileName} from path {FilePath}",
                HosterCode,
                fileName,
                source.FilePath);
            throw;
        }
    }
}
