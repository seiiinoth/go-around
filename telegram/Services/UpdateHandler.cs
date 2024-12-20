using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using go_around.Models;
using go_around.Interfaces;
using GooglePlaces.Models;
using GooglePlaces.Services;
using System.Text;

namespace go_around.Services
{
  public class UpdateHandler(ITelegramBotClient bot, ILogger<UpdateHandler> logger, ISessionsStoreService sessionsStoreService, IPlacesStoreService placesStoreService, IUserSessionService userSessionService, IGooglePlacesService googlePlacesService) : IUpdateHandler
  {
    private readonly ITelegramBotClient _bot = bot;
    private readonly ILogger<UpdateHandler> _logger = logger;
    private readonly ISessionsStoreService _sessionsStoreService = sessionsStoreService;
    private readonly IPlacesStoreService _placesStoreService = placesStoreService;
    private readonly IUserSessionService _userSessionService = userSessionService;
    private readonly IGooglePlacesService _googlePlacesService = googlePlacesService;

    public async Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, HandleErrorSource source, CancellationToken cancellationToken)
    {
      _logger.LogInformation("HandleError: {Exception}", exception);
      // Cooldown in case of network connection error
      if (exception is RequestException)
        await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
    }

    public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
      cancellationToken.ThrowIfCancellationRequested();
      await (update switch
      {
        { Message: { } message } => OnMessage(message),
        { EditedMessage: { } message } => OnMessage(message),
        { CallbackQuery: { } callbackQuery } => OnCallbackQuery(callbackQuery),
        _ => UnknownUpdateHandlerAsync(update)
      });
    }

    private async Task OnMessage(Message msg)
    {
      _logger.LogInformation("Receive message type: {MessageType}", msg.Type);

      if (msg.Text is not { } || !msg.Text.StartsWith('/'))
      {
        var workingStage = await _userSessionService.GetSessionWorkingStage(msg.Chat.Id.ToString());
        var editedLocation = await _userSessionService.GetLocationIdWithEditMode(msg.Chat.Id.ToString()) ?? "0";

        await (workingStage switch
        {
          WorkingStage.ENTER_LOCATION => EnterLocationHandler(msg),
          WorkingStage.ENTER_RADIUS => EnterLocationRadiusHandler(msg, editedLocation),
          _ => Usage(msg)
        });

        return;
      }

      if (msg.Text is not { } messageText)
        return;

      await _sessionsStoreService.SetSessionAttribute(msg.Chat.Id.ToString(), "WorkingStage", "Initial");

      Message sentMessage = await (messageText.Split(' ')[0] switch
      {
        "/start" => SendMenu(msg),
        "/locations" => ListLocations(msg),
        _ => Usage(msg)
      });

      _logger.LogInformation("The message was sent with id: {SentMessageId}", sentMessage.Id);
    }

    async Task<Message> EnterLocationHandler(Message msg)
    {
      await _sessionsStoreService.DeleteSessionAttribute(msg.Chat.Id.ToString(), "ReplyKeyboardMarkupMessage");
      await _userSessionService.ClearSessionWorkingStage(msg.Chat.Id.ToString());

      var location = new SavedLocation { };

      if (msg.Location is not null)
      {
        location.LatLng = new LatLng { Latitude = msg.Location.Latitude, Longitude = msg.Location.Longitude };
      }

      if (msg.Text is not null)
      {
        location.TextQuery = msg.Text;
      }

      if (location.TextQuery is null && location.LatLng is null)
      {
        const string errorMessage = "Please, provide your correct location to find places near you";
        return await _bot.SendMessage(msg.Chat, errorMessage, parseMode: ParseMode.Html);
      }

      var locationId = await _userSessionService.AddSavedLocation(msg.Chat.Id.ToString(), location);

      await _bot.SendMessage(msg.Chat, "👍", replyMarkup: new ReplyKeyboardRemove());

      return await GoAroundLocation(msg, locationId);
    }

    async Task<Message> EnterLocationRadiusHandler(Message msg, string locationId)
    {
      await _sessionsStoreService.DeleteSessionAttribute(msg.Chat.Id.ToString(), "ReplyKeyboardMarkupMessage");
      await _userSessionService.ClearSessionWorkingStage(msg.Chat.Id.ToString());

      if (msg.Text is null)
      {
        return await SendLocationRadiusRequest(msg, locationId);
      }

      try
      {
        var locationRadius = uint.Parse(msg.Text ?? "0");

        var location = await _userSessionService.GetSavedLocation(msg.Chat.Id.ToString(), locationId);

        if (location is not null)
        {
          location.Radius = locationRadius;

          await _userSessionService.UpdateSavedLocation(msg.Chat.Id.ToString(), locationId, location);

          await _bot.SendMessage(msg.Chat, "👍", replyMarkup: new ReplyKeyboardRemove());
        }

        return await GoAroundLocation(msg, locationId);
      }
      catch (Exception err)
      {
        _logger.LogInformation("Error parsing location radius: {err}", err);
        return await SendLocationRadiusRequest(msg, locationId);
      }
    }

    async Task<Message> ConfirmLocationPlacesCategories(Message msg, string locationId)
    {
      await RemoveMessageWithReplyKeyboard(msg);

      var locationPlacesCategories = await _userSessionService.GetLocationPlacesCategories(msg.Chat.Id.ToString(), locationId);
      await _userSessionService.SetLocationPlacesCategories(msg.Chat.Id.ToString(), locationId, locationPlacesCategories);

      return await GoAroundLocation(msg, locationId);
    }

    async Task<Message> Usage(Message msg)
    {
      await RemoveMessageWithReplyKeyboard(msg);
      const string usage = """
            <b><u>Bot menu</u></b>:
            /start - Start bot
            /locations - List saved locations
            """;
      return await _bot.SendMessage(msg.Chat, usage, parseMode: ParseMode.Html, replyMarkup: new ReplyKeyboardRemove());
    }

    async Task<Message> ListLocations(Message msg)
    {
      await RemoveMessageWithReplyKeyboard(msg);
      var savedLocations = await _userSessionService.GetSavedLocations(msg.Chat.Id.ToString());

      if (savedLocations.Count == 0)
      {
        const string locationsNotFoundMessage = "You don't have saved locations";

        var backToMenuButton = new InlineKeyboardMarkup()
                                    .AddButton("Back to menu", "GoToMenu");

        if (msg.From?.IsBot == true)
        {
          try
          {
            return await _bot.EditMessageText(msg.Chat, msg.MessageId, locationsNotFoundMessage, parseMode: ParseMode.Html, replyMarkup: backToMenuButton);
          }
          catch (Exception)
          {
            await _bot.DeleteMessage(msg.Chat, msg.Id);
          }
        }
        return await _bot.SendMessage(msg.Chat, locationsNotFoundMessage, parseMode: ParseMode.Html, replyMarkup: backToMenuButton);
      }

      const string listLocationsMessage = "Your saved locations:";

      var inlineMarkup = new InlineKeyboardMarkup();

      savedLocations.ToList().ForEach(location =>
      {
        var locationTitle = location.Value.Title;

        if (locationTitle is null)
        {
          if (location.Value.TextQuery is not null)
          {
            locationTitle = $"Location at {location.Value.TextQuery}";
          }
          else if (location.Value.LatLng is not null)
          {
            locationTitle = $"Location at {location.Value.LatLng.Latitude} {location.Value.LatLng.Longitude}";
          }
          else
          {
            locationTitle = "Unknown location";
          }
        }

        inlineMarkup.AddNewRow().AddButton(locationTitle, $"LocInf {location.Key}");
      });

      inlineMarkup.AddNewRow().AddButton("Back to menu", "GoToMenu");

      if (msg.From?.IsBot == true)
      {
        try
        {
          return await _bot.EditMessageText(msg.Chat, msg.MessageId, listLocationsMessage, parseMode: ParseMode.Html, replyMarkup: inlineMarkup);
        }
        catch (Exception)
        {
          await _bot.DeleteMessage(msg.Chat, msg.Id);
        }
      }
      return await _bot.SendMessage(msg.Chat, listLocationsMessage, parseMode: ParseMode.Html, replyMarkup: inlineMarkup);
    }

    async Task RemoveMessageWithReplyKeyboard(Message msg)
    {
      var replyMarkupMessageId = await _sessionsStoreService.GetSessionAttribute(msg.Chat.Id.ToString(), "ReplyKeyboardMarkupMessage");

      if (!string.IsNullOrEmpty(replyMarkupMessageId))
      {
        await _sessionsStoreService.DeleteSessionAttribute(msg.Chat.Id.ToString(), "ReplyKeyboardMarkupMessage");

        try
        {
          await _bot.DeleteMessage(msg.Chat, int.Parse(replyMarkupMessageId));
        }
        catch (Exception err)
        {
          _logger.LogInformation("Error deleting message with reply keyboard: {err}", err);
        }
      }
    }

    async Task<Message> SendSelectLocationRequest(Message msg)
    {
      if ((await _userSessionService.GetSavedLocations(msg.Chat.Id.ToString())).Count == 0)
      {
        return await SendLocationRequest(msg);
      }

      const string requestUserLocationMessage = """
            Provide your location to find places near you
            or enter your address manually
            """;

      var inlineMarkup = new InlineKeyboardMarkup()
                              .AddButton("Enter manually or send your current address", "EnterOrSendLocation")
                              .AddNewRow().AddButton("Use my saved location", "ToLocationsList")
                              .AddNewRow().AddButton("Back to menu", "GoToMenu");

      if (msg.From?.IsBot == true)
      {
        try
        {
          return await _bot.EditMessageText(msg.Chat, msg.MessageId, requestUserLocationMessage, parseMode: ParseMode.Html, replyMarkup: inlineMarkup);
        }
        catch (Exception)
        {
          await _bot.DeleteMessage(msg.Chat, msg.Id);
        }
      }
      return await _bot.SendMessage(msg.Chat, requestUserLocationMessage, parseMode: ParseMode.Html, replyMarkup: inlineMarkup);
    }

    async Task<Message> SendLocationRequest(Message msg)
    {
      await _userSessionService.SetSessionWorkingStage(msg.Chat.Id.ToString(), WorkingStage.ENTER_LOCATION);

      const string requestLocationMessage = "Provide your location to find places near you";

      var replyMarkup = new ReplyKeyboardMarkup(true)
                            .AddButton(KeyboardButton.WithRequestLocation("Location"));

      var message = await _bot.SendMessage(msg.Chat, requestLocationMessage, parseMode: ParseMode.Html, replyMarkup: replyMarkup);

      await _sessionsStoreService.SetSessionAttribute(msg.Chat.Id.ToString(), "ReplyKeyboardMarkupMessage", message.Id.ToString());

      return message;
    }

    async Task<Message> SendLocationRadiusRequest(Message msg, string locationId)
    {
      await _userSessionService.SetSessionWorkingStage(msg.Chat.Id.ToString(), WorkingStage.ENTER_RADIUS);
      await _userSessionService.EnableLocationEditMode(msg.Chat.Id.ToString(), locationId);

      string locationRadiusRequestMessage = $"""
    Specify the radius in meters around which the search will be performed.
    The value must be specified as an integer, without periods or commas.

    <b>Example:</b> {new Random().Next(200, 5000)}
    """;

      var inlineMarkup = new ReplyKeyboardMarkup(true)
                            .AddButtons("500", "1000")
                            .AddNewRow()
                            .AddButtons("1500", "2000")
                            .AddNewRow()
                            .AddButtons("2500", "3000");

      var message = await _bot.SendMessage(msg.Chat, locationRadiusRequestMessage, parseMode: ParseMode.Html, replyMarkup: inlineMarkup);

      await _sessionsStoreService.SetSessionAttribute(msg.Chat.Id.ToString(), "ReplyKeyboardMarkupMessage", message.Id.ToString());

      return message;
    }

    async Task<Message> SendLocationPlacesTypesRequest(Message msg, string locationId)
    {
      await RemoveMessageWithReplyKeyboard(msg);
      var selectedPlacesCategories = await _userSessionService.GetLocationPlacesCategories(msg.Chat.Id.ToString(), locationId);

      string locationTypesRequestMessage = "Specify the places categories";

      var inlineMarkup = new InlineKeyboardMarkup();
      List<InlineKeyboardButton> currentRow = [];

      GooglePlacesTypes.Categories.ToList().ForEach(category =>
      {
        var categoryName = category.Key;

        if (selectedPlacesCategories?.Contains(category.Key) == true)
        {
          categoryName += " ✓";
        }

        var button = InlineKeyboardButton.WithCallbackData(categoryName, $"SelLocPlcCat {locationId} {category.Key}");

        if (categoryName.Length > 15)
        {
          if (currentRow.Count > 0)
          {
            inlineMarkup.AddNewRow().AddButtons([.. currentRow]);
            currentRow.Clear();
          }

          inlineMarkup.AddNewRow().AddButton(button);
        }
        else
        {
          currentRow.Add(button);

          if (currentRow.Count == 2)
          {
            inlineMarkup.AddNewRow().AddButtons([.. currentRow]);
            currentRow.Clear();
          }
        }
      });

      if (currentRow.Count > 0)
      {
        inlineMarkup.AddNewRow().AddButtons([.. currentRow]);
      }

      inlineMarkup.AddNewRow().AddButton("Confirm", $"ConfirmPlacesCategories {locationId}")
                  .AddNewRow().AddButton("Back to menu", "GoToMenu");

      if (msg.From?.IsBot == true)
      {
        try
        {
          return await _bot.EditMessageText(msg.Chat, msg.MessageId, locationTypesRequestMessage, parseMode: ParseMode.Html, replyMarkup: inlineMarkup);
        }
        catch (Exception)
        {
          await _bot.DeleteMessage(msg.Chat, msg.Id);
        }
      }
      return await _bot.SendMessage(msg.Chat, locationTypesRequestMessage, parseMode: ParseMode.Html, replyMarkup: inlineMarkup);
    }

    async Task<Message> EditLocation(Message msg, string locationId, string locationField)
    {
      return await (Enum.Parse<SavedLocationField>(locationField) switch
      {
        SavedLocationField.LatLng => SendLocationRequest(msg),
        SavedLocationField.TextQuery => SendLocationRequest(msg),
        SavedLocationField.Radius => SendLocationRadiusRequest(msg, locationId),
        SavedLocationField.PlacesCategories => SendLocationPlacesTypesRequest(msg, locationId),
        _ => throw new NotImplementedException(),
      });
    }

    async Task<Message> SelectLocationPlacesCategory(Message msg, string locationId, string category)
    {
      var locationPlaces = await _userSessionService.GetLocationPlacesCategories(msg.Chat.Id.ToString(), locationId);

      if (locationPlaces?.Contains(category) == true)
      {
        await _userSessionService.RemoveLocationPlacesCategory(msg.Chat.Id.ToString(), locationId, category);
      }
      else
      {
        await _userSessionService.AddLocationPlacesCategory(msg.Chat.Id.ToString(), locationId, category);
      }

      return await SendLocationPlacesTypesRequest(msg, locationId);
    }

    async Task<Message> SendMenu(Message msg)
    {
      await RemoveMessageWithReplyKeyboard(msg);
      const string startMessage = """
            Welcome to <b>Go Around</b>!

            <b>GoAround</b> is a convenient and intuitive service that will help you easily find interesting places nearby.
            No more spending hours searching for entertainment or places to relax.
            With GoAround, you will instantly receive a personalized list of establishments according to your preferences
            """;
      var inlineMarkup = new InlineKeyboardMarkup()
                              .AddButton("GoAround!", "GoAround");

      if ((await _userSessionService.GetSavedLocations(msg.Chat.Id.ToString())).Count > 0)
      {
        inlineMarkup.AddNewRow().AddButton("View saved location", "ToLocationsList");
      }

      if (msg.From?.IsBot == true)
      {
        try
        {
          return await _bot.EditMessageText(msg.Chat, msg.MessageId, startMessage, parseMode: ParseMode.Html, replyMarkup: inlineMarkup);
        }
        catch (Exception)
        {
          await _bot.DeleteMessage(msg.Chat, msg.Id);
        }
      }
      return await _bot.SendMessage(msg.Chat, startMessage, parseMode: ParseMode.Html, replyMarkup: inlineMarkup);
    }

    async Task<Message> SendLocationInfo(Message msg, string locationId)
    {
      await RemoveMessageWithReplyKeyboard(msg);
      var location = await _userSessionService.GetSavedLocation(msg.Chat.Id.ToString(), locationId);

      if (location is null)
      {
        const string locationNotFoundMessage = "Location not found";
        var locationNotFoundButtonsMarkup = new InlineKeyboardMarkup()
                                                .AddButton("Back to locations list", $"ToLocationsList");

        if (msg.From?.IsBot == true)
        {
          try
          {
            return await _bot.EditMessageText(msg.Chat, msg.MessageId, locationNotFoundMessage, parseMode: ParseMode.Html, replyMarkup: locationNotFoundButtonsMarkup);
          }
          catch (Exception)
          {
            await _bot.DeleteMessage(msg.Chat, msg.Id);
          }
        }
        return await _bot.SendMessage(msg.Chat, locationNotFoundMessage, parseMode: ParseMode.Html, replyMarkup: locationNotFoundButtonsMarkup);
      }

      location.Title ??= $"Unknown location";

      string locationInfo = $"""
        {location.Title}

        Longitude: {location.LatLng?.Longitude}
        Latitude: {location.LatLng?.Latitude}

        Radius: {location.Radius}m
        """;

      var inlineMarkup = new InlineKeyboardMarkup();

      if (location.Places.Count == 0)
      {
        inlineMarkup.AddButton("GoAround!", $"GoAroundLocation {locationId}")
                    .AddNewRow();
      }
      else
      {
        inlineMarkup.AddButton($"Get places ({location.Places.Count})", $"PlaceInf {locationId} {location.Places.First()}")
                    .AddNewRow();
      }

      inlineMarkup.AddButton("Remove", $"RemoveLocation {locationId}")
                  // .AddNewRow()
                  // .AddButton("Edit", $"EditLocation {locationId}")
                  .AddNewRow()
                  .AddButton("Back to locations list", $"ToLocationsList");

      if (msg.From?.IsBot == true)
      {
        try
        {
          return await _bot.EditMessageText(msg.Chat, msg.MessageId, locationInfo, parseMode: ParseMode.Html, replyMarkup: inlineMarkup);
        }
        catch (Exception)
        {
          await _bot.DeleteMessage(msg.Chat, msg.Id);
        }
      }
      return await _bot.SendMessage(msg.Chat, locationInfo, parseMode: ParseMode.Html, replyMarkup: inlineMarkup);
    }

    async Task<Message> RemoveSavedLocation(Message msg, string locationId)
    {
      var result = await _userSessionService.RemoveSavedLocation(msg.Chat.Id.ToString(), locationId);

      string message = "Location removed";
      if (!result)
      {
        message = "Location not found";
      }

      var inlineMarkup = new InlineKeyboardMarkup()
                              .AddButton("Back to locations list", $"ToLocationsList");

      if (msg.From?.IsBot == true)
      {
        try
        {
          return await _bot.EditMessageText(msg.Chat, msg.MessageId, message, parseMode: ParseMode.Html, replyMarkup: inlineMarkup);
        }
        catch (Exception)
        {
          await _bot.DeleteMessage(msg.Chat, msg.Id);
        }
      }
      return await _bot.SendMessage(msg.Chat, message, parseMode: ParseMode.Html, replyMarkup: inlineMarkup);
    }

    async Task<Message> SendPlaceInfo(Message msg, string locationId, string placeId)
    {
      await RemoveMessageWithReplyKeyboard(msg);
      var location = await _userSessionService.GetSavedLocation(msg.Chat.Id.ToString(), locationId);

      if (location is null)
      {
        const string placeNotFoundMessage = "Location not found";
        var placeNotFoundButtonsMarkup = new InlineKeyboardMarkup()
                                                .AddButton("Back to location info", $"LocInf {locationId}");

        if (msg.From?.IsBot == true)
        {
          try
          {
            return await _bot.EditMessageText(msg.Chat, msg.MessageId, placeNotFoundMessage, parseMode: ParseMode.Html, replyMarkup: placeNotFoundButtonsMarkup);
          }
          catch (Exception)
          {
            await _bot.DeleteMessage(msg.Chat, msg.Id);
          }
        }
        return await _bot.SendMessage(msg.Chat, placeNotFoundMessage, parseMode: ParseMode.Html, replyMarkup: placeNotFoundButtonsMarkup);
      }

      string placeInfo = "";
      var inlineMarkup = new InlineKeyboardMarkup();

      if (!location.Places.Contains(placeId))
        placeId = location.Places.First();

      var place = await _placesStoreService.GetPlace(placeId);

      if (place is not null)
      {
        var placeListIndex = location.Places.IndexOf(placeId);

        var builder = new StringBuilder();

        var mediaPhoto = new InputMediaPhoto("https://placehold.co/400x400?text=No+Image");

        if (place.Photos is not null && place.Photos?.Count > 0)
        {
          mediaPhoto = new InputMediaPhoto(place.Photos?.First().GoogleMapsUri!);
        }

        if (!string.IsNullOrEmpty(place.DisplayName?.Text))
        {
          builder.AppendLine(place.DisplayName.Text);
          builder.AppendLine("");
        }

        if (place.Rating.HasValue)
        {
          builder.AppendLine($"Rating: {place.Rating?.ToString("0.0⭐️")}");
          builder.AppendLine("");
        }

        if (!string.IsNullOrEmpty(place.FormattedAddress))
        {
          builder.AppendLine($"Address: {place.FormattedAddress}");
          builder.AppendLine("");
        }

        if (!string.IsNullOrEmpty(place.GoogleMapsLinks?.ReviewsUri))
        {
          inlineMarkup.AddButton(InlineKeyboardButton.WithUrl("Reviews", place.GoogleMapsLinks.ReviewsUri))
                      .AddNewRow();
        }

        if (!string.IsNullOrEmpty(place.GoogleMapsUri))
        {
          inlineMarkup.AddButton(InlineKeyboardButton.WithUrl("Google Maps URI", place.GoogleMapsUri))
                      .AddNewRow();
        }

        placeInfo = builder.ToString();

        if (placeListIndex != 0)
        {
          var prevPlaceIndex = location.Places.IndexOf(placeId) - 1;
          inlineMarkup.AddButton("« Previous place", $"PlaceInf {locationId} {location.Places[prevPlaceIndex]}");
        }
        if (placeListIndex < (location.Places.Count - 1))
        {
          var nextPlaceIndex = location.Places.IndexOf(placeId) + 1;
          inlineMarkup.AddButton("Next place »", $"PlaceInf {locationId} {location.Places[nextPlaceIndex]}");
        }

        inlineMarkup.AddNewRow().AddButton("Back", $"LocInf {locationId}");

        await _bot.EditMessageMedia(msg.Chat, msg.MessageId, mediaPhoto);
        return await _bot.EditMessageCaption(msg.Chat, msg.MessageId, placeInfo, replyMarkup: inlineMarkup);
      }

      if (msg.From?.IsBot == true)
      {
        try
        {
          return await _bot.EditMessageText(msg.Chat, msg.MessageId, placeInfo, parseMode: ParseMode.Html, replyMarkup: inlineMarkup);
        }
        catch (Exception)
        {
          await _bot.DeleteMessage(msg.Chat, msg.Id);
        }
      }
      return await _bot.SendMessage(msg.Chat, placeInfo, parseMode: ParseMode.Html, replyMarkup: inlineMarkup);
    }

    async Task<Message> GoAroundLocation(Message msg, string locationId)
    {
      var location = await _userSessionService.GetSavedLocation(msg.Chat.Id.ToString(), locationId);

      if (location is null)
      {
        const string locationNotFoundMessage = "Location not found";
        var locationNotFoundButtonsMarkup = new InlineKeyboardMarkup()
                                                .AddButton("To locations list", $"ToLocationsList");

        if (msg.From?.IsBot == true)
        {
          return await _bot.EditMessageText(msg.Chat, msg.MessageId, locationNotFoundMessage, parseMode: ParseMode.Html, replyMarkup: locationNotFoundButtonsMarkup);
        }
        return await _bot.SendMessage(msg.Chat, locationNotFoundMessage, replyMarkup: locationNotFoundButtonsMarkup);
      }

      if (location.LatLng is null && location.TextQuery is null)
      {
        return await SendLocationRequest(msg);
      }

      if (location.Radius == 0)
      {
        return await SendLocationRadiusRequest(msg, locationId);
      }

      if (location.PlacesCategories is null)
      {
        return await SendLocationPlacesTypesRequest(msg, locationId);
      }

      await _userSessionService.DisableLocationEditMode(msg.Chat.Id.ToString(), locationId);

      await ExecuteSearch(msg, locationId);

      return await SendLocationInfo(msg, locationId);
    }

    async Task ExecuteSearch(Message msg, string locationId)
    {
      var location = await _userSessionService.GetSavedLocation(msg.Chat.Id.ToString(), locationId);

      if (location is not null)
      {
        if (location?.LatLng is not null)
        {
          SearchNearbyQueryInput searchNearbyQueryInput = new()
          {
            LanguageCode = "uk",
            RegionCode = "UA",
            IncludedTypes = location.PlacesCategories?.SelectMany(category => GooglePlacesTypes.Categories[category]).ToList() ?? [],
            LocationRestriction = new LocationRestriction()
            {
              Circle = new Circle()
              {
                Center = location.LatLng,
                Radius = location.Radius
              }
            }
          };

          var searchResult = await _googlePlacesService.SearchNearbyAsync(searchNearbyQueryInput);

          searchResult.Places.ToList().ForEach(async place =>
          {
            await _placesStoreService.SavePlace(place);
          });

          location.Places = searchResult.Places.Select(place => place.Id).ToList();
        }

        await _userSessionService.UpdateSavedLocation(msg.Chat.Id.ToString(), locationId, location!);
      }
    }

    private async Task OnCallbackQuery(CallbackQuery callbackQuery)
    {
      await _bot.AnswerCallbackQuery(callbackQuery.Id, $"{callbackQuery.Data}");

      var msg = callbackQuery.Message;

      if (msg is null)
      {
        return;
      }

      var args = callbackQuery.Data?.Split(' ');

      await (args?[0] switch
      {
        "GoToMenu" => SendMenu(msg),
        "GoAround" => SendSelectLocationRequest(msg),
        "EnterOrSendLocation" => SendLocationRequest(msg),
        "GoAroundLocation" => GoAroundLocation(msg, args?[1] ?? "0"),
        // "EditLocation" => EditLocation(msg, args?[1] ?? "0", args?[2] ?? "0"),
        "LocInf" => SendLocationInfo(msg, args?[1] ?? "0"),
        "PlaceInf" => SendPlaceInfo(msg, args?[1] ?? "0", args?[2] ?? "0"),
        "ConfirmPlacesCategories" => ConfirmLocationPlacesCategories(msg, args?[1] ?? "0"),
        "RemoveLocation" => RemoveSavedLocation(msg, args?[1] ?? "0"),
        "SelLocPlcCat" => SelectLocationPlacesCategory(msg, args?[1] ?? "0", callbackQuery.Data?.Split(' ')[2] ?? "0"),
        "ToLocationsList" => ListLocations(msg),
        _ => Usage(msg)
      });
    }

    private Task UnknownUpdateHandlerAsync(Update update)
    {
      _logger.LogInformation("Unknown update type: {UpdateType}", update.Type);
      return Task.CompletedTask;
    }
  }
}
