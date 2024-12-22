using System.Text.Json.Serialization;

namespace GoogleGeocoding.Models
{
  public class GetAddressGeocodingQueryInput
  {
    public required string Address { get; set; }
    public string? Language { get; set; }
    public string? Region { get; set; }
    public List<ExtraComputations>? ExtraComputations { get; set; }
  }

  public enum ExtraComputations
  {
    ADDRESS_DESCRIPTORS,
    BUILDING_AND_ENTRANCES
  }

  public class GetAddressGeocodingQueryOutput
  {
    public List<GeocodingResult> Results { get; set; } = [];
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public GeocodingStatus Status { get; set; }
  }

  public class GeocodingResult
  {
    public List<string>? Types { get; set; }
    public string? Formatted_Address { get; set; }
    public List<AddressComponent>? Address_Components { get; set; }
    public Geometry? Geometry { get; set; }
    public string? Place_Id { get; set; }
  }

  public class AddressComponent
  {
    public string? Long_Name { get; set; }
    public string? Short_Name { get; set; }
    public List<string>? Types { get; set; }
  }

  public class Geometry
  {
    public Location? Location { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public LocationType? Location_Type { get; set; }
    public Viewport? Viewport { get; set; }
  }

  public class Viewport
  {
    public Location? Northeast { get; set; }
    public Location? Southwest { get; set; }
  }

  public class Location
  {
    public required double Lat { get; set; }
    public required double Lng { get; set; }
  }

  public enum GeocodingStatus
  {
    OK,
    ZERO_RESULTS,
    OVER_DAILY_LIMIT,
    OVER_QUERY_LIMIT,
    REQUEST_DENIED,
    INVALID_REQUEST,
    UNKNOWN_ERROR,
  }

  public enum LocationType
  {
    ROOFTOP,
    RANGE_INTERPOLATED,
    GEOMETRIC_CENTER,
    APPROXIMATE
  }
}