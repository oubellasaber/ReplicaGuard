using ReplicaGuard.Core.Domain.Replication.Policies;

namespace ReplicaGuard.Infrastructure.Policies;

public sealed class ExponentialJitterRetryPolicy : IRetryPolicy
{
    private const double InitialSeconds = 30d;
    private const double MaxSeconds = 600d; // 10 minutes

    private static readonly Random _rng = new();

    public TimeSpan GetDelay(int attempt)
    {
        // attempt is 1-based in domain semantics
        var a = Math.Max(1, attempt);

        // Exponential backoff
        var baseSeconds = Math.Min(
            InitialSeconds * Math.Pow(2, a - 1),
            MaxSeconds
        );

        // Jitter: 0.8x to 1.2x
        var jitter = 0.8 + (_rng.NextDouble() * 0.4);

        return TimeSpan.FromSeconds(baseSeconds * jitter);
    }
}
