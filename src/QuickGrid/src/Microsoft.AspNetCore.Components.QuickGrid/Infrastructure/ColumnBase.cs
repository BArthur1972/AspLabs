using Microsoft.AspNetCore.Components.Rendering;

namespace Microsoft.AspNetCore.Components.QuickGrid.Infrastructure;

public abstract class ColumnBase<TGridItem> : ComponentBase
{
    private readonly static RenderFragment<TGridItem> EmptyChildContent = _ => builder => { };

    [CascadingParameter] internal InternalGridContext<TGridItem> InternalGridContext { get; set; } = default!;

    [Parameter] public string? Title { get; set; }
    [Parameter] public string? Class { get; set; }
    [Parameter] public Align Align { get; set; }
    [Parameter] public RenderFragment<ColumnBase<TGridItem>>? HeaderTemplate { get; set; }
    [Parameter] public RenderFragment? ColumnOptions { get; set; }

    internal RenderFragment HeaderContent { get; }

    protected internal RenderFragment<TGridItem> CellContent { get; protected set; } = EmptyChildContent;

    public ColumnBase()
    {
        HeaderContent = __builder => __builder.AddContent(0, Title);
    }

    public QuickGrid<TGridItem> Grid => InternalGridContext.Grid;

    internal virtual bool CanSort => false;

    internal virtual IQueryable<TGridItem> GetSortedItems(IQueryable<TGridItem> source, bool ascending) => source;

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        InternalGridContext.Grid.AddColumn(this);
    }
}
