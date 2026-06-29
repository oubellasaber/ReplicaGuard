namespace ReplicaGuard.Application.Hosters;

public sealed record HosterResponse(
    Guid Id,
    string Code,
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
