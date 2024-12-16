using go_around.Models;
using go_around.Interfaces;
using System.Text.Json;

namespace go_around.Services
{
  public class UserSessionService(ISessionsStoreService sessionsStoreService) : IUserSessionService
  {
    private readonly ISessionsStoreService _sessionsStoreService = sessionsStoreService;

    public async Task<Dictionary<Guid, LocationQuery>> GetSavedLocations(string userId)
    {
      var savedLocationsJson = await _sessionsStoreService.GetSessionAttribute(userId, "Locations");

      if (string.IsNullOrEmpty(savedLocationsJson))
      {
        return [];
      }

      try
      {
        return JsonSerializer.Deserialize<Dictionary<Guid, LocationQuery>>(savedLocationsJson) ?? [];
      }
      catch (JsonException)
      {
        await SetSavedLocations(userId, []);
        return [];
      }
    }

    public async Task SetSavedLocations(string userId, Dictionary<Guid, LocationQuery> locationQueries)
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

    public async Task<LocationQuery?> GetFromSavedLocations(string userId, Guid id)
    {
      var savedLocations = await GetSavedLocations(userId);

      savedLocations.TryGetValue(id, out var locationQuery);

      return locationQuery;
    }

    public async Task AddToSavedLocations(string userId, LocationQuery locationQuery)
    {
      var savedLocations = await GetSavedLocations(userId);

      savedLocations.Add(Guid.NewGuid(), locationQuery);

      await SetSavedLocations(userId, savedLocations);
    }

    public async Task<bool> RemoveFromSavedLocations(string userId, Guid id)
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

    public async Task<List<string>?> GetLocationPlacesCategories(string userId, Guid locationId)
    {
      var location = await GetFromSavedLocations(userId, locationId);

      return location?.PlacesCategories;
    }

    public async Task AddLocationPlacesCategories(string userId, Guid locationId, string category)
    {
      var location = await GetFromSavedLocations(userId, locationId);

      if (location is null)
      {
        return;
      }

      location.PlacesCategories?.Add(category);
    }

    public async Task RemoveLocationPlacesCategories(string userId, Guid locationId, string category)
    {
      var location = await GetFromSavedLocations(userId, locationId);

      if (location is null)
      {
        return;
      }

      location.PlacesCategories?.Remove(category);
    }
  }
}