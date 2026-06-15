using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using ReplicaGuard.Core.Abstractions;
using ReplicaGuard.Core.Replication;
using ReplicaGuard.Infrastructure.Messaging.Commands;

namespace ReplicaGuard.Infrastructure.Messaging.Consumers;

public sealed class UploadReplicaConsumer(ISender sender, ILogger<UploadReplicaConsumer> logger) : IConsumer<UploadReplicaCommand>
{
    public async Task Consume(ConsumeContext<UploadReplicaCommand> context)
    {
        var cmd = new Application.Replication.UploadReplica.UploadReplicaCommand(
            context.Message.UserId,
            context.Message.AssetId,
            context.Message.ReplicaId
        );

        var result = await sender.Send(cmd);

        if (result.IsSuccess)
        {
            logger.LogInformation(
                "Replica upload succeeded. User={UserId}, Asset={AssetId}, Replica={ReplicaId}",
                cmd.UserId, cmd.AssetId, cmd.ReplicaId);
            return;
        }

        var error = result.Error;

        logger.LogError(
            "Replica upload failed. User={UserId}, Asset={AssetId}, Replica={ReplicaId}, HosterCode={HosterCode}, Kind={Kind}, Type={Type}, Message={Message}, Detail={Detail}, Metadata={Metadata}",
            cmd.UserId,
            cmd.AssetId,
            cmd.ReplicaId,
            error.Code,
            error.MessagingKind,
            error.Type,
            error.Message,
            error.Detail,
            error.Metadata is { Count: > 0 }
                ? string.Join(", ", error.Metadata.Select(kvp => $"{kvp.Key}={kvp.Value}"))
                : "None");

        if (error.IsPermanent)
            return;

        throw new TransientException(error);
    }
}

public sealed class UploadReplicaFaultConsumer(IReplicaRepository assets, IUnitOfWork uow, ILogger<UploadReplicaFaultConsumer> logger) : IConsumer<Fault<UploadReplicaCommand>>
{
    public async Task Consume(ConsumeContext<Fault<UploadReplicaCommand>> context)
    {
        var fault = context.Message;

        var replica = await assets.GetByIdAsync(fault.Message.ReplicaId, context.CancellationToken);
        replica?.MarkAsFailed();
        await uow.SaveChangesAsync(context.CancellationToken);

        logger.LogError(
            "UploadReplicaCommand failed. User={UserId}, Asset={AssetId}, Replica={ReplicaId}, FaultMessage={FaultMessage}",
            fault.Message.UserId,
            fault.Message.AssetId,
            fault.Message.ReplicaId,
            fault.Exceptions.FirstOrDefault()?.Message ?? "No exception message");

        return;
    }
}

public sealed class UploadReplicaConsumerDefinition : ConsumerDefinition<UploadReplicaConsumer>
{
    public UploadReplicaConsumerDefinition()
    {
        EndpointName = "upload-replica";
        ConcurrentMessageLimit = 5;
    }

    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<UploadReplicaConsumer> consumerConfigurator,
        IRegistrationContext context)
    {
        endpointConfigurator.UseMessageRetry(r => r.Exponential(
            retryLimit: 3,
            minInterval: TimeSpan.FromSeconds(5),
            maxInterval: TimeSpan.FromMinutes(2),
            intervalDelta: TimeSpan.FromSeconds(10)));

        endpointConfigurator.UseTimeout(t => t.Timeout = TimeSpan.FromMinutes(60));
    }
}
