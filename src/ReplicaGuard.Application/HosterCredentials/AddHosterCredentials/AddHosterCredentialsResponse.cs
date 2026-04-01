namespace ReplicaGuard.Application.HosterCredentials.AddHosterCredentials;

public sealed record AddHosterCredentialsResponse(
    Guid Id,
    Guid HosterId,
    string Status,
    DateTime CreatedAt);
