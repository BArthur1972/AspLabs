namespace Microsoft.AspNetCore.Components.QuickGrid;

public interface ISortBuilderColumn<TGridItem>
{
    public GridSort<TGridItem>? SortBuilder { get; }
}
