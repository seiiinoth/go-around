using Telegram.Bot;
using go_around.Abstract;

namespace go_around.Services
{
    // Compose Receiver and UpdateHandler implementation
    public class ReceiverService(ITelegramBotClient botClient, UpdateHandler updateHandler, ILogger<ReceiverServiceBase<UpdateHandler>> logger)
        : ReceiverServiceBase<UpdateHandler>(botClient, updateHandler, logger);
}
