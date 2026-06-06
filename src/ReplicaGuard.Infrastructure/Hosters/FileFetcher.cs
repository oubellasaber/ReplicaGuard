using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReplicaGuard.Application.Replication.UploadReplica.Fetching;
using ReplicaGuard.Application.Replication.UploadReplica.Spooling;
using ReplicaGuard.Core.Abstractions;
using ReplicaGuard.Core.Domain.Replication;

namespace ReplicaGuard.Infrastructure.Hosters;

public sealed class FileFetcher : IFileFetcher
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ISpoolFileLocator _spoolFileLocator;
    private readonly string _spoolingDirectory;
    private readonly ILogger<FileFetcher> _logger;

    public FileFetcher(
        IHttpClientFactory httpClientFactory,
        ISpoolFileLocator spoolFileLocator,
        IOptions<FileFetcherOptions> fetcherOptions,
        ILogger<FileFetcher> logger)
    {
        _httpClientFactory = httpClientFactory;
        _spoolFileLocator = spoolFileLocator;
        _logger = logger;
        _spoolingDirectory = fetcherOptions.Value.SpoolDirectory;
    }

    public async Task<Result<SpooledFile>> DownloadAsync(
        Guid assetId,
        RemoteFileSource source,
        CancellationToken ct = default)
    {
        string spoolPath = _spoolFileLocator.GetSpoolPath(assetId);

        if (File.Exists(spoolPath))
        {
            long existingSize = new FileInfo(spoolPath).Length;
            _logger.LogInformation("Already spooled: {Path} ({Bytes} bytes)", spoolPath, existingSize);
            return Result.Success(new SpooledFile(spoolPath, existingSize));
        }

        try
        {
            Directory.CreateDirectory(_spoolingDirectory);

            _logger.LogInformation("Downloading {Url}", source.Url);

            using HttpClient client = _httpClientFactory.CreateClient("FileUploadingHttpClient");

            foreach (KeyValuePair<string, string> header in source.Headers)
                client.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);

            using HttpResponseMessage response = await client.GetAsync(
                source.Url.Value,
                HttpCompletionOption.ResponseHeadersRead,
                ct);

            response.EnsureSuccessStatusCode();

            await using FileStream fs = File.Create(spoolPath);
            await response.Content.CopyToAsync(fs, ct);

            long sizeBytes = fs.Length;

            _logger.LogInformation("Downloaded {Bytes} bytes to {Path}", sizeBytes, spoolPath);

            return Result.Success(new SpooledFile(spoolPath, sizeBytes));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Download failed for {Url}", source.Url);
            throw;
        }
    }
}


