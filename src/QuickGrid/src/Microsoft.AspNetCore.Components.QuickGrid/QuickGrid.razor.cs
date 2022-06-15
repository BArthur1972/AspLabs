using Microsoft.AspNetCore.Components.QuickGrid.Infrastructure;
using Microsoft.AspNetCore.Components.Web.Virtualization;
using Microsoft.JSInterop;

namespace Microsoft.AspNetCore.Components.QuickGrid;

[CascadingTypeParameter(nameof(TGridItem))]
public partial class QuickGrid<TGridItem> : IAsyncDisposable
{
    [Parameter] public IQueryable<TGridItem>? Items { get; set; }
    [Parameter] public GridItemsProvider<TGridItem>? ItemsProvider { get; set; }
    [Parameter] public string? Class { get; set; }
    [Parameter] public string? Theme { get; set; } = "default";
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public bool Virtualize { get; set; }
    [Parameter] public bool ResizableColumns { get; set; }
    [Parameter] public float ItemSize { get; set; } = 50;
    [Parameter] public Func<TGridItem, object> ItemKey { get; set; } = x => x;
    [Parameter] public PaginationState? Pagination { get; set; }
    [Inject] private IJSRuntime JS { get; set; }

    private Virtualize<(int, TGridItem)>? _virtualizeComponent;
    private List<ColumnBase<TGridItem>> _columns;
    private ColumnBase<TGridItem>? _sortByColumn;
    private ColumnBase<TGridItem>? _displayOptionsForColumn;
    private bool _checkColumnOptionsPosition;
    private bool _sortByAscending;
    private int _rowCount;
    private IJSObjectReference? _jsModule;
    private IJSObjectReference? _jsEventDisposable;
    private ElementReference _tableReference;
    private InternalGridContext<TGridItem> _internalGridContext;
    private readonly EventCallbackSubscriber<PaginationState> _currentPageItemsChanged;

    // This is the final filtered and sorted data to be rendered.
    // We update it asynchronously each time the sort order changes, or if an external
    // component like Paginator tells us to do so. We only render after this has been updated.
    private ICollection<TGridItem> _currentNonVirtualizedViewItems = Array.Empty<TGridItem>();
    private int _lastRefreshedPaginationStateHash;
    private object? _lastAssignedItemsOrProvider;
    private CancellationTokenSource? _pendingDataLoadCancellationTokenSource;

    private bool _needsDeferredRender;

    public QuickGrid()
    {
        _columns = new();
        _internalGridContext = new(this);
        _currentPageItemsChanged = new(EventCallback.Factory.Create<PaginationState>(this, RefreshDataAsync));
    }

    public async Task RefreshDataAsync()
    {
        _pendingDataLoadCancellationTokenSource?.Cancel();
        var thisLoadCts = _pendingDataLoadCancellationTokenSource = new CancellationTokenSource();
        _lastRefreshedPaginationStateHash = Pagination?.GetHashCode() ?? 0;

        // Querying may be expensive (especially if it's not in-memory data), so we only update
        // the currently-rendered row data when explicitly asked, or when an event occurs that
        // specifically changes the data, such as sorting.
        if (_virtualizeComponent is not null)
        {
            // This will call back into ProvideVirtualizedItems
            await _virtualizeComponent.RefreshDataAsync();
            _pendingDataLoadCancellationTokenSource = null;
        }
        else if (ItemsProvider is not null)
        {
            var startIndex = Pagination is null ? 0 : (Pagination.CurrentPageIndex * Pagination.ItemsPerPage);
            var count = Pagination?.ItemsPerPage;
            var result = await ItemsProvider(new GridItemsProviderRequest<TGridItem>(
                startIndex, count, _sortByColumn, _sortByAscending, thisLoadCts.Token));
            if (!thisLoadCts.IsCancellationRequested)
            {
                // See below
                _currentNonVirtualizedViewItems = result.Items;
                _rowCount = result.TotalItemCount; // TODO: Need to distinguish "total row count" from "rows currently being rendered (when paginated)" as ARIA wants the former when virtualized and latter when not
                Pagination?.SetTotalItemCountAsync(result.TotalItemCount);
                _pendingDataLoadCancellationTokenSource = null;
            }
        }
        else if (Items is not null)
        {
            throw new NotImplementedException("IQueryable was removed. TODO: Put back");
        }

        // Depending on whether the RefreshDataAsync is being chained into an event handler that in turn will
        // re-render the grid anyway, we may or may not need to queue a re-render here. To avoid redundant
        // re-rendering, we'll defer the "data has loaded" re-rendering until after a Task.Yield.
        _ = StateHasChangedDeferredAsync(); // Not expected to throw
    }

    // Invoked by descendant columns at a special time during rendering
    internal void AddColumn(ColumnBase<TGridItem> column, SortDirection? isDefaultSortDirection)
    {
        _columns.Add(column);

        if (_sortByColumn is null && isDefaultSortDirection.HasValue)
        {
            _sortByColumn = column;
            _sortByAscending = isDefaultSortDirection.Value != SortDirection.Descending;
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _jsModule = await JS.InvokeAsync<IJSObjectReference>("import", "./_content/Microsoft.AspNetCore.Components.QuickGrid/QuickGrid.razor.js");
            _jsEventDisposable = await _jsModule.InvokeAsync<IJSObjectReference>("init", _tableReference);
        }

        if (_checkColumnOptionsPosition && _displayOptionsForColumn is not null)
        {
            _checkColumnOptionsPosition = false;
            _ = _jsModule?.InvokeVoidAsync("checkColumnOptionsPosition", _tableReference);
        }
    }

