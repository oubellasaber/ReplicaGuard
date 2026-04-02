namespace ReplicaGuard.Core.Capabilities.Upload;

/// <summary>
/// Response from a successful file upload to a hoster.
/// </summary>
public sealed record UploadResponse(
    string FileId,
    Uri FileUrl,
    string FileName,
    long? SizeBytes,
    DateTime? UploadedAt);
