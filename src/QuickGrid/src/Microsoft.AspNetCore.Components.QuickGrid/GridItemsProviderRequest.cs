namespace Microsoft.AspNetCore.Components.QuickGrid;

public struct GridItemsProviderRequest<TGridItem>
{
    public int StartIndex { get; }
    public int? Count { get; }
    public SortBy<TGridItem>? SortBy { get; }
    public CancellationToken CancellationToken { get; }

    public GridItemsProviderRequest(
        int startIndex, int? count, SortBy<TGridItem>? sortBy, CancellationToken cancellationToken)
    {
        StartIndex = startIndex;
        Count = count;
        SortBy = sortBy;
        CancellationToken = cancellationToken;
    }
}

public delegate ValueTask<GridItemsProviderResult<TGridItem>> GridItemsProvider<TGridItem>(
    GridItemsProviderRequest<TGridItem> request);
