using ReplicaGuard.Application.Abstractions.Messaging;
using ReplicaGuard.Core.HosterAccounts;
using ReplicaGuard.Core.Hosters;

namespace ReplicaGuard.Application.HosterAccounts.CreateHosterAccount;

public sealed record CreateHosterAccountCommand(
    HosterCode Id,
    string Alias,
    string? Description,
    List<IdentityDto> Identities) : ICommand<CreateHosterAccountResponse>;

public sealed record IdentityDto(
    IdentityType Type,
    string? Value,
    Dictionary<SecretType, string> PlaintextSecrets);
