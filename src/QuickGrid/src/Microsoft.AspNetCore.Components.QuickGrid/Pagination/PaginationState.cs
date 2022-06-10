namespace Microsoft.AspNetCore.Components.QuickGrid;

public class PaginationState
{
    public int ItemsPerPage { get; set; } = 10;
    public int CurrentPageIndex { get; set; }
    public int TotalItemCount { get; set; }

    public int LastPageIndex => TotalItemCount / ItemsPerPage;

    public IQueryable<T> ApplyPagination<T>(IQueryable<T> source)
    {
        return source.Skip(CurrentPageIndex * ItemsPerPage).Take(ItemsPerPage);
    }
}
