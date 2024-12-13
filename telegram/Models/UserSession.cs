using GooglePlaces.Models;

namespace go_around.Models
{
  public class LocationQuery
  {
    public LatLng? LatLng { get; set; }
    public string? TextQuery { get; set; }
    public decimal Radius { get; set; }
  }
}