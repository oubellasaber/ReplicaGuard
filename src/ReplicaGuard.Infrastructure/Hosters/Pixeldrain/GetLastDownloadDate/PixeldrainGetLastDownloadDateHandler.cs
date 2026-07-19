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

        var dayResult = await FindLastActivityAsync(
            fileId,
            now.AddDays(-30),
            now,
            1440,
            ct);
        var day = dayResult.Value;

        if (day is null)
            return null;

        var dayStart = day.Value.Date;

        var hourResult = await FindLastActivityAsync(
            fileId,
            dayStart,
            dayStart.AddDays(1),
            60,
            ct);
        var hour = hourResult.Value;

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

        var minuteResult = await FindLastActivityAsync(
            fileId,
            hourStart,
            hourStart.AddHours(1),
            1,
            ct);
        var minute = minuteResult.Value;

        return minute ?? hour;
    }

    private async Task<Result<DateTime?>> FindLastActivityAsync(
        string fileId,
        DateTime start,
        DateTime end,
        int interval,
        CancellationToken ct)
    {
        var url = $"{_options.ApiBaseUrl}/api/file/{fileId}/timeseries";
        var query = $"?start={start:yyyy-MM-ddTHH:mm:ss.fffZ}&end={end:yyyy-MM-ddTHH:mm:ss.fffZ}&interval={interval}";

        using var response = await _httpClient.GetAsync(url + query, ct);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return Result.Failure<DateTime?>(
                PixeldrainGetLastDownloadDateErrors.FileNotFound(fileId));

        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(ct);
        using var json = JsonDocument.Parse(body);

        var downloads = json.RootElement.GetProperty("downloads");
        var timestamps = downloads.GetProperty("timestamps");
        var amounts = downloads.GetProperty("amounts");

        for (var i = timestamps.GetArrayLength() - 1; i >= 0; i--)
        {
            if (amounts[i].GetInt32() > 0)
                return Result.Success<DateTime?>(timestamps[i].GetDateTime());
        }

        return Result.Success<DateTime?>(null);
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
