using ReplicaGuard.Application.Abstractions.Authentication;
using ReplicaGuard.Application.Abstractions.Messaging;
using ReplicaGuard.Application.HosterAccounts.GetHosterAccount;
using ReplicaGuard.Domain.Abstractions;
using ReplicaGuard.Domain.HosterAccounts;

namespace ReplicaGuard.Application.HosterAccounts.GetHosterAccounts;

public sealed class GetHosterAccountsQueryHandler(
    IHosterAccountRepository accountsRepo,
    IUserContext userContext) : IQueryHandler<GetHosterAccountsQuery, GetHosterAccountsResponse>
{
    public async Task<Result<GetHosterAccountsResponse>> Handle(GetHosterAccountsQuery request, CancellationToken cancellationToken)
    {
        var accs = await accountsRepo.GetAccounts(userContext.UserId, cancellationToken);
        return new GetHosterAccountsResponse(accs.Select(MapToResponse).ToList());
    }

    private GetHosterAccountResponse MapToResponse(HosterAccount account)
    {
        var identities = account.Identities
            .Select(i => new IdentityResponseDto(
                i.Type,
                i.Value,
                i.Status))
            .ToList();

        return new GetHosterAccountResponse(
            account.Id,
            account.HosterCode,
            account.Alias,
            account.Description,
            account.CreatedAtUtc,
            account.UpdatedAtUtc,
            identities);
    }
}
