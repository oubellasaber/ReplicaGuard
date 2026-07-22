namespace ReplicaGuard.Application.Abstractions.Common;

public record ResourceParameters : PagedResourceParameters
{
    public string? Search { get; init; }
    public string? Filters { get; init; }
    public string? Sorts { get; init; }
}
