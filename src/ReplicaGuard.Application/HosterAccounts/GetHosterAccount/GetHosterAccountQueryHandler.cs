using Microsoft.EntityFrameworkCore;
using ReplicaGuard.Application.Abstractions.Authentication;
using ReplicaGuard.Application.Abstractions.Data;
using ReplicaGuard.Application.Abstractions.Messaging;
using ReplicaGuard.Domain.Abstractions;
using ReplicaGuard.Domain.HosterAccounts;

namespace ReplicaGuard.Application.HosterAccounts.GetHosterAccount;

public sealed class GetHosterAccountQueryHandler(
    IApplicationDbContext dbContext,
    IUserContext userContext)
    : IQueryHandler<GetHosterAccountQuery, HosterAccountResponse>
{
    public async Task<Result<HosterAccountResponse>> Handle(
        GetHosterAccountQuery request,
        CancellationToken cancellationToken)
    {
        var response = await dbContext.Set<HosterAccount>()
            .Where(a => a.Id == request.HosterAccountId && a.UserId == userContext.UserId)
            .Select(a => new HosterAccountResponse(
                a.Id,
                a.Hoster.Code,
                a.Hoster.DisplayName,
                a.Alias,
                a.Description,
                a.CreatedAtUtc,
                a.UpdatedAtUtc,
                a.Identities.Select(i => new IdentityResponseDto(
                    i.Id,
                    i.Type,
                    i.Value,
                    i.Status)).ToList()))
            .FirstOrDefaultAsync(cancellationToken);

        if (response is null)
            return Result.Failure<HosterAccountResponse>(
                HosterAccountErrors.NotFound(request.HosterAccountId));

        return Result.Success(response);
    }
}
