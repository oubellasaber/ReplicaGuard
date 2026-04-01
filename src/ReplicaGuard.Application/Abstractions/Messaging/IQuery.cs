using MediatR;
using ReplicaGuard.Core.Abstractions;

namespace ReplicaGuard.Application.Abstractions.Messaging;

public interface IQuery<TResponse> : IRequest<Result<TResponse>>;
