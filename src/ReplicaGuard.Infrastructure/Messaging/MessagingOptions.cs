using ReplicaGuard.Infrastructure.Persistence;

namespace ReplicaGuard.Infrastructure.Messaging;

public class MessagingOptions
{
    public const string SectionName = "Messaging";

    public int QueryDelayInSeconds { get; init; }
    public int DuplicateDetectionWindowInMinutes { get; init; }
}
