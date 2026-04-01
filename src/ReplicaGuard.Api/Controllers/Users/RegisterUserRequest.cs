namespace ReplicaGuard.Api.Controllers.Users;

public sealed record RegisterUserRequest(string Name, string Email, string Password, string ConfirmationPassword);
