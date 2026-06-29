using ReplicaGuard.Domain.Abstractions;
using ReplicaGuard.Domain.Hosters;

namespace ReplicaGuard.Domain.Capabilities;

public interface ICapabilityHandler<TIn, TOut>
{
    HosterCode HosterCode { get; }         // implementation declares hoster
    CapabilityCode CapabilityCode { get; } // implementation declares capability
    Task<Result<TOut>> HandleAsync(TIn input, CancellationToken ct = default);
}

public interface ICapabilityHandler<TIn>
{
    HosterCode HosterCode { get; }
    CapabilityCode CapabilityCode { get; }
    Task<Result> HandleAsync(TIn input, CancellationToken ct = default);
}

// specialized capability example
//public interface IUploadCapability : ICapability<UploadRequest, UploadResult> { }
