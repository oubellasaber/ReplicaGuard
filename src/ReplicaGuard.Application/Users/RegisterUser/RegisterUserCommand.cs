using ReplicaGuard.Application.Abstractions.Messaging;

namespace ReplicaGuard.Application.Users.RegisterUser;

public sealed record RegisterUserCommand(
    string Name,
    string Email,
    string Password,
    string ConfirmationPassword) : ICommand<AccessTokensResponse>;
