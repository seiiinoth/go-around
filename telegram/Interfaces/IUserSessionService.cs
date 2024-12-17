using go_around.Models;

namespace go_around.Interfaces
{
  public interface IUserSessionService
  {
    Task<Dictionary<string, LocationQuery>> GetSavedLocations(string userId);
    Task SetSavedLocations(string userId, Dictionary<string, LocationQuery> locationQueries);
    Task<LocationQuery?> GetFromSavedLocations(string userId, string id);
    Task AddToSavedLocations(string userId, LocationQuery locationQuery);
    Task<bool> RemoveFromSavedLocations(string userId, string id);
    Task ClearSavedLocations(string userId);
    Task<List<string>?> GetLocationPlacesCategories(string userId, string locationId);
    Task AddLocationPlacesCategories(string userId, string locationId, string category);
    Task RemoveLocationPlacesCategories(string userId, string locationId, string category);
  }
}