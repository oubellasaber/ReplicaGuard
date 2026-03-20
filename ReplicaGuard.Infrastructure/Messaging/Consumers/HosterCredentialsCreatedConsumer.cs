using MassTransit;
using Microsoft.Extensions.Logging;
using ReplicaGuard.Core.Abstractions;
using ReplicaGuard.Core.Capabilities;
using ReplicaGuard.Core.Capabilities.Credentials;
using ReplicaGuard.Core.Domain.Credentials;
using ReplicaGuard.Core.Domain.Hoster;

namespace ReplicaGuard.Infrastructure.Messaging.Consumers;

public sealed class HosterCredentialsCreatedConsumer(
    IHosterCredentialsRepository credentials,
    IHosterRepository hosters,
    IHosterClientRegistry clients,
    IUnitOfWork uow,
    ILogger<HosterCredentialsCreatedConsumer> logger) : IConsumer<HosterCredentialsCreated>
{
    public async Task Consume(ConsumeContext<HosterCredentialsCreated> context)
    {
        var message = context.Message;

        logger.LogInformation(
            "Processing HosterCredentialsCreated {Id} (v{Version})",
            message.CredentialsId,
            message.Version);

        var credential = await credentials.GetByIdAsync(message.CredentialsId, context.CancellationToken);

        if (credential is null)
        {
            logger.LogWarning("Credentials {Id} not found", message.CredentialsId);
            return;
        }

        if (credential.SyncStatus == CredentialsSyncStatus.Synced)
        {
            logger.LogDebug("Credentials {Id} already synced", message.CredentialsId);
            return;
        }

        if (credential.Version != message.Version)
        {
            logger.LogDebug(
                "Credentials {Id} version mismatch (event={EventV}, current={CurrentV})",
                message.CredentialsId, message.Version, credential.Version);
            return;
        }

        var hoster = await hosters.GetByIdAsync(credential.HosterId, context.CancellationToken);
        if (hoster is null)
        {
            logger.LogError("Hoster {HosterId} not found", credential.HosterId);
            credential.MarkCredentialAsFailed(message.Version);
            await uow.SaveChangesAsync(context.CancellationToken);
            return;
        }

        var validator = clients.TryGetHosterCapability<IValidateCredentials>(hoster.Code);

        if (validator is null)
        {
            logger.LogWarning("Hoster {Code} has no validator, marking failed", hoster.Code);
            credential.MarkCredentialAsFailed(message.Version);
            await uow.SaveChangesAsync(context.CancellationToken);
            return;
        }

        var validationResult = await validator.ValidateAsync(
            new CredentialSet(
                credential.ApiKey,
                credential.Email,
                credential.Username,
                credential.Password),
            context.CancellationToken);

        if (validationResult.IsFailure)
        {
            logger.LogWarning(
                "Credentials {Id} validation failed: {Error}",
                message.CredentialsId, validationResult.Error.Message);
            credential.MarkCredentialAsFailed(message.Version);
            await uow.SaveChangesAsync(context.CancellationToken);
            return;
        }

        var validated = validationResult.Value;
        credential.ApplyValidatedCredentials(
            validated.ApiKey,
            validated.Email,
            validated.Username,
            validated.Password);

        var syncResult = credential.MarkCredentialAsSynced(message.Version);
        if (syncResult.IsFailure)
        {
            logger.LogInformation("Version changed while processing {Id}", message.CredentialsId);
            return;
        }

        await uow.SaveChangesAsync(context.CancellationToken);
        logger.LogInformation("Credentials {Id} validated successfully", message.CredentialsId);
    }
}
