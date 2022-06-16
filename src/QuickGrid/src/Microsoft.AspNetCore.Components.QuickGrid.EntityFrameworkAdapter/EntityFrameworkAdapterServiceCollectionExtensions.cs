using Microsoft.AspNetCore.Components.QuickGrid.EntityFrameworkAdapter;
using Microsoft.AspNetCore.Components.QuickGrid.Infrastructure;

namespace Microsoft.Extensions.DependencyInjection;

public static class EntityFrameworkAdapterServiceCollectionExtensions
{
    public static void AddQuickGridEntityFrameworkAdapter(this IServiceCollection services)
    {
        services.AddSingleton<IAsyncQueryExecutor, EntityFrameworkAsyncQueryExecutor>();
    }
}
