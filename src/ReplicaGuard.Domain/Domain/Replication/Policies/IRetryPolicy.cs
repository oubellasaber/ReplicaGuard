namespace ReplicaGuard.Core.Domain.Replication.Policies;

public interface IRetryPolicy
{
    TimeSpan GetDelay(int attempt);
}
