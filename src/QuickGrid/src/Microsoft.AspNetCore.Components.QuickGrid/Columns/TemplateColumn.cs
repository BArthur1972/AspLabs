using Microsoft.AspNetCore.Components.Rendering;

namespace Microsoft.AspNetCore.Components.QuickGrid;

public class TemplateColumn<TGridItem> : ColumnBase<TGridItem>, ISortBuilderColumn<TGridItem>
{
    private readonly static RenderFragment<TGridItem> EmptyChildContent = _ => builder => { };

    [Parameter] public RenderFragment<TGridItem> ChildContent { get; set; } = EmptyChildContent;
    [Parameter] public Func<IEnumerable<TGridItem>, SortBy<TGridItem>>? SortBy { get; set; }

    public SortBy<TGridItem>? SortBuilder => SortBy is null ? null : SortBy(null!);

    protected internal override void CellContent(RenderTreeBuilder builder, TGridItem item)
        => builder.AddContent(0, ChildContent(item));

    protected override bool IsSortableByDefault()
        => SortBy is not null;
}