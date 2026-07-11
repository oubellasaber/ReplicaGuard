using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReplicaGuard.Application.Abstractions.Storage;
using ReplicaGuard.Application.Replication.UploadReplica.Fetching;
using ReplicaGuard.Application.Replication.UploadReplica.Spooling;
using ReplicaGuard.Domain.Abstractions;
using ReplicaGuard.Domain.Capabilities;
using ReplicaGuard.Domain.Replication;
using ReplicaGuard.Infrastructure.Storage;

namespace ReplicaGuard.Infrastructure.Hosters;

public sealed class FileFetcher : IFileFetcher
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ISpoolFileLocator _spoolFileLocator;
    private readonly FileFetcherOptions _options;
    private readonly StorageOptions _storageOptions;
    private readonly IStorageMonitor _storageMonitor;
    private readonly ILogger<FileFetcher> _logger;

    private static readonly SemaphoreSlim _concurrencySemaphore = new(3);

    public FileFetcher(
        IHttpClientFactory httpClientFactory,
        ISpoolFileLocator spoolFileLocator,
        IOptions<FileFetcherOptions> fetcherOptions,
        IOptions<StorageOptions> storageOptions,
        IStorageMonitor storageMonitor,
        ILogger<FileFetcher> logger)
    {
        _httpClientFactory = httpClientFactory;
        _spoolFileLocator = spoolFileLocator;
        _options = fetcherOptions.Value;
        _storageOptions = storageOptions.Value;
        _storageMonitor = storageMonitor;
        _logger = logger;
    }

    public async Task<Result<SpooledFile>> DownloadAsync(
        Guid assetId,
        string fileName,
        RemoteFileSource source,
        Action<TransferProgress>? onProgress = null,
        CancellationToken ct = default)
    {
        string spoolPath = _spoolFileLocator.GetSpoolPath(assetId, fileName);
        string tempPath = _spoolFileLocator.GetTempSpoolPath(assetId, fileName);

        try { if (File.Exists(tempPath)) File.Delete(tempPath); }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to delete stale temp file: {Path}", tempPath); }

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

            long? contentLength = response.Content.Headers.ContentLength;
            long estimatedSize = contentLength ?? _storageOptions.MinFreeBytes * 2;
            var diskStatus = _storageMonitor.GetStatus(_options.SpoolDirectory);

            if (diskStatus.FreeBytes < estimatedSize + _storageOptions.MinFreeBytes)
            {
                _logger.LogWarning(
                    "Insufficient disk space: need {NeedGB:N2} GB, have {HaveGB:N2} GB",
                    estimatedSize / (1024.0 * 1024 * 1024),
                    diskStatus.FreeBytes / (1024.0 * 1024 * 1024));
                return Result.Failure<SpooledFile>(
                    new Error("Storage.InsufficientSpace",
                        "Not enough disk space to download this file.",
                        ErrorType.Failure));
            }

            await _concurrencySemaphore.WaitAsync(ct);
            try
            {
                await using FileStream fs = File.Create(tempPath);
                await using var countingStream = new CountingStream(fs, _storageOptions.MaxFileSizeBytes);
                await using Stream remoteStream = await response.Content.ReadAsStreamAsync(ct);

                byte[] buffer = new byte[81920];
                int bytesRead;
                long totalRead = 0;

                while ((bytesRead = await remoteStream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
                {
                    await countingStream.WriteAsync(buffer, 0, bytesRead, ct);
                    totalRead += bytesRead;
                    onProgress?.Invoke(new TransferProgress(totalRead));
                }

                await countingStream.FlushAsync(ct);
                await countingStream.DisposeAsync();
                await fs.DisposeAsync();

                File.Move(tempPath, spoolPath, overwrite: true);

                _logger.LogInformation("Downloaded {Bytes} bytes to {Path}", totalRead, spoolPath);
                return Result.Success(new SpooledFile(spoolPath, totalRead));
            }
            finally
            {
                _concurrencySemaphore.Release();
            }
        }
        catch (FileTooLargeException ex)
        {
            _logger.LogWarning(ex, "File too large: {Url}", source.Url);
            TryDelete(tempPath);
            return Result.Failure<SpooledFile>(
                new Error("Storage.FileTooLarge",
                    $"File exceeds maximum allowed size of {_storageOptions.MaxFileSizeBytes:N0} bytes.",
                    ErrorType.Failure));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Download failed: {Url}", source.Url);
            TryDelete(tempPath);
            return Result.Failure<SpooledFile>(
                new Error("Storage.DownloadFailed",
                    $"Download failed: {ex.Message}",
                    ErrorType.Failure));
        }
    }

    private void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to delete temp file: {Path}", path); }
    }
}
