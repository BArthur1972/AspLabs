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
    [Inject] private IServiceProvider Services { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private Virtualize<(int, TGridItem)>? _virtualizeComponent;
    private List<ColumnBase<TGridItem>> _columns;
    private ColumnBase<TGridItem>? _sortByColumn;
    private ColumnBase<TGridItem>? _displayOptionsForColumn;
    private bool _checkColumnOptionsPosition;
    private bool _sortByAscending;
    private int _ariaBodyRowCount;
    private IJSObjectReference? _jsModule;
    private IJSObjectReference? _jsEventDisposable;
    private ElementReference _tableReference;
    private InternalGridContext<TGridItem> _internalGridContext;
    private IAsyncQueryExecutor? _asyncQueryExecutor;
    private readonly EventCallbackSubscriber<PaginationState> _currentPageItemsChanged;
    private readonly RenderFragment _renderColumnHeaders; // Cache of method->delegate conversion
    private readonly RenderFragment _renderNonVirtualizedRows; // Cache of method->delegate conversion

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
        _renderColumnHeaders = RenderColumnHeaders;
        _renderNonVirtualizedRows = RenderNonVirtualizedRows;
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
        else
        {
            var startIndex = Pagination is null ? 0 : (Pagination.CurrentPageIndex * Pagination.ItemsPerPage);
            var request = new GridItemsProviderRequest<TGridItem>(
                startIndex, Pagination?.ItemsPerPage, _sortByColumn, _sortByAscending, thisLoadCts.Token);
            var result = await ResolveItemsRequestAsync(request);
            if (!thisLoadCts.IsCancellationRequested)
            {
                // See below
                _currentNonVirtualizedViewItems = result.Items;
                _ariaBodyRowCount = _currentNonVirtualizedViewItems.Count;
                Pagination?.SetTotalItemCountAsync(result.TotalItemCount);
                _pendingDataLoadCancellationTokenSource = null;
            }
        }

        // Depending on whether the RefreshDataAsync is being chained into an event handler that in turn will
        // re-render the grid anyway, we may or may not need to queue a re-render here. To avoid redundant
        // re-rendering, we'll defer the "data has loaded" re-rendering until after a Task.Yield.
        _ = StateHasChangedDeferredAsync(); // Not expected to throw
    }

    private async ValueTask<ItemsProviderResult<(int, TGridItem)>> ProvideVirtualizedItems(ItemsProviderRequest request)
    {
        // Debounce the requests. This eliminates a lot of redundant queries at the cost of slight lag after interactions.
        // TODO: Make this smarter
        //  - On the very first data request, don't delay at all (otherwise we're hurting prerendering)
        //  - After that,
        //    - Use 200ms as the default "short scrolling" debounce period, but if you make a second data load request during
        //      that time, we switch into "long scrolling" mode where the debounce period is 500ms
        //    - Switch back to "short scrolling" mode once some request actually completes
        await Task.Delay(100);
        if (request.CancellationToken.IsCancellationRequested)
        {
            return default;
        }

        var startIndex = request.StartIndex;
        var count = request.Count;
        if (Pagination is not null)
        {
            startIndex += Pagination.CurrentPageIndex * Pagination.ItemsPerPage;
            count = Math.Min(request.Count, Pagination.ItemsPerPage - request.StartIndex);
        }

        var providerRequest = new GridItemsProviderRequest<TGridItem>(
            startIndex, count, _sortByColumn, _sortByAscending, request.CancellationToken);
        var providerResult = await ResolveItemsRequestAsync(providerRequest);
        if (!request.CancellationToken.IsCancellationRequested)
        {
            // This method gets called directly by the Virtualize child, and doesn't implicitly re-render the QuickGrid component
            // So, if during this process we discover that something about the data has changed, we may have to cause the QuickGrid
            // container to re-render manually. Note that the logic for calculating the body row count when paging and virtualizing
            // together may need to be changed to be (TotalItemCount % ItemsPerPage) when on the last page.
            var newBodyRowCount = Pagination is null ? providerResult.TotalItemCount : Pagination.ItemsPerPage;
            var bodyRowCountChanged = newBodyRowCount != _ariaBodyRowCount;
            _ariaBodyRowCount = newBodyRowCount;
            if (bodyRowCountChanged)
            {
                _ = StateHasChangedDeferredAsync(); // Not expected to throw
            }

            Pagination?.SetTotalItemCountAsync(providerResult.TotalItemCount);
            return new ItemsProviderResult<(int, TGridItem)>(
                 items: providerResult.Items.Select((x, i) => ValueTuple.Create(i + request.StartIndex + 2, x)),
                 totalItemCount: _ariaBodyRowCount);
        }

        return default;
    }

    private async ValueTask<GridItemsProviderResult<TGridItem>> ResolveItemsRequestAsync(GridItemsProviderRequest<TGridItem> request)
    {
        if (ItemsProvider is not null)
        {
            return await ItemsProvider(request);
        }
        else if (Items is not null)
        {
            var totalItemCount = _asyncQueryExecutor is null ? Items.Count() : await _asyncQueryExecutor.CountAsync(Items);
            var result = request.ApplySorting(Items).Skip(request.StartIndex);
            if (request.Count.HasValue)
            {
                result = result.Take(request.Count.Value);
            }
            var resultArray = _asyncQueryExecutor is null ? result.ToArray() : await _asyncQueryExecutor.ToArrayAsync(result);
            return GridItemsProviderResult.From(resultArray, totalItemCount);
        }
        else
        {
            return GridItemsProviderResult.From(Array.Empty<TGridItem>(), 0);
        }
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
            // It's legal to supply neither Items nor ItemsProvider, because that's convenient when you're acquiring
            // Items asynchronously but don't have it yet. In this case we just render zero items.
            dataSourceHasChanged = _lastAssignedItemsOrProvider is not null;
        }

        if (dataSourceHasChanged)
        {
            _asyncQueryExecutor = AsyncQueryExecutorSupplier.GetAsyncQueryExecutor(Services, Items);
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
