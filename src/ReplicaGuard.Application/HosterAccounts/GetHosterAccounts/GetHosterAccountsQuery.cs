using ReplicaGuard.Application.Abstractions.Common;
using ReplicaGuard.Application.Abstractions.Messaging;

namespace ReplicaGuard.Application.HosterAccounts.GetHosterAccounts;

public sealed record GetHosterAccountsQuery(PagedResourceParameters Parameters) : IQuery<PagedList<HosterAccountSummaryResponse>>;
