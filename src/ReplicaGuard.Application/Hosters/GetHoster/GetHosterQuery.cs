using ReplicaGuard.Application.Abstractions.Messaging;
using ReplicaGuard.Core.Hosters;

namespace ReplicaGuard.Application.Hosters.GetHoster;

public sealed record GetHosterQuery(string Id) : IQuery<HosterResponse>;
