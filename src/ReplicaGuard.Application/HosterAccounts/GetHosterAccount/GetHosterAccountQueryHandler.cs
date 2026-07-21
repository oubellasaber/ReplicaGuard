using ReplicaGuard.Application.Abstractions.Authentication;
using ReplicaGuard.Application.Abstractions.Messaging;
using ReplicaGuard.Domain.Abstractions;
using ReplicaGuard.Domain.HosterAccounts;

namespace ReplicaGuard.Application.HosterAccounts.GetHosterAccount;

public sealed class GetHosterAccountQueryHandler(
    IHosterAccountRepository hosterAccountsRepo,
    IUserContext userContext)
    : IQueryHandler<GetHosterAccountQuery, GetHosterAccountResponse>
{
    public async Task<Result<GetHosterAccountResponse>> Handle(
        GetHosterAccountQuery request,
        CancellationToken cancellationToken)
    {
        var account = await hosterAccountsRepo.GetByIdAsync(request.HosterAccountId, userContext.UserId, cancellationToken);

        if (account is null)
            return Result.Failure<GetHosterAccountResponse>(
                HosterAccountErrors.NotFound(request.HosterAccountId));

        var identities = account.Identities
            .Select(i => new IdentityResponseDto(
                i.Type,
                i.Value,
                i.Status))
            .ToList();

        var response = new GetHosterAccountResponse(
            account.Id,
            account.HosterCode,
            account.Alias,
            account.Description,
            account.CreatedAtUtc,
            account.UpdatedAtUtc,
            identities);

        return Result.Success(response);
    }
}
