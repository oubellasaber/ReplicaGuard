using System.Threading.Channels;

namespace ReplicaGuard.Application.Replication.ProgressStreaming;

public sealed record ReplicaSubscription(
    Channel<ReplicaStreamEvent> Channel)
{
    public ChannelReader<ReplicaStreamEvent> Reader
        => Channel.Reader;
}
