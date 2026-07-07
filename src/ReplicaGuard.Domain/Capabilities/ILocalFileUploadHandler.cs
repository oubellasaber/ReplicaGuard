using ReplicaGuard.Domain.Replication;

namespace ReplicaGuard.Domain.Capabilities;

public sealed record TransferProgress(long BytesTransferred);

public sealed record LocalFileUploadRequest(
    HosterAccounts.HosterAccount Account,
    string FileName,
    LocalFileSource Source,
    Action<TransferProgress>? OnProgress = null);

public sealed record LocalFileUploadResponse(
    string FileId,
    Uri FileUrl,
    string FileName,
    long? SizeBytes);

public interface ILocalFileUploadHandler : ICapabilityHandler<LocalFileUploadRequest, LocalFileUploadResponse>
{
}
