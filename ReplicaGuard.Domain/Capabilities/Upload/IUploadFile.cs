using ReplicaGuard.Core.Abstractions;
using ReplicaGuard.Core.Capabilities.Credentials;
using ReplicaGuard.Core.Domain.Replication;

namespace ReplicaGuard.Core.Capabilities.Upload;

/// <summary>
/// Capability for uploading files to a hoster.
/// </summary>
public interface IUploadFile
{
    /// <summary>
    /// Uploads a file directly to the hoster.
    /// </summary>
    /// <param name="credentials">User credentials for authentication</param>
    /// <param name="fileName">Name of the file being uploaded</param>
    /// <param name="fileStream">Stream containing the file content</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Upload result containing the file URL and metadata</returns>
    Task<Result<UploadResponse>> UploadFromLocalStorageAsync(
        CredentialSet credentials,
        string fileName,
        FileStream fileStream,
        CancellationToken ct = default);

    /// <summary>
    /// Uploads a file by providing a remote URL for the hoster to fetch.
    /// </summary>
    /// <param name="credentials">User credentials for authentication</param>
    /// <param name="fileName">Name of the file being uploaded</param>
    /// <param name="remoteUrl">URL where the hoster can download the file</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Upload result containing the file URL and metadata</returns>
    Task<Result<UploadResponse>> UploadFromRemoteUrlAsync(
        CredentialSet credentials,
        string fileName,
        RemoteFileSource remoteUrl,
        CancellationToken ct = default);
}