    protected override async Task OnInitializedAsync()
    {
        // We need to delay the first data query (triggered from OnParametersSetAsync) until after the first
        // render, so that we have an initial set of columns. If we didn't, it wouldn't be possible to have a
        // default sort column take effect on the first data query. This is still compatible with prerendering.
        await Task.Yield();
    }

    protected override Task OnParametersSetAsync()
    {
        _currentPageItemsChanged.SubscribeOrMove(Pagination?.CurrentPageItemsChanged);

        bool dataSourceHasChanged;

        if (Items is not null)
        {
            if (ItemsProvider is not null)
            {
                throw new InvalidOperationException($"{nameof(QuickGrid)} requires one of {nameof(Items)} or {nameof(ItemsProvider)}, but both were specified.");
            }

            dataSourceHasChanged = Items != _lastAssignedItemsOrProvider;
            _lastAssignedItemsOrProvider = Items;

        }
        else if (ItemsProvider is not null)
        {
            dataSourceHasChanged = ItemsProvider != (_lastAssignedItemsOrProvider as GridItemsProvider<TGridItem>);
            _lastAssignedItemsOrProvider = ItemsProvider;
        }
        else
        {
            throw new InvalidOperationException($"{nameof(QuickGrid)} requires one of {nameof(Items)} or {nameof(ItemsProvider)}, but neither were specified.");
        }

        var mustRefreshData = dataSourceHasChanged
            || (Pagination is not null && Pagination.GetHashCode() != _lastRefreshedPaginationStateHash);

        return mustRefreshData ? RefreshDataAsync() : Task.CompletedTask;
    }

    private string AriaSortValue(ColumnBase<TGridItem> column)
        => _sortByColumn == column
            ? (_sortByAscending ? "ascending" : "descending")
            : "none";

    private string? ColumnHeaderClass(ColumnBase<TGridItem> column)
        => _sortByColumn == column
        ? $"{ColumnClass(column)} {(_sortByAscending ? "col-sort-asc" : "col-sort-desc")}"
        : ColumnClass(column);

    private string GridClass()
        => $"quickgrid {Class} {(_pendingDataLoadCancellationTokenSource is null ? null : "loading")}";

    private string? ColumnClass(ColumnBase<TGridItem> column)
    {
        switch (column.Align)
        {
            case Align.Center: return $"col-justify-center {column.Class}";
            case Align.Right: return $"col-justify-end {column.Class}";
            default: return column.Class;
        }
    }

    public Task SortByColumnAsync(ColumnBase<TGridItem> column, SortDirection direction = SortDirection.Auto)
    {
        _sortByAscending = direction switch
        {
            SortDirection.Ascending => true,
            SortDirection.Descending => false,
            SortDirection.Auto => _sortByColumn == column ? !_sortByAscending : true,
            _ => throw new NotSupportedException($"Unknown sort direction {direction}"),
        };

        _sortByColumn = column;

        StateHasChanged();
        return RefreshDataAsync();
    }

    public void ShowColumnOptions(ColumnBase<TGridItem> column)
    {
        _displayOptionsForColumn = column;
        _checkColumnOptionsPosition = true;
        StateHasChanged();
    }

    private async ValueTask<ItemsProviderResult<(int, TGridItem)>> ProvideVirtualizedItems(ItemsProviderRequest request)
    {
        // Debounce the requests. This eliminates a lot of redundant queries at the cost of slight lag after interactions.
        // If you wanted, you could try to make it only debounce on the 2nd-and-later request within a cluster.
        await Task.Delay(20);
        if (request.CancellationToken.IsCancellationRequested)
        {
            return default;
        }

        if (Items is not null)
        {
            var records = _currentNonVirtualizedViewItems.Skip(request.StartIndex).Take(request.Count).AsEnumerable();
            _rowCount = Items.Count();
            var result = new ItemsProviderResult<(int, TGridItem)>(
                items: records.Select((x, i) => ValueTuple.Create(i + request.StartIndex + 2, x)),
                totalItemCount: _rowCount);
            return result;
        }
        else if (ItemsProvider is not null)
        {
            var gridRequest = new GridItemsProviderRequest<TGridItem>(
                request.StartIndex, request.Count, _sortByColumn, _sortByAscending, request.CancellationToken);
            var result = await ItemsProvider(gridRequest);
            _rowCount = result.TotalItemCount;
            return new ItemsProviderResult<(int, TGridItem)>(
                items: result.Items.Select((x, i) => ValueTuple.Create(i + request.StartIndex + 2, x)),
                totalItemCount: _rowCount);
        }
        else
        {
            return new ItemsProviderResult<(int, TGridItem)>(Enumerable.Empty<(int, TGridItem)>(), 0);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _currentPageItemsChanged.Dispose();

        try
        {
            if (_jsEventDisposable is not null)
            {
                await _jsEventDisposable.InvokeVoidAsync("stop");
                await _jsEventDisposable.DisposeAsync();
            }
            if (_jsModule is not null)
            {
                await _jsModule.DisposeAsync();
            }
        }
        catch
        {
        }
    }

    private void CloseColumnOptions()
    {
        _displayOptionsForColumn = null;
    }

    private async Task StateHasChangedDeferredAsync()
    {
        _needsDeferredRender = true;
        await Task.Yield();
        if (_needsDeferredRender)
        {
            StateHasChanged();
        }
    }
}
