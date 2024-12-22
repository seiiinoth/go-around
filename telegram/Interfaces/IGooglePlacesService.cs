using GooglePlaces.Models;

namespace GooglePlaces.Interfaces
{
  public interface IGooglePlacesService
  {
    Task<SearchNearbyQueryOutput> SearchNearbyAsync(SearchNearbyQueryInput searchNearbyQueryInput);
    Task<SearchTextQueryOutput> SearchTextAsync(SearchTextQueryInput searchTextQueryInput);
  }
}
