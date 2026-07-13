using ReplicaGuard.Application.Assets.Services;
using ReplicaGuard.Domain.Abstractions;
using ReplicaGuard.Domain.Capabilities;
using ReplicaGuard.Domain.Hosters;
using ReplicaGuard.Domain.Replication;

namespace ReplicaGuard.Infrastructure.Hosters.Pixeldrain;

internal class PixeldrainExpiryPolicy : IHosterExpiryPolicy
{
    public HosterCode HosterCode => HosterCode.Pixeldrain;

    private readonly ICapabilityFactory _capabilityFactory;

    public PixeldrainExpiryPolicy(ICapabilityFactory capabilityFactory)
        => _capabilityFactory = capabilityFactory;

    public async Task<Result<DateTime>> Predict(Replica replica)
    {
        var handler = _capabilityFactory.Get<IGetLastDownloadDateCapabilityHandler>(HosterCode);

        var result = await handler.HandleAsync(new GetLastDownloadDateRequest(replica));

        if (result.IsFailure)
            return Result.Failure<DateTime>(result.Error);

        if (result.Value.LastDownloadDate is null)
            return replica.CreatedAtUtc.AddDays(60);
        return result.Value.LastDownloadDate.Value.AddDays(60);
    }
}
