using ReplicaGuard.Application.Abstractions.Messaging;
using ReplicaGuard.Core.Abstractions;
using ReplicaGuard.Core.Hosters;

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
        string friendly = request.Id.Trim().ToLowerInvariant();
        HosterCode hosterCode;
        try
        {
            hosterCode = HosterCodeExtensions.FromFriendlyString(friendly);
        }
        catch
        {
            return Result.Failure<HosterResponse>(HosterErrors.NotFound(friendly));
        }

        Hoster? hoster = await _hosters.GetByIdAsync(hosterCode, cancellationToken);
        if (hoster is null)
            return Result.Failure<HosterResponse>(HosterErrors.NotFound(friendly));
        var hosterDef = _resolver.Resolve(hosterCode);
        return Result.Success(HosterResponseMapper.Map(hoster, hosterDef));
    }
}
