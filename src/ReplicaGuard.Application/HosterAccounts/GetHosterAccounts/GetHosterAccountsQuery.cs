using ReplicaGuard.Application.Abstractions.Messaging;

namespace ReplicaGuard.Application.HosterAccounts.GetHosterAccounts;

public sealed record GetHosterAccountsQuery : IQuery<GetHosterAccountsResponse>;
