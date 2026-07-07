using System.Threading.Channels;
using ReplicaGuard.Application.Replication.ProgressStreaming;

namespace ReplicaGuard.Infrastructure.Streaming;

internal sealed class AssetStream
{
    private const int MaxHistory = 200;

    private long _sequence;

    //
    // 0 = active
    // 1 = completed
    //
    private int _completed;

    // Protected by lock(Subscribers)
    public HashSet<Channel<ReplicaStreamEvent>> Subscribers { get; }
        = [];

    // Protected by lock(History)
    public List<ReplicaStreamEvent> History { get; }
        = [];

    public bool IsCompleted =>
        Volatile.Read(ref _completed) == 1;

    public long NextSequenceNumber()
    {
        return Interlocked.Increment(ref _sequence);
    }

    public void AddToHistory(ReplicaStreamEvent evt)
    {
        if (IsCompleted)
        {
            return;
        }

        lock (History)
        {
            if (IsCompleted)
            {
                return;
            }

            History.Add(evt);

            if (History.Count > MaxHistory)
            {
                History.RemoveAt(0);
            }
        }
    }

    public IReadOnlyList<ReplicaStreamEvent> Replay(
        long lastEventId)
    {
        lock (History)
        {
            return History
                .Where(x =>
                    x.SequenceNumber > lastEventId)
                .ToList();
        }
    }

    public void Complete()
    {
        //
        // Only one thread wins.
        //
        if (Interlocked.Exchange(
                ref _completed,
                1) == 1)
        {
            return;
        }

        lock (Subscribers)
        {
            foreach (var subscriber in Subscribers)
            {
                subscriber.Writer.TryComplete();
            }

            Subscribers.Clear();
        }
    }
}
