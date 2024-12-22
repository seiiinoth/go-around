using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;

namespace go_around.Abstract
{
  /// <summary>
  /// An abstract class to compose Receiver Service and Update Handler classes
  /// </summary>
  /// <typeparam name="TUpdateHandler">Update Handler to use in Update Receiver</typeparam>
  public abstract class ReceiverServiceBase<TUpdateHandler> : IReceiverService
      where TUpdateHandler : IUpdateHandler
  {
    private readonly ITelegramBotClient _botClient;
    private readonly IUpdateHandler _updateHandler;
    private readonly ILogger<ReceiverServiceBase<TUpdateHandler>> _logger;

    internal ReceiverServiceBase(
        ITelegramBotClient botClient,
        TUpdateHandler updateHandler,
        ILogger<ReceiverServiceBase<TUpdateHandler>> logger)
    {
      _botClient = botClient;
      _updateHandler = updateHandler;
      _logger = logger;
    }

    /// <summary>
    /// Start to service Updates with provided Update Handler class
    /// </summary>
    /// <param name="stoppingToken"></param>
    /// <returns></returns>
    [Obsolete]
    public async Task ReceiveAsync(CancellationToken stoppingToken)
    {
      // ToDo: we can inject ReceiverOptions through IOptions container
      var receiverOptions = new ReceiverOptions()
      {
        AllowedUpdates = [],
        DropPendingUpdates = true,
      };

      var me = await _botClient.GetMeAsync(stoppingToken);
      _logger.LogInformation("Start receiving updates for {BotName}", me.Username ?? "My Awesome Bot");

      BotCommand[] botCommands = [
        new BotCommand { Command = "start", Description = "Start the bot" },
        new BotCommand { Command = "locations", Description = "List saved locations" },
        new BotCommand { Command = "language", Description = "Set interface language" }
      ];

      var currentBotCommands = await _botClient.GetMyCommandsAsync(cancellationToken: stoppingToken);

      if (currentBotCommands != botCommands)
      {
        await _botClient.SetMyCommandsAsync(botCommands, cancellationToken: stoppingToken);
      }

      // Start receiving updates
      await _botClient.ReceiveAsync(
          updateHandler: _updateHandler,
          receiverOptions: receiverOptions,
          cancellationToken: stoppingToken);
    }
  }
}
