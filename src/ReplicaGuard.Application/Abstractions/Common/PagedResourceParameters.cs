namespace ReplicaGuard.Application.Abstractions.Common;

public record PagedResourceParameters
{
    private const int MaxPageSize = 100;
    private int _pageSize = 10;

    public int Page { get; init; } = 1;

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value > MaxPageSize ? MaxPageSize : value;
    }
}
