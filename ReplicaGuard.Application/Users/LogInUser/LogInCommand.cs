using ReplicaGuard.Application.Abstractions.Messaging;

namespace ReplicaGuard.Application.Users.LogInUser;

public sealed record LogInUserCommand(string Email, string Password) : ICommand<AccessTokensResponse>;
