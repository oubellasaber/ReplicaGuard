using ReplicaGuard.Domain.Abstractions;

namespace ReplicaGuard.Domain.Hosters;

/// <summary>
/// Represents a hoster provider (e.g., "pixeldrain", "send", "krakenfiles")
/// </summary>
public sealed class Hoster : Entity<Guid>
{
    public HosterCode Code { get; }
    /// <summary>
    /// Human-readable name of the hoster.
    /// </summary>
    public string DisplayName { get; }
    public DateTime CreatedAtUtc { get; }
    public DateTime UpdatedAtUtc { get; private set; }

    public Hoster(HosterCode code, string displayName)
        : base(Guid.NewGuid())
    {
        Code = code;
        DisplayName = displayName.Trim();
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = CreatedAtUtc;
    }
}
