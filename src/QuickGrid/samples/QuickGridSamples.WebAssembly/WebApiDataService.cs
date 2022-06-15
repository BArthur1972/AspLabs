using System.Net.Http.Json;
using QuickGridSamples.Core.Models;

namespace QuickGridSamples.WebAssembly;

internal class WebApiDataService : IDataService
{
    private readonly HttpClient _httpClient;

    public WebApiDataService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<(ICollection<Country> Items, int TotalCount)> GetCountriesAsync(int startIndex, int? count, string sortBy, bool sortAscending, CancellationToken cancellationToken)
    {
        // TODO: Also pass sorting params
        return await _httpClient.GetFromJsonAsync<(Country[], int)>($"/api/countries?startIndex={startIndex}&count={count}");
    }
}
