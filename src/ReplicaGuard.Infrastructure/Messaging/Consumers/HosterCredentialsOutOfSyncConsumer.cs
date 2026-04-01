using MassTransit;
using Microsoft.Extensions.Logging;
using ReplicaGuard.Core.Abstractions;
using ReplicaGuard.Core.Capabilities;
using ReplicaGuard.Core.Capabilities.Credentials;
using ReplicaGuard.Core.Domain.Credentials;
using ReplicaGuard.Core.Domain.Hoster;

namespace ReplicaGuard.Infrastructure.Messaging.Consumers;

public sealed class HosterCredentialsOutOfSyncConsumer(
    IHosterCredentialsRepository credentials,
    IHosterRepository hosters,
    IHosterClientRegistry clients,
    IUnitOfWork uow,
    ILogger<HosterCredentialsOutOfSyncConsumer> logger) : IConsumer<HosterCredentialsAreOutOfSync>
{
    public async Task Consume(ConsumeContext<HosterCredentialsAreOutOfSync> context)
    {
        var message = context.Message;

        logger.LogInformation(
            "Re-validating credentials {Id} (v{Version})",
            message.CredentialsId, message.Version);

        var credential = await credentials.GetByIdAsync(message.CredentialsId, context.CancellationToken);

        if (credential is null || credential.Version != message.Version)
        {
            logger.LogDebug("Credentials {Id} skipped (deleted or version mismatch)", message.CredentialsId);
            return;
        }

        if (credential.SyncStatus == CredentialsSyncStatus.Synced)
        {
            logger.LogDebug("Credentials {Id} already synced", message.CredentialsId);
            return;
        }

        var hoster = await hosters.GetByIdAsync(credential.HosterId, context.CancellationToken);
        if (hoster is null)
        {
            credential.MarkCredentialAsFailed(message.Version);
            await uow.SaveChangesAsync(context.CancellationToken);
            return;
        }

        var validator = clients.TryGetHosterCapability<IValidateCredentials>(hoster.Code);
        if (validator is null)
        {
            credential.MarkCredentialAsFailed(message.Version);
            await uow.SaveChangesAsync(context.CancellationToken);
            return;
        }

        var result = await validator.ValidateAsync(
            new CredentialSet(
                credential.ApiKey,
                credential.Email,
                credential.Username,
                credential.Password),
            context.CancellationToken);

        if (result.IsFailure)
        {
            logger.LogWarning("Credentials {Id} re-validation failed", message.CredentialsId);
            credential.MarkCredentialAsFailed(message.Version);
        }
        else
        {
            var validated = result.Value;
            credential.ApplyValidatedCredentials(
                validated.ApiKey,
                validated.Email,
                validated.Username,
                validated.Password);
            credential.MarkCredentialAsSynced(message.Version);
            logger.LogInformation("Credentials {Id} re-validated successfully", message.CredentialsId);
        }

        await uow.SaveChangesAsync(context.CancellationToken);
    }
}
