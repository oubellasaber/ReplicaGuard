using ReplicaGuard.Domain.Abstractions;
using ReplicaGuard.Domain.Hosters;
using ReplicaGuard.Domain.Replication;

namespace ReplicaGuard.Application.Assets.Services;

public interface IHosterExpiryPolicy
{
    HosterCode HosterCode { get; }

    Task<Result<DateTime>> Predict(Replica replica);
}
