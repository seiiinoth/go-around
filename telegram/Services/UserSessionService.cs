using go_around.Models;
using go_around.Interfaces;
using System.Text.Json;

namespace go_around.Services
{
  public class UserSessionService(ISessionsStoreService sessionsStoreService) : IUserSessionService
  {
    private readonly ISessionsStoreService _sessionsStoreService = sessionsStoreService;

    public async Task<Dictionary<string, LocationQuery>> GetSavedLocations(string userId)
    {
      var savedLocationsJson = await _sessionsStoreService.GetSessionAttribute(userId, "Locations");

      if (string.IsNullOrEmpty(savedLocationsJson))
      {
        return [];
      }

      try
      {
        return JsonSerializer.Deserialize<Dictionary<string, LocationQuery>>(savedLocationsJson) ?? [];
      }
      catch (JsonException)
      {
        await SetSavedLocations(userId, []);
        return [];
      }
    }

    public async Task SetSavedLocations(string userId, Dictionary<string, LocationQuery> locationQueries)
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

    public async Task<LocationQuery?> GetSavedLocation(string userId, string id)
    {
      var savedLocations = await GetSavedLocations(userId);

      savedLocations.TryGetValue(id, out var locationQuery);

      return locationQuery;
    }

    public async Task AddSavedLocation(string userId, LocationQuery locationQuery)
    {
      var savedLocations = await GetSavedLocations(userId);

      string? uuid;

      do
      {
        // Generate 7 chars long UUID
        uuid = Guid.NewGuid().ToString().Split("-")[0][..7];
      } while (savedLocations.ContainsKey(uuid));

      savedLocations.Add(uuid, locationQuery);

      await SetSavedLocations(userId, savedLocations);
    }

    public async Task UpdateSavedLocation(string userId, string locationId, LocationQuery locationQuery)
    {
      var savedLocations = await GetSavedLocations(userId);

      savedLocations[locationId] = locationQuery;

      await SetSavedLocations(userId, savedLocations);
    }

    public async Task<bool> RemoveSavedLocation(string userId, string id)
    {
      var savedLocations = await GetSavedLocations(userId);

      var result = savedLocations.Remove(id);

      if (result)
      {
        await SetSavedLocations(userId, savedLocations);
      }

      return result;
    }

    public async Task ClearSavedLocations(string userId)
    {
      await SetSavedLocations(userId, []);
    }

    public async Task<List<string>?> GetLocationPlacesCategories(string userId, string locationId)
    {
      var location = await GetSavedLocation(userId, locationId);

      return location?.PlacesCategories;
    }

    public async Task AddLocationPlacesCategory(string userId, string locationId, string category)
    {
      var location = await GetSavedLocation(userId, locationId);

      if (location is not null)
      {
        location.PlacesCategories ??= [];

        location.PlacesCategories.Add(category);

        await UpdateSavedLocation(userId, locationId, location);
      }
    }

    public async Task RemoveLocationPlacesCategory(string userId, string locationId, string category)
    {
      var location = await GetSavedLocation(userId, locationId);

      if (location is not null && location.PlacesCategories is not null)
      {
        location.PlacesCategories.Remove(category);

        await UpdateSavedLocation(userId, locationId, location);
      }
    }
  }
}