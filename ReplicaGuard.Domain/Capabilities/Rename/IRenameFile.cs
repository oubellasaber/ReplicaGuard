using ReplicaGuard.Core.Abstractions;
using ReplicaGuard.Core.Capabilities.Credentials;

namespace ReplicaGuard.Core.Capabilities.Rename;

/// <summary>
/// Capability for renaming an already uploaded file on a hoster.
/// </summary>
public interface IRenameFile
{
    Task<Result> RenameFileAsync(
        CredentialSet credentials,
        string fileCode,
        string newFileName,
        CancellationToken ct = default);
}
