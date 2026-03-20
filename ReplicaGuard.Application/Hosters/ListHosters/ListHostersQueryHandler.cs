using ReplicaGuard.Application.Abstractions.Messaging;
using ReplicaGuard.Core.Abstractions;
using ReplicaGuard.Core.Domain.Hoster;

namespace ReplicaGuard.Application.Hosters.ListHosters;

public sealed class ListHostersQueryHandler(
    IHosterRepository hosters)
        : IQueryHandler<ListHostersQuery, List<HosterResponse>>
{
    public async Task<Result<List<HosterResponse>>> Handle(
        ListHostersQuery request,
        CancellationToken cancellationToken)
    {
        List<Hoster> items = await hosters.GetAllAsync(cancellationToken);

        List<HosterResponse> response = items
            .Select(HosterResponseMapper.Map)
            .ToList();

        return Result.Success(response);
    }
}
