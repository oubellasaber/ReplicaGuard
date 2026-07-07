namespace ReplicaGuard.Application.Abstractions.Streaming;

public interface IEventStream
{
    Task PublishAsync<T>(
        string streamKey,
        T evt,
        CancellationToken ct = default);
}
