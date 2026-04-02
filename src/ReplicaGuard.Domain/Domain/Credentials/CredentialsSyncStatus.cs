namespace ReplicaGuard.Core.Domain.Credentials;

public enum CredentialsSyncStatus
{
    /// <summary>
    /// Credentials have been added or modified and are pending validation by the background worker.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// All secondary credentials have been validated against the primary credential by the background worker.
    /// </summary>
    Synced = 1,

    /// <summary>
    /// Validation failed. The credential synchronization encountered an error.
    /// </summary>
    Failed = 2,
}
