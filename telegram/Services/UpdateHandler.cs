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
      var workingStage = await sessionsStoreService.GetSessionAttribute(msg.Chat.Id.ToString(), "WorkingStage");

      await (workingStage.Split(' ')[0] switch
      {
        "EnterLocation" => EnterLocationHandler(msg),
        "EnterLocationRadius" => EnterLocationRadiusHandler(msg, workingStage.Split(' ')[1]),
        _ => Usage(msg)
      });
    }

    if (msg.Text is not { } messageText)
      return;

    await sessionsStoreService.SetSessionAttribute(msg.Chat.Id.ToString(), "WorkingStage", "Initial");

    Message sentMessage = await (messageText.Split(' ')[0] switch
    {
      "/start" => SendStartMessage(msg),
      "/locations" => ListLocations(msg),
      _ => Usage(msg)
    });

    logger.LogInformation("The message was sent with id: {SentMessageId}", sentMessage.Id);
  }

  async Task<Message> EnterLocationHandler(Message msg)
  {
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

    await userSessionService.AddSavedLocation(msg.Chat.Id.ToString(), location);

    const string successMessage = "Very good!";
    return await RemoveKeyboard(msg, successMessage);
  }

  async Task<Message> EnterLocationRadiusHandler(Message msg, string locationId)
  {
    if (msg.Text is null)
    {
      return await SendLocationRadiusRequest(msg, locationId);
    }

    try
    {
      var locationRadius = int.Parse(msg.Text ?? "0");

      var location = await userSessionService.GetSavedLocation(msg.Chat.Id.ToString(), locationId);

      if (location is not null)
      {
        location.Radius = locationRadius;

        await userSessionService.UpdateSavedLocation(msg.Chat.Id.ToString(), locationId, location);
      }

      return await GoAroundLocation(msg, locationId);
    }
    catch (Exception)
    {
      return await SendLocationRadiusRequest(msg, locationId);
    }
  }

  async Task<Message> Usage(Message msg)
  {
    const string usage = """
            <b><u>Bot menu</u></b>:
            /start - start bot
            /locations - list locations
            """;
    return await bot.SendMessage(msg.Chat, usage, parseMode: ParseMode.Html, replyMarkup: new ReplyKeyboardRemove());
  }

  async Task<Message> ListLocations(Message msg)
  {
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

    var inlineMarkup = new InlineKeyboardMarkup(savedLocations.Select(location =>
    {
      return InlineKeyboardButton.WithCallbackData(location.Value.TextQuery ?? $"Unknown location at {location.Value.LatLng?.Latitude} {location.Value.LatLng?.Longitude}", $"LocationInfo {location.Key}");
    }));

    inlineMarkup.AddNewRow().AddButton("Back to menu", "GoToMenu");

    if (msg.From?.IsBot == true)
    {
      return await bot.EditMessageText(msg.Chat, msg.MessageId, listLocationsMessage, parseMode: ParseMode.Html, replyMarkup: inlineMarkup);
    }
    return await bot.SendMessage(msg.Chat, listLocationsMessage, parseMode: ParseMode.Html, replyMarkup: inlineMarkup);
  }

  async Task<Message> RemoveKeyboard(Message msg, string message)
  {
    return await bot.SendMessage(msg.Chat, message, replyMarkup: new ReplyKeyboardRemove());
  }

  async Task<Message> RequestUserLocation(Message msg)
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
                            .AddNewRow()
                            .AddButton("Use my saved location", "ToLocationsList");

    if (msg.From?.IsBot == true)
    {
      return await bot.EditMessageText(msg.Chat, msg.MessageId, requestUserLocationMessage, parseMode: ParseMode.Html, replyMarkup: inlineMarkup);
    }
    return await bot.SendMessage(msg.Chat, requestUserLocationMessage, parseMode: ParseMode.Html, replyMarkup: inlineMarkup);
  }

  async Task<Message> SendLocationRequest(Message msg)
  {
    await sessionsStoreService.SetSessionAttribute(msg.Chat.Id.ToString(), "WorkingStage", "EnterLocation");

    const string requestLocationMessage = "Provide your location to find places near you";

    var replyMarkup = new ReplyKeyboardMarkup(true)
                          .AddButton(KeyboardButton.WithRequestLocation("Location"));

    return await bot.SendMessage(msg.Chat, requestLocationMessage, parseMode: ParseMode.Html, replyMarkup: replyMarkup);
  }

  async Task<Message> SendLocationRadiusRequest(Message msg, string locationId)
  {
    await sessionsStoreService.SetSessionAttribute(msg.Chat.Id.ToString(), "WorkingStage", $"EnterLocationRadius {locationId}");

    string locationRadiusRequestMessage = $"""
    Specify the radius in meters around which the search will be performed.
    The value must be specified as an integer, without periods or commas.

    <b>Example:</b> {new Random().Next(10, 1000)}
    """;

    var inlineMarkup = new ReplyKeyboardMarkup(true)
                          .AddButtons("500", "1000")
                          .AddNewRow()
                          .AddButtons("1500", "2000")
                          .AddNewRow()
                          .AddButtons("2500", "3000")
                          .AddNewRow()
                          .AddButtons("Back to menu", "GoToMenu");

    return await bot.SendMessage(msg.Chat, locationRadiusRequestMessage, parseMode: ParseMode.Html, replyMarkup: inlineMarkup);
  }

  async Task<Message> SendLocationPlacesTypesRequest(Message msg, string locationId)
  {
    await sessionsStoreService.SetSessionAttribute(msg.Chat.Id.ToString(), "WorkingStage", "EnterLocationTypes");

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

    inlineMarkup.AddNewRow().AddButton("Confirm", "ConfirmLocationPlacesCategories")
                .AddNewRow().AddButton("Back to menu", "GoToMenu");

    if (msg.From?.IsBot == true)
    {
      return await bot.EditMessageText(msg.Chat, msg.MessageId, locationTypesRequestMessage, parseMode: ParseMode.Html, replyMarkup: inlineMarkup);
    }
    return await bot.SendMessage(msg.Chat, locationTypesRequestMessage, parseMode: ParseMode.Html, replyMarkup: inlineMarkup);
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

  async Task<Message> SendStartMessage(Message msg)
  {
    const string startMessage = """
            Welcome to <b>Go Around</b>!

            <b>GoAround</b> is a convenient and intuitive service that will help you easily find interesting places nearby.
            No more spending hours searching for entertainment or places to relax.
            With GoAround, you will instantly receive a personalized list of establishments according to your preferences
            """;
    var inlineMarkup = new InlineKeyboardMarkup()
                            .AddButton("GoAround!", "GoAround");

    if (msg.From?.IsBot == true)
    {
      return await bot.EditMessageText(msg.Chat, msg.MessageId, startMessage, parseMode: ParseMode.Html, replyMarkup: inlineMarkup);
    }
    return await bot.SendMessage(msg.Chat, startMessage, parseMode: ParseMode.Html, replyMarkup: inlineMarkup);
  }

  async Task<Message> SendLocationInfo(Message msg, string locationId)
  {
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

    string locationInfo = location.TextQuery ?? $"Unknown location at {location.LatLng?.Latitude} {location.LatLng?.Longitude}";
    var inlineMarkup = new InlineKeyboardMarkup()
                            .AddButton("Remove", $"RemoveLocation {locationId}")
                            .AddNewRow()
                            .AddButton("GoAround!", $"GoAroundLocation {locationId}")
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

    if ((location.LatLng is null || location.LatLng.Latitude == 0 || location.LatLng.Longitude == 0) && location.TextQuery is null)
    {
      return await SendLocationRequest(msg);
    }

    if (location.Radius is null)
    {
      return await SendLocationRadiusRequest(msg, locationId);
    }

    if (location.PlacesCategories is null)
    {
      return await SendLocationPlacesTypesRequest(msg, locationId);
    }

    const string message = "Searching for places...";
    var inlineMarkup = new InlineKeyboardMarkup()
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

    await (callbackQuery.Data?.Split(' ')[0] switch
    {
      "GoToMenu" => SendStartMessage(msg),
      "GoAround" => RequestUserLocation(msg),
      "GoAroundLocation" => GoAroundLocation(msg, callbackQuery.Data?.Split(' ')[1] ?? "0"),
      "LocationInfo" => SendLocationInfo(msg, callbackQuery.Data?.Split(' ')[1] ?? "0"),
      "RemoveLocation" => RemoveSavedLocation(msg, callbackQuery.Data?.Split(' ')[1] ?? "0"),
      "SelLocPlcCat" => SelectLocationPlacesCategory(msg, callbackQuery.Data?.Split(' ')[1] ?? "0", callbackQuery.Data?.Split(' ')[2] ?? "0"),
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