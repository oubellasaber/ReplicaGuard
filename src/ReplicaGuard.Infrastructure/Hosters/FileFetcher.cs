using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReplicaGuard.Core.Abstractions;
using ReplicaGuard.Core.Domain.Replication;

namespace ReplicaGuard.Infrastructure.Hosters;

public sealed class FileFetcher
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _spoolDirectory;
    private readonly ILogger<FileFetcher> _logger;

    public FileFetcher(
        IHttpClientFactory httpClientFactory,
        IOptions<SpoolOptions> spoolOptions,
        ILogger<FileFetcher> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;

        string? configuredDirectory = spoolOptions.Value?.SpoolDirectory;
        if (string.IsNullOrWhiteSpace(configuredDirectory))
        {
            configuredDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ReplicaGuard", "Spool");

            _logger.LogWarning(
                "Upload spool directory not configured. Falling back to {SpoolDirectory}",
                configuredDirectory);
        }   

        _spoolDirectory = Path.GetFullPath(configuredDirectory);
        Directory.CreateDirectory(_spoolDirectory);

        _logger.LogDebug("Using spool directory {SpoolDirectory}", _spoolDirectory);
    }

    public string GetSpoolPath(Guid assetId) =>
        Path.Combine(_spoolDirectory, assetId.ToString());

    public bool IsSpooled(Guid assetId) =>
        File.Exists(GetSpoolPath(assetId));

    public async Task<Result<FetchedFile>> DownloadAsync(
        Guid assetId,
        RemoteFileSource source,
        CancellationToken ct = default)
    {
        string spoolPath = GetSpoolPath(assetId);

        if (File.Exists(spoolPath))
        {
            long existingSize = new FileInfo(spoolPath).Length;
            _logger.LogInformation("Already spooled: {Path} ({Bytes} bytes)", spoolPath, existingSize);
            return Result.Success(new FetchedFile(spoolPath, existingSize));
        }

        try
        {
            Directory.CreateDirectory(_spoolDirectory);

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

            return Result.Success(new FetchedFile(spoolPath, sizeBytes));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Download failed for {Url}", source.Url);
            return Result.Failure<FetchedFile>(new Error(
                "Fetch.Failed", ex.Message, ErrorType.Failure));
        }
    }
}

public sealed record FetchedFile(string Path, long SizeBytes);

public sealed class SpoolOptions
{
    public required string SpoolDirectory { get; init; }
}
