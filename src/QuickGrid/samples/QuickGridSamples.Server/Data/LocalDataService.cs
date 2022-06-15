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

    public async Task<(ICollection<Country>, int)> GetCountriesAsync(int startIndex, int? count, string sortBy, bool sortAscending, CancellationToken cancellationToken)
    {
        var ordered = (sortBy, sortAscending) switch
        {
            (nameof(Country.Name), true) => _dbContext.Countries.OrderBy(c => c.Name),
            (nameof(Country.Name), false) => _dbContext.Countries.OrderByDescending(c => c.Name),
            (nameof(Country.Code), true) => _dbContext.Countries.OrderBy(c => c.Code),
            (nameof(Country.Code), false) => _dbContext.Countries.OrderByDescending(c => c.Code),
            ("Medals.Gold", true) => _dbContext.Countries.OrderBy(c => c.Medals.Gold),
            ("Medals.Gold", false) => _dbContext.Countries.OrderByDescending(c => c.Medals.Gold),
            _ => _dbContext.Countries.OrderByDescending(c => c.Medals.Gold),
        };

        var result = ordered.Skip(startIndex);

        if (count.HasValue)
        {
            result = result.Take(count.Value);
        }

        return (await result.ToListAsync(cancellationToken), await ordered.CountAsync());
    }
}
