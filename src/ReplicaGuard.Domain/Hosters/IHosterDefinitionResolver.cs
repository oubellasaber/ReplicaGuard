namespace ReplicaGuard.Domain.Hosters;

public interface IHosterDefinitionResolver
{
    IHosterDefinition Resolve(HosterCode code);
    bool TryResolve(HosterCode code, out IHosterDefinition? definition);
}
