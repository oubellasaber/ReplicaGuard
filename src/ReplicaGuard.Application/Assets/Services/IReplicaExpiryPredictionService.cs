using ReplicaGuard.Domain.Abstractions;
using ReplicaGuard.Domain.Hosters;
using ReplicaGuard.Domain.Replication;

namespace ReplicaGuard.Application.Assets.Services;

public interface IReplicaExpiryPredictionService
{
    Task<Result<DateTime>> Predict(IHosterDefinition hoster, Replica replica);
}

public class ReplicaExpiryPredictionService
    : IReplicaExpiryPredictionService
{
    private readonly IReadOnlyDictionary<HosterCode, IHosterExpiryPolicy> _policies;

    public ReplicaExpiryPredictionService(
        IEnumerable<IHosterExpiryPolicy> policies)
    {
        _policies = policies.ToDictionary(x => x.HosterCode);
    }


    public Task<Result<DateTime>> Predict(IHosterDefinition hoster, Replica replica)
    {
        if (!_policies.TryGetValue(hoster.Code, out var policy))
            return Task.FromResult(Result.Failure<DateTime>(
                new Error("NoExpiryPolicy", $"No expiry policy registered for hoster {hoster.Code}")
                    .AsPermanent()));

        return policy.Predict(replica);
    }
}
