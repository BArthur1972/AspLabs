namespace QuickGridSamples.Core.Models;

public interface IDataService
{
    Task<(ICollection<Country> Items, int TotalCount)> GetCountriesAsync(int startIndex, int? count, string sortBy, bool sortAscending, CancellationToken cancellationToken);
}
