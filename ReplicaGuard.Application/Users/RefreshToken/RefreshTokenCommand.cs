using ReplicaGuard.Application.Abstractions.Messaging;

namespace ReplicaGuard.Application.Users.RefreshToken;

public record RefreshTokenCommand(string refreshToken) : ICommand<AccessTokensResponse>;
