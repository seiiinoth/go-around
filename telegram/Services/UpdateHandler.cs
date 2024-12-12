using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using System.Text.Json;
using go_around.Models;
using GooglePlaces.Models;

namespace go_around.Services;

public class UpdateHandler(ITelegramBotClient bot, ILogger<UpdateHandler> logger, IUsersSessionsService usersSessionsService) : IUpdateHandler
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
      if (await usersSessionsService.GetSessionAttribute(msg.Chat.Id.ToString(), "WorkingStage") == "EnterLocation")
      {
        await EnterLocationHandler(msg);
      }
    }

    if (msg.Text is not { } messageText)
      return;

    await usersSessionsService.SetSessionAttribute(msg.Chat.Id.ToString(), "WorkingStage", "Initial");

    Message sentMessage = await (messageText.Split(' ')[0] switch
    {
      "/start" => SendStartMessage(msg),
      _ => Usage(msg)
    });

    logger.LogInformation("The message was sent with id: {SentMessageId}", sentMessage.Id);
  }

  async Task<Message> EnterLocationHandler(Message msg)
  {
    var location = new LocationRecord { };

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

    await usersSessionsService.SetSessionAttribute(msg.Chat.Id.ToString(), "Location", JsonSerializer.Serialize(location));

    const string successMessage = "Very good!";
    return await RemoveKeyboard(msg, successMessage);
  }

  async Task<Message> Usage(Message msg)
  {
    const string usage = """
            <b><u>Bot menu</u></b>:
            /start          - start bot
            """;
    return await bot.SendMessage(msg.Chat, usage, parseMode: ParseMode.Html, replyMarkup: new ReplyKeyboardRemove());
  }

  async Task<Message> RemoveKeyboard(Message msg, string message)
  {
    return await bot.SendMessage(msg.Chat, message, replyMarkup: new ReplyKeyboardRemove());
  }

  async Task<Message> RequestUserLocation(Message msg)
  {
    await usersSessionsService.SetSessionAttribute(msg.Chat.Id.ToString(), "WorkingStage", "EnterLocation");

    const string requestLocationMessage = """
            Provide your location to find places near you
            or enter your address manually
            """;

    if (string.IsNullOrEmpty(await usersSessionsService.GetSessionAttribute(msg.Chat.Id.ToString(), "Location")))
    {
      var replyMarkup = new ReplyKeyboardMarkup(true)
        .AddButton(KeyboardButton.WithRequestLocation("Location"));

      return await bot.SendMessage(msg.Chat, requestLocationMessage, parseMode: ParseMode.Html, replyMarkup: replyMarkup);
    }

    var inlineMarkup = new InlineKeyboardMarkup()
      .AddButton("Enter manually or send your current address", "EnterOrSendLocation")
      .AddNewRow()
      .AddButton("Use my saved location", "UseSavedLocation");

    return await bot.SendMessage(msg.Chat, requestLocationMessage, parseMode: ParseMode.Html, replyMarkup: inlineMarkup);
  }

  async Task<Message> SendLocationRequest(Message msg)
  {
    await usersSessionsService.SetSessionAttribute(msg.Chat.Id.ToString(), "WorkingStage", "EnterLocation");

    const string requestLocationMessage = """
            Provide your location to find places near you
            """;

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

  private async Task OnCallbackQuery(CallbackQuery callbackQuery)
  {
    await bot.AnswerCallbackQuery(callbackQuery.Id, $"{callbackQuery.Data}");

    switch (callbackQuery.Data)
    {
      case "GoAround":
        await RequestUserLocation(callbackQuery.Message!);
        break;

      case "EnterOrSendLocation":
        await SendLocationRequest(callbackQuery.Message!);
        break;
    }
  }

  private Task UnknownUpdateHandlerAsync(Update update)
  {
    logger.LogInformation("Unknown update type: {UpdateType}", update.Type);
    return Task.CompletedTask;
  }
}