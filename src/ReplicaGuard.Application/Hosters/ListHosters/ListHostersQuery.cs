using ReplicaGuard.Application.Abstractions.Messaging;

namespace ReplicaGuard.Application.Hosters.ListHosters;

public sealed record ListHostersQuery : IQuery<List<HosterResponse>>;
