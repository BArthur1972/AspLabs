namespace Microsoft.AspNetCore.Components.QuickGrid;

public struct GridItemsProviderRequest<TGridItem>
{
    public int StartIndex { get; }
    public int? Count { get; }
    public ColumnBase<TGridItem>? SortByColumn { get; }
    public bool SortByAscending { get; }
    public CancellationToken CancellationToken { get; }

    internal GridItemsProviderRequest(
        int startIndex, int? count, ColumnBase<TGridItem>? sortByColumn, bool sortByAscending,
        CancellationToken cancellationToken)
    {
        StartIndex = startIndex;
        Count = count;
        SortByColumn = sortByColumn;
        SortByAscending = sortByAscending;
        CancellationToken = cancellationToken;
    }
}

public delegate ValueTask<GridItemsProviderResult<TGridItem>> GridItemsProvider<TGridItem>(
    GridItemsProviderRequest<TGridItem> request);
