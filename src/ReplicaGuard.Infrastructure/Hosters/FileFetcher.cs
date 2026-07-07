using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReplicaGuard.Application.Replication.UploadReplica.Fetching;
using ReplicaGuard.Application.Replication.UploadReplica.Spooling;
using ReplicaGuard.Domain.Abstractions;
using ReplicaGuard.Domain.Capabilities;
using ReplicaGuard.Domain.Replication;

namespace ReplicaGuard.Infrastructure.Hosters;

public sealed class FileFetcher : IFileFetcher
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ISpoolFileLocator _spoolFileLocator;
    private readonly FileFetcherOptions _options;
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
        _options = fetcherOptions.Value;
    }

    public async Task<Result<SpooledFile>> DownloadAsync(
        Guid assetId,
        RemoteFileSource source,
        Action<TransferProgress>? onProgress = null,
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
            Directory.CreateDirectory(_options.SpoolDirectory);

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
            await using Stream remoteStream = await response.Content.ReadAsStreamAsync(ct);

            byte[] buffer = new byte[81920]; // 80 KB chunks
            int bytesRead;
            long totalRead = 0;

            while ((bytesRead = await remoteStream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
            {
                await fs.WriteAsync(buffer, 0, bytesRead, ct);
                totalRead += bytesRead;
                onProgress?.Invoke(new TransferProgress(totalRead));
            }

            _logger.LogInformation("Downloaded {Bytes} bytes to {Path}", totalRead, spoolPath);

            return Result.Success(new SpooledFile(spoolPath, totalRead));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Download failed for {Url}", source.Url);
            throw;
        }
    }
}
