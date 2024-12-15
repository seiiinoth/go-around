using go_around.Models;

namespace go_around.Interfaces
{
  public interface IUserSessionService
  {
    Task<Dictionary<Guid, LocationQuery>> GetSavedLocations(string userId);
    Task SetSavedLocations(string userId, Dictionary<Guid, LocationQuery> locationQueries);
    Task<LocationQuery?> GetFromSavedLocations(string userId, Guid id);
    Task AddToSavedLocations(string userId, LocationQuery locationQuery);
    Task<bool> RemoveFromSavedLocations(string userId, Guid id);
  }
}