using System.Threading.Channels;

namespace ReplicaGuard.Application.Replication.ProgressStreaming;

using ReplicaGuard.Application.Replication.ProgressStreaming;

/// <summary>
/// Handles real-time streaming of replica events per asset.
/// Supports:
/// - Pub/Sub (live updates)
/// - Replay (Last-Event-ID)
/// - Completion signalling
/// - Subscription lifecycle management
/// </summary>
public interface IReplicaEventStream
{
    /// <summary>
    /// Publishes a new replica event to all subscribers
    /// and stores it for replay.
    /// </summary>
    void Publish(
        Guid userId,
        Guid assetId,
        ReplicaStreamEvent evt);

    /// <summary>
    /// Subscribes a client to live events for an asset.
    /// Returns a subscription handle that must be disposed/unsubscribed.
    /// </summary>
    ReplicaSubscription Subscribe(
        Guid userId,
        Guid assetId);

    /// <summary>
    /// Replays missed events after a given sequence number.
    /// Used for SSE reconnection via Last-Event-ID.
    /// </summary>
    IReadOnlyList<ReplicaStreamEvent> Replay(
        Guid userId,
        Guid assetId,
        long lastEventId);

    /// <summary>
    /// Removes a subscriber and closes its channel.
    /// Should be called when SSE connection ends.
    /// </summary>
    void Unsubscribe(
        Guid userId,
        Guid assetId,
        ReplicaSubscription subscription);

    /// <summary>
    /// Marks the asset stream as completed and closes all
    /// active subscriptions.
    ///
    /// The stream is retained temporarily for replay support.
    /// </summary>
    void CompleteAsset(
        Guid userId,
        Guid assetId);
}
