using ReplicaGuard.Domain.Abstractions;
using ReplicaGuard.Domain.Capabilities;
using ReplicaGuard.Domain.Hosters;
using ReplicaGuard.Infrastructure.Captcha;

namespace ReplicaGuard.Infrastructure.Hosters.SendCm.GenerateDownloadUrl;

internal sealed class GenerateDownloadUrlHandler : IGenerateDownloadUrlCapabilityHandler
{
    private readonly HttpClient _httpClient;
    private readonly ScraperHttpClient _scraperClient;
    private readonly IHosterDefinitionResolver _resolver;

    public GenerateDownloadUrlHandler(
        HttpClient httpClient,
        ScraperHttpClient scraperClient,
        IHosterDefinitionResolver resolver)
    {
        _httpClient = httpClient;
        _scraperClient = scraperClient;
        _resolver = resolver;
    }

    public HosterCode HosterCode => HosterCode.SendCm;
    public CapabilityCode CapabilityCode => CapabilityCode.GenerateDownloadUrl;

    public async Task<Result<DownloadFileResponse>> HandleAsync(DownloadFileRequest input, CancellationToken ct = default)
    {
        var fileCodeResult = _resolver
            .Resolve(HosterCode)
            .ExtractFileCode(input.Url);

        if (fileCodeResult.IsFailure)
        {
            return Result.Failure<DownloadFileResponse>(fileCodeResult.Error);
        }

        try
        {
            using var response = await _scraperClient.SendAsync(() =>
            {
                var request = new HttpRequestMessage(HttpMethod.Post, input.Url);

                var formData = new Dictionary<string, string>
                {
                    { "op", "download2" },
                    { "id",  fileCodeResult.Value},
                    { "download_a", "CONTINUE" }
                };

                request.Content = new FormUrlEncodedContent(formData);

                return request;
            },
            RunParallelWarmupRequests,
            ct);

            var headers = new Dictionary<string, string>()
            {
                ["Referer"] = "https://send.now/"
            };

            if (response.StatusCode == System.Net.HttpStatusCode.Redirect)
            {
                var directUrl = response.Headers.Location?.ToString();

                if (string.IsNullOrEmpty(directUrl))
                {
                    return Result.Failure<DownloadFileResponse>(
                        new Error("SendCm.MissingLocation", "Received redirect but Location header was empty."));
                }

                return Result.Success(new DownloadFileResponse(new Uri(directUrl), headers));
            }

            return Result.Failure<DownloadFileResponse>(
                new Error("SendCm.HttpError", $"Unexpected status code: {response.StatusCode}"));
        }
        catch (Exception ex)
        {
            return Result.Failure<DownloadFileResponse>(
                new Error("SendCm.Exception", ex.Message));
        }
    }

    private async Task RunParallelWarmupRequests()
    {
        var loginUrl = "https://send.now/login";

        var tasks = Enumerable.Range(1, 6).Select(async i =>
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, loginUrl);
                using var res = await _httpClient.SendAsync(req);
            }
            catch { }
        });

        await Task.WhenAll(tasks);
    }
}
