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

    public async Task<ICollection<Country>> GetCountriesAsync(int startIndex, int? count, CancellationToken cancellationToken)
    {
        return await _httpClient.GetFromJsonAsync<Country[]>($"/api/countries?startIndex={startIndex}&count={count}");
    }
}
