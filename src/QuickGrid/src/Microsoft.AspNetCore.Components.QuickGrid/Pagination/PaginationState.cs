using Microsoft.AspNetCore.Components.QuickGrid.Infrastructure;

namespace Microsoft.AspNetCore.Components.QuickGrid;

public class PaginationState
{
    internal EventCallbackSubscribable<PaginationState> CurrentPageItemsChanged { get; } = new();
    internal EventCallbackSubscribable<PaginationState> TotalItemCountChanged { get; } = new();

    public int ItemsPerPage { get; set; } = 10;
    public int CurrentPageIndex { get; private set; }
    public int? TotalItemCount { get; private set; }

    public int? LastPageIndex => TotalItemCount / ItemsPerPage;

    public IQueryable<T> ApplyPagination<T>(IQueryable<T> source)
    {
        return source.Skip(CurrentPageIndex * ItemsPerPage).Take(ItemsPerPage);
    }

    public override int GetHashCode()
        => HashCode.Combine(ItemsPerPage, CurrentPageIndex, TotalItemCount);

    public Task SetCurrentPageIndexAsync(int pageIndex)
    {
        CurrentPageIndex = pageIndex;
        return CurrentPageItemsChanged.InvokeCallbacksAsync(this);
    }

    // Can be internal because this only needs to be called by QuickGrid itself, not any custom pagination UI components.
    internal Task SetTotalItemCountAsync(int totalItemCount)
    {
        TotalItemCount = totalItemCount;
        return TotalItemCountChanged.InvokeCallbacksAsync(this);
    }
}
