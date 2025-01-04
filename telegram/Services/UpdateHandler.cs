using System.Text;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using go_around.Models;
using go_around.Interfaces;
using GooglePlaces.Models;
using GooglePlaces.Interfaces;
using GoogleGeocoding.Models;
using GoogleGeocoding.Interfaces;

namespace go_around.Services
{
  public class UpdateHandler(ITelegramBotClient bot, IMessageLocalizerService messageLocalizerService, ILogger<UpdateHandler> logger, ISessionsStoreService sessionsStoreService, IPlacesStoreService placesStoreService, IUserSessionService userSessionService, IStoreService storeService, IGooglePlacesService googlePlacesService, IGoogleGeocodingService googleGeocodingService, IConfiguration configuration) : IUpdateHandler
  {
    private readonly ITelegramBotClient _bot = bot;
    private readonly IMessageLocalizerService _messageLocalizerService = messageLocalizerService;
    private readonly ILogger<UpdateHandler> _logger = logger;
    private readonly ISessionsStoreService _sessionsStoreService = sessionsStoreService;
    private readonly IPlacesStoreService _placesStoreService = placesStoreService;
    private readonly IUserSessionService _userSessionService = userSessionService;
    private readonly IStoreService _storeService = storeService;
    private readonly IGooglePlacesService _googlePlacesService = googlePlacesService;
    private readonly IGoogleGeocodingService _googleGeocodingService = googleGeocodingService;
    private readonly long _adminId = long.Parse(configuration["BotConfiguration:AdminId"] ?? throw new Exception("Telegram AdminId is required"));

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

      var locale = await _userSessionService.GetSessionLanguage(msg.Chat.Id.ToString());

      if (msg.Text is not { } || !msg.Text.StartsWith('/'))
      {
        var workingStage = await _userSessionService.GetSessionWorkingStage(msg.Chat.Id.ToString());
        var editedLocation = await _userSessionService.GetLocationIdWithEditMode(msg.Chat.Id.ToString()) ?? "0";

        await (workingStage switch
        {
          WorkingStage.ENTER_LOCATION => EnterLocationHandler(msg, locale),
          WorkingStage.ENTER_RADIUS => EnterLocationRadiusHandler(msg, editedLocation, locale),
          _ => Usage(msg, locale)
        });

        return;
      }

      if (msg.Text is not { } messageText)
        return;

      Message sentMessage = await (messageText.Split(' ')[0] switch
      {
        "/start" => SendMenu(msg, locale),
        "/locations" => ListLocations(msg, locale),
        "/language" => SendLanguageSelector(msg, locale),
        "/search" => SendSearchSwitch(msg, locale),
        _ => Usage(msg, locale)
      });

      _logger.LogInformation("The message was sent with id: {SentMessageId}", sentMessage.Id);
    }

    async Task<Message> SendLanguageSelector(Message msg, Language locale)
    {
      await RemoveMessageWithReplyKeyboard(msg);

      string selectLanguageMessage = _messageLocalizerService.GetMessage("SelectInterfaceLanguage", locale);

      var inlineMarkup = new InlineKeyboardMarkup();

      foreach (Language language in Enum.GetValues<Language>())
      {
        inlineMarkup.AddButton(language.ToString(), $"SetLang {language}");
        inlineMarkup.AddNewRow();
      }

      return await _bot.SendMessage(msg.Chat, selectLanguageMessage, parseMode: ParseMode.Html, replyMarkup: inlineMarkup);
    }

    async Task<Message> SendSearchSwitch(Message msg, Language locale)
    {
      await RemoveMessageWithReplyKeyboard(msg);

      if (msg.Chat.Id != _adminId)
      {
        return await _bot.SendMessage(msg.Chat, _messageLocalizerService.GetMessage("Forbidden", locale), parseMode: ParseMode.Html);
      }

      string selectLanguageMessage = _messageLocalizerService.GetMessage("SelectSearchMode", locale);

      var inlineMarkup = new InlineKeyboardMarkup()
                              .AddButton("Enable", $"SetSearchMode {true}")
                              .AddButton("Disable", $"SetSearchMode {false}");

      return await _bot.SendMessage(msg.Chat, selectLanguageMessage, parseMode: ParseMode.Html, replyMarkup: inlineMarkup);
    }

