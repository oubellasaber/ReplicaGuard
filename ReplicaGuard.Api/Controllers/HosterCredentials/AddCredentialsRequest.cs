namespace ReplicaGuard.Api.Controllers.HosterCredentials;

public sealed record AddCredentialsRequest(
    string? ApiKey,
    string? Username,
    string? Email,
    string? Password);
