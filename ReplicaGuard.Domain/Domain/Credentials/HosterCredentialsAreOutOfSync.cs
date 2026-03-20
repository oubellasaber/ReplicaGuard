using ReplicaGuard.Core.Abstractions;

namespace ReplicaGuard.Core.Domain.Credentials;

public sealed record HosterCredentialsAreOutOfSync(Guid CredentialsId, uint Version) : IDomainEvent;
