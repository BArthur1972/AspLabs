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

    public Task<Country[]> GetCountriesAsync()
    {
        return _dbContext.Countries.ToArrayAsync();
    }
}
