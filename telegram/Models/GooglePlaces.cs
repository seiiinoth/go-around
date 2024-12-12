using System.Text.Json.Serialization;

namespace GooglePlaces.Models
{
  public enum RankPreference { RANK_PREFERENCE_UNSPECIFIED, DISTANCE, POPULARITY }

  public class SearchNearbyQueryInput
  {
    public string? LanguageCode { get; set; }
    public required string RegionCode { get; set; }
    public List<string> IncludedTypes { get; set; } = [];
    public List<string> ExcludedTypes { get; set; } = [];
    public List<string> IncludedPrimaryTypes { get; set; } = [];
    public List<string> ExcludedPrimaryTypes { get; set; } = [];
    public decimal MaxResultCount { get; set; }
    public required LocationRestriction LocationRestriction { get; set; }
    public RankPreference RankPreference { get; set; } = RankPreference.POPULARITY;
    public RoutingParameters? RoutingParameters { get; set; }
  }

  public class RoutingParameters
  {
    public required LatLng? Origin { get; set; }
    public required TravelMode? TravelMode { get; set; }
    public required RouteModifiers? RouteModifiers { get; set; }
    public required RoutingPreference? RoutingPreference { get; set; }
  }

  public enum TravelMode
  {
    DRIVE,
    BICYCLE,
    WALK,
    TWO_WHEELER
  }

  public class RouteModifiers
  {
    public bool? AvoidTolls { get; set; }
    public bool? AvoidHighways { get; set; }
    public bool? AvoidFerries { get; set; }
    public bool? AvoidIndoor { get; set; }
  }

  public enum RoutingPreference
  {
    TRAFFIC_UNAWARE,
    TRAFFIC_AWARE,
    TRAFFIC_AWARE_OPTIMAL
  }

  public class LocationRestriction
  {
    public required Circle Circle { get; set; }
  }

  public class Circle
  {
    public required LatLng Center { get; set; }
    public decimal Radius { get; set; }
  }

  public class LatLng
  {
    public required double Latitude { get; set; }
    public required double Longitude { get; set; }
  }

  public class SearchNearbyQueryOutput
  {
    public List<Place> Places { get; set; } = [];
    public List<RoutingSummary> RoutingSummaries { get; set; } = [];
  }

