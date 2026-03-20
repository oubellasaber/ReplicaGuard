using ReplicaGuard.Application.Abstractions.Clock;

namespace ReplicaGuard.Infrastructure.Clock;

internal sealed class DateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
