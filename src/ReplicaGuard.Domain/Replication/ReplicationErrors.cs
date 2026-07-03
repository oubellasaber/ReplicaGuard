using ReplicaGuard.Domain.Abstractions;
using ReplicaGuard.Domain.Common;

namespace ReplicaGuard.Domain.Replication;

public static class ReplicationErrors
{
    public static Error AssetNotFound(Guid id) =>
        CommonErrors.NotFound(nameof(Asset), id);

    // Replica errors
    public static Error DuplicateReplica(Guid assetId, Guid hosterId) =>
        new Error("Replica.Duplicate",
            $"A replica for the specified asset already exists for this hoster.",
            ErrorType.Conflict)
        .WithMetadata("AssetId", assetId)
        .WithMetadata("HosterId", hosterId);

    public static Error ReplicaNotFound(Guid id) =>
        CommonErrors.NotFound(nameof(Replica), id);

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
        new Error("FileUrl.Invalid",
            $"The provided URL is not a valid absolute URI.",
            ErrorType.InvalidInput)
        .WithMetadata("Url", url);

    public static Error FileUrlUnsupportedScheme(string scheme) =>
        new Error("FileUrl.UnsupportedScheme",
            $"URL scheme is not supported. Only HTTP and HTTPS are allowed.",
            ErrorType.InvalidInput)
        .WithMetadata("schema", scheme)
        .WithMetadata("SupportedSchemas", new string[] { "https", "http" });

    // RemoteFileSource errors
    public static Error HeadersCannotBeEmpty =>
        new("RemoteFileSource.HeadersNullOrEmpty",
            "Headers dictionary cannot be null or empty.",
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
        new Error("LocalFileSource.FileAccessDenied",
            $"Access denied to the file in the specified path.",
            ErrorType.Forbidden)
        .WithMetadata("FilePath", filePath);

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
        new Error("FileName.TooLong",
            $"File name is too long than maximum allowed characters.",
            ErrorType.InvalidInput)
        .WithMetadata("FileLength", length)
        .WithMetadata("AllowedLength", 255);
}
