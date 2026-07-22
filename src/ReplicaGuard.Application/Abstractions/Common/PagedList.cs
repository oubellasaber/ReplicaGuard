namespace ReplicaGuard.Application.Abstractions.Common;

public class PagedList<T>
{
    public List<T> Items { get; }
    public int TotalCount { get; }
    public int CurrentPage { get; }
    public int PageSize { get; }
    public int TotalPages { get; }

    public PagedList(List<T> items, int count, int pageNumber, int pageSize)
    {
        TotalCount = count;
        PageSize = pageSize;
        CurrentPage = pageNumber;
        TotalPages = (int)Math.Ceiling(count / (double)pageSize);
        Items = items;
    }
}
