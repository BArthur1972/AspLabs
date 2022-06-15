namespace Microsoft.AspNetCore.Components.QuickGrid;

public class PaginationState
{
    public int ItemsPerPage { get; set; } = 10;
    public int CurrentPageIndex { get; set; }
    public int? TotalItemCount { get; private set; }

    public int? LastPageIndex => TotalItemCount / ItemsPerPage;

    public event EventHandler? CurrentPageItemsChanged; 
    public event EventHandler? TotalItemCountChanged;

    public IQueryable<T> ApplyPagination<T>(IQueryable<T> source)
    {
        return source.Skip(CurrentPageIndex * ItemsPerPage).Take(ItemsPerPage);
    }

    public override int GetHashCode()
        => HashCode.Combine(ItemsPerPage, CurrentPageIndex, TotalItemCount);

    internal void SetTotalItemCount(int totalItemCount)
    {
        TotalItemCount = totalItemCount;
        TotalItemCountChanged?.Invoke(this, EventArgs.Empty);
    }

    internal void NotifyCurrentPageItemsChanged()
        => CurrentPageItemsChanged?.Invoke(this, EventArgs.Empty);
}
