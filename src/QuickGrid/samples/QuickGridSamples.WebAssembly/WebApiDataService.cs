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

    public Task<Country[]> GetCountriesAsync()
    {
        return _httpClient.GetFromJsonAsync<Country[]>("/api/countries");
    }
}
