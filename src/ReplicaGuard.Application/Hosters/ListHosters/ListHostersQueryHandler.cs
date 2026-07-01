using ReplicaGuard.Application.Abstractions.Messaging;
using ReplicaGuard.Domain.Abstractions;
using ReplicaGuard.Domain.Hosters;

namespace ReplicaGuard.Application.Hosters.ListHosters;

public sealed class ListHostersQueryHandler : IQueryHandler<ListHostersQuery, List<HosterResponse>>
{
    private readonly IHosterRepository _hosters;
    private readonly IHosterDefinitionResolver _resolver;

    public ListHostersQueryHandler(
        IHosterRepository hosters,
        IHosterDefinitionResolver resolver)
    {
        _hosters = hosters;
        _resolver = resolver;
    }

    public async Task<Result<List<HosterResponse>>> Handle(
        ListHostersQuery request,
        CancellationToken cancellationToken)
    {
        List<Hoster> items = await _hosters.GetAllAsync(cancellationToken);

        List<HosterResponse> response = items
            .Select(h =>
            {
                var def = _resolver.Resolve(h.Code);
                return HosterResponseMapper.Map(h, def);
            })
            .ToList();

        return Result.Success(response);
    }
}

