using ReplicaGuard.Application.Abstractions.Messaging;

namespace ReplicaGuard.Application.HosterCredentials.UpdateHosterCredentials;

public sealed record UpdateHosterCredentialsCommand(
    Guid HosterId,
    string? ApiKey,
    string? Username,
    string? Email,
    string? Password) : ICommand;
