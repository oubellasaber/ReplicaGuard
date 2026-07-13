using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ReplicaGuard.Domain.Abstractions;
using ReplicaGuard.Domain.Capabilities;
using ReplicaGuard.Domain.HosterAccounts;
using ReplicaGuard.Domain.Hosters;

namespace ReplicaGuard.Infrastructure.Hosters.SendCm.GetFileInfo;

internal sealed class SendCmGetFileInfoHandler : IGetFileInfoCapabilityHandler
{
    private const string SendCmFormat = "yyyy-MM-dd HH:mm:ss";

    public HosterCode HosterCode => HosterCode.SendCm;
    public CapabilityCode CapabilityCode => CapabilityCode.GetFileInfo;

    private readonly HttpClient _httpClient;
    private readonly ISecretEncryptionService _secretEncryptionService;
    private readonly IHosterDefinitionResolver _resolver;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SendCmOptions _sendCmOptions;

    public SendCmGetFileInfoHandler(
        HttpClient httpClient,
        ISecretEncryptionService secretEncryptionService,
        IHosterDefinitionResolver resolver,
        IOptions<SendCmOptions> sendCmOptions,
        IServiceScopeFactory scopeFactory)
    {
        _httpClient = httpClient;
        _secretEncryptionService = secretEncryptionService;
        _resolver = resolver;
        _scopeFactory = scopeFactory;
        _sendCmOptions = sendCmOptions.Value;
    }

    public async Task<Result<GetFileInfoResponse>> HandleAsync(
        GetFileInfoRequest input,
        CancellationToken ct = default)
    {
        // Getting file info for anon files is not supported for now, so we require a hoster account to be provided.
        ArgumentNullException.ThrowIfNull(input.Replica.HosterAccountId, nameof(input.Replica.HosterAccountId));
        ArgumentNullException.ThrowIfNull(input.Replica.Link, nameof(input.Replica.Link));
        using var scope = _scopeFactory.CreateScope();
        var hosterAccountRepository = scope.ServiceProvider.GetRequiredService<IHosterAccountRepository>();
        var hosterAccount = await hosterAccountRepository.GetByIdAsync(input.Replica.HosterAccountId.Value, ct);

        ArgumentNullException.ThrowIfNull(hosterAccount, nameof(input.Replica.HosterAccountId));
        var decryptedApiKeyResult = hosterAccount.GetApiKey(_secretEncryptionService);

        if (decryptedApiKeyResult.IsFailure)
            return Result.Failure<GetFileInfoResponse>(
                decryptedApiKeyResult.Error);

        var decryptedApiKey = decryptedApiKeyResult.Value;

        var hoster = _resolver.Resolve(HosterCode.SendCm);

        var fileCodeResult = hoster.ExtractFileCode(input.Replica.Link);

        if (fileCodeResult.IsFailure)
            return Result.Failure<GetFileInfoResponse>(
                fileCodeResult.Error);

        var fileCode = fileCodeResult.Value;

        var requestUrl =
            $"{_sendCmOptions.FileInfoEndpoint}?key={decryptedApiKey}&file_code={fileCode}";


        using var response = await _httpClient.GetAsync(requestUrl, ct);

        var receivedAtUtc = DateTime.UtcNow;

        var body = await response.Content.ReadAsStringAsync(ct);

        using var json = JsonDocument.Parse(body);

        var root = json.RootElement;

        var status = root
            .GetProperty("status")
            .GetInt32();


        if (status == 403)
        {
            return Result.Failure<GetFileInfoResponse>(
                SendCmGetFileInfoErrors.ValidApiKeyIsRequired());
        }

        if (status == 404)
        {
            return Result.Failure<GetFileInfoResponse>(
                SendCmGetFileInfoErrors.FileNotFound(fileCode));
        }

        if (status != 200)
        {
            return Result.Failure<GetFileInfoResponse>(
                SendCmGetFileInfoErrors.UnknownError(status));
        }


        var results = root.GetProperty("result");

        if (results.GetArrayLength() == 0)
        {
            return Result.Failure<GetFileInfoResponse>(
                SendCmGetFileInfoErrors.FileNotFound(fileCode));
        }


        var file = results[0];

        var fileStatus = file
            .GetProperty("file_status")
            .GetInt32();

        if (fileStatus != 200)
        {
            return Result.Failure<GetFileInfoResponse>(
                SendCmGetFileInfoErrors.FileNotFound(fileCode));
        }


        var serverTime = root
            .GetProperty("server_time")
            .GetString()!;

        var serverOffset = CalculateServerOffset(
            serverTime,
            receivedAtUtc);


        var uploadedUtc = ConvertSendCmToUtc(
            file.GetProperty("uploaded").GetString()!,
            serverOffset);


        DateTime lastDownloadUtc = DateTime.MinValue;

        if (file.TryGetProperty("file_last_download", out var lastDownloadElement))
        {
            var lastDownload = lastDownloadElement.GetString();

            if (!string.IsNullOrWhiteSpace(lastDownload))
            {
                lastDownloadUtc = ConvertSendCmToUtc(
                    lastDownload,
                    serverOffset);
            }
        }


        var fileInfo = new GetFileInfoResponse
        (
            Id: file.GetProperty("filecode").GetString()!,
            Url: file.GetProperty("url").GetString()!,
            Name: file.GetProperty("name").GetString()!,
            TotalBytes: file.GetProperty("size").GetInt64(),

            UploadedToHosterAt: uploadedUtc,

            LastDownloadDateFromHoster: input.IncludeLastDownloadDate ? lastDownloadUtc : null,

            Sha256Hash:
                file.TryGetProperty("file_sha256", out var sha)
                    ? sha.GetString()
                    : null,

            Md5Hash:
                file.TryGetProperty("file_md5", out var md5)
                    ? md5.GetString()
                    : null
        );

        return Result.Success(fileInfo);
    }


