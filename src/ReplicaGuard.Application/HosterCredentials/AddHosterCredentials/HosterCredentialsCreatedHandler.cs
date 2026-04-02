using MediatR;
using Microsoft.Extensions.Logging;
using ReplicaGuard.Core.Abstractions;
using ReplicaGuard.Core.Capabilities;
using ReplicaGuard.Core.Capabilities.Credentials;
using ReplicaGuard.Core.Domain.Credentials;
using ReplicaGuard.Core.Domain.Hoster;

namespace ReplicaGuard.Application.HosterCredentials.AddHosterCredentials;

public sealed class HosterCredentialsCreatedHandler(
    IHosterCredentialsRepository credentials,
    IHosterRepository hosters,
    IHosterClientRegistry clients,
    IUnitOfWork uow,
    ILogger<HosterCredentialsCreatedHandler> logger) : INotificationHandler<HosterCredentialsCreated>
{
    private readonly IHosterCredentialsRepository _credentials = credentials;
    private readonly IHosterRepository _hosters = hosters;
    private readonly IHosterClientRegistry _clients = clients;
    private readonly IUnitOfWork _uow = uow;
    private readonly ILogger<HosterCredentialsCreatedHandler> _logger = logger;

    public async Task Handle(HosterCredentialsCreated notification, CancellationToken cancellationToken)
    {
        var credentials = await _credentials.GetByIdAsync(notification.CredentialsId, cancellationToken);

        // GUARD: Entity deleted while pending
        if (credentials is null)
        {
            _logger.LogWarning(
                "Credentials {Id} not found (possibly deleted)",
                notification.CredentialsId);
            return;
        }

        // GUARD: Already processed (idempotency)
        if (credentials.SyncStatus == CredentialsSyncStatus.Synced)
        {
            _logger.LogDebug(
                "Credentials {Id} already synced, skipping",
                notification.CredentialsId);
            return;
        }

        // GUARD: Version mismatch (newer update superseded this event)
        if (credentials.Version != notification.Version)
        {
            _logger.LogDebug(
                "Credentials {Id} version mismatch (event: {EventVersion}, current: {CurrentVersion}). " +
                "Newer event will handle validation.",
                notification.CredentialsId,
                notification.Version,
                credentials.Version);
            return;
        }

        var hoster = await _hosters.GetByIdAsync(credentials.HosterId, cancellationToken);
        if (hoster is null)
        {
            _logger.LogError(
                "Hoster {HosterId} not found for credentials {Id}. This indicates data inconsistency.",
                credentials.HosterId,
                notification.CredentialsId);
            credentials.MarkCredentialAsFailed(notification.Version);
            await _uow.SaveChangesAsync(cancellationToken);
            return;
        }

        var credentialsValidator = _clients.TryGetHosterCapability<IValidateCredentials>(hoster.Code);

        if (credentialsValidator is null)
        {
            // No validator = trust credentials as-is (mark synced)
            _logger.LogWarning(
                "Hoster {HosterCode} does not support credential validation. Marking credentials {Id} as synced without validation.",
                hoster.Code,
                notification.CredentialsId);
            credentials.MarkCredentialAsFailed(notification.Version);
            await _uow.SaveChangesAsync(cancellationToken);
            return;
        }

        try
        {
            var validatedCredsResult = await credentialsValidator.ValidateAsync(
                new CredentialSet(
                    credentials.ApiKey,
                    credentials.Email,
                    credentials.Username,
                    credentials.Password),
                cancellationToken);

            if (validatedCredsResult.IsFailure)
            {
                _logger.LogWarning(
                    "Credentials {Id} failed validation: {Error}",
                    notification.CredentialsId,
                    validatedCredsResult.Error.Message);
                credentials.MarkCredentialAsFailed(notification.Version);
                await _uow.SaveChangesAsync(cancellationToken);
                return;
            }

            var validatedCreds = validatedCredsResult.Value;
            credentials.ApplyValidatedCredentials(
                validatedCreds.ApiKey,
                validatedCreds.Email,
                validatedCreds.Username,
                validatedCreds.Password);

            // Mark as synced (with version check)
            var result = credentials.MarkCredentialAsSynced(notification.Version);
            if (result.IsFailure)
            {
                _logger.LogInformation(
                    "Version mismatch while marking {Id} as synced - concurrent update occurred",
                    notification.CredentialsId);
                return; 

            }

            _logger.LogInformation(
                "Credentials {Id} validated successfully",
                notification.CredentialsId);
            await _uow.SaveChangesAsync(cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            // Transient failure - rethrow to let outbox retry
            _logger.LogWarning(
                ex,
                "Transient error validating credentials {Id}, will retry",
                notification.CredentialsId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating credentials {Id}", notification.CredentialsId);
            credentials.MarkCredentialAsFailed(notification.Version);
            await _uow.SaveChangesAsync(cancellationToken);
        }
    }
}
