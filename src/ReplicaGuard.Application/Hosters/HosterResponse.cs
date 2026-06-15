namespace ReplicaGuard.Application.Hosters;

public sealed record HosterResponse(
    string Id,
    PrimaryIdentitiesDto PrimaryIdentities,
    List<CapabilityRequirementDto> CapabilityRequirements
);

public sealed record PrimaryIdentitiesDto(
    string Description,
    List<RequirementPathDto> Paths
);

public sealed record CapabilityRequirementDto(
    string Capability,
    string Description,
    List<RequirementPathDto> Paths
);

public sealed record RequirementPathDto(
    List<string> And
);
