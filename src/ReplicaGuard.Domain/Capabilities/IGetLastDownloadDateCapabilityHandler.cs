using ReplicaGuard.Domain.Replication;

namespace ReplicaGuard.Domain.Capabilities;

public sealed record GetLastDownloadDateRequest(Replica Replica);

public sealed record GetLastDownloadDateResponse(DateTime? LastDownloadDate);

public interface IGetLastDownloadDateCapabilityHandler
    : ICapabilityHandler<GetLastDownloadDateRequest, GetLastDownloadDateResponse>;
