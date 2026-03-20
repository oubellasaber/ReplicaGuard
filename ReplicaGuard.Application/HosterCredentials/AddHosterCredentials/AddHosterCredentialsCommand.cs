using ReplicaGuard.Application.Abstractions.Messaging;

namespace ReplicaGuard.Application.HosterCredentials.AddHosterCredentials;
public sealed record AddHosterCredentialsCommand(
    Guid HosterId,
    string? ApiKey,
    string? Username,
    string? Email,
    string? Password
) : ICommand<AddHosterCredentialsResponse>;
