namespace ReplicaGuard.Application.HosterCredentials.GetHosterCredentials;

public sealed record GetHosterCredentialsResponse(
    Guid Id,
    Guid HosterId,
    string? ApiKey,
    string? Username,
    string? Email,
    string? Password,
    string Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
