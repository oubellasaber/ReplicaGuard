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
        HttpClient httpClient,
        IHosterDefinitionResolver resolver,
        IOptions<PixeldrainOptions> options)
    {
        _httpClient = httpClient;
        _resolver = resolver;
        _options = options.Value;
    }

    public Task<Result<DownloadFileResponse>> HandleAsync(DownloadFileRequest input, CancellationToken ct = default)
    {
        var hoster = _resolver.Resolve(HosterCode.Pixeldrain);

        var fileCodeResult = hoster.ExtractFileCode(input.Url);

        if (fileCodeResult.IsFailure)
            return Task.FromResult(Result.Failure<DownloadFileResponse>(fileCodeResult.Error));

        var directUrl = new Uri($"{_options.ApiBaseUrl.TrimEnd('/')}/api/file/{fileCodeResult.Value}");

        var headers = new Dictionary<string, string>();
        return Task.FromResult(Result.Success(new DownloadFileResponse(directUrl, headers)));
    }
}
