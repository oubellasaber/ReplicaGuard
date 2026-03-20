using ReplicaGuard.Application.Abstractions.Messaging;
using ReplicaGuard.Core.Abstractions;
using ReplicaGuard.Core.Domain.Hoster;

namespace ReplicaGuard.Application.Hosters.GetHoster;

public sealed class GetHosterQueryHandler(
    IHosterRepository hosters)
        : IQueryHandler<GetHosterQuery, HosterResponse>
{
    public async Task<Result<HosterResponse>> Handle(
        GetHosterQuery request,
        CancellationToken cancellationToken)
    {
        Hoster? hoster = await hosters.GetByIdAsync(request.HosterId, cancellationToken);

        if (hoster is null)
            return Result.Failure<HosterResponse>(HosterErrors.NotFound(request.HosterId));

        return Result.Success(HosterResponseMapper.Map(hoster));
    }
}
