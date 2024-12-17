using GooglePlaces.Models;

namespace go_around.Models
{
  public class LocationQuery
  {
    public LatLng? LatLng { get; set; }
    public string? TextQuery { get; set; }
    public uint? Radius { get; set; }
    public List<string>? PlacesCategories { get; set; }
    public string? Title { get; set; }
    public bool EditMode { get; set; }
  }

  public enum LocationQueryField
  {
    LatLng,
    TextQuery,
    Radius,
    PlacesCategories
  }

  public enum WorkingStage
  {
    ENTER_LOCATION,
    ENTER_RADIUS,
    ENTER_PLACES_CATEGORIES,
    ENTER_TEXT_QUERY
  }
}