using MediatR;
using ReplicaGuard.Domain.Abstractions;

namespace ReplicaGuard.Application.Abstractions.Messaging;

public interface IQueryHandler<TQuery, TResponse> : IRequestHandler<TQuery, Result<TResponse>>
    where TQuery : IQuery<TResponse>;
