using ReplicaGuard.Application.Abstractions.Messaging;

namespace ReplicaGuard.Application.Hosters.GetHoster;

public sealed record GetHosterQuery(Guid HosterId) : IQuery<HosterResponse>;
