using ReplicaGuard.Application.Abstractions.Messaging;

namespace ReplicaGuard.Application.HosterAccounts.VerifiyIdentity;

public sealed record VerifyIdentityCommand(Guid IdentityId) : ICommand;
