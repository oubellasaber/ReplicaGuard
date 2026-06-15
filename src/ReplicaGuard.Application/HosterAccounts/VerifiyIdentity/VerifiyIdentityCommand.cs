using ReplicaGuard.Application.Abstractions.Messaging;

namespace ReplicaGuard.Application.HosterAccounts.VerifiyIdentity;

public sealed record VerifiyIdentityCommand(Guid IdentityId) : ICommand;
