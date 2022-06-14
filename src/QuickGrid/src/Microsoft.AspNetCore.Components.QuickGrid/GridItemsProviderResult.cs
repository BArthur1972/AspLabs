namespace Microsoft.AspNetCore.Components.QuickGrid;

public struct GridItemsProviderResult<TGridItem>
{
    public ICollection<TGridItem> Items { get; }
    public int TotalItemCount { get; }

    public GridItemsProviderResult(ICollection<TGridItem> items, int totalItemCount)
    {
        Items = items;
        TotalItemCount = totalItemCount;
    }
}

public static class GridItemsProviderResult
{
    public static GridItemsProviderResult<T> From<T>(ICollection<T> items, int totalItemCount)
        => new GridItemsProviderResult<T>(items, totalItemCount);
}
