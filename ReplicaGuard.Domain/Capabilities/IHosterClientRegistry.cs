namespace ReplicaGuard.Core.Capabilities;

public interface IHosterClientRegistry
{
    public TCapability? GetHosterCapability<TCapability>(string hosterCode)
    where TCapability : class;

    public TCapability? TryGetHosterCapability<TCapability>(string hosterCode)
    where TCapability : class;
}