    async Task<Message> SetSearchModeHandler(Message msg, string searchMode, Language locale)
    {
      await RemoveMessageWithReplyKeyboard(msg);

      await _storeService.HashSetAsync("global", "searchEnabled", searchMode.ToString());

      string changeSearchModeMessage = "👍";

      return await _bot.SendMessage(msg.Chat, changeSearchModeMessage, parseMode: ParseMode.Html);
    }

    async Task<Message> SetLanguageHandler(Message msg, string language, Language locale)
    {
      await RemoveMessageWithReplyKeyboard(msg);

      await _userSessionService.SetSessionLanguage(msg.Chat.Id.ToString(), Enum.Parse<Language>(language));

      const string changeLanguageMessage = "👍";

      return await _bot.SendMessage(msg.Chat, changeLanguageMessage, parseMode: ParseMode.Html);
    }

    async Task<Message> EnterLocationHandler(Message msg, Language locale)
    {
      await _sessionsStoreService.DeleteSessionAttribute(msg.Chat.Id.ToString(), "ReplyKeyboardMarkupMessage");
      await _userSessionService.ClearSessionWorkingStage(msg.Chat.Id.ToString());

      var location = new SavedLocation { };

      if (msg.Location is not null)
      {
        location.LatLng = new LatLng { Latitude = msg.Location.Latitude, Longitude = msg.Location.Longitude };

        GetAddressLookupQueryInput getAddressLookupQueryInput = new()
        {
          Latlng = new GoogleGeocoding.Models.Location { Lat = msg.Location.Latitude, Lng = msg.Location.Longitude },
          Language = "uk",
          Region = "UA"
        };

        var searchResult = await _googleGeocodingService.GetAddressLookupAsync(getAddressLookupQueryInput);

        if (searchResult.Results.Count > 0)
        {
          location.Title = searchResult.Results.First().Formatted_Address;
        }
      }

      if (msg.Text is not null)
      {
        location.TextQuery = msg.Text;
      }

      if (location.TextQuery is null && location.LatLng is null)
      {
        string errorMessage = _messageLocalizerService.GetMessage("EnterLocationErrorMessage", locale);
        return await _bot.SendMessage(msg.Chat, errorMessage, parseMode: ParseMode.Html);
      }

      if (location.TextQuery is not null && location.LatLng is null)
      {
        GetAddressGeocodingQueryInput getAddressGeocodingQueryInput = new()
        {
          Address = location.TextQuery,
          Language = "uk",
          Region = "UA"
        };

        var searchResult = await _googleGeocodingService.GetAddressGeocodingAsync(getAddressGeocodingQueryInput);

        if (searchResult.Results.Count > 1)
        {
          var locations = searchResult.Results.Where(result => result.Formatted_Address != null).Select(result => result.Formatted_Address!);
          return await SendTextLocationsSelect(msg, locations.ToList(), locale);
        }

        if (searchResult.Results.Count > 0)
        {
          location.LatLng = new LatLng()
          {
            Latitude = searchResult.Results.First().Geometry?.Location?.Lat ?? 0,
            Longitude = searchResult.Results.First().Geometry?.Location?.Lng ?? 0,
          };

          location.Title = searchResult.Results.First().Formatted_Address;
        }
      }

      var locationId = await _userSessionService.AddSavedLocation(msg.Chat.Id.ToString(), location);

      await _bot.SendMessage(msg.Chat, "👍", replyMarkup: new ReplyKeyboardRemove());

      return await GoAroundLocation(msg, locationId, locale);
    }

    async Task<Message> EnterLocationRadiusHandler(Message msg, string locationId, Language locale)
    {
      await _sessionsStoreService.DeleteSessionAttribute(msg.Chat.Id.ToString(), "ReplyKeyboardMarkupMessage");
      await _userSessionService.ClearSessionWorkingStage(msg.Chat.Id.ToString());

      if (msg.Text is null)
      {
        return await SendLocationRadiusRequest(msg, locationId, locale);
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

        return await GoAroundLocation(msg, locationId, locale);
      }
      catch (Exception err)
      {
        _logger.LogInformation("Error parsing location radius: {err}", err);
        return await SendLocationRadiusRequest(msg, locationId, locale);
      }
    }

