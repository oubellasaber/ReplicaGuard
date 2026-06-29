using ReplicaGuard.Application.Abstractions.Messaging;

namespace ReplicaGuard.Application.Hosters.GetHoster;

public sealed record GetHosterQuery(Guid Id) : IQuery<HosterResponse>;
