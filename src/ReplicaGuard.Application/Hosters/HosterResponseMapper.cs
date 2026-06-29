using ReplicaGuard.Domain.Hosters;

namespace ReplicaGuard.Application.Hosters;

internal static class HosterResponseMapper
{
    public static HosterResponse Map(Hoster hoster, IHosterDefinition def)
    {
        return new HosterResponse(
            Id: hoster.Id,
            Code: hoster.Code.ToFriendlyString(),
            PrimaryIdentities: MapPrimary(def.PrimaryIdentities),
            CapabilityRequirements: def.CapabilityRequirements
                .Select(MapCapability)
                .ToList()
        );
    }

    private static PrimaryIdentitiesDto MapPrimary(PrimaryIdentityRequirement req)
    {
        return new PrimaryIdentitiesDto(
            Description: BuildPrimaryDescription(req),
            Paths: req.Paths.Select(MapPath).ToList()
        );
    }

    private static CapabilityRequirementDto MapCapability(CapabilityRequirement req)
    {
        return new CapabilityRequirementDto(
            Capability: req.Capability.ToString(),
            Description: BuildCapabilityDescription(req),
            Paths: req.Paths.Select(MapPath).ToList()
        );
    }

    private static RequirementPathDto MapPath(RequirementPath path)
    {
        return new RequirementPathDto(
            And: path.RequiredIdentities.Select(t => t.ToString()).ToList()
        );
    }

    private static string BuildPrimaryDescription(PrimaryIdentityRequirement req)
    {
        var all = req.Paths
            .Select(p => string.Join(" and ", p.RequiredIdentities.Select(t => t.ToString())))
            .ToList();

        // Example: "api_key", "email", "username"
        var orList = string.Join(" or ", all);

        return $"At least one of the following identity sets must be provided; this requires {orList} to be submitted during account creation";
    }

    private static string BuildCapabilityDescription(CapabilityRequirement req)
    {
        var all = req.Paths
            .Select(p => string.Join(" and ", p.RequiredIdentities.Select(t => t.ToString())))
            .ToList();

        var orList = string.Join(" or ", all);

        return $"{req.Capability.ToString().Replace('_', ' ')} requires {orList} to be linked with the used hoster account.";
    }

}
