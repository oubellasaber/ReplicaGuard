using ReplicaGuard.Domain.HosterAccounts;
using ReplicaGuard.Domain.Replication;

namespace ReplicaGuard.Domain.Capabilities;

public sealed record RemoteFileUploadRequest(
    HosterAccount Account,
    string FileName,
    RemoteFileSource Source,
    Action<TransferProgress>? OnProgress);

public sealed record RemoteFileUploadResponse(
    string FileId,
    Uri FileUrl,
    string FileName,
    long? SizeBytes);

public interface IRemoteFileUploadHandler : ICapabilityHandler<RemoteFileUploadRequest, RemoteFileUploadResponse>
{
}
