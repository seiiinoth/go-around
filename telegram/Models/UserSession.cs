using GooglePlaces.Models;

namespace go_around.Models
{
  public class SavedLocation
  {
    public LatLng? LatLng { get; set; }
    public string? TextQuery { get; set; }
    public uint Radius { get; set; }
    public List<string>? PlacesCategories { get; set; }
    public string? Title { get; set; }
    public bool EditMode { get; set; }
    public List<string> Places { get; set; } = [];
  }

  public enum SavedLocationField
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