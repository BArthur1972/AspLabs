using Microsoft.AspNetCore.Components.QuickGrid.Infrastructure;

namespace Microsoft.AspNetCore.Components.QuickGrid;

public class PaginationState
{
    internal EventCallbackSubscribable<PaginationState> CurrentPageItemsChanged { get; } = new();
    internal EventCallbackSubscribable<PaginationState> TotalItemCountChanged { get; } = new();

    public int ItemsPerPage { get; set; } = 10;
    public int CurrentPageIndex { get; set; }
    public int? TotalItemCount { get; private set; }

    public int? LastPageIndex => TotalItemCount / ItemsPerPage;

    public IQueryable<T> ApplyPagination<T>(IQueryable<T> source)
    {
        return source.Skip(CurrentPageIndex * ItemsPerPage).Take(ItemsPerPage);
    }

    public override int GetHashCode()
        => HashCode.Combine(ItemsPerPage, CurrentPageIndex, TotalItemCount);

    internal async Task SetTotalItemCount(int totalItemCount)
    {
        TotalItemCount = totalItemCount;
        await TotalItemCountChanged.InvokeCallbacksAsync(this);
    }
}
