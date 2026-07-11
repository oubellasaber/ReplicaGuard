using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReplicaGuard.Application.Abstractions.Storage;
using ReplicaGuard.Application.Assets.Services;
using ReplicaGuard.Application.Replication.UploadReplica.Fetching;
using ReplicaGuard.Infrastructure.Storage;

namespace ReplicaGuard.Infrastructure.Cleanup;

internal sealed class AssetCleanupBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IStorageMonitor _storageMonitor;
    private readonly FileFetcherOptions _fetcherOptions;
    private readonly IOptions<StorageOptions> _storageOptions;
    private readonly ILogger<AssetCleanupBackgroundService> _logger;

    public AssetCleanupBackgroundService(
        IServiceScopeFactory scopeFactory,
        IStorageMonitor storageMonitor,
        IOptions<FileFetcherOptions> fetcherOptions,
        IOptions<StorageOptions> storageOptions,
        ILogger<AssetCleanupBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _storageMonitor = storageMonitor;
        _fetcherOptions = fetcherOptions.Value;
        _storageOptions = storageOptions;
        _logger = logger;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Running startup cleanup...");

        using var scope = _scopeFactory.CreateScope();
        var cleanup = scope.ServiceProvider.GetRequiredService<IAssetCleanupService>();
        var cleaned = await cleanup.CleanupOrphanedTempFilesAsync(cancellationToken);
        _logger.LogInformation("Startup temp cleanup: {Count} files removed", cleaned);

        LogStorageSummary();

        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var interval = TimeSpan.FromMinutes(_storageOptions.Value.CleanupIntervalMinutes);
            await Task.Delay(interval, stoppingToken);

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var cleanup = scope.ServiceProvider.GetRequiredService<IAssetCleanupService>();
                await cleanup.CleanupExpiredAssetsAsync(stoppingToken);
                LogStorageSummary();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cleanup cycle failed");
            }
        }
    }

    private void LogStorageSummary()
    {
        var dir = _fetcherOptions.SpoolDirectory;
        if (string.IsNullOrEmpty(dir)) return;

        var status = _storageMonitor.GetStatus(dir);
        _logger.LogInformation(
            "Storage: {FreeGB:N2} GB free of {TotalGB:N2} GB ({Level})",
            status.FreeBytes / (1024.0 * 1024 * 1024),
            status.TotalBytes / (1024.0 * 1024 * 1024),
            status.Level);
    }
}
