using Microsoft.AspNetCore.Components.QuickGrid.Infrastructure;

namespace Microsoft.AspNetCore.Components.QuickGrid;

public class PaginationState
{
    public int ItemsPerPage { get; set; } = 10;
    public int CurrentPageIndex { get; private set; }
    public int? TotalItemCount { get; private set; }
    public int? LastPageIndex => (TotalItemCount - 1) / ItemsPerPage;

    internal EventCallbackSubscribable<PaginationState> CurrentPageItemsChanged { get; } = new();
    internal EventCallbackSubscribable<PaginationState> TotalItemCountChanged { get; } = new();

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

        if (CurrentPageIndex > 0 && CurrentPageIndex > LastPageIndex)
        {
            // If the number of items has reduced such that the current page index is no longer valid, move
            // automatically to the final valid page index and trigger a further data load.
            return SetCurrentPageIndexAsync(LastPageIndex.Value);
        }
        else
        {
            // Under normal circumstances, we just want any associated pagination UI to update
            return TotalItemCountChanged.InvokeCallbacksAsync(this);
        }
    }
}
