using System.Collections.Concurrent;
using System.Threading.Channels;
using ReplicaGuard.Application.Replication.ProgressStreaming;

namespace ReplicaGuard.Infrastructure.Streaming;

public sealed class SseReplicaEventStream
    : IReplicaEventStream
{
    private static readonly TimeSpan Retention =
        TimeSpan.FromMinutes(5);

    private readonly ConcurrentDictionary<
        (Guid UserId, Guid AssetId),
        AssetStream> _streams = new();

    public void Publish(
        Guid userId,
        Guid assetId,
        ReplicaStreamEvent evt)
    {
        var key = (userId, assetId);

        var stream = _streams.GetOrAdd(
            key,
            _ => new AssetStream());

        //
        // Assign sequence number for replay support.
        //
        var eventWithId =
            evt with
            {
                SequenceNumber =
                    stream.NextSequenceNumber()
            };

        //
        // Store event for future reconnects.
        //
        stream.AddToHistory(eventWithId);

        Channel<ReplicaStreamEvent>[] snapshot;

        //
        // Snapshot subscribers so we don't hold the lock
        // while writing.
        //
        lock (stream.Subscribers)
        {
            snapshot =
                stream.Subscribers.ToArray();
        }

        List<Channel<ReplicaStreamEvent>>?
            deadChannels = null;

        foreach (var subscriber in snapshot)
        {
            //
            // Can fail if the channel has already been completed.
            //
            if (!subscriber.Writer.TryWrite(eventWithId))
            {
                deadChannels ??= [];
                deadChannels.Add(subscriber);
            }
        }

        if (deadChannels is null)
        {
            return;
        }

        //
        // Cleanup dead subscribers.
        //
        lock (stream.Subscribers)
        {
            foreach (var dead in deadChannels)
            {
                stream.Subscribers.Remove(dead);
            }
        }
    }

    public ReplicaSubscription Subscribe(
        Guid userId,
        Guid assetId)
    {
        var key = (userId, assetId);

        var stream = _streams.GetOrAdd(
            key,
            _ => new AssetStream());

        //
        // Bounded channels prevent slow clients from
        // consuming unbounded memory.
        //
        var channel =
            Channel.CreateBounded<ReplicaStreamEvent>(
                new BoundedChannelOptions(100)
                {
                    SingleReader = true,
                    SingleWriter = false,
                    FullMode =
                        BoundedChannelFullMode.DropOldest
                });

        lock (stream.Subscribers)
        {
            //
            // If the asset already finished, immediately
            // complete the channel so the controller exits.
            //
            if (stream.IsCompleted)
            {
                channel.Writer.TryComplete();
            }
            else
            {
                stream.Subscribers.Add(channel);
            }
        }

        return new ReplicaSubscription(channel);
    }

    public IReadOnlyList<ReplicaStreamEvent> Replay(
        Guid userId,
        Guid assetId,
        long lastEventId)
    {
        if (!_streams.TryGetValue(
                (userId, assetId),
                out var stream))
        {
            return [];
        }

        return stream.Replay(lastEventId);
    }

    public void Unsubscribe(
        Guid userId,
        Guid assetId,
        ReplicaSubscription subscription)
    {
        var key = (userId, assetId);

        if (!_streams.TryGetValue(
                key,
                out var stream))
        {
            return;
        }

        lock (stream.Subscribers)
        {
            if (stream.Subscribers.Remove(
                    subscription.Channel))
            {
                subscription.Channel
                    .Writer
                    .TryComplete();
            }

            //
            // Remove immediately only if:
            // - stream is not completed
            // - nobody is listening
            //
            // Completed streams stay around for replay retention.
            //
            if (!stream.IsCompleted &&
                stream.Subscribers.Count == 0)
            {
                _streams.TryRemove(
                    new KeyValuePair<
                        (Guid UserId, Guid AssetId),
                        AssetStream>(
                        key,
                        stream));
            }
        }
    }

    public void CompleteAsset(
        Guid userId,
        Guid assetId)
    {
        var key = (userId, assetId);

        if (!_streams.TryGetValue(
                key,
                out var stream))
        {
            return;
        }

        //
        // Complete all subscribers.
        //
        stream.Complete();

        //
        // Keep replay history around for reconnects.
        //
        _ = Task.Run(async () =>
        {
            await Task.Delay(Retention);

            _streams.TryRemove(
                new KeyValuePair<
                    (Guid UserId, Guid AssetId),
                    AssetStream>(
                    key,
                    stream));
        });
    }
}