    async Task<Message> ConfirmLocationPlacesCategories(Message msg, string locationId, Language locale)
    {
      await RemoveMessageWithReplyKeyboard(msg);

      var locationPlacesCategories = await _userSessionService.GetLocationPlacesCategories(msg.Chat.Id.ToString(), locationId);
      await _userSessionService.SetLocationPlacesCategories(msg.Chat.Id.ToString(), locationId, locationPlacesCategories);

      return await GoAroundLocation(msg, locationId, locale);
    }

    async Task<Message> Usage(Message msg, Language locale)
    {
      await RemoveMessageWithReplyKeyboard(msg);
      const string usage = """
            <b><u>Bot menu</u></b>:
            /start - Start bot
            /locations - List saved locations
            /language - Select interface language
            """;
      return await _bot.SendMessage(msg.Chat, usage, parseMode: ParseMode.Html, replyMarkup: new ReplyKeyboardRemove());
    }

    async Task<Message> ListLocations(Message msg, Language locale)
    {
      await RemoveMessageWithReplyKeyboard(msg);
      var savedLocations = await _userSessionService.GetSavedLocations(msg.Chat.Id.ToString());

      if (savedLocations.Count == 0)
      {
        string locationsNotFoundMessage = _messageLocalizerService.GetMessage("DontHaveSavedLocations", locale);

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

      string listLocationsMessage = _messageLocalizerService.GetMessage("SavedLocations", locale);

      var inlineMarkup = new InlineKeyboardMarkup();

      savedLocations.ToList().ForEach(location =>
      {
        var locationTitle = location.Value.Title;

        if (locationTitle is null)
        {
          if (location.Value.TextQuery is not null)
          {
            locationTitle = $"{_messageLocalizerService.GetMessage("LocationAt", locale)} {location.Value.TextQuery}";
          }
          else if (location.Value.LatLng is not null)
          {
            locationTitle = $"{_messageLocalizerService.GetMessage("LocationAt", locale)} {location.Value.LatLng.Latitude} {location.Value.LatLng.Longitude}";
          }
          else
          {
            locationTitle = _messageLocalizerService.GetMessage("UnknownLocation", locale);
          }
        }

        inlineMarkup.AddNewRow().AddButton(locationTitle, $"LocInf {location.Key}");
      });

      inlineMarkup.AddNewRow().AddButton(_messageLocalizerService.GetMessage("BackToMenu", locale), "GoToMenu");

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

    async Task<Message> SendSelectLocationRequest(Message msg, Language locale)
    {
      if ((await _userSessionService.GetSavedLocations(msg.Chat.Id.ToString())).Count == 0)
      {
        return await SendLocationRequest(msg, locale);
      }

      string requestUserLocationMessage = _messageLocalizerService.GetMessage("EnterLocation", locale);

      var inlineMarkup = new InlineKeyboardMarkup()
                              .AddButton(_messageLocalizerService.GetMessage("EnterLocationShort", locale), "EnterOrSendLocation")
                              .AddNewRow().AddButton(_messageLocalizerService.GetMessage("UseSavedLocations", locale), "ToLocationsList")
                              .AddNewRow().AddButton(_messageLocalizerService.GetMessage("BackToMenu", locale), "GoToMenu");

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

    async Task<Message> SendLocationRequest(Message msg, Language locale)
    {
      await _userSessionService.SetSessionWorkingStage(msg.Chat.Id.ToString(), WorkingStage.ENTER_LOCATION);

      var replyMarkup = new ReplyKeyboardMarkup(true)
                            .AddButton(KeyboardButton.WithRequestLocation("Location"));

      string requestLocationMessage = _messageLocalizerService.GetMessage("ProvideLocation", locale);

      var message = await _bot.SendMessage(msg.Chat, requestLocationMessage, parseMode: ParseMode.Html, replyMarkup: replyMarkup);

      await _sessionsStoreService.SetSessionAttribute(msg.Chat.Id.ToString(), "ReplyKeyboardMarkupMessage", message.Id.ToString());

      return message;
    }

    async Task<Message> SendTextLocationsSelect(Message msg, List<string> locations, Language locale)
    {
      await _userSessionService.SetSessionWorkingStage(msg.Chat.Id.ToString(), WorkingStage.ENTER_LOCATION);

      var replyMarkup = new ReplyKeyboardMarkup(true);

      locations.ForEach(location =>
      {
        replyMarkup.AddButton(location);
        replyMarkup.AddNewRow();
      });

      string textLocationSelectMessage = _messageLocalizerService.GetMessage("SelectAppropriateOption", locale);

      var message = await _bot.SendMessage(msg.Chat, textLocationSelectMessage, parseMode: ParseMode.Html, replyMarkup: replyMarkup);

      await _sessionsStoreService.SetSessionAttribute(msg.Chat.Id.ToString(), "ReplyKeyboardMarkupMessage", message.Id.ToString());

      return message;
    }

    async Task<Message> SendLocationRadiusRequest(Message msg, string locationId, Language locale)
    {
      await _userSessionService.SetSessionWorkingStage(msg.Chat.Id.ToString(), WorkingStage.ENTER_RADIUS);
      await _userSessionService.EnableLocationEditMode(msg.Chat.Id.ToString(), locationId);

      string locationRadiusRequestMessage = _messageLocalizerService.GetMessage("SpecifyRadius", locale);

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

    async Task<Message> SendLocationPlacesTypesRequest(Message msg, string locationId, Language locale)
    {
      await RemoveMessageWithReplyKeyboard(msg);
      var selectedPlacesCategories = await _userSessionService.GetLocationPlacesCategories(msg.Chat.Id.ToString(), locationId);

      string locationTypesRequestMessage = _messageLocalizerService.GetMessage("SpecifyPlacesCategories", locale);

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

      inlineMarkup.AddNewRow().AddButton(_messageLocalizerService.GetMessage("Confirm", locale), $"ConfirmPlacesCategories {locationId}")
                  .AddNewRow().AddButton(_messageLocalizerService.GetMessage("BackToMenu", locale), "GoToMenu");

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

    async Task<Message> EditLocation(Message msg, string locationId, string locationField, Language locale)
    {
      return await (Enum.Parse<SavedLocationField>(locationField) switch
      {
        SavedLocationField.LatLng => SendLocationRequest(msg, locale),
        SavedLocationField.TextQuery => SendLocationRequest(msg, locale),
        SavedLocationField.Radius => SendLocationRadiusRequest(msg, locationId, locale),
        SavedLocationField.PlacesCategories => SendLocationPlacesTypesRequest(msg, locationId, locale),
        _ => throw new NotImplementedException(),
      });
    }

    async Task<Message> SelectLocationPlacesCategory(Message msg, string locationId, string category, Language locale)
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

      return await SendLocationPlacesTypesRequest(msg, locationId, locale);
    }

    async Task<Message> SendMenu(Message msg, Language locale)
    {
      await RemoveMessageWithReplyKeyboard(msg);

      string startMessage = _messageLocalizerService.GetMessage("HelloMessage", locale);

      var inlineMarkup = new InlineKeyboardMarkup()
                              .AddButton("GoAround!", "GoAround");

      if ((await _userSessionService.GetSavedLocations(msg.Chat.Id.ToString())).Count > 0)
      {
        inlineMarkup.AddNewRow().AddButton(_messageLocalizerService.GetMessage("ViewSavedLocations", locale), "ToLocationsList");
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

    async Task<Message> SendLocationInfo(Message msg, string locationId, Language locale)
    {
      await RemoveMessageWithReplyKeyboard(msg);
      var location = await _userSessionService.GetSavedLocation(msg.Chat.Id.ToString(), locationId);

      if (location is null)
      {
        string locationNotFoundMessage = _messageLocalizerService.GetMessage("LocationNotFound", locale);
        var locationNotFoundButtonsMarkup = new InlineKeyboardMarkup()
                                                .AddButton(_messageLocalizerService.GetMessage("ViewSavedLocations", locale), $"ToLocationsList");

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

      var builder = new StringBuilder();

      builder.AppendLine(location.Title ?? _messageLocalizerService.GetMessage("UnknownLocation", locale));
      builder.AppendLine("");

      if (location.LatLng is not null)
      {
        builder.AppendLine($"{_messageLocalizerService.GetMessage("Longitude", locale)}: {location.LatLng.Longitude}");
        builder.AppendLine($"{_messageLocalizerService.GetMessage("Latitude", locale)}: {location.LatLng.Latitude}");
        builder.AppendLine("");
      }

      if (location.TextQuery is not null)
      {
        builder.AppendLine($"{_messageLocalizerService.GetMessage("Query", locale)}: {location.TextQuery}");
        builder.AppendLine("");
      }

      if (location.Radius > 0)
      {
        builder.AppendLine($"{_messageLocalizerService.GetMessage("Radius", locale)}: {location.Radius}");
        builder.AppendLine("");
      }

      if (location.PlacesCategories?.Count > 0)
      {
        builder.Append($"{_messageLocalizerService.GetMessage("SelectedCategories", locale)}: ");

        location.PlacesCategories?.ToList().ForEach(category =>
        {
          builder.Append($"{category}");

          if (category != location.PlacesCategories.Last())
          {
            builder.Append(", ");
          }
        });

        builder.AppendLine("");
      }

      var locationInfo = builder.ToString();

      var inlineMarkup = new InlineKeyboardMarkup();

      if (location.Places.Count == 0)
      {
        inlineMarkup.AddButton("GoAround!", $"GoAroundLocation {locationId}")
                    .AddNewRow();
      }
      else
      {
        inlineMarkup.AddButton($"{_messageLocalizerService.GetMessage("GetPlaces", locale)} ({location.Places.Count})", $"PlaceInf {locationId} {location.Places.First()}")
                    .AddNewRow();
      }

      inlineMarkup.AddButton(_messageLocalizerService.GetMessage("Remove", locale), $"RemoveLocation {locationId}")
                  // .AddNewRow()
                  // .AddButton("Edit", $"EditLocation {locationId}")
                  .AddNewRow()
                  .AddButton(_messageLocalizerService.GetMessage("ViewSavedLocations", locale), $"ToLocationsList");

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

    async Task<Message> RemoveSavedLocation(Message msg, string locationId, Language locale)
    {
      var result = await _userSessionService.RemoveSavedLocation(msg.Chat.Id.ToString(), locationId);

      string message = _messageLocalizerService.GetMessage("LocationRemoved", locale);
      if (!result)
      {
        message = _messageLocalizerService.GetMessage("LocationNotFound", locale);
      }

      var inlineMarkup = new InlineKeyboardMarkup()
                              .AddButton(_messageLocalizerService.GetMessage("ViewSavedLocations", locale), $"ToLocationsList");

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

    async Task<Message> SendPlaceInfo(Message msg, string locationId, string placeId, Language locale)
    {
      await RemoveMessageWithReplyKeyboard(msg);
      var location = await _userSessionService.GetSavedLocation(msg.Chat.Id.ToString(), locationId);

      if (location is null)
      {
        string placeNotFoundMessage = _messageLocalizerService.GetMessage("LocationNotFound", locale);

        var placeNotFoundButtonsMarkup = new InlineKeyboardMarkup()
                                                .AddButton(_messageLocalizerService.GetMessage("ToLocationInfo", locale), $"LocInf {locationId}");

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
          builder.AppendLine($"{_messageLocalizerService.GetMessage("Rating", locale)}: {place.Rating?.ToString("0.0⭐️")}");
          builder.AppendLine("");
        }

        if (!string.IsNullOrEmpty(place.FormattedAddress))
        {
          builder.AppendLine($"{_messageLocalizerService.GetMessage("Address", locale)}: {place.FormattedAddress}");
          builder.AppendLine("");
        }

        if (!string.IsNullOrEmpty(place.GoogleMapsLinks?.ReviewsUri))
        {
          inlineMarkup.AddButton(InlineKeyboardButton.WithUrl(_messageLocalizerService.GetMessage("Reviews", locale), place.GoogleMapsLinks.ReviewsUri))
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
          inlineMarkup.AddButton($"« {_messageLocalizerService.GetMessage("PreviousPlace", locale)}", $"PlaceInf {locationId} {location.Places[prevPlaceIndex]}");
        }
        if (placeListIndex < (location.Places.Count - 1))
        {
          var nextPlaceIndex = location.Places.IndexOf(placeId) + 1;
          inlineMarkup.AddButton($"{_messageLocalizerService.GetMessage("NextPlace", locale)} »", $"PlaceInf {locationId} {location.Places[nextPlaceIndex]}");
        }

        inlineMarkup.AddNewRow().AddButton(_messageLocalizerService.GetMessage("Back", locale), $"LocInf {locationId}");

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

    async Task<Message> GoAroundLocation(Message msg, string locationId, Language locale)
    {
      var location = await _userSessionService.GetSavedLocation(msg.Chat.Id.ToString(), locationId);

      if (location is null)
      {
        string locationNotFoundMessage = _messageLocalizerService.GetMessage("LocationNotFound", locale);

        var locationNotFoundButtonsMarkup = new InlineKeyboardMarkup()
                                                .AddButton(_messageLocalizerService.GetMessage("ViewSavedLocations", locale), $"ToLocationsList");

        if (msg.From?.IsBot == true)
        {
          return await _bot.EditMessageText(msg.Chat, msg.MessageId, locationNotFoundMessage, parseMode: ParseMode.Html, replyMarkup: locationNotFoundButtonsMarkup);
        }
        return await _bot.SendMessage(msg.Chat, locationNotFoundMessage, replyMarkup: locationNotFoundButtonsMarkup);
      }

      if (location.LatLng is null && location.TextQuery is null)
      {
        return await SendLocationRequest(msg, locale);
      }

      if (location.Radius == 0)
      {
        return await SendLocationRadiusRequest(msg, locationId, locale);
      }

      if (location.PlacesCategories is null)
      {
        return await SendLocationPlacesTypesRequest(msg, locationId, locale);
      }

      await _userSessionService.DisableLocationEditMode(msg.Chat.Id.ToString(), locationId);

      await ExecuteSearch(msg, locationId, locale);

      return await SendLocationInfo(msg, locationId, locale);
    }

    async Task ExecuteSearch(Message msg, string locationId, Language locale)
    {
      var searchEnabled = await _storeService.HashGetAsync("global", "searchEnabled");

      if (searchEnabled == "False" || searchEnabled.IsNull)
      {
        var messageText = "Search is disabled";
        var message = await _bot.SendMessage(msg.Chat, messageText, parseMode: ParseMode.Html);
        return;
      }

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

      var locale = await _userSessionService.GetSessionLanguage(msg.Chat.Id.ToString());

      var args = callbackQuery.Data?.Split(' ');

      await (args?[0] switch
      {
        "GoToMenu" => SendMenu(msg, locale),
        "GoAround" => SendSelectLocationRequest(msg, locale),
        "EnterOrSendLocation" => SendLocationRequest(msg, locale),
        "GoAroundLocation" => GoAroundLocation(msg, args?[1] ?? "0", locale),
        // "EditLocation" => EditLocation(msg, args?[1] ?? "0", args?[2] ?? "0"),
        "LocInf" => SendLocationInfo(msg, args?[1] ?? "0", locale),
        "PlaceInf" => SendPlaceInfo(msg, args?[1] ?? "0", args?[2] ?? "0", locale),
        "ConfirmPlacesCategories" => ConfirmLocationPlacesCategories(msg, args?[1] ?? "0", locale),
        "RemoveLocation" => RemoveSavedLocation(msg, args?[1] ?? "0", locale),
        "SelLocPlcCat" => SelectLocationPlacesCategory(msg, args?[1] ?? "0", callbackQuery.Data?.Split(' ')[2] ?? "0", locale),
        "ToLocationsList" => ListLocations(msg, locale),
        "SetLang" => SetLanguageHandler(msg, args?[1] ?? "0", locale),
        "SetSearchMode" => SetSearchModeHandler(msg, args?[1] ?? "0", locale),
        _ => Usage(msg, locale)
      });
    }

    private Task UnknownUpdateHandlerAsync(Update update)
    {
      _logger.LogInformation("Unknown update type: {UpdateType}", update.Type);
      return Task.CompletedTask;
    }
  }
}
