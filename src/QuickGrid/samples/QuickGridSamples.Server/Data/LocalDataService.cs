using Microsoft.EntityFrameworkCore;
using QuickGridSamples.Core.Models;

namespace QuickGridSamples.Server.Data;

public class LocalDataService : IDataService
{
    private readonly ApplicationDbContext _dbContext;

    public LocalDataService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<IQueryable<Country>> GetCountriesAsync()
    {
        return Task.FromResult(_dbContext.Countries.AsQueryable());
    }

    public async Task<ICollection<Country>> GetCountriesAsync(int startIndex, int? count, CancellationToken cancellationToken)
    {
        var result = _dbContext.Countries.Skip(startIndex);

        if (count.HasValue)
        {
            result = result.Take(count.Value);
        }

        return await result.ToListAsync(cancellationToken);
    }
}
