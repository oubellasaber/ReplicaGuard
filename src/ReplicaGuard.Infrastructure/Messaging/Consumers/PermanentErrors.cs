using ReplicaGuard.Core.Abstractions;

namespace ReplicaGuard.Infrastructure.Messaging.Consumers;

/// <summary>
/// Centralized registry of permanent (non‑retryable) replication errors.
/// These errors indicate conditions that cannot be resolved by retrying
/// and should immediately mark the replica as permanently failed.
/// </summary>
public static class PermanentErrors
{
    /// <summary>
    /// The hoster does not support uploads for this asset or file type.
    /// Retrying cannot fix this.
    /// </summary>
    public static readonly Error UploadNotSupported =
        new Error(
            code: "Upload.NotSupported",
            message: "The selected hoster does not support uploading this asset."
        ).AsPermanent();

    /// <summary>
    /// No credentials were provided for the hoster.
    /// This is a configuration error, not a transient one.
    /// </summary>
    public static readonly Error NoCredentials =
        new Error(
            code: "Credentials.Missing",
            message: "No credentials were provided for the hoster."
        ).AsPermanent();
}
