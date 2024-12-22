using System.Net.Http.Json;
using GoogleGeocoding.Models;
using GoogleGeocoding.Interfaces;
using System.Web;

namespace GoogleGeocoding.Services
{
  public class GoogleGeocodingService(HttpClient httpClient, IConfiguration configuration) : IGoogleGeocodingService
  {
    private readonly HttpClient _httpClient = httpClient;
    private readonly string _apiKey = configuration["GoogleCloud:ApiKey"] ?? throw new Exception("Api Key for Google Cloud is required");

    public async Task<GetAddressGeocodingQueryOutput> GetAddressGeocodingAsync(GetAddressGeocodingQueryInput getAddressGeocodingQueryInput)
    {
      var baseUri = "https://maps.googleapis.com/maps/api/geocode/json";

      var query = HttpUtility.ParseQueryString(string.Empty);

      query["key"] = _apiKey;
      query["address"] = getAddressGeocodingQueryInput.Address;

      if (getAddressGeocodingQueryInput.Language is not null)
      {
        query["language"] = getAddressGeocodingQueryInput.Language;
      }

      // getAddressGeocodingQueryInput.ExtraComputations?.ForEach(extraComputation =>
      // {
      //   queryParams.Add("extra_computations", extraComputation.ToString());
      // });

      var apiUri = string.Join("?", baseUri, query.ToString());

      var httpRequest = new HttpRequestMessage(HttpMethod.Get, apiUri);
      httpRequest.Headers.Add("X-Goog-Api-Key", _apiKey);

      try
      {
        var request = await _httpClient.SendAsync(httpRequest);

        if (request.IsSuccessStatusCode)
        {
          return await request.Content.ReadFromJsonAsync<GetAddressGeocodingQueryOutput>() ?? throw new HttpRequestException("Error: Received null data from Google Geocoding API");
        }
        else
        {
          string msg = await request.Content.ReadAsStringAsync();
          throw new HttpRequestException($"Error fetching data from Google Geocoding API: {msg}");
        }
      }
      catch (Exception)
      {
        throw;
      }
    }

    public async Task<GetAddressLookupQueryOutput> GetAddressLookupAsync(GetAddressLookupQueryInput getAddressLookupQueryInput)
    {
      var baseUri = "https://maps.googleapis.com/maps/api/geocode/json";

      var query = HttpUtility.ParseQueryString(string.Empty);

      query["key"] = _apiKey;
      query["latlng"] = $"{getAddressLookupQueryInput.Latlng.Lat},{getAddressLookupQueryInput.Latlng.Lng}";

      if (getAddressLookupQueryInput.Language is not null)
      {
        query["language"] = getAddressLookupQueryInput.Language;
      }

      // getAddressLookupQueryInput.ExtraComputations?.ForEach(extraComputation =>
      // {
      //   queryParams.Add("extra_computations", extraComputation.ToString());
      // });

      var apiUri = string.Join("?", baseUri, query.ToString());

      var httpRequest = new HttpRequestMessage(HttpMethod.Get, apiUri);
      httpRequest.Headers.Add("X-Goog-Api-Key", _apiKey);

      try
      {
        var request = await _httpClient.SendAsync(httpRequest);

        if (request.IsSuccessStatusCode)
        {
          return await request.Content.ReadFromJsonAsync<GetAddressLookupQueryOutput>() ?? throw new HttpRequestException("Error: Received null data from Google Geocoding API");
        }
        else
        {
          string msg = await request.Content.ReadAsStringAsync();
          throw new HttpRequestException($"Error fetching data from Google Geocoding API: {msg}");
        }
      }
      catch (Exception)
      {
        throw;
      }
    }
  }
}