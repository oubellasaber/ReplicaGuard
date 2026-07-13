using System.Text.Json;
using Microsoft.Extensions.Options;
using ReplicaGuard.Domain.Abstractions;
using ReplicaGuard.Domain.Capabilities;
using ReplicaGuard.Domain.Hosters;

namespace ReplicaGuard.Infrastructure.Hosters.Pixeldrain.GetLastDownloadDate;

internal sealed class PixeldrainGetLastDownloadDateHandler : IGetLastDownloadDateCapabilityHandler
{
    public HosterCode HosterCode => HosterCode.Pixeldrain;
    public CapabilityCode CapabilityCode => CapabilityCode.GetLastDownloadDate;

    private readonly HttpClient _httpClient;
    private readonly IHosterDefinitionResolver _resolver;
    private readonly PixeldrainOptions _options;

    public PixeldrainGetLastDownloadDateHandler(
        HttpClient httpClient,
        IHosterDefinitionResolver resolver,
        IOptions<PixeldrainOptions> options)
    {
        _httpClient = httpClient;
        _resolver = resolver;
        _options = options.Value;
    }

    public async Task<Result<GetLastDownloadDateResponse>> HandleAsync(
        GetLastDownloadDateRequest input,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input.Replica.Link);

        var hoster = _resolver.Resolve(HosterCode.Pixeldrain);

        var fileIdResult = hoster.ExtractFileCode(input.Replica.Link);

        if (fileIdResult.IsFailure)
            return Result.Failure<GetLastDownloadDateResponse>(
                fileIdResult.Error);

        var lastDownload = await FindLastDownloadAsync(fileIdResult.Value, ct);

        return Result.Success(new GetLastDownloadDateResponse(lastDownload));
    }

    private async Task<DateTime?> FindLastDownloadAsync(
        string fileId,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        var day = await FindLastActivityAsync(
            fileId,
            now.AddDays(-30),
            now,
            1440,
            ct);

        if (day is null)
            return null;

        var dayStart = day.Value.Date;

        var hour = await FindLastActivityAsync(
            fileId,
            dayStart,
            dayStart.AddDays(1),
            60,
            ct);

        if (hour is null)
            return day;

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

public static class PixeldrainGetLastDownloadDateErrors
{
    public static Error FileNotFound(string fileId) =>
        new Error(
            "Hoster.Pixeldrain.GetLastDownloadDate.FileNotFound",
            "The specified file was not found on Pixeldrain.")
        .WithDetail($"No file with id '{fileId}' was found.")
        .WithType(ErrorType.NotFound)
        .AsPermanent();
}
