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

    public IQueryable<TGridItem> ApplySorting(IQueryable<TGridItem> source) => SortByColumn switch
    {
        ISortBuilderColumn<TGridItem> sbc => sbc.SortBuilder?.Apply(source, SortByAscending) ?? source,
        null => source,
        _ => throw new NotSupportedException(ColumnNotSortableMessage(SortByColumn)),
    };

    public IReadOnlyCollection<(string PropertyName, SortDirection Direction)> GetSortByProperties() => SortByColumn switch
    {
        ISortBuilderColumn<TGridItem> sbc => sbc.SortBuilder?.ToPropertyList(SortByAscending) ?? Array.Empty<(string, SortDirection)>(),
        null => Array.Empty<(string, SortDirection)>(),
        _ => throw new NotSupportedException(ColumnNotSortableMessage(SortByColumn)),
    };

    private static string ColumnNotSortableMessage<T>(ColumnBase<T> col)
        => $"The current sort column is of type '{col.GetType().FullName}', which does not implement {nameof(ISortBuilderColumn<TGridItem>)}, so its sorting rules cannot be applied automatically.";
}

public delegate ValueTask<GridItemsProviderResult<TGridItem>> GridItemsProvider<TGridItem>(
    GridItemsProviderRequest<TGridItem> request);
