using ReplicaGuard.Domain.Hosters;

namespace ReplicaGuard.Application.HosterAccounts.GetHosterAccounts;

public sealed record HosterAccountSummaryResponse(
    Guid Id,
    HosterCode HosterCode,
    string HosterDisplayName,
    string Alias,
    string? Description,
    int IdentityCount,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
