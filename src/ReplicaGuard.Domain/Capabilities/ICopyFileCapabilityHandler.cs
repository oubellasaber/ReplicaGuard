using ReplicaGuard.Domain.HosterAccounts;

namespace ReplicaGuard.Domain.Capabilities;

public sealed record CopyFileRequest(HosterAccount Account, Uri Url);

public sealed record CopyFileResponse(string FileCode);

public interface ICopyFileCapabilityHandler : ICapabilityHandler<CopyFileRequest, CopyFileResponse>;
