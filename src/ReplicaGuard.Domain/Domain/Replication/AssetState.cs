namespace ReplicaGuard.Core.Domain.Replication;

/// <summary>
/// Represents the lifecycle state of an asset.
/// </summary>
public enum AssetState
{
    /// <summary>
    /// Asset created by user, source file not yet fetched.
    /// </summary>
    Created = 0,

    /// <summary>
    /// At least one replica is currently being uploaded, waiting, or retrying.
    /// </summary>
    InProgress = 1,

    /// <summary>
    /// No work is in progress and at least one replica is available.
    /// </summary>
    Completed = 2,

    /// <summary>
    /// All replicas have failed permanently.
    /// </summary>
    Failed = 3
}
