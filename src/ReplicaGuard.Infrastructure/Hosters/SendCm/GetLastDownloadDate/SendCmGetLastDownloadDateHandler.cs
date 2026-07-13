using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ReplicaGuard.Domain.Abstractions;
using ReplicaGuard.Domain.Capabilities;
using ReplicaGuard.Domain.HosterAccounts;
using ReplicaGuard.Domain.Hosters;

namespace ReplicaGuard.Infrastructure.Hosters.SendCm.GetLastDownloadDate;

internal sealed class SendCmGetLastDownloadDateHandler : IGetLastDownloadDateCapabilityHandler
{
    private const string SendCmFormat = "yyyy-MM-dd HH:mm:ss";

    public HosterCode HosterCode => HosterCode.SendCm;
    public CapabilityCode CapabilityCode => CapabilityCode.GetLastDownloadDate;

    private readonly HttpClient _httpClient;
    private readonly ISecretEncryptionService _secretEncryptionService;
    private readonly IHosterDefinitionResolver _resolver;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SendCmOptions _sendCmOptions;

    public SendCmGetLastDownloadDateHandler(
        HttpClient httpClient,
        ISecretEncryptionService secretEncryptionService,
        IHosterDefinitionResolver resolver,
        IServiceScopeFactory serviceScopeFactory,
        IOptions<SendCmOptions> sendCmOptions)
    {
        _httpClient = httpClient;
        _secretEncryptionService = secretEncryptionService;
        _resolver = resolver;
        _scopeFactory = serviceScopeFactory;
        _sendCmOptions = sendCmOptions.Value;
    }

    public async Task<Result<GetLastDownloadDateResponse>> HandleAsync(
        GetLastDownloadDateRequest input,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input.Replica.HosterAccountId, nameof(input.Replica.HosterAccountId));
        ArgumentNullException.ThrowIfNull(input.Replica.Link, nameof(input.Replica.Link));
        using var scope = _scopeFactory.CreateScope();
        var hosterAccountRepository = scope.ServiceProvider.GetRequiredService<IHosterAccountRepository>();

        var hosterAccount = await hosterAccountRepository.GetByIdAsync(input.Replica.HosterAccountId.Value, ct);

        ArgumentNullException.ThrowIfNull(hosterAccount, nameof(input.Replica.HosterAccountId));

        var decryptedApiKeyResult = hosterAccount.GetApiKey(_secretEncryptionService);

        if (decryptedApiKeyResult.IsFailure)
            return Result.Failure<GetLastDownloadDateResponse>(
                decryptedApiKeyResult.Error);

        var decryptedApiKey = decryptedApiKeyResult.Value;

        var hoster = _resolver.Resolve(HosterCode.SendCm);

        var fileCodeResult = hoster.ExtractFileCode(input.Replica.Link);

        if (fileCodeResult.IsFailure)
            return Result.Failure<GetLastDownloadDateResponse>(
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
            return Result.Failure<GetLastDownloadDateResponse>(
                SendCmGetLastDownloadDateErrors.ValidApiKeyIsRequired());
        }

        if (status == 404)
        {
            return Result.Failure<GetLastDownloadDateResponse>(
                SendCmGetLastDownloadDateErrors.FileNotFound(fileCode));
        }

        if (status != 200)
        {
            return Result.Failure<GetLastDownloadDateResponse>(
                SendCmGetLastDownloadDateErrors.UnknownError(status));
        }

        var results = root.GetProperty("result");

        if (results.GetArrayLength() == 0)
        {
            return Result.Failure<GetLastDownloadDateResponse>(
                SendCmGetLastDownloadDateErrors.FileNotFound(fileCode));
        }

        var file = results[0];

        var fileStatus = file
            .GetProperty("file_status")
            .GetInt32();

        if (fileStatus != 200)
        {
            return Result.Failure<GetLastDownloadDateResponse>(
                SendCmGetLastDownloadDateErrors.FileNotFound(fileCode));
        }

        var serverTime = root
            .GetProperty("server_time")
            .GetString()!;

        var serverOffset = CalculateServerOffset(
            serverTime,
            receivedAtUtc);

        DateTime? lastDownloadUtc = null;

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

        return Result.Success(new GetLastDownloadDateResponse(lastDownloadUtc));
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

public static class SendCmGetLastDownloadDateErrors
{
    public static Error ValidApiKeyIsRequired() =>
        new Error(
            "Hoster.SendCm.GetLastDownloadDate.ValidApiKeyIsRequired",
            "A valid api key is required for this operation.")
        .WithType(ErrorType.Unauthorized)
        .AsPermanent();

    public static Error FileNotFound(string fileCode) =>
        new Error(
            "Hoster.SendCm.GetLastDownloadDate.FileNotFound",
            "The specified file was not found on SendCm.")
        .WithDetail($"No file with code '{fileCode}' was found on SendCm.")
        .WithType(ErrorType.NotFound)
        .AsPermanent();

    public static Error UnknownError(int statusCode) =>
        new Error(
            "Hoster.SendCm.GetLastDownloadDate.Unknown",
            "An unknown error occurred while retrieving last download date.")
        .WithMetadata("StatusCode", statusCode)
        .WithType(ErrorType.Failure);
}
