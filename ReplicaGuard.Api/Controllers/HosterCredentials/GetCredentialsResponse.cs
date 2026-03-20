namespace ReplicaGuard.Api.Controllers.HosterCredentials;

public sealed record GetCredentialsResponse(
    Guid Id,
    Guid HosterId,
    string? ApiKey,
    string? Username,
    string? Email,
    string? Password,
    string Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
