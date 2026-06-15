using ReplicaGuard.Application.Abstractions.Messaging;

namespace ReplicaGuard.Application.HosterAccounts.GetHosterAccount;

public sealed record GetHosterAccountQuery(Guid HosterAccountId)
    : IQuery<GetHosterAccountResponse>;
