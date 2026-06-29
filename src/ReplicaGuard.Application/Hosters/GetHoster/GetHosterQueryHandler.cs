using ReplicaGuard.Application.Abstractions.Messaging;
using ReplicaGuard.Domain.Abstractions;
using ReplicaGuard.Domain.Hosters;

namespace ReplicaGuard.Application.Hosters.GetHoster;

public sealed class GetHosterQueryHandler : IQueryHandler<GetHosterQuery, HosterResponse>
{
    private readonly IHosterRepository _hosters;
    private readonly IHosterDefinitionResolver _resolver;

    public GetHosterQueryHandler(
        IHosterRepository hosters,
        IHosterDefinitionResolver resolver)
    {
        _hosters = hosters;
        _resolver = resolver;
    }

    public async Task<Result<HosterResponse>> Handle(
        GetHosterQuery request,
        CancellationToken cancellationToken)
    {
        var hosterId = request.Id;
        
        var hoster = await _hosters.GetByIdAsync(hosterId, cancellationToken);
        if (hoster is null)
            return Result.Failure<HosterResponse>(HosterErrors.NotFound(hosterId));

        var hosterDef = _resolver.Resolve(hoster.Code);

        return Result.Success(HosterResponseMapper.Map(hoster, hosterDef));
    }
}
