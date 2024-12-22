using GoogleGeocoding.Models;

namespace GoogleGeocoding.Interfaces
{
  public interface IGoogleGeocodingService
  {
    Task<GetAddressGeocodingQueryOutput> GetAddressGeocodingAsync(GetAddressGeocodingQueryInput getAddressGeocodingQueryInput);
  }
}
