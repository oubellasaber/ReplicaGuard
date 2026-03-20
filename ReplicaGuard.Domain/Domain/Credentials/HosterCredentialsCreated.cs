using ReplicaGuard.Core.Abstractions;

namespace ReplicaGuard.Core.Domain.Credentials;

public sealed record HosterCredentialsCreated(Guid CredentialsId, uint Version) : IDomainEvent;
