using Microsoft.AspNetCore.Components.Rendering;

namespace Microsoft.AspNetCore.Components.QuickGrid;

public class TemplateColumn<TGridItem> : ColumnBase<TGridItem>, ISortBuilderColumn<TGridItem>
{
    private readonly static RenderFragment<TGridItem> EmptyChildContent = _ => builder => { };

    [Parameter] public RenderFragment<TGridItem> ChildContent { get; set; } = EmptyChildContent;
    [Parameter] public GridSort<TGridItem>? SortBy { get; set; }

    GridSort<TGridItem>? ISortBuilderColumn<TGridItem>.SortBuilder => SortBy;

    protected internal override void CellContent(RenderTreeBuilder builder, TGridItem item)
        => builder.AddContent(0, ChildContent(item));

    protected override bool IsSortableByDefault()
        => SortBy is not null;
}
