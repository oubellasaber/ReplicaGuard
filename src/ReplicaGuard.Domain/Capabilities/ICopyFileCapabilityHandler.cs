using ReplicaGuard.Core.Capabilities;
using ReplicaGuard.Core.HosterAccounts;

namespace ReplicaGuard.Infrastructure.Hosters.Capabilities;

public sealed record CopyFileRequest(HosterAccount Account, Uri Url);

public sealed record CopyFileResponse(string FileCode);

public interface ICopyFileCapabilityHandler : ICapabilityHandler<CopyFileRequest, CopyFileResponse> { }
