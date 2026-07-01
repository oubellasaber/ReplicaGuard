using ReplicaGuard.Core.Abstractions;

namespace ReplicaGuard.Core.Hosters;

public enum CapabilityCode : short
{
    RemoteFileUpload = 1,
    LocalFileUpload = 2,
    IdentityVerification = 3,
    CopyFile = 4,
    GenerateDownloadUrl = 5
}

//public interface ICapability<THoster> where THoster : IHosterDefinition
//{
//    Capability Capability { get; }
//}

//public interface ICapabilityHandler<ICapability, TRequest, TResponse> where ICapability : ICapability<IHosterDefinition>
//{
//    Task<Result<TResponse>> HandleAsync(TRequest request, CancellationToken ct = default);
//}

//public sealed class UploadCapability : ICapability<IHosterDefinition>
//{
//    public Capability Capability => Capability.RemoteUpload;
//}
