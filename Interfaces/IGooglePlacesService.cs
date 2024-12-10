using GooglePlaces.Models;

namespace GooglePlaces.Services
{
  public interface IGooglePlacesService
  {
    Task<SearchNearbyQueryOutput> SearchNearbyAsync(SearchNearbyQueryInput searchNearbyQueryInput);
  }
}
