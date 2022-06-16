using System.Linq.Expressions;
using Microsoft.AspNetCore.Components.Rendering;

namespace Microsoft.AspNetCore.Components.QuickGrid;

public class PropertyColumn<TGridItem, TProp> : ColumnBase<TGridItem>, ISortBuilderColumn<TGridItem>
{
    private Expression<Func<TGridItem, TProp>>? _cachedProperty;
    private Func<TGridItem, string?>? _cellTextFunc;
    private GridSort<TGridItem>? _sortBuilder;

    [Parameter, EditorRequired] public Expression<Func<TGridItem, TProp>> Property { get; set; } = default!;
    [Parameter] public string? Format { get; set; }

    public GridSort<TGridItem>? SortBuilder => _sortBuilder;

    protected override void OnParametersSet()
    {
        if (_cachedProperty != Property)
        {
            _cachedProperty = Property;
            var compiledPropertyExpression = Property.Compile();

            if (!string.IsNullOrEmpty(Format))
            {
                if (typeof(IFormattable).IsAssignableFrom(typeof(TProp)))
                {
                    _cellTextFunc = item => ((IFormattable?)compiledPropertyExpression!(item))?.ToString(Format, null);

                }
                else
                {
                    throw new InvalidOperationException($"A '{nameof(Format)}' parameter was supplied, but the type '{typeof(TProp)}' does not implement '{typeof(IFormattable)}'.");
                }
            }
            else
            {
                _cellTextFunc = item => compiledPropertyExpression!(item)?.ToString();
            }

            _sortBuilder = GridSort<TGridItem>.ByAscending(Property);
        }

        if (Title is null && _cachedProperty.Body is MemberExpression memberExpression)
        {
            Title = memberExpression.Member.Name;
        }
    }

    protected internal override void CellContent(RenderTreeBuilder builder, TGridItem item)
        => builder.AddContent(0, _cellTextFunc!(item));
}
