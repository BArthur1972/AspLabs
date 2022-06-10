using Microsoft.AspNetCore.Components.Rendering;

namespace Microsoft.AspNetCore.Components.QuickGrid;

public class TemplateColumn<TGridItem> : ColumnBase<TGridItem>
{
    private readonly static RenderFragment<TGridItem> EmptyChildContent = _ => builder => { };

    [Parameter] public RenderFragment<TGridItem> ChildContent { get; set; } = EmptyChildContent;
    [Parameter] public Func<IQueryable<TGridItem>, SortBy<TGridItem>>? SortBy { get; set; }

    protected internal override IQueryable<TGridItem> ApplyColumnSort(IQueryable<TGridItem> source, bool ascending)
        => SortBy == null ? source : SortBy(source).Apply(source, ascending);

    protected internal override void CellContent(RenderTreeBuilder builder, TGridItem item)
        => builder.AddContent(0, ChildContent(item));

    protected override bool SupportsSorting()
        => SortBy is not null;
}
