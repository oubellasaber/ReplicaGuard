namespace ReplicaGuard.Api.Controllers.HosterCredentials;

public sealed record UpdateCredentialsRequest(
    string? ApiKey,
    string? Username,
    string? Email,
    string? Password);
