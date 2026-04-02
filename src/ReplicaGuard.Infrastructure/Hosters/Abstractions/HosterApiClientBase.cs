using ReplicaGuard.Core.Domain.Hoster;

namespace ReplicaGuard.Infrastructure.Hosters.Abstractions;

public abstract class HosterApiClientBase<THoster> : IHosterApiClient
    where THoster : IHosterDefinition
{
    public string Code => THoster.Code;
}
