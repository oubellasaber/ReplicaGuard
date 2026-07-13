using System.Text.Json;
using MassTransit.Configuration;
using Microsoft.Extensions.Options;
using ReplicaGuard.Domain.Abstractions;
using ReplicaGuard.Domain.Capabilities;
using ReplicaGuard.Domain.Hosters;

namespace ReplicaGuard.Infrastructure.Hosters.Pixeldrain.GetFileInfo;

internal sealed class PixeldrainGetFileInfoHandler : IGetFileInfoCapabilityHandler
{
    public HosterCode HosterCode => HosterCode.Pixeldrain;
    public CapabilityCode CapabilityCode => CapabilityCode.GetFileInfo;

    private readonly HttpClient _httpClient;
    private readonly IHosterDefinitionResolver _resolver;
    private readonly PixeldrainOptions _options;

    public PixeldrainGetFileInfoHandler(
        HttpClient httpClient,
        IHosterDefinitionResolver resolver,
        IOptions<PixeldrainOptions> options)
    {
        _httpClient = httpClient;
        _resolver = resolver;
        _options = options.Value;
    }


    public async Task<Result<GetFileInfoResponse>> HandleAsync(
        GetFileInfoRequest input,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input.Replica.Link);

        var hoster = _resolver.Resolve(HosterCode.Pixeldrain);

        var fileIdResult = hoster.ExtractFileCode(input.Replica.Link);

        if (fileIdResult.IsFailure)
            return Result.Failure<GetFileInfoResponse>(
                fileIdResult.Error);


        var fileId = fileIdResult.Value;


        // Only query timeseries when requested
        var infoTask = GetFileInfoAsync(fileId, ct);

        Task<DateTime?> lastDownloadTask =
            input.IncludeLastDownloadDate
                ? FindLastDownloadAsync(fileId, ct)
                : Task.FromResult<DateTime?>(null);


        // Run both in parallel
        await Task.WhenAll(
            infoTask,
            lastDownloadTask);


        var infoResult = await infoTask;

        if (infoResult.IsFailure)
            return infoResult;


        var info = infoResult.Value;

        var lastDownload = await lastDownloadTask;


        return Result.Success(
            info with
            {
                LastDownloadDateFromHoster = lastDownload
            });
    }



    private async Task<Result<GetFileInfoResponse>> GetFileInfoAsync(
        string fileId,
        CancellationToken ct)
    {
        var url =
            $"{_options.ApiBaseUrl}/api/file/{fileId}/info";


        using var response =
            await _httpClient.GetAsync(url, ct);


        var body =
            await response.Content.ReadAsStringAsync(ct);


        using var json =
            JsonDocument.Parse(body);


        var root = json.RootElement;


        if (!root.GetProperty("success").GetBoolean())
        {
            return Result.Failure<GetFileInfoResponse>(
                PixeldrainGetFileInfoErrors.FileNotFound(fileId));
        }


        return Result.Success(
            new GetFileInfoResponse
            (
                Id: root.GetProperty("id").GetString()!,

                Url:
                    $"https://pixeldrain.com/u/{fileId}",

                Name:
                    root.GetProperty("name").GetString()!,

                TotalBytes:
                    root.GetProperty("size").GetInt64(),

                UploadedToHosterAt:
                    root.GetProperty("date_upload")
                        .GetDateTime(),

                LastDownloadDateFromHoster:
                    DateTime.MinValue,

                Sha256Hash:
                    root.TryGetProperty(
                        "hash_sha256",
                        out var sha)
                        ? sha.GetString()
                        : null,

                Md5Hash:
                    null
            ));
    }

    private async Task<DateTime?> FindLastDownloadAsync(
        string fileId,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        // Search last 30 days by day
        var day = await FindLastActivityAsync(
            fileId,
            now.AddDays(-30),
            now,
            1440,
            ct);


        if (day is null)
            return null;


        // Search that day by hour
        var dayStart = day.Value.Date;

        var hour = await FindLastActivityAsync(
            fileId,
            dayStart,
            dayStart.AddDays(1),
            60,
            ct);


        if (hour is null)
            return day;


        // Search that hour by minute
        var hourStart = new DateTime(
            hour.Value.Year,
            hour.Value.Month,
            hour.Value.Day,
            hour.Value.Hour,
            0,
            0,
            DateTimeKind.Utc);


        var minute = await FindLastActivityAsync(
            fileId,
            hourStart,
            hourStart.AddHours(1),
            1,
            ct);


        return minute ?? hour;
    }


    private async Task<DateTime?> FindLastActivityAsync(
        string fileId,
        DateTime start,
        DateTime end,
        int interval,
        CancellationToken ct)
    {
        var url =
            $"{_options.ApiBaseUrl}/api/file/{fileId}/timeseries";


        var query =
            $"?start={start:yyyy-MM-ddTHH:mm:ss.fffZ}" +
            $"&end={end:yyyy-MM-ddTHH:mm:ss.fffZ}" +
            $"&interval={interval}";


        using var response = await _httpClient.GetAsync(
            url + query,
            ct);


        var body = await response.Content.ReadAsStringAsync(ct);


        using var json = JsonDocument.Parse(body);


        var downloads =
            json.RootElement.GetProperty("downloads");


        var timestamps =
            downloads.GetProperty("timestamps");


        var amounts =
            downloads.GetProperty("amounts");


        for (var i = timestamps.GetArrayLength() - 1; i >= 0; i--)
        {
            if (amounts[i].GetInt32() > 0)
            {
                return timestamps[i]
                    .GetDateTime();
            }
        }


        return null;
    }
}

public static class PixeldrainGetFileInfoErrors
{
    public static Error FileNotFound(string fileId) =>
        new Error(
            "Hoster.Pixeldrain.GetFileInfo.FileNotFound",
            "The specified file was not found on Pixeldrain.")
        .WithDetail($"No file with id '{fileId}' was found.")
        .WithType(ErrorType.NotFound)
        .AsPermanent();


    public static Error UnknownError(int statusCode) =>
        new Error(
            "Hoster.Pixeldrain.GetFileInfo.Unknown",
            "An unknown error occurred while retrieving file information.")
        .WithMetadata("StatusCode", statusCode)
        .WithType(ErrorType.Failure);
}
