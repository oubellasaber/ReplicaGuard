using ReplicaGuard.Application.Abstractions.Messaging;
using ReplicaGuard.Domain.HosterAccounts;

namespace ReplicaGuard.Application.HosterAccounts.CreateHosterAccount;

public sealed record CreateHosterAccountCommand(
    Guid HosterId,
    string Alias,
    string? Description,
    List<IdentityDto> Identities) : ICommand<CreateHosterAccountResponse>;

public sealed record IdentityDto(
    IdentityType Type,
    string? Value,
    Dictionary<SecretType, string> PlaintextSecrets);
