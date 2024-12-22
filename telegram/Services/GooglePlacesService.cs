using System.Net.Http.Json;
using GooglePlaces.Models;
using GooglePlaces.Interfaces;

namespace GooglePlaces.Services
{
  public class GooglePlacesService(HttpClient httpClient, IConfiguration configuration) : IGooglePlacesService
  {
    private readonly HttpClient _httpClient = httpClient;
    private readonly string _apiKey = configuration["GoogleCloud:ApiKey"] ?? throw new Exception("Api Key for Google Cloud is required");

    public async Task<SearchNearbyQueryOutput> SearchNearbyAsync(SearchNearbyQueryInput searchNearbyQueryInput)
    {
      var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://places.googleapis.com/v1/places:searchNearby");
      httpRequest.Headers.Add("X-Goog-Api-Key", _apiKey);
      httpRequest.Headers.Add("X-Goog-FieldMask", "*");
      httpRequest.Content = JsonContent.Create(searchNearbyQueryInput);

      try
      {
        var request = await _httpClient.SendAsync(httpRequest);

        if (request.IsSuccessStatusCode)
        {
          return await request.Content.ReadFromJsonAsync<SearchNearbyQueryOutput>() ?? throw new HttpRequestException("Error: Received null data from Google Places API");
        }
        else
        {
          string msg = await request.Content.ReadAsStringAsync();
          throw new HttpRequestException($"Error fetching data from Google Places API: {msg}");
        }
      }
      catch (Exception)
      {
        throw;
      }
    }

    public async Task<SearchTextQueryOutput> SearchTextAsync(SearchTextQueryInput searchTextQueryInput)
    {
      var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://places.googleapis.com/v1/places:searchText");
      httpRequest.Headers.Add("X-Goog-Api-Key", _apiKey);
      httpRequest.Headers.Add("X-Goog-FieldMask", "*");
      httpRequest.Content = JsonContent.Create(searchTextQueryInput);

      try
      {
        var request = await _httpClient.SendAsync(httpRequest);

        if (request.IsSuccessStatusCode)
        {
          return await request.Content.ReadFromJsonAsync<SearchTextQueryOutput>() ?? throw new HttpRequestException("Error: Received null data from Google Places API");
        }
        else
        {
          string msg = await request.Content.ReadAsStringAsync();
          throw new HttpRequestException($"Error fetching data from Google Places API: {msg}");
        }
      }
      catch (Exception)
      {
        throw;
      }
    }
  }
}