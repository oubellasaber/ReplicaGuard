using ReplicaGuard.Domain.HosterAccounts;
using ReplicaGuard.Domain.Hosters;

namespace ReplicaGuard.Application.HosterAccounts.GetHosterAccount;

public sealed record HosterAccountResponse(
    Guid Id,
    HosterCode HosterCode,
    string HosterDisplayName,
    string Alias,
    string? Description,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    List<IdentityResponseDto> Identities);

public sealed record IdentityResponseDto(
    Guid Id,
    IdentityType Type,
    string? Value,
    IdentityVerificationStatus Status);
