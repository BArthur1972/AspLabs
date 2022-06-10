namespace QuickGridSamples.Core.Models;

public interface IDataService
{
    Task<Country[]> GetCountriesAsync();
}
