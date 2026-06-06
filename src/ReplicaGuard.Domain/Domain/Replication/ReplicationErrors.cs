using ReplicaGuard.Core.Abstractions;
using ReplicaGuard.Core.Domain.Common;

namespace ReplicaGuard.Core.Domain.Replication;

public static class ReplicationErrors
{
    public static Error AssetNotFound(Guid id) =>
        CommonErrors.NotFound(nameof(Asset), id);

    // Replica errors
    public static Error DuplicateReplica(Guid assetId, Guid hosterId) =>
        new("Replica.Duplicate",
            $"A replica for asset '{assetId}' on hoster '{hosterId}' already exists.",
            ErrorType.Conflict);

    public static Error ReplicaNotFound(Guid id) =>
        new("Replica.NotFound",
            $"Replica with ID '{id}' was not found.",
            ErrorType.NotFound);

    public static Error ReplicaTerminalState(Guid id) =>
        new Error("Replica.TerminalState",
            "Replica is already in terminal state.",
            ErrorType.InvalidInput)
        .WithMetadata("ReplicaId", id);

    // FileUrl errors
    public static Error FileUrlEmpty =>
        new("FileUrl.Empty",
            "File URL cannot be null, empty, or whitespace.",
            ErrorType.InvalidInput);

    public static Error FileUrlInvalid(string url) =>
        new("FileUrl.Invalid",
            $"The provided URL '{url}' is not a valid absolute URI.",
            ErrorType.InvalidInput);

    public static Error FileUrlUnsupportedScheme(string scheme) =>
        new("FileUrl.UnsupportedScheme",
            $"URL scheme '{scheme}' is not supported. Only HTTP and HTTPS are allowed.",
            ErrorType.InvalidInput);

    // RemoteFileSource errors
    public static Error HeadersCannotBeNull =>
        new("RemoteFileSource.HeadersNull",
            "Headers dictionary cannot be null.",
            ErrorType.InvalidInput);

    // LocalFileSource errors
    public static Error FilePathEmpty =>
        new("LocalFileSource.FilePathEmpty",
            "File path cannot be null, empty, or whitespace.",
            ErrorType.InvalidInput);

    public static Error FileNotFound(string filePath) =>
        new("LocalFileSource.FileNotFound",
            $"File not found at path: {filePath}",
            ErrorType.NotFound);

    public static Error FileAccessDenied(string filePath) =>
        new("LocalFileSource.FileAccessDenied",
            $"Access denied to file: {filePath}",
            ErrorType.Forbidden);

    // FileName errors
    public static Error FileNameEmpty =>
        new("FileName.Empty",
            "File name cannot be null, empty, or whitespace.",
            ErrorType.InvalidInput);

    public static Error FileNameInvalidChars =>
        new("FileName.InvalidChars",
            "File name contains invalid characters for the file system.",
            ErrorType.InvalidInput);

    public static Error FileNameTooLong(int length) =>
        new("FileName.TooLong",
            $"File name is too long ({length} characters). Maximum allowed is 255 characters.",
            ErrorType.InvalidInput);
}
