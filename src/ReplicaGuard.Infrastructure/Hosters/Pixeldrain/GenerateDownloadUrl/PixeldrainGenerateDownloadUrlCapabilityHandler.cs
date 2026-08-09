using Microsoft.Extensions.Options;
using ReplicaGuard.Domain.Abstractions;
using ReplicaGuard.Domain.Capabilities;
using ReplicaGuard.Domain.Hosters;

namespace ReplicaGuard.Infrastructure.Hosters.Pixeldrain.GenerateDownloadUrl;

internal sealed class PixeldrainGenerateDownloadUrlCapabilityHandler : IGenerateDownloadUrlCapabilityHandler
{
    private readonly HttpClient _httpClient;
    private readonly IHosterDefinitionResolver _resolver;
    private readonly PixeldrainOptions _options;

    public HosterCode HosterCode => HosterCode.Pixeldrain;
    public CapabilityCode CapabilityCode => CapabilityCode.GenerateDownloadUrl;

    public PixeldrainGenerateDownloadUrlCapabilityHandler(
        IHttpClientFactory httpClientFactory,
        IHosterDefinitionResolver resolver,
        IOptions<PixeldrainOptions> options)
    {
        _httpClient = httpClientFactory.CreateClient(HosterCode.Pixeldrain.ToFriendlyString());
        _resolver = resolver;
        _options = options.Value;
    }

    public async Task<Result<DownloadFileResponse>> HandleAsync(DownloadFileRequest input, CancellationToken ct = default)
    {
        var hoster = _resolver.Resolve(HosterCode.Pixeldrain);
        var fileCodeResult = hoster.ExtractFileCode(input.Url);

        if (fileCodeResult.IsFailure)
            return Result.Failure<DownloadFileResponse>(fileCodeResult.Error);

        var apiUrl = new Uri($"{_options.ApiBaseUrl.TrimEnd('/')}/api/file/{fileCodeResult.Value}");

        try
        {
            using var response = await _httpClient.GetAsync(apiUrl, ct);

            var headers = new Dictionary<string, string>();

            if (response.StatusCode == System.Net.HttpStatusCode.Redirect)
            {
                var directUrl = response.Headers.Location?.ToString();
                if (string.IsNullOrEmpty(directUrl))
                    return Result.Failure<DownloadFileResponse>(
                        new Error("Pixeldrain.MissingLocation", "Received redirect but Location header was empty."));

                return Result.Success(new DownloadFileResponse(new Uri(directUrl), headers));
            }

            if (response.IsSuccessStatusCode)
                return Result.Success(new DownloadFileResponse(apiUrl, headers));

            return Result.Failure<DownloadFileResponse>(
                new Error("Pixeldrain.HttpError", $"Unexpected status code: {response.StatusCode}"));
        }
        catch (Exception ex)
        {
            return Result.Failure<DownloadFileResponse>(
                new Error("Pixeldrain.Exception", ex.Message));
        }
    }
}
