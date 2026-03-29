namespace ReplicaGuard.Core.Domain.Replication;

/// <summary>
/// Represents the state of a single replica on a hoster.
/// </summary>
public enum ReplicaState
{
    /// <summary>
    /// Replica is waiting for a worker to upload it.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Replica is waiting for a sibling replica to finish so the file becomes available locally.
    /// </summary>
    WaitingForPeer = 1,

    /// <summary>
    /// Replica is downloading/spooling the source file.
    /// </summary>
    Downloading = 2,

    /// <summary>
    /// Replica is currently being uploaded by a worker.
    /// </summary>
    Uploading = 3,

    /// <summary>
    /// Replica was successfully uploaded and is accessible.
    /// </summary>
    Completed = 4,

    /// <summary>
    /// Replica upload failed permanently (no retries remaining).
    /// </summary>
    Failed = 5,

    /// <summary>
    /// Replica failed but is scheduled for another attempt.
    /// </summary>
    Retrying = 6
}
