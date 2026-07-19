using ReplicaGuard.Application.Assets.Services;
using ReplicaGuard.Domain.Abstractions;
using ReplicaGuard.Domain.Capabilities;
using ReplicaGuard.Domain.Hosters;
using ReplicaGuard.Domain.Replication;

namespace ReplicaGuard.Infrastructure.Hosters.SendCm;

internal class SendCmExpiryPolicy : IHosterExpiryPolicy
{
    public HosterCode HosterCode => HosterCode.SendCm;

    private readonly ICapabilityFactory _capabilityFactory;

    public SendCmExpiryPolicy(ICapabilityFactory capabilityFactory)
        => _capabilityFactory = capabilityFactory;

    public async Task<Result<DateTime>> Predict(Replica replica)
    {
        var handler = _capabilityFactory.Get<IGetLastDownloadDateCapabilityHandler>(HosterCode);

        var result = await handler.HandleAsync(new GetLastDownloadDateRequest(replica));

        if (result.IsFailure)
            return Result.Failure<DateTime>(result.Error);

        var ttl = replica.HosterAccountId is null ? 15 : 30;
        if (result.Value.LastDownloadDate is null)
            return replica.CreatedAtUtc.AddDays(ttl);
        return result.Value.LastDownloadDate.Value.AddDays(ttl);
    }
}
