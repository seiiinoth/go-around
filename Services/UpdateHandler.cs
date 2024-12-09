using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace go_around.Services;

public class UpdateHandler(ITelegramBotClient bot, ILogger<UpdateHandler> logger) : IUpdateHandler
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
    if (msg.Text is not { } messageText)
      return;

    Message sentMessage = await (messageText.Split(' ')[0] switch
    {
      "/inline_buttons" => SendInlineKeyboard(msg),
      "/keyboard" => SendReplyKeyboard(msg),
      "/remove" => RemoveKeyboard(msg),
      "/request" => RequestLocation(msg),
      _ => Usage(msg)
    });
    logger.LogInformation("The message was sent with id: {SentMessageId}", sentMessage.Id);
  }

  async Task<Message> Usage(Message msg)
  {
    const string usage = """
            <b><u>Bot menu</u></b>:
            /inline_buttons - send inline buttons
            /keyboard       - send keyboard buttons
            /remove         - remove keyboard buttons
            /request        - request location or contact
            """;
    return await bot.SendMessage(msg.Chat, usage, parseMode: ParseMode.Html, replyMarkup: new ReplyKeyboardRemove());
  }

  async Task<Message> SendInlineKeyboard(Message msg)
  {
    var inlineMarkup = new InlineKeyboardMarkup()
        .AddNewRow("1.1", "1.2", "1.3")
        .AddNewRow()
            .AddButton("WithCallbackData", "CallbackData")
            .AddButton(InlineKeyboardButton.WithUrl("WithUrl", "https://github.com/TelegramBots/Telegram.Bot"));
    return await bot.SendMessage(msg.Chat, "Inline buttons:", replyMarkup: inlineMarkup);
  }

  async Task<Message> SendReplyKeyboard(Message msg)
  {
    var replyMarkup = new ReplyKeyboardMarkup(true)
        .AddNewRow("1.1", "1.2", "1.3")
        .AddNewRow().AddButton("2.1").AddButton("2.2");
    return await bot.SendMessage(msg.Chat, "Keyboard buttons:", replyMarkup: replyMarkup);
  }

  async Task<Message> RemoveKeyboard(Message msg)
  {
    return await bot.SendMessage(msg.Chat, "Removing keyboard", replyMarkup: new ReplyKeyboardRemove());
  }

  async Task<Message> RequestLocation(Message msg)
  {
    var replyMarkup = new ReplyKeyboardMarkup(true)
        .AddButton(KeyboardButton.WithRequestLocation("Location"));
    return await bot.SendMessage(msg.Chat, "Where are you?", replyMarkup: replyMarkup);
  }

  // Process Inline Keyboard callback data
  private async Task OnCallbackQuery(CallbackQuery callbackQuery)
  {
    logger.LogInformation("Received inline keyboard callback from: {CallbackQueryId}", callbackQuery.Id);
    await bot.AnswerCallbackQuery(callbackQuery.Id, $"Received {callbackQuery.Data}");
    await bot.SendMessage(callbackQuery.Message!.Chat, $"Received {callbackQuery.Data}");
  }

  private Task UnknownUpdateHandlerAsync(Update update)
  {
    logger.LogInformation("Unknown update type: {UpdateType}", update.Type);
    return Task.CompletedTask;
  }
}