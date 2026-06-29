using MediatR;
using ReplicaGuard.Domain.Abstractions;

namespace ReplicaGuard.Application.Abstractions.Messaging;

public interface IQuery<TResponse> : IRequest<Result<TResponse>>;
