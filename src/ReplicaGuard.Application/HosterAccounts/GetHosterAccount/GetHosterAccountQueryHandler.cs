using ReplicaGuard.Application.Abstractions.Messaging;
using ReplicaGuard.Domain.Abstractions;
using ReplicaGuard.Domain.HosterAccounts;

namespace ReplicaGuard.Application.HosterAccounts.GetHosterAccount;

public sealed class GetHosterAccountQueryHandler
    : IQueryHandler<GetHosterAccountQuery, GetHosterAccountResponse>
{
    private readonly IHosterAccountRepository _repository;

    public GetHosterAccountQueryHandler(IHosterAccountRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<GetHosterAccountResponse>> Handle(
        GetHosterAccountQuery request,
        CancellationToken cancellationToken)
    {
        var account = await _repository.GetByIdAsync(request.HosterAccountId, cancellationToken);

        if (account is null)
            return Result.Failure<GetHosterAccountResponse>(
                HosterAccountErrors.NotFound(request.HosterAccountId));

        var identities = account.Identities
            .Select(i => new IdentityResponseDto(
                i.Type,
                i.Value ?? i.Type.ToString(),
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
