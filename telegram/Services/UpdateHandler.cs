using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using go_around.Models;
using go_around.Interfaces;
using GooglePlaces.Models;

namespace go_around.Services;

public class UpdateHandler(ITelegramBotClient bot, ILogger<UpdateHandler> logger, ISessionsStoreService sessionsStoreService, IUserSessionService userSessionService) : IUpdateHandler
{
  public async Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, HandleErrorSource source, CancellationToken cancellationToken)
  {
    logger.LogInformation("HandleError: {Exception}", exception);
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
    logger.LogInformation("Receive message type: {MessageType}", msg.Type);

    if (msg.Text is not { } || !msg.Text.StartsWith('/'))
    {
      var workingStage = await userSessionService.GetSessionWorkingStage(msg.Chat.Id.ToString());
      var editedLocation = await userSessionService.GetLocationIdWithEditMode(msg.Chat.Id.ToString()) ?? "0";

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

    await sessionsStoreService.SetSessionAttribute(msg.Chat.Id.ToString(), "WorkingStage", "Initial");

    Message sentMessage = await (messageText.Split(' ')[0] switch
    {
      "/start" => SendMenu(msg),
      "/locations" => ListLocations(msg),
      _ => Usage(msg)
    });

    logger.LogInformation("The message was sent with id: {SentMessageId}", sentMessage.Id);
  }

  async Task<Message> EnterLocationHandler(Message msg)
  {
    await sessionsStoreService.DeleteSessionAttribute(msg.Chat.Id.ToString(), "ReplyKeyboardMarkupMessage");
    await userSessionService.ClearSessionWorkingStage(msg.Chat.Id.ToString());

    var location = new LocationQuery { };

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
      return await bot.SendMessage(msg.Chat, errorMessage, parseMode: ParseMode.Html);
    }

    var locationId = await userSessionService.AddSavedLocation(msg.Chat.Id.ToString(), location);

    await bot.SendMessage(msg.Chat, "👍", replyMarkup: new ReplyKeyboardRemove());

    return await GoAroundLocation(msg, locationId);
  }

  async Task<Message> EnterLocationRadiusHandler(Message msg, string locationId)
  {
    await sessionsStoreService.DeleteSessionAttribute(msg.Chat.Id.ToString(), "ReplyKeyboardMarkupMessage");
    await userSessionService.ClearSessionWorkingStage(msg.Chat.Id.ToString());

    if (msg.Text is null)
    {
      return await SendLocationRadiusRequest(msg, locationId);
    }

    try
    {
      var locationRadius = uint.Parse(msg.Text ?? "0");

      var location = await userSessionService.GetSavedLocation(msg.Chat.Id.ToString(), locationId);

      if (location is not null)
      {
        location.Radius = locationRadius;

        await userSessionService.UpdateSavedLocation(msg.Chat.Id.ToString(), locationId, location);

        await bot.SendMessage(msg.Chat, "👍", replyMarkup: new ReplyKeyboardRemove());
      }

      return await GoAroundLocation(msg, locationId);
    }
    catch (Exception err)
    {
      Console.WriteLine($"Error parsing location radius: {err}");
      return await SendLocationRadiusRequest(msg, locationId);
    }
  }

  async Task<Message> ConfirmLocationPlacesCategories(Message msg, string locationId)
  {
    await RemoveMessageWithReplyKeyboard(msg);

    var locationPlacesCategories = await userSessionService.GetLocationPlacesCategories(msg.Chat.Id.ToString(), locationId);
    await userSessionService.SetLocationPlacesCategories(msg.Chat.Id.ToString(), locationId, locationPlacesCategories);

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
    return await bot.SendMessage(msg.Chat, usage, parseMode: ParseMode.Html, replyMarkup: new ReplyKeyboardRemove());
  }

  async Task<Message> ListLocations(Message msg)
  {
    await RemoveMessageWithReplyKeyboard(msg);
    var savedLocations = await userSessionService.GetSavedLocations(msg.Chat.Id.ToString());

    if (savedLocations.Count == 0)
    {
      const string locationsNotFoundMessage = "You don't have saved locations";

      var backToMenuButton = new InlineKeyboardMarkup()
                                  .AddButton("Back to menu", "GoToMenu");

      if (msg.From?.IsBot == true)
      {
        return await bot.EditMessageText(msg.Chat, msg.MessageId, locationsNotFoundMessage, replyMarkup: backToMenuButton);
      }
      return await bot.SendMessage(msg.Chat, locationsNotFoundMessage, replyMarkup: backToMenuButton);
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

      inlineMarkup.AddNewRow().AddButton(locationTitle, $"LocationInfo {location.Key}");
    });

    inlineMarkup.AddNewRow().AddButton("Back to menu", "GoToMenu");

    if (msg.From?.IsBot == true)
    {
      return await bot.EditMessageText(msg.Chat, msg.MessageId, listLocationsMessage, parseMode: ParseMode.Html, replyMarkup: inlineMarkup);
    }
    return await bot.SendMessage(msg.Chat, listLocationsMessage, parseMode: ParseMode.Html, replyMarkup: inlineMarkup);
  }

  async Task RemoveMessageWithReplyKeyboard(Message msg)
  {
    var replyMarkupMessageId = await sessionsStoreService.GetSessionAttribute(msg.Chat.Id.ToString(), "ReplyKeyboardMarkupMessage");

    if (!string.IsNullOrEmpty(replyMarkupMessageId))
    {
      await sessionsStoreService.DeleteSessionAttribute(msg.Chat.Id.ToString(), "ReplyKeyboardMarkupMessage");
      await bot.DeleteMessage(msg.Chat, int.Parse(replyMarkupMessageId));
    }
  }

  async Task<Message> SendSelectLocationRequest(Message msg)
  {
    if ((await userSessionService.GetSavedLocations(msg.Chat.Id.ToString())).Count == 0)
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
      return await bot.EditMessageText(msg.Chat, msg.MessageId, requestUserLocationMessage, parseMode: ParseMode.Html, replyMarkup: inlineMarkup);
    }
    return await bot.SendMessage(msg.Chat, requestUserLocationMessage, parseMode: ParseMode.Html, replyMarkup: inlineMarkup);
  }

  async Task<Message> SendLocationRequest(Message msg)
  {
    await userSessionService.SetSessionWorkingStage(msg.Chat.Id.ToString(), WorkingStage.ENTER_LOCATION);

    const string requestLocationMessage = "Provide your location to find places near you";

    var replyMarkup = new ReplyKeyboardMarkup(true)
                          .AddButton(KeyboardButton.WithRequestLocation("Location"));

    var message = await bot.SendMessage(msg.Chat, requestLocationMessage, parseMode: ParseMode.Html, replyMarkup: replyMarkup);

    await sessionsStoreService.SetSessionAttribute(msg.Chat.Id.ToString(), "ReplyKeyboardMarkupMessage", message.Id.ToString());

    return message;
  }

  async Task<Message> SendLocationRadiusRequest(Message msg, string locationId)
  {
    await userSessionService.SetSessionWorkingStage(msg.Chat.Id.ToString(), WorkingStage.ENTER_RADIUS);
    await userSessionService.EnableLocationEditMode(msg.Chat.Id.ToString(), locationId);

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

    var message = await bot.SendMessage(msg.Chat, locationRadiusRequestMessage, parseMode: ParseMode.Html, replyMarkup: inlineMarkup);

    await sessionsStoreService.SetSessionAttribute(msg.Chat.Id.ToString(), "ReplyKeyboardMarkupMessage", message.Id.ToString());

    return message;
  }

  async Task<Message> SendLocationPlacesTypesRequest(Message msg, string locationId)
  {
    await RemoveMessageWithReplyKeyboard(msg);
    var selectedPlacesCategories = await userSessionService.GetLocationPlacesCategories(msg.Chat.Id.ToString(), locationId);

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
      return await bot.EditMessageText(msg.Chat, msg.MessageId, locationTypesRequestMessage, parseMode: ParseMode.Html, replyMarkup: inlineMarkup);
    }
    return await bot.SendMessage(msg.Chat, locationTypesRequestMessage, parseMode: ParseMode.Html, replyMarkup: inlineMarkup);
  }

  async Task<Message> EditLocation(Message msg, string locationId, string locationField)
  {
    return await (Enum.Parse<LocationQueryField>(locationField) switch
    {
      LocationQueryField.LatLng => SendLocationRequest(msg),
      LocationQueryField.TextQuery => SendLocationRequest(msg),
      LocationQueryField.Radius => SendLocationRadiusRequest(msg, locationId),
      LocationQueryField.PlacesCategories => SendLocationPlacesTypesRequest(msg, locationId),
      _ => throw new NotImplementedException(),
    });
  }

  async Task<Message> SelectLocationPlacesCategory(Message msg, string locationId, string category)
  {
    var locationPlaces = await userSessionService.GetLocationPlacesCategories(msg.Chat.Id.ToString(), locationId);

    if (locationPlaces?.Contains(category) == true)
    {
      await userSessionService.RemoveLocationPlacesCategory(msg.Chat.Id.ToString(), locationId, category);
    }
    else
    {
      await userSessionService.AddLocationPlacesCategory(msg.Chat.Id.ToString(), locationId, category);
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

    if ((await userSessionService.GetSavedLocations(msg.Chat.Id.ToString())).Count > 0)
    {
      inlineMarkup.AddNewRow().AddButton("View saved location", "ToLocationsList");
    }

    if (msg.From?.IsBot == true)
    {
      return await bot.EditMessageText(msg.Chat, msg.MessageId, startMessage, parseMode: ParseMode.Html, replyMarkup: inlineMarkup);
    }

    return await bot.SendMessage(msg.Chat, startMessage, parseMode: ParseMode.Html, replyMarkup: inlineMarkup);
  }

  async Task<Message> SendLocationInfo(Message msg, string locationId)
  {
    await RemoveMessageWithReplyKeyboard(msg);
    var location = await userSessionService.GetSavedLocation(msg.Chat.Id.ToString(), locationId);

    if (location is null)
    {
      const string locationNotFoundMessage = "Location not found";
      var locationNotFoundButtonsMarkup = new InlineKeyboardMarkup()
                                              .AddButton("Back to locations list", $"ToLocationsList");

      if (msg.From?.IsBot == true)
      {
        return await bot.EditMessageText(msg.Chat, msg.MessageId, locationNotFoundMessage, parseMode: ParseMode.Html, replyMarkup: locationNotFoundButtonsMarkup);
      }
      return await bot.SendMessage(msg.Chat, locationNotFoundMessage, replyMarkup: locationNotFoundButtonsMarkup);
    }

    location.Title ??= $"Unknown location";

    string locationInfo = $"""
        {location.Title}

        Longitude: {location.LatLng?.Longitude}
        Latitude: {location.LatLng?.Latitude}

        Radius: {location.Radius}m
        """;
    var inlineMarkup = new InlineKeyboardMarkup()
                            .AddButton("GoAround!", $"GoAroundLocation {locationId}")
                            // .AddNewRow()
                            // .AddButton("Edit", $"EditLocation {locationId}")
                            .AddNewRow()
                            .AddButton("Remove", $"RemoveLocation {locationId}")
                            .AddNewRow()
                            .AddButton("Back to locations list", $"ToLocationsList");

    if (msg.From?.IsBot == true)
    {
      return await bot.EditMessageText(msg.Chat, msg.MessageId, locationInfo, parseMode: ParseMode.Html, replyMarkup: inlineMarkup);
    }
    return await bot.SendMessage(msg.Chat, locationInfo, parseMode: ParseMode.Html, replyMarkup: inlineMarkup);
  }

  async Task<Message> RemoveSavedLocation(Message msg, string locationId)
  {
    var result = await userSessionService.RemoveSavedLocation(msg.Chat.Id.ToString(), locationId);

    string message = "Location removed";
    if (!result)
    {
      message = "Location not found";
    }

    var inlineMarkup = new InlineKeyboardMarkup()
                            .AddButton("Back to locations list", $"ToLocationsList");

    if (msg.From?.IsBot == true)
    {
      return await bot.EditMessageText(msg.Chat, msg.MessageId, message, parseMode: ParseMode.Html, replyMarkup: inlineMarkup);
    }
    return await bot.SendMessage(msg.Chat, message, parseMode: ParseMode.Html, replyMarkup: inlineMarkup);
  }

  async Task<Message> GoAroundLocation(Message msg, string locationId)
  {
    var location = await userSessionService.GetSavedLocation(msg.Chat.Id.ToString(), locationId);

    if (location is null)
    {
      const string locationNotFoundMessage = "Location not found";
      var locationNotFoundButtonsMarkup = new InlineKeyboardMarkup()
                                              .AddButton("To locations list", $"ToLocationsList");

      if (msg.From?.IsBot == true)
      {
        return await bot.EditMessageText(msg.Chat, msg.MessageId, locationNotFoundMessage, parseMode: ParseMode.Html, replyMarkup: locationNotFoundButtonsMarkup);
      }
      return await bot.SendMessage(msg.Chat, locationNotFoundMessage, replyMarkup: locationNotFoundButtonsMarkup);
    }

    if (location.LatLng is null && location.TextQuery is null)
    {
      return await SendLocationRequest(msg);
    }

    if (location.Radius is null || location.Radius == 0)
    {
      return await SendLocationRadiusRequest(msg, locationId);
    }

    if (location.PlacesCategories is null)
    {
      return await SendLocationPlacesTypesRequest(msg, locationId);
    }

    await userSessionService.DisableLocationEditMode(msg.Chat.Id.ToString(), locationId);

    const string message = "Searching for places...";
    var inlineMarkup = new InlineKeyboardMarkup()
                            .AddButton("Back to locations list", $"ToLocationsList")
                            .AddNewRow()
                            .AddButton("Back to menu", "GoToMenu");

    if (msg.From?.IsBot == true)
    {
      return await bot.EditMessageText(msg.Chat, msg.MessageId, message, parseMode: ParseMode.Html, replyMarkup: inlineMarkup);
    }
    return await bot.SendMessage(msg.Chat, message, parseMode: ParseMode.Html, replyMarkup: inlineMarkup);
  }

  private async Task OnCallbackQuery(CallbackQuery callbackQuery)
  {
    await bot.AnswerCallbackQuery(callbackQuery.Id, $"{callbackQuery.Data}");

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
      "LocationInfo" => SendLocationInfo(msg, args?[1] ?? "0"),
      "ConfirmPlacesCategories" => ConfirmLocationPlacesCategories(msg, args?[1] ?? "0"),
      "RemoveLocation" => RemoveSavedLocation(msg, args?[1] ?? "0"),
      "SelLocPlcCat" => SelectLocationPlacesCategory(msg, args?[1] ?? "0", callbackQuery.Data?.Split(' ')[2] ?? "0"),
      "ToLocationsList" => ListLocations(msg),
      _ => Usage(msg)
    });
  }

  private Task UnknownUpdateHandlerAsync(Update update)
  {
    logger.LogInformation("Unknown update type: {UpdateType}", update.Type);
    return Task.CompletedTask;
  }
}