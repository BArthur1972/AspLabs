using System.Linq.Expressions;
using System.Text;

namespace Microsoft.AspNetCore.Components.QuickGrid;

public interface ISortBuilderColumn<TGridItem>
{
    public SortBy<TGridItem>? SortBuilder { get; }
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

    internal IReadOnlyCollection<(string PropertyName, SortDirection Direction)> ToPropertyList(bool ascending)
    {
        // TODO: Throw if any of the expressions were not representable as property names
        var result = new List<(string, SortDirection)>();
        result.Add((_firstProperty.Item1!, (_firstProperty.Item2 ^ ascending) ? SortDirection.Descending : SortDirection.Ascending));
        for (var i = 0; i < _count; i++)
        {
            result.Add((_thenProperties[i].Item1!, (_thenProperties[i].Item2 ^ ascending) ? SortDirection.Descending : SortDirection.Ascending));
        }

        return result;
    }

    // Not sure we really want this level of complexity, but it converts expressions like @(c => c.Medals.Gold) to "Medals.Gold"
    // TODO: Don't do all this expression walking and string building up front. Just store the Expression object. We can do all this
    // computation later if the developer calls ToPropertyList().
    private static string? ToPropertyName<U, V>(Expression<Func<U, V>> expression)
    {
        var body = expression.Body as MemberExpression;
        if (body is null)
        {
            return null;
        }

        if (body.Expression is ParameterExpression)
        {
            return body.Member.Name;
        }

        var length = body.Member.Name.Length;
        var node = body;
        while (node.Expression is not null)
        {
            if (node.Expression is MemberExpression parentMember)
            {
                length += parentMember.Member.Name.Length + 1;
                node = parentMember;
            }
            else if (node.Expression is ParameterExpression)
            {
                break;
            }
            else
            {
                // Not representable
                return null;
            }
        }

        return string.Create(length, body, (chars, body) =>
        {
            var nextPos = chars.Length;
            while (body is not null)
            {
                nextPos -= body.Member.Name.Length;
                body.Member.Name.CopyTo(chars.Slice(nextPos));
                if (nextPos > 0)
                {
                    chars[--nextPos] = '.';
                }
                body = (body.Expression as MemberExpression)!;
            }
        });
    }
}

public enum Align
{
    Left,
    Center,
    Right,
}
