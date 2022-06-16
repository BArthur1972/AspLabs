using System.Linq.Expressions;

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
    private const string ExpressionNotRepresentableMessage = "The supplied expression can't be represented as a property name for sorting. Only simple member expressions, such as @(x => x.SomeProperty), can be converted to property names.";

    private Func<IQueryable<T>, bool, IOrderedQueryable<T>> _first;
    private Func<IOrderedQueryable<T>, bool, IOrderedQueryable<T>>[] _then = new Func<IOrderedQueryable<T>, bool, IOrderedQueryable<T>>[10];

    private (LambdaExpression, bool) _firstExpression;
    private (LambdaExpression, bool)[] _thenExpressions = new (LambdaExpression, bool)[10];

    internal SortBy(Func<IQueryable<T>, bool, IOrderedQueryable<T>> first, (LambdaExpression, bool) firstExpression)
    {
        _first = first;
        _firstExpression = firstExpression;
        _count = 0;
    }

    public static SortBy<T> Ascending<U>(Expression<Func<T, U>> expression)
        => new SortBy<T>((queryable, asc) => asc ? queryable.OrderBy(expression) : queryable.OrderByDescending(expression),
            (expression, true));

    public static SortBy<T> Descending<U>(Expression<Func<T, U>> expression)
        => new SortBy<T>((queryable, asc) => asc ? queryable.OrderByDescending(expression) : queryable.OrderBy(expression),
            (expression, false));

    public SortBy<T> ThenAscending<U>(Expression<Func<T, U>> expression)
    {
        _then[_count++] = (queryable, asc) => asc ? queryable.ThenBy(expression) : queryable.ThenByDescending(expression);
        _thenExpressions[_count] = (expression, true);
        return this;
    }

    public SortBy<T> ThenDescending<U>(Expression<Func<T, U>> expression)
    {
        _then[_count++] = (queryable, asc) => asc ? queryable.ThenByDescending(expression) : queryable.ThenBy(expression);
        _thenExpressions[_count] = (expression, false);
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
        var result = new List<(string, SortDirection)>();
        result.Add((ToPropertyName(_firstExpression.Item1), (_firstExpression.Item2 ^ ascending) ? SortDirection.Descending : SortDirection.Ascending));
        for (var i = 0; i < _count; i++)
        {
            result.Add((ToPropertyName(_thenExpressions[i].Item1), (_thenExpressions[i].Item2 ^ ascending) ? SortDirection.Descending : SortDirection.Ascending));
        }

        return result;
    }

    // Not sure we really want this level of complexity, but it converts expressions like @(c => c.Medals.Gold) to "Medals.Gold"
    private static string ToPropertyName(LambdaExpression expression)
    {
        var body = expression.Body as MemberExpression;
        if (body is null)
        {
            throw new ArgumentException(ExpressionNotRepresentableMessage);
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
                throw new ArgumentException(ExpressionNotRepresentableMessage);
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
