using MassTransit;
using MediatR;
using ReplicaGuard.Application.HosterAccounts.VerifiyIdentity;
using ReplicaGuard.Contracts.IntegrationEvents;
using ReplicaGuard.Domain.Abstractions;
using ReplicaGuard.Domain.HosterAccounts;
using ReplicaGuard.Infrastructure.Persistence;

namespace ReplicaGuard.Infrastructure.Messaging.Consumers;

internal sealed class IdentityCreatedIntegrationEventConsumer :
    IConsumer<IdentityCreatedIntegrationEvent>
{
    private readonly IHosterAccountRepository _accounts;
    private readonly ISender _sender;

    public IdentityCreatedIntegrationEventConsumer(
        IHosterAccountRepository accounts,
        ISender sender)
    {
        _accounts = accounts;
        _sender = sender;
    }

    public async Task Consume(ConsumeContext<IdentityCreatedIntegrationEvent> context)
    {
        var identityId = context.Message.IdentityId;

        var account = await _accounts.GetByIdentityIdAsync(identityId);
        if (account is null)
            return; // nothing to do

        var command = new VerifyIdentityCommand(identityId);
        var result = await _sender.Send(command, context.CancellationToken);

        if (result.IsSuccess || result.Error.IsPermanent)
            return;

        // retryable
        throw new TransientException(result.Error);
    }
}

internal sealed class IdentityCreatedIntegrationEventFaultConsumer :
    IConsumer<Fault<IdentityCreatedIntegrationEvent>>
{
    private readonly IHosterAccountRepository _accounts;
    private readonly IUnitOfWork _uow;

    public IdentityCreatedIntegrationEventFaultConsumer(
        IHosterAccountRepository accounts,
        IUnitOfWork uow)
    {
        _accounts = accounts;
        _uow = uow;
    }

    public async Task Consume(ConsumeContext<Fault<IdentityCreatedIntegrationEvent>> context)
    {
        var identityId = context.Message.Message.IdentityId;

        var account = await _accounts.GetByIdentityIdAsync(identityId);
        if (account is null)
            return;

        var identity = account.Identities.SingleOrDefault(i => i.Id == identityId);
        if (identity is null)
            return;

        identity.MarkAsRejected();

        await _uow.SaveChangesAsync(context.CancellationToken);
    }
}

internal sealed class IdentityCreatedIntegrationEventConsumerDefinition
    : ConsumerDefinition<IdentityCreatedIntegrationEventConsumer>
{
    public IdentityCreatedIntegrationEventConsumerDefinition()
    {
        EndpointName = "identity-created";
        ConcurrentMessageLimit = 4;
    }

    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<IdentityCreatedIntegrationEventConsumer> consumerConfigurator,
        IRegistrationContext context)
    {
        consumerConfigurator.UseMessageRetry(r =>
        {
            r.Exponential(
                retryLimit: 5,
                minInterval: TimeSpan.FromSeconds(5),
                maxInterval: TimeSpan.FromMinutes(1),
                intervalDelta: TimeSpan.FromSeconds(10));
        });
        endpointConfigurator.UseEntityFrameworkOutbox<ApplicationDbContext>(context);
    }
}
