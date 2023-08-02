using Nagase.Data;
using NLog;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Nagase.Services;

public class TelegramService
{
    static readonly object instanceLock = new();

    static TelegramService instance;

    readonly Logger logger;

    TelegramBotClient botClient;

    public TelegramService()
    {
        logger = LogManager.GetCurrentClassLogger();
    }

    public static TelegramService Instance
    {
        get
        {
            if (instance == null)
            {

                lock (instanceLock)
                {
                    if (instance == null)
                    {
                        instance = new TelegramService();
                    }

                }
            }

            return instance;
        }
    }

    public void Init(Func<ITelegramBotClient, Update, CancellationToken, Task> updateHandler,
        Func<ITelegramBotClient, Exception, CancellationToken, Task> pollingErrorHandler)
    {
        ReceiverOptions receiverOptions = new() { AllowedUpdates = new[] { UpdateType.Message } };
        botClient.StartReceiving(updateHandler, pollingErrorHandler, receiverOptions);
    }

    public async Task SendMessageAsync(TelegramSendMessageRequest req, Action<TelegramSendMessageResponse> onSuccess)
    {
        try
        {
            var msg = await botClient.SendTextMessageAsync(req.TelegramChatId, req.Text, null, null, null, null, null,
                null, req.TelegramReplyToMessageId);

            var resp = new TelegramSendMessageResponse(req, msg.MessageId);
            onSuccess(resp);

        }
        catch (Exception e)
        {
            logger.Error(e);
        }
    }

    public async Task EditMessageAsync(TelegramEditMessageRequest req, Action<TelegramEditMessageResponse> onSuccess)
    {
        try
        {
            await botClient.EditMessageTextAsync(req.TelegramChatID, req.TelegramMessageID, req.Text);
            var resp = new TelegramEditMessageResponse(req);
            onSuccess(resp);
        }
        catch (Exception e)
        {
            logger.Error(e);
        }
    }
}
