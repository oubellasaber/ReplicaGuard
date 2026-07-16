using ReplicaGuard.Domain.HosterAccounts;
using ReplicaGuard.Domain.Replication;

namespace ReplicaGuard.Domain.Capabilities;

public sealed record GetFileInfoRequest(Replica Replica, bool IncludeLastDownloadDate = false);

public sealed record GetFileInfoResponse(
    string Id,
    string Url,
    string Name,
    long TotalBytes,
    DateTime UploadedToHosterAt,
    DateTime? LastDownloadDateFromHoster,
    string? Sha256Hash,
    string? Md5Hash);

public interface IGetFileInfoCapabilityHandler : ICapabilityHandler<GetFileInfoRequest, GetFileInfoResponse>;
