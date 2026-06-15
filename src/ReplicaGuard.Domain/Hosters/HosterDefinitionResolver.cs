namespace ReplicaGuard.Core.Hosters;

public sealed class HosterDefinitionResolver : IHosterDefinitionResolver
{
    private readonly IReadOnlyDictionary<HosterCode, IHosterDefinition> _definitions;

    public HosterDefinitionResolver()
    {
        var defs = HosterDefinitions.All;
        _definitions = defs.ToDictionary(d => d.HosterId);
    }

    public IHosterDefinition Resolve(HosterCode code)
    {
        if (_definitions.TryGetValue(code, out var def))
            return def;

        throw new KeyNotFoundException($"Hoster definition not found for code: {code}");
    }

    public bool TryResolve(HosterCode code, out IHosterDefinition? definition)
        => _definitions.TryGetValue(code, out definition);
}
