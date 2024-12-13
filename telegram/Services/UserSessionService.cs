using go_around.Models;
using go_around.Interfaces;
using System.Text.Json;

namespace go_around.Services
{
  public class UserSessionService(ISessionsStoreService sessionsStoreService) : IUserSessionService
  {
    private readonly ISessionsStoreService _sessionsStoreService = sessionsStoreService;

    public async Task<List<LocationQuery>> GetSavedLocations(string userId)
    {
      var savedLocationsJson = await _sessionsStoreService.GetSessionAttribute(userId, "Locations");

      if (string.IsNullOrEmpty(savedLocationsJson))
      {
        return [];
      }

      try
      {
        return JsonSerializer.Deserialize<List<LocationQuery>>(savedLocationsJson) ?? [];
      }
      catch (JsonException)
      {
        await SetSavedLocations(userId, []);
        return [];
      }
    }

    public async Task SetSavedLocations(string userId, List<LocationQuery> locationQueries)
    {
      try
      {
        var savedLocationsJson = JsonSerializer.Serialize(locationQueries);
        await _sessionsStoreService.SetSessionAttribute(userId, "Locations", savedLocationsJson);
      }
      catch (JsonException)
      {
        var fallbackArray = JsonSerializer.Serialize<List<LocationQuery>>([]);
        await _sessionsStoreService.SetSessionAttribute(userId, "Locations", fallbackArray);
      }
    }

    public async Task AddToSavedLocations(string userId, LocationQuery locationQuery)
    {
      var savedLocations = await GetSavedLocations(userId);

      savedLocations.Add(locationQuery);

      await SetSavedLocations(userId, savedLocations);
    }

    public async Task RemoveFromSavedLocations(string userId, LocationQuery locationQuery)
    {
      var savedLocations = await GetSavedLocations(userId);

      savedLocations.Remove(locationQuery);

      await SetSavedLocations(userId, savedLocations);
    }

    public async Task RemoveFromSavedLocations(string userId, int index)
    {
      var savedLocations = await GetSavedLocations(userId);

      savedLocations.RemoveAt(index);

      await SetSavedLocations(userId, savedLocations);
    }
  }

}