using go_around.Models;

namespace go_around.Interfaces
{
  public interface IUserSessionService
  {
    Task<Dictionary<string, LocationQuery>> GetSavedLocations(string userId);
    Task SetSavedLocations(string userId, Dictionary<string, LocationQuery> locationQueries);
    Task<LocationQuery?> GetSavedLocation(string userId, string id);
    Task<string> AddSavedLocation(string userId, LocationQuery locationQuery);
    Task UpdateSavedLocation(string userId, string locationId, LocationQuery locationQuery);
    Task<bool> RemoveSavedLocation(string userId, string id);
    Task ClearSavedLocations(string userId);
    Task<List<string>?> GetLocationPlacesCategories(string userId, string locationId);
    Task AddLocationPlacesCategory(string userId, string locationId, string category);
    Task RemoveLocationPlacesCategory(string userId, string locationId, string category);
    Task SetLocationPlacesCategories(string userId, string locationId, List<string>? categories);
    Task EnableLocationEditMode(string userId, string locationId);
    Task DisableLocationEditMode(string userId, string locationId);
    Task<string?> GetLocationIdWithEditMode(string userId);
    Task<WorkingStage?> GetSessionWorkingStage(string userId);
    Task SetSessionWorkingStage(string userId, WorkingStage workingStage);
    Task ClearSessionWorkingStage(string userId);
  }
}