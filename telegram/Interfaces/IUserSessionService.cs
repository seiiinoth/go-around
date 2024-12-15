using go_around.Models;

namespace go_around.Interfaces
{
  public interface IUserSessionService
  {
    Task<Dictionary<Guid, LocationQuery>> GetSavedLocations(string userId);
    Task SetSavedLocations(string userId, Dictionary<Guid, LocationQuery> locationQueries);
    Task AddToSavedLocations(string userId, LocationQuery locationQuery);
    Task RemoveFromSavedLocation(string userId, Guid id);
  }
}