using ReplicaGuard.Application.Abstractions.Messaging;

namespace ReplicaGuard.Application.HosterCredentials.GetHosterCredentials;

public sealed record GetHosterCredentialsQuery(Guid HosterId) : IQuery<GetHosterCredentialsResponse>;
