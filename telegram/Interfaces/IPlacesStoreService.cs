using GooglePlaces.Models;

namespace go_around.Interfaces
{
  public interface IPlacesStoreService
  {
    Task<Place?> GetPlace(string id);
    Task SetPlace(string id, Place place);
  }
}