    private static DateTime ParseSendCmTime(string value)
    {
        return DateTime.ParseExact(
            value,
            SendCmFormat,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None);
    }


    private static TimeSpan CalculateServerOffset(
        string serverTime,
        DateTime receivedAtUtc)
    {
        var sendCmNow = ParseSendCmTime(serverTime);

        return sendCmNow - receivedAtUtc;
    }


    private static DateTime ConvertSendCmToUtc(
        string sendCmTimestamp,
        TimeSpan serverOffset)
    {
        var sendCmTime = ParseSendCmTime(sendCmTimestamp);

        return DateTime.SpecifyKind(
            sendCmTime - serverOffset,
            DateTimeKind.Utc);
    }
}

public static class SendCmGetFileInfoErrors
{
    public static Error InvalidUrl(string url) =>
        new Error(
            "Hoster.SendCm.GetFileInfo.InvalidUrl",
            "The provided URL is invalid.")
        .WithDetail($"'{url}' is not a valid SendCm URL.")
        .WithType(ErrorType.InvalidInput)
        .AsPermanent();


    public static Error ValidApiKeyIsRequired() =>
        new Error(
            "Hoster.SendCm.GetFileInfo.ValidApiKeyIsRequired",
            "A valid api key is required for this operation.")
        .WithType(ErrorType.Unauthorized)
        .AsPermanent();


    public static Error FileNotFound(string fileCode) =>
        new Error(
            "Hoster.SendCm.GetFileInfo.FileNotFound",
            "The specified file was not found on SendCm.")
        .WithDetail($"No file with code '{fileCode}' was found on SendCm.")
        .WithType(ErrorType.NotFound)
        .AsPermanent();


    public static Error UnknownError(int statusCode) =>
        new Error(
            "Hoster.SendCm.GetFileInfo.Unknown",
            "An unknown error occurred while retrieving file information.")
        .WithMetadata("StatusCode", statusCode)
        .WithType(ErrorType.Failure);
}
