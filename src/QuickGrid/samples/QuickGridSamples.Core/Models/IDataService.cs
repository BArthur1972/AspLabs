namespace QuickGridSamples.Core.Models;

public interface IDataService
{
    Task<ICollection<Country>> GetCountriesAsync(int startIndex, int? count, CancellationToken cancellationToken);
}