  public class Place
  {
    public required string Name { get; set; }
    public required string Id { get; set; }
    public LocalizedText? DisplayName { get; set; }
    public List<string>? Types { get; set; }
    public string? PrimaryType { get; set; }
    public LocalizedText? PrimaryTypeDisplayName { get; set; }
    public string? NationalPhoneNumber { get; set; }
    public string? InternationalPhoneNumber { get; set; }
    public string? FormattedAddress { get; set; }
    public string? ShortFormattedAddress { get; set; }
    public List<AddressComponent>? AddressComponents { get; set; }
    public PlusCode? PlusCode { get; set; }
    public LatLng? Location { get; set; }
    public Viewport? Viewport { get; set; }
    public float? Rating { get; set; }
    public string? GoogleMapsUri { get; set; }
    public string? WebsiteUri { get; set; }
    public List<Review>? Reviews { get; set; }
    public OpeningHours? RegularOpeningHours { get; set; }
    public List<Photo>? Photos { get; set; }
    public string? AdrFormatAddress { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public BusinessStatus? BusinessStatus { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PriceLevel? PriceLevel { get; set; }
    public List<Attribution>? Attributions { get; set; }
    public string? IconMaskBaseUri { get; set; }
    public string? IconBackgroundColor { get; set; }
    public OpeningHours? CurrentOpeningHours { get; set; }
    public List<OpeningHours>? CurrentSecondaryOpeningHours { get; set; }
    public List<OpeningHours>? RegularSecondaryOpeningHours { get; set; }
    public LocalizedText? EditorialSummary { get; set; }
    public PaymentOptions? PaymentOptions { get; set; }
    public ParkingOptions? ParkingOptions { get; set; }
    public List<SubDestination>? SubDestinations { get; set; }
    public FuelOptions? FuelOptions { get; set; }
    public EVChargeOptions? EvChargeOptions { get; set; }
    public GenerativeSummary? GenerativeSummary { get; set; }
    public AreaSummary? AreaSummary { get; set; }
    public List<ContainingPlace>? ContainingPlaces { get; set; }
    public AddressDescriptor? AddressDescriptor { get; set; }
    public GoogleMapsLinks? GoogleMapsLinks { get; set; }
    public PriceRange? PriceRange { get; set; }
    public int? UtcOffsetMinutes { get; set; }
    public int? UserRatingCount { get; set; }
    public bool? Takeout { get; set; }
    public bool? Delivery { get; set; }
    public bool? DineIn { get; set; }
    public bool? CurbsidePickup { get; set; }
    public bool? Reservable { get; set; }
    public bool? ServesBreakfast { get; set; }
    public bool? ServesLunch { get; set; }
    public bool? ServesDinner { get; set; }
    public bool? ServesBeer { get; set; }
    public bool? ServesWine { get; set; }
    public bool? ServesBrunch { get; set; }
    public bool? ServesVegetarianFood { get; set; }
    public bool? OutdoorSeating { get; set; }
    public bool? LiveMusic { get; set; }
    public bool? MenuForChildren { get; set; }
    public bool? ServesCocktails { get; set; }
    public bool? ServesDessert { get; set; }
    public bool? ServesCoffee { get; set; }
    public bool? GoodForChildren { get; set; }
    public bool? AllowsDogs { get; set; }
    public bool? Restroom { get; set; }
    public bool? GoodForGroups { get; set; }
    public bool? GoodForWatchingSports { get; set; }
    public AccessibilityOptions? AccessibilityOptions { get; set; }
    public bool? PureServiceAreaBusiness { get; set; }
  }

  public class LocalizedText
  {
    public required string Text { get; set; }
    public required string LanguageCode { get; set; }
  }

  public class AddressComponent
  {
    public string? LongText { get; set; }
    public string? ShortText { get; set; }
    public List<string>? Types { get; set; }
    public string? LanguageCode { get; set; }
  }

  public class PlusCode
  {
    public string? GlobalCode { get; set; }
    public string? CompoundCode { get; set; }
  }

  public class Viewport
  {
    public required LatLng Low { get; set; }
    public required LatLng High { get; set; }
  }

  public class Review
  {
    public string? Name { get; set; }
    public string? RelativePublishTimeDescription { get; set; }
    public LocalizedText? Text { get; set; }
    public LocalizedText? OriginalText { get; set; }
    public float? Rating { get; set; }
    public AuthorAttribution? AuthorAttribution { get; set; }
    public string? PublishTime { get; set; }
    public string? FlagContentUri { get; set; }
    public string? GoogleMapsUri { get; set; }
  }

  public class AuthorAttribution
  {
    public string? DisplayName { get; set; }
    public string? Uri { get; set; }
    public string? PhotoUri { get; set; }
  }

  public class OpeningHours
  {
    public List<Period>? Periods { get; set; }
    public List<string>? WeekdayDescriptions { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SecondaryHoursType? SecondaryHoursType { get; set; }
    public List<SpecialDay>? SpecialDays { get; set; }
    public string? NextOpenTime { get; set; }
    public string? NextCloseTime { get; set; }
    public bool? OpenNow { get; set; }
  }

  public class Period
  {
    public Point? Open { get; set; }
    public Point? Close { get; set; }
  }

  public class Point
  {
    public Date? Date { get; set; }
    public bool? Truncated { get; set; }
    public int? Day { get; set; }
    public int? Hour { get; set; }
    public int? Minute { get; set; }
  }

  public class Date
  {
    public int? Year { get; set; }
    public int? Month { get; set; }
    public int? Day { get; set; }
  }

  public class SpecialDay
  {
    public Date? Date { get; set; }
  }

  public enum SecondaryHoursType
  {
    DRIVE_THROUGH,
    HAPPY_HOUR,
    DELIVERY,
    TAKEOUT,
    KITCHEN,
    BREAKFAST,
    LUNCH,
    DINNER,
    BRUNCH,
    PICKUP,
    ACCESS,
    SENIOR_HOURS,
    ONLINE_SERVICE_HOURS
  }

  public class Photo
  {
    public string? Name { get; set; }
    public int? WidthPx { get; set; }
    public int? HeightPx { get; set; }
    public List<AuthorAttribution>? AuthorAttributions { get; set; }
    public string? FlagContentUri { get; set; }
    public string? GoogleMapsUri { get; set; }
  }

  public enum BusinessStatus
  {
    OPERATIONAL,
    CLOSED_TEMPORARILY,
    CLOSED_PERMANENTLY
  }

  public enum PriceLevel
  {
    PRICE_LEVEL_UNSPECIFIED,
    PRICE_LEVEL_FREE,
    PRICE_LEVEL_INEXPENSIVE,
    PRICE_LEVEL_MODERATE,
    PRICE_LEVEL_EXPENSIVE,
    PRICE_LEVEL_VERY_EXPENSIVE
  }

  public class Attribution
  {
    public string? Provider { get; set; }
    public string? ProviderUri { get; set; }
  }

  public class PaymentOptions
  {
    public bool? AcceptsCreditCards { get; set; }
    public bool? AcceptsDebitCards { get; set; }
    public bool? AcceptsCashOnly { get; set; }
    public bool? AcceptsNfc { get; set; }
  }

  public class ParkingOptions
  {
    public bool? FreeParkingLot { get; set; }
    public bool? PaidParkingLot { get; set; }
    public bool? FreeStreetParking { get; set; }
    public bool? PaidStreetParking { get; set; }
    public bool? ValetParking { get; set; }
    public bool? FreeGarageParking { get; set; }
    public bool? PaidGarageParking { get; set; }
  }

  public class SubDestination
  {
    public string? Name { get; set; }
    public string? Id { get; set; }
  }

  public class FuelOptions
  {
    public FuelPrice? FuelPrices { get; set; }
  }

  public class FuelPrice
  {
    public FuelType? Type { get; set; }
    public Money? Price { get; set; }
    public string? UpdateTime { get; set; }
  }

  public class Money
  {
    public string? CurrencyCode { get; set; }
    public string? Units { get; set; }
    public int? Nanos { get; set; }
  }

  public enum FuelType
  {
    FUEL_TYPE_UNSPECIFIED,
    DIESEL,
    REGULAR_UNLEADED,
    MIDGRADE,
    PREMIUM,
    SP91,
    SP91_E10,
    SP92,
    SP95,
    SP95_E10,
    SP98,
    SP99,
    SP100,
    LPG,
    E80,
    E85,
    METHANE,
    BIO_DIESEL,
    TRUCK_DIESEL
  }

  public class EVChargeOptions
  {
    public int? ConnectorCount { get; set; }
    public ConnectorAggregation? ConnectorAggregation { get; set; }
  }

  public class ConnectorAggregation
  {
    public EVConnectorType? Type { get; set; }
    public decimal? MaxChargeRateKw { get; set; }
    public int? Count { get; set; }
    public string? AvailabilityLastUpdateTime { get; set; }
    public int? AvailableCount { get; set; }
    public int? OutOfServiceCount { get; set; }
  }

  public enum EVConnectorType
  {
    EV_CONNECTOR_TYPE_UNSPECIFIED,
    EV_CONNECTOR_TYPE_OTHER,
    EV_CONNECTOR_TYPE_J1772,
    EV_CONNECTOR_TYPE_TYPE_2,
    EV_CONNECTOR_TYPE_CHADEMO,
    EV_CONNECTOR_TYPE_CCS_COMBO_1,
    EV_CONNECTOR_TYPE_CCS_COMBO_2,
    EV_CONNECTOR_TYPE_TESLA,
    EV_CONNECTOR_TYPE_UNSPECIFIED_GB_T,
    EV_CONNECTOR_TYPE_UNSPECIFIED_WALL_OUTLET
  }

  public class GenerativeSummary
  {
    public LocalizedText? Overview { get; set; }
    public string? OverviewFlagContentUri { get; set; }
    public LocalizedText? Description { get; set; }
    public string? DescriptionFlagContentUri { get; set; }
    public References? References { get; set; }
  }

  public class References
  {
    public List<Review>? Reviews { get; set; }
    public List<string>? Places { get; set; }
  }

  public class AreaSummary
  {
    public ContentBlock? ContentBlocks { get; set; }
    public string? FlagContentUri { get; set; }
  }

  public class ContentBlock
  {
    public string? Topic { get; set; }
    public LocalizedText? Content { get; set; }
    public References? References { get; set; }
  }

  public class ContainingPlace
  {
    public string? Name { get; set; }
    public string? Id { get; set; }
  }

  public class AddressDescriptor
  {
    public List<Landmark>? Landmarks { get; set; }
    public List<Area>? Areas { get; set; }
  }

  public class Landmark
  {
    public string? Name { get; set; }
    public string? PlaceId { get; set; }
    public LocalizedText? DisplayName { get; set; }
    public List<string>? Types { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SpatialRelationship? SpatialRelationship { get; set; }
    public decimal? StraightLineDistanceMeters { get; set; }
    public decimal? TravelDistanceMeters { get; set; }
  }

  public enum SpatialRelationship
  {
    NEAR,
    WITHIN,
    BESIDE,
    ACROSS_THE_ROAD,
    DOWN_THE_ROAD,
    AROUND_THE_CORNER,
    BEHIND
  }

  public class Area
  {
    public string? Name { get; set; }
    public string? PlaceId { get; set; }
    public LocalizedText? DisplayName { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Containment? Containment { get; set; }
  }

  public enum Containment
  {
    CONTAINMENT_UNSPECIFIED,
    WITHIN,
    OUTSKIRTS,
    NEAR
  }

  public class GoogleMapsLinks
  {
    public string? DirectionsUri { get; set; }
    public string? PlaceUri { get; set; }
    public string? WriteAReviewUri { get; set; }
    public string? ReviewsUri { get; set; }
    public string? PhotosUri { get; set; }
  }

  public class PriceRange
  {
    public Money? StartPrice { get; set; }
    public Money? EndPrice { get; set; }
  }

  public class AccessibilityOptions
  {
    public bool? WheelchairAccessibleParking { get; set; }
    public bool? WheelchairAccessibleEntrance { get; set; }
    public bool? WheelchairAccessibleRestroom { get; set; }
    public bool? WheelchairAccessibleSeating { get; set; }
  }

  public class RoutingSummary
  {
    public List<Leg>? Legs { get; set; }
    public string? DirectionsUri { get; set; }
  }

  public class Leg
  {
    public string? Duration { get; set; }
    public string? DistanceMeters { get; set; }
  }

  public class SearchTextQueryInput
  {
    public required string TextQuery { get; set; }
    public string? LanguageCode { get; set; }
    public string? RegionCode { get; set; }
    public RankPreference RankPreference { get; set; } = RankPreference.POPULARITY;
    public string? IncludedType { get; set; }
    public bool? OpenNow { get; set; }
    public double? MinRating { get; set; }

    [Obsolete("Deprecated: Use pageSize instead.", false)]
    public int? MaxResultCount { get; set; }
    public int? PageSize { get; set; }
    public string? PageToken { get; set; }
    public List<PriceLevel>? PriceLevels { get; set; }
    public bool? StrictTypeFiltering { get; set; }
    public LocationBias? LocationBias { get; set; }
    public LocationRestriction? LocationRestriction { get; set; }
    public EVOptions? EvOptions { get; set; }
    public RoutingParameters? RoutingParameters { get; set; }
    public SearchAlongRouteParameters? SearchAlongRouteParameters { get; set; }
    public bool? IncludePureServiceAreaBusinesses { get; set; }
  }

  public class SearchTextQueryOutput
  {
    public List<Place> Places { get; set; } = [];
    public List<RoutingSummary> RoutingSummaries { get; set; } = [];
    public List<ContextualContent> ContextualContents { get; set; } = [];
    public string? NextPageToken { get; set; }
    public string? SearchUri { get; set; }
  }

  public class LocationBias
  {
    public Viewport? Rectangle { get; set; }
    public Circle? Circle { get; set; }
  }

  public class EVOptions
  {
    public double? MinimumChargingRateKw { get; set; }
    public List<EVConnectorType>? ConnectorTypes { get; set; }
  }

  public class SearchAlongRouteParameters
  {
    public required Polyline Polyline { get; set; }
  }

  public class Polyline
  {
    public required string EncodedPolyline { get; set; }
  }

  public class ContextualContent
  {
    public List<Review> Reviews { get; set; } = [];
    public List<Photo> Photos { get; set; } = [];
    public List<Justification> Justifications { get; set; } = [];

  }

  public class Justification
  {
    public ReviewJustification? ReviewJustification { get; set; }
    public BusinessAvailabilityAttributesJustification? businessAvailabilityAttributesJustification { get; set; }
  }

  public class ReviewJustification
  {
    public HighlightedText? HighlightedText { get; set; }
    public Review? Review { get; set; }
  }

  public class HighlightedText
  {
    public string? Text { get; set; }
    public List<HighlightedTextRange>? HighlightedTextRanges { get; set; }
  }

  public class HighlightedTextRange
  {
    public int StartIndex { get; set; }
    public int EndIndex { get; set; }
  }

  public class BusinessAvailabilityAttributesJustification
  {
    public bool? Takeout { get; set; }
    public bool? Delivery { get; set; }
    public bool? DineIn { get; set; }
  }
}