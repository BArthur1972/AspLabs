using System.Linq.Expressions;

namespace Microsoft.AspNetCore.Components.QuickGrid;

public interface ISortBuilderColumn<TGridItem>
{
    SortBy<TGridItem>? SortBuilder { get; }
}

public static class GridSortExtensions
{
    public static SortBy<T> SortByAscending<T, U>(this IEnumerable<T> _, Expression<Func<T, U>> keySelector)
        => SortBy<T>.Ascending(keySelector);

    public static SortBy<T> SortByDescending<T, U>(this IEnumerable<T> _, Expression<Func<T, U>> keySelector)
        => SortBy<T>.Descending(keySelector);
}

public struct SortBy<T>
{
    private int _count;

    private Func<IQueryable<T>, bool, IOrderedQueryable<T>> _first;
    private Func<IOrderedQueryable<T>, bool, IOrderedQueryable<T>>[] _then = new Func<IOrderedQueryable<T>, bool, IOrderedQueryable<T>>[10];

    private (string?, bool) _firstProperty;
    private (string?, bool)[] _thenProperties = new (string?, bool)[10];

    internal SortBy(Func<IQueryable<T>, bool, IOrderedQueryable<T>> first, (string?, bool) firstProperty)
    {
        _first = first;
        _firstProperty = firstProperty;
        _count = 0;
    }

    public static SortBy<T> Ascending<U>(Expression<Func<T, U>> expression)
        => new SortBy<T>((queryable, asc) => asc ? queryable.OrderBy(expression) : queryable.OrderByDescending(expression),
            (ToPropertyName(expression), true));

    public static SortBy<T> Descending<U>(Expression<Func<T, U>> expression)
        => new SortBy<T>((queryable, asc) => asc ? queryable.OrderByDescending(expression) : queryable.OrderBy(expression),
            (ToPropertyName(expression), false));

    public SortBy<T> ThenAscending<U>(Expression<Func<T, U>> expression)
    {
        _then[_count++] = (queryable, asc) => asc ? queryable.ThenBy(expression) : queryable.ThenByDescending(expression);
        _thenProperties[_count] = (ToPropertyName(expression), true);
        return this;
    }

    public SortBy<T> ThenDescending<U>(Expression<Func<T, U>> expression)
    {
        _then[_count++] = (queryable, asc) => asc ? queryable.ThenByDescending(expression) : queryable.ThenBy(expression);
        _thenProperties[_count] = (ToPropertyName(expression), false);
        return this;
    }

    internal IOrderedQueryable<T> Apply(IQueryable<T> queryable, bool ascending)
    {
        var orderedQueryable = _first(queryable, ascending);
        for (var i = 0; i < _count; i++)
        {
            orderedQueryable = _then[i](orderedQueryable, ascending);
        }
        return orderedQueryable;
    }

    public IReadOnlyCollection<(string PropertyName, bool Ascending)> ToPropertyList()
    {
        // TODO: Throw if any of the expressions were not representable as property names
        var result = new List<(string, bool)>();
        result.Add((_firstProperty.Item1!, _firstProperty.Item2));
        for (var i = 0; i < _count; i++)
        {
            result.Add((_thenProperties[i].Item1!, _thenProperties[i].Item2));
        }

        return result;
    }

    private static string? ToPropertyName<U, V>(Expression<Func<U, V>> expression)
        => expression.Body switch
        {
            MemberExpression m => m.Member.Name,
            _ => null
        };
}

public enum Align
{
    Left,
    Center,
    Right,
}
