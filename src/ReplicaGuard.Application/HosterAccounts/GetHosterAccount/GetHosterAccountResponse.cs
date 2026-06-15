using ReplicaGuard.Core.HosterAccounts;
using ReplicaGuard.Core.Hosters;

namespace ReplicaGuard.Application.HosterAccounts.GetHosterAccount;

public sealed record GetHosterAccountResponse(
    Guid HosterAccountId,
    HosterCode HosterId,
    string Alias,
    string? Description,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    List<IdentityResponseDto> Identities);

public sealed record IdentityResponseDto(
    IdentityType Type,
    string? Value,
    IdentityVerificationStatus Status);
