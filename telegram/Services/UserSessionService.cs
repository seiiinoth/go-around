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

    public async Task<string> AddSavedLocation(string userId, LocationQuery locationQuery)
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

      return uuid;
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

    private async Task SetLocationEditMode(string userId, string locationId, bool mode)
    {
      var location = await GetSavedLocation(userId, locationId);

      if (location is not null)
      {
        location.EditMode = mode;

        await UpdateSavedLocation(userId, locationId, location);
      }
    }

    public async Task EnableLocationEditMode(string userId, string locationId)
    {
      // Edit mode can be enabled only for one document
      var locations = await GetSavedLocations(userId);

      foreach (var location in locations)
      {
        await DisableLocationEditMode(userId, location.Key);
      }

      await SetLocationEditMode(userId, locationId, true);
    }

    public async Task DisableLocationEditMode(string userId, string locationId)
    {
      await SetLocationEditMode(userId, locationId, false);
    }

    public async Task<string?> GetLocationIdWithEditMode(string userId)
    {
      var locations = await GetSavedLocations(userId);

      return locations.FirstOrDefault(x => x.Value.EditMode).Key;
    }

    public async Task<WorkingStage?> GetSessionWorkingStage(string userId)
    {
      try
      {
        return Enum.Parse<WorkingStage>(await sessionsStoreService.GetSessionAttribute(userId, "WorkingStage"));
      }
      catch (Exception)
      {
        await ClearSessionWorkingStage(userId);
        return null;
      }
    }

    public async Task SetSessionWorkingStage(string userId, WorkingStage workingStage)
    {
      await sessionsStoreService.SetSessionAttribute(userId, "WorkingStage", workingStage.ToString());
    }

    public async Task ClearSessionWorkingStage(string userId)
    {
      await sessionsStoreService.DeleteSessionAttribute(userId, "WorkingStage");
    }
  }
}