using MassTransit;
using Microsoft.Extensions.Logging;
using ReplicaGuard.Core.Abstractions;
using ReplicaGuard.Core.Domain.Credentials;

namespace ReplicaGuard.Infrastructure.Messaging.Consumers;

public sealed class HosterCredentialsFaultConsumer(
    IHosterCredentialsRepository credentials,
    IUnitOfWork uow,
    ILogger<HosterCredentialsFaultConsumer> logger)
    : IConsumer<Fault<HosterCredentialsCreated>>,
      IConsumer<Fault<HosterCredentialsAreOutOfSync>>
{
    public async Task Consume(ConsumeContext<Fault<HosterCredentialsCreated>> context)
    {
        await HandleFaultAsync(
            context.Message.Message.CredentialsId,
            context.Message.Message.Version,
            context.Message.Exceptions,
            context.CancellationToken);
    }

    public async Task Consume(ConsumeContext<Fault<HosterCredentialsAreOutOfSync>> context)
    {
        await HandleFaultAsync(
            context.Message.Message.CredentialsId,
            context.Message.Message.Version,
            context.Message.Exceptions,
            context.CancellationToken);
    }

    private async Task HandleFaultAsync(
        Guid credentialsId,
        uint version,
        ExceptionInfo[] exceptions,
        CancellationToken ct)
    {
        logger.LogError(
            "Credentials {Id} permanently failed after all retries. Errors: {Errors}",
            credentialsId,
            string.Join("; ", exceptions.Select(e => e.Message)));

        var credential = await credentials.GetByIdAsync(credentialsId, ct);
        if (credential is null)
            return;

        credential.MarkCredentialAsFailed(version);
        await uow.SaveChangesAsync(ct);
    }
}
