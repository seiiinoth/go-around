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
      if (await sessionsStoreService.GetSessionAttribute(msg.Chat.Id.ToString(), "WorkingStage") == "EnterLocation")
      {
        await EnterLocationHandler(msg);
      }
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

    switch (msg.Type)
    {
      case MessageType.Location:
        location.LatLng = new LatLng { Latitude = msg.Location!.Latitude, Longitude = msg.Location!.Longitude };
        break;

      case MessageType.Text:
        location.TextQuery = msg.Text;
        break;
    }

    if (location.TextQuery is null && location.LatLng is null)
    {
      const string errorMessage = "Please, provide your correct location to find places near you";
      return await bot.SendMessage(msg.Chat, errorMessage, parseMode: ParseMode.Html);
    }

    await userSessionService.AddToSavedLocations(msg.Chat.Id.ToString(), location);

    const string successMessage = "Very good!";
    return await RemoveKeyboard(msg, successMessage);
  }

  async Task<Message> Usage(Message msg)
  {
    const string usage = """
            <b><u>Bot menu</u></b>:
            /start - start bot
            /locations - list locations
            /help - show help
            """;
    return await bot.SendMessage(msg.Chat, usage, parseMode: ParseMode.Html, replyMarkup: new ReplyKeyboardRemove());
  }

  async Task<Message> ListLocations(Message msg)
  {
    var savedLocations = await userSessionService.GetSavedLocations(msg.Chat.Id.ToString());

    if (savedLocations.Count == 0)
    {
      const string locationsNotFoundMessage = "You don't have saved locations";

      if (msg.From?.IsBot == true)
      {
        return await bot.EditMessageText(msg.Chat, msg.MessageId, locationsNotFoundMessage);
      }
      return await bot.SendMessage(msg.Chat, locationsNotFoundMessage);
    }

    const string listLocationsMessage = "Your saved locations:";
    var inlineMarkup = new InlineKeyboardMarkup(savedLocations.Select(location =>
    {
      return new[] { InlineKeyboardButton.WithCallbackData(location.Value.TextQuery ?? $"Unknown location at {location.Value.LatLng?.Latitude} {location.Value.LatLng?.Longitude}", $"LocationInfo {location.Key}") };
    }));

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

    const string requestLocationMessage = """
            Provide your location to find places near you
            or enter your address manually
            """;
    var inlineMarkup = new InlineKeyboardMarkup()
      .AddButton("Enter manually or send your current address", "EnterOrSendLocation")
      .AddNewRow()
      .AddButton("Use my saved location", "ToLocationsList");

    if (msg.From?.IsBot == true)
    {
      return await bot.EditMessageText(msg.Chat, msg.MessageId, requestLocationMessage, parseMode: ParseMode.Html, replyMarkup: inlineMarkup);
    }
    return await bot.SendMessage(msg.Chat, requestLocationMessage, parseMode: ParseMode.Html, replyMarkup: inlineMarkup);
  }

  async Task<Message> SendLocationRequest(Message msg)
  {
    await sessionsStoreService.SetSessionAttribute(msg.Chat.Id.ToString(), "WorkingStage", "EnterLocation");

    const string requestLocationMessage = "Provide your location to find places near you";
    var replyMarkup = new ReplyKeyboardMarkup(true)
        .AddButton(KeyboardButton.WithRequestLocation("Location"));

    return await bot.SendMessage(msg.Chat, requestLocationMessage, parseMode: ParseMode.Html, replyMarkup: replyMarkup);
  }

  async Task<Message> SendStartMessage(Message msg)
  {
    const string startMessage = """
            Welcome to <b>Go Around</b>!

            <b>GoAround</b> is a convenient and intuitive service that will help you easily find interesting places nearby.
            No more spending hours searching for entertainment or places to relax.
            With GoAround, you will instantly receive a personalized list of establishments according to your preferences
            """;

    var inlineMarkup = new InlineKeyboardMarkup().AddButton("GoAround!", "GoAround");
    return await bot.SendMessage(msg.Chat, startMessage, parseMode: ParseMode.Html, replyMarkup: inlineMarkup);
  }

  async Task<Message> SendLocationInfo(Message msg, string locationId)
  {
    var location = await userSessionService.GetFromSavedLocations(msg.Chat.Id.ToString(), Guid.Parse(locationId));

    if (location is null)
    {
      const string locationNotFoundMessage = "Location not found";
      var locationNotFoundButtonsMarkup = new InlineKeyboardMarkup().AddButton("Back to locations list", $"ToLocationsList");

      if (msg.From?.IsBot == true)
      {
        return await bot.EditMessageText(msg.Chat, msg.MessageId, locationNotFoundMessage, parseMode: ParseMode.Html, replyMarkup: locationNotFoundButtonsMarkup);
      }
      return await bot.SendMessage(msg.Chat, locationNotFoundMessage, replyMarkup: locationNotFoundButtonsMarkup);
    }

    string locationInfo = location.TextQuery ?? $"Unknown location at {location.LatLng?.Latitude} {location.LatLng?.Longitude}";
    var inlineMarkup = new InlineKeyboardMarkup()
                          .AddButton("Remove", $"Remove {locationId}")
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
      "GoAround" => RequestUserLocation(msg),
      "LocationInfo" => SendLocationInfo(msg, callbackQuery.Data?.Split(' ')[1] ?? "0"),
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