using ReplicaGuard.Core.Replication;

namespace ReplicaGuard.Core.Capabilities;

public sealed record RemoteFileUploadRequest(
    HosterAccounts.HosterAccount Account,
    string FileName,
    RemoteFileSource Source);

public sealed record RemoteFileUploadResponse(
    string FileId,
    Uri FileUrl,
    string FileName,
    long? SizeBytes);

public interface IRemoteFileUploadHandler : ICapabilityHandler<RemoteFileUploadRequest, RemoteFileUploadResponse>
{
}
