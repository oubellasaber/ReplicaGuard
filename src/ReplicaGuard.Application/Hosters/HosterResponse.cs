namespace ReplicaGuard.Application.Hosters;

public sealed record HosterResponse(
    Guid Id,
    string Code,
    string DisplayName,
    List<string> PrimaryCredentials,
    List<HosterFeatureRequirementResponse> Requirements);

public sealed record HosterFeatureRequirementResponse(
    string Feature,
    List<string> RequiredCredentials);
