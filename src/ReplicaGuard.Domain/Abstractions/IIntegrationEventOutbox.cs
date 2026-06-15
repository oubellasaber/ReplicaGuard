namespace ReplicaGuard.Core.Abstractions;

public interface IIntegrationEventOutbox
{
    Task Add<T>(T integrationEvent)
        where T : class;
}
