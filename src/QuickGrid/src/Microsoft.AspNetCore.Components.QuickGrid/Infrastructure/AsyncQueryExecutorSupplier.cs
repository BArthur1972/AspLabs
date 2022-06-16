using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.AspNetCore.Components.QuickGrid.Infrastructure;

internal static class AsyncQueryExecutorSupplier
{
    public static IAsyncQueryExecutor? GetAsyncQueryExecutor<T>(IServiceProvider services, IQueryable<T>? queryable)
    {
        if (queryable is not null)
        {
            var executor = services.GetService<IAsyncQueryExecutor>();

            if (executor is null)
            {
                if (IsEntityFrameworkQueryable(queryable))
                {
                    throw new InvalidOperationException($"The supplied {nameof(IQueryable)} is provided by Entity Framework. To query it efficiently, you must reference the package Microsoft.AspNetCore.Components.QuickGrid.EntityFrameworkAdapter and call AddQuickGridEntityFrameworkAdapter on your service collection.");
                }
            }
            else if (executor.IsSupported(queryable))
            {
                return executor;
            }
        }

        return null;
    }

    private static bool IsEntityFrameworkQueryable<T>(IQueryable<T> queryable)
        => queryable.Provider?.GetType().GetInterfaces().Any(x => string.Equals(x.FullName, "Microsoft.EntityFrameworkCore.Query.IAsyncQueryProvider")) == true;
}
