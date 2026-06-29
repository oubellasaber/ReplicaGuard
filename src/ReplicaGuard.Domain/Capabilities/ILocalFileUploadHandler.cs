using ReplicaGuard.Domain.Replication;

namespace ReplicaGuard.Domain.Capabilities;

public sealed record LocalFileUploadRequest(
    HosterAccounts.HosterAccount Account,
    string FileName,
    LocalFileSource Source);

public sealed record LocalFileUploadResponse(
    string FileId,
    Uri FileUrl,
    string FileName,
    long? SizeBytes);

public interface ILocalFileUploadHandler : ICapabilityHandler<LocalFileUploadRequest, LocalFileUploadResponse>
{
}
