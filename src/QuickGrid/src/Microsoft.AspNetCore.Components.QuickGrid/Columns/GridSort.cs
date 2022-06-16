using System.Linq.Expressions;

namespace Microsoft.AspNetCore.Components.QuickGrid;

public class GridSort<T>
{
    private const string ExpressionNotRepresentableMessage = "The supplied expression can't be represented as a property name for sorting. Only simple member expressions, such as @(x => x.SomeProperty), can be converted to property names.";

    private Func<IQueryable<T>, bool, IOrderedQueryable<T>> _first;
    private List<Func<IOrderedQueryable<T>, bool, IOrderedQueryable<T>>>? _then;

    private (LambdaExpression, bool) _firstExpression;
    private List<(LambdaExpression, bool)>? _thenExpressions;

    private IReadOnlyCollection<(string PropertyName, SortDirection Direction)>? _cachedPropertyListAscending;
    private IReadOnlyCollection<(string PropertyName, SortDirection Direction)>? _cachedPropertyListDescending;

    internal GridSort(Func<IQueryable<T>, bool, IOrderedQueryable<T>> first, (LambdaExpression, bool) firstExpression)
    {
        _first = first;
        _firstExpression = firstExpression;
        _then = default;
        _thenExpressions = default;
    }

    public static GridSort<T> ByAscending<U>(Expression<Func<T, U>> expression)
        => new GridSort<T>((queryable, asc) => asc ? queryable.OrderBy(expression) : queryable.OrderByDescending(expression),
            (expression, true));

    public static GridSort<T> ByDescending<U>(Expression<Func<T, U>> expression)
        => new GridSort<T>((queryable, asc) => asc ? queryable.OrderByDescending(expression) : queryable.OrderBy(expression),
            (expression, false));

    public GridSort<T> ThenAscending<U>(Expression<Func<T, U>> expression)
    {
        _then ??= new();
        _thenExpressions ??= new();
        _then.Add((queryable, asc) => asc ? queryable.ThenBy(expression) : queryable.ThenByDescending(expression));
        _thenExpressions.Add((expression, true));
        _cachedPropertyListAscending = null;
        _cachedPropertyListDescending = null;
        return this;
    }

    public GridSort<T> ThenDescending<U>(Expression<Func<T, U>> expression)
    {
        _then ??= new();
        _thenExpressions ??= new();
        _then.Add((queryable, asc) => asc ? queryable.ThenByDescending(expression) : queryable.ThenBy(expression));
        _thenExpressions.Add((expression, false));
        _cachedPropertyListAscending = null;
        _cachedPropertyListDescending = null;
        return this;
    }

    internal IOrderedQueryable<T> Apply(IQueryable<T> queryable, bool ascending)
    {
        var orderedQueryable = _first(queryable, ascending);

        if (_then is not null)
        {
            foreach (var clause in _then)
            {
                orderedQueryable = clause(orderedQueryable, ascending);
            }
        }

        return orderedQueryable;
    }

    internal IReadOnlyCollection<(string PropertyName, SortDirection Direction)> ToPropertyList(bool ascending)
    {
        if (ascending)
        {
            _cachedPropertyListAscending ??= BuildPropertyList(ascending: true);
            return _cachedPropertyListAscending;
        }
        else
        {
            _cachedPropertyListDescending ??= BuildPropertyList(ascending: false);
            return _cachedPropertyListDescending;
        }
    }

    private IReadOnlyCollection<(string PropertyName, SortDirection Direction)> BuildPropertyList(bool ascending)
    {
        var result = new List<(string, SortDirection)>();
        result.Add((ToPropertyName(_firstExpression.Item1), (_firstExpression.Item2 ^ ascending) ? SortDirection.Descending : SortDirection.Ascending));

        if (_thenExpressions is not null)
        {
            foreach (var (thenLambda, thenAscending) in _thenExpressions)
            {
                result.Add((ToPropertyName(thenLambda), (thenAscending ^ ascending) ? SortDirection.Descending : SortDirection.Ascending));
            }
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
