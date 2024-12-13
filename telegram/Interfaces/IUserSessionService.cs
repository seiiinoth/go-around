using go_around.Models;

namespace go_around.Interfaces
{
  public interface IUserSessionService
  {
    Task<List<LocationQuery>> GetSavedLocations(string userId);
    Task SetSavedLocations(string userId, List<LocationQuery> locationQueries);
    Task AddToSavedLocations(string userId, LocationQuery locationQuery);
    Task RemoveFromSavedLocations(string userId, LocationQuery locationQuery);
    Task RemoveFromSavedLocations(string userId, int index);
  }
}