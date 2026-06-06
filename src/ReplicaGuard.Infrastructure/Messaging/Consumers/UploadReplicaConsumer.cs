using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using ReplicaGuard.Infrastructure.Messaging.Commands;

namespace ReplicaGuard.Infrastructure.Messaging.Consumers;

public sealed class UploadReplicaConsumer(ISender sender, ILogger<UploadReplicaConsumer> logger) : IConsumer<UploadReplicaCommand>
{
    public async Task Consume(ConsumeContext<UploadReplicaCommand> context)
    {
        var attempt = context.GetRetryAttempt();
        var max = 3;
        var isLastRetry = attempt >= max;

        var cmd = new Application.Replication.UploadReplica.UploadReplicaCommand(
            context.Message.ReplicaId,
            context.Message.AssetId,
            context.Message.HosterId,
            isLastRetry
        );

        var result = await sender.Send(cmd);

        if (result.IsSuccess)
        {
            logger.LogInformation(
                "Replica upload succeeded. Asset={AssetId}, Replica={ReplicaId}, Hoster={HosterId}",
                cmd.AssetId, cmd.ReplicaId, cmd.HosterId);
            return;
        }

        var error = result.Error;

        logger.LogError(
            "Replica upload failed. Code={Code}, Kind={Kind}, Type={Type}, Message={Message}, Detail={Detail}, " +
            "Asset={AssetId}, Replica={ReplicaId}, Hoster={HosterId}, Metadata={Metadata}",
            error.Code,
            error.MessagingKind,
            error.Type,
            error.Message,
            error.Detail,
            cmd.AssetId,
            cmd.ReplicaId,
            cmd.HosterId,
            error.Metadata is { Count: > 0 }
                ? string.Join(", ", error.Metadata.Select(kvp => $"{kvp.Key}={kvp.Value}"))
                : "None");

        if (error.IsPermanent)
            return;

        if (error.IsTransient)
            throw new TransientException(error);
    }

}
