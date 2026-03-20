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
    /// At least one replica is currently being uploaded.
    /// </summary>
    Uploading = 1,

    /// <summary>
    /// All replicas have been successfully uploaded.
    /// </summary>
    Completed = 2,

    /// <summary>
    /// All replicas have failed permanently.
    /// </summary>
    Failed = 3
}
