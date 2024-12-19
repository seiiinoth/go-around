using System.Text.Json;
using go_around.Interfaces;
using GooglePlaces.Models;
using StackExchange.Redis;

namespace go_around.Services
{
  public class PlacesStoreService(IStoreService storeService) : IPlacesStoreService
  {
    private readonly IStoreService _storeService = storeService;

    private static string GetPlaceKey(string id)
    {
      return $"place:{id}";
    }

    public async Task<Place?> GetPlace(string id)
    {
      var placeJson = await _storeService.StringGetAsync(GetPlaceKey(id));

      if (string.IsNullOrEmpty(placeJson) && placeJson == RedisValue.Null)
      {
        return null;
      }

      return JsonSerializer.Deserialize<Place>(placeJson!) ?? null;
    }

    public async Task SetPlace(string id, Place place)
    {
      var placeJson = JsonSerializer.Serialize(place);

      await _storeService.StringSetAsync(GetPlaceKey(id), placeJson);
    }
  }
}