using Microsoft.AspNetCore.Components.QuickGrid.Infrastructure;
using Microsoft.AspNetCore.Components.Rendering;

namespace Microsoft.AspNetCore.Components.QuickGrid;

public abstract class PaginatorBase<TGridItem> : ComponentBase
{
    [CascadingParameter] internal InternalGridContext<TGridItem> InternalGridContext { get; set; } = default!;

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        InternalGridContext.Grid.SetPaginator(this);
    }
}
