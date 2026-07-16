using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReplicaGuard.Application.Assets.Services;
using ReplicaGuard.Domain.Abstractions;
using ReplicaGuard.Domain.Capabilities;
using ReplicaGuard.Domain.Hosters;
using ReplicaGuard.Domain.Replication;

namespace ReplicaGuard.Infrastructure.Recovery;
internal class ExpirationRefreshWorker : BackgroundService
{
    // Our expiration refresh worker workflow is as follows:
    // 1. Query the database for all replicas that are expired or about to expire or not yet have an expiration (new added replicas). (should have a completed replication status)
    // 2. If the expiration date is not set, we will set the expiration
    // 3. If the expiration date is set, we will check if it is expired or about to expire. If it is, we get a new predicted exp date
    // 4. If the new predicted exp date is not in the danger zone, we will update the expiration date in the database. If it is in the danger zone
    // we will mark the replica as expiring soon
    // 5. if replica is already expired, we will mark it as expired and schedule a recovery job for it
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ICapabilityFactory _capabilityFactory;
    private readonly ExpirationRefreshOptions _options;
    private readonly ILogger<ExpirationRefreshWorker> _logger;

    public ExpirationRefreshWorker(
        IServiceScopeFactory scopeFactory,
        ICapabilityFactory capabilityFactory,
        IOptions<ExpirationRefreshOptions> options,
        ILogger<ExpirationRefreshWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _capabilityFactory = capabilityFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected async override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ExpirationRefreshWorker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Expiration refresh cycle failed");
            }

            await Task.Delay(TimeSpan.FromMinutes(_options.IntervalMinutes), stoppingToken);
        }
    }

    public async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();

        var repo = scope.ServiceProvider.GetRequiredService<IReplicaRepository>();
        var hosterRepo = scope.ServiceProvider.GetRequiredService<IHosterRepository>();
        var hosterDefs = scope.ServiceProvider.GetRequiredService<IHosterDefinitionResolver>();
        var expiryPrediction = scope.ServiceProvider.GetRequiredService<IReplicaExpiryPredictionService>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var now = DateTime.UtcNow;
        var scanWindow = TimeSpan.FromDays(_options.ScanWindowDays);
        var expiringSoonThreshold = TimeSpan.FromDays(_options.ExpiringSoonThresholdDays);

        var replicas = await repo.GetReplicasNearExpiryAsync(now, scanWindow, _options.BatchSize, ct);

        _logger.LogInformation("Found {Count} replicas near expiry", replicas.Count);

        foreach (var r in replicas)
        {
            try
            {
                var hoster = await hosterRepo.GetByIdAsync(r.HosterId, ct);
                if (hoster == null) continue;

                var definition = hosterDefs.Resolve(hoster.Code);

                // ToDo: use proper capability to dertmine the availability of the file.
                var getFileInfoCapability = _capabilityFactory.Get<IGetFileInfoCapabilityHandler>(hoster.Code);
                var fileInfoResult = await getFileInfoCapability.HandleAsync(new GetFileInfoRequest(r), ct);
                if (fileInfoResult.IsFailure)
                {
                    _logger.LogWarning(
                        "File info check failed for replica {ReplicaId}: {Error}",
                        r.Id, fileInfoResult.Error);
                    r.MarkAsTombstoned();
                    continue;
                }

                var expiryResult = await expiryPrediction.Predict(definition, r);

                if (expiryResult.IsFailure)
                {
                    _logger.LogWarning(
                        "Expiration check failed for replica {ReplicaId}: {Error}",
                        r.Id, expiryResult.Error);
                    continue;
                }

                var actualExpiry = expiryResult.Value;

                r.UpdateExpiry(actualExpiry, expiringSoonThreshold);

                await unitOfWork.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Expiration check failed for replica {ReplicaId}", r.Id);
            }
        }
    }
}
