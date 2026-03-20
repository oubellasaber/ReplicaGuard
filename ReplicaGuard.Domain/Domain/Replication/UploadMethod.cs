namespace ReplicaGuard.Core.Domain.Replication;

/// <summary>
/// Specifies how a file should be uploaded to a hoster.
/// </summary>
public enum UploadMethod
{
    /// <summary>
    /// Hoster fetches the file directly from the source URL (remote upload).
    /// </summary>
    RemoteUrl = 0,

    /// <summary>
    /// File is uploaded directly from local storage (direct upload).
    /// </summary>
    LocalStorage = 1
}
