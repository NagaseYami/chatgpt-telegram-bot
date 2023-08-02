using System.Text.RegularExpressions;
using Nagase.Data;
using NLog;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Nagase.Services;

public class TelegramService
{
    static readonly object instanceLock = new();

    static TelegramService instance;

    readonly Logger logger;

    readonly TelegramBotClient botClient;

    User botInfo;

    Action<long, int, string?, string> newChatHandler;
    Action<long, int, int, string?, string> replyHandler;

    TelegramService()
    {
        logger = LogManager.GetCurrentClassLogger();
        botClient = new TelegramBotClient(Config.Instance.TelegramBotApiToken);
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

    public void Init(Action<long, int, string?, string> newChatHandler,
        Action<long, int, int, string?, string> replyHandler)
    {
        this.newChatHandler = newChatHandler;
        this.replyHandler = replyHandler;

        ReceiverOptions receiverOptions = new() { AllowedUpdates = new[] { UpdateType.Message } };
        botClient.StartReceiving(HandleUpdateAsync, HandlePollingErrorAsync, receiverOptions);
        botInfo = botClient.GetMeAsync().Result;

        logger.Info(
            $"The bot has started successfully. Hello world! I'm {botInfo.Username} and my ID is {botInfo.Id}.");
    }

    public async Task SendMessageAsync(TelegramSendMessageRequest req, Action<TelegramSendMessageResponse> onSuccess)
    {
        try
        {
            var msg = await botClient.SendTextMessageAsync(req.TelegramChatId, req.Text, null, null, null, null, null,
                null, req.TelegramReplyToMessageId);

            var resp = new TelegramSendMessageResponse(req, msg.MessageId, msg.ReplyToMessage?.MessageId);
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

    async Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception,
        CancellationToken cancellationToken)
    {
        var errorMessage = exception switch
        {
            ApiRequestException apiRequestException =>
                $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
            _ => exception.ToString()
        };

        logger.Error(errorMessage);
    }

    async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        switch (update.Type)
        {
            case UpdateType.Message:
                if (update.Message is { Text: not null })
                {
                    OnReceiveMessage(update.Message);
                }

                break;
            default:
                logger.Warn($"Detected unhandled message. UpdateType : {update.Type}");
                break;
        }
    }

    void OnReceiveMessage(Message msg)
    {
        if (msg.From?.IsBot == true)
        {
            return;
        }

        switch (msg.Chat.Type)
        {
            case ChatType.Supergroup:
            case ChatType.Group:
                if (!Config.Instance.ChatIdWhiteList.Contains(msg.Chat.Id))
                {
                    logger.Warn(
                        $"Received a message from a non-whitelisted group.\nGroup Title : {msg.Chat.Title}\nGroup Chat ID : {msg.Chat.Id}");
                    return;
                }

                break;
            case ChatType.Private:
            case ChatType.Sender:
                if (msg.From == null || !Config.Instance.UsernameWhiteList.Contains(msg.From.Username))
                {
                    logger.Warn(
                        $"Received a message from a non-whitelisted user.\nUsername : {msg.From?.Username}\nFirstname: {msg.From?.FirstName}\nUser ID : {msg.From?.Id}");
                    return;
                }

                break;
            default:
                return;
        }

        if (msg.ReplyToMessage?.From?.Id == Instance.botInfo.Id)
        {
            replyHandler(msg.Chat.Id, msg.MessageId, msg.ReplyToMessage.MessageId, msg.From.Username, msg.Text);
        }
        else
        {
            var commandPattern = @"^/([^ ]+)(.*)$";
            var match = Regex.Match(msg.Text, commandPattern);
            if (!match.Success)
            {
                return;
            }

            var command = match.Groups[1].Value;
            var fullArg = match.Groups[2].Value;
            var args = fullArg.Split(" ").Where(s => !string.IsNullOrEmpty(s)).ToArray();

            if (CommandCheck(msg.Chat.Type, command))
            {
                logger.Info($"Recived command {command} from {msg.From?.Username}\nMessage : {fullArg}");
                if (string.IsNullOrWhiteSpace(fullArg))
                {
                    fullArg = "Hi!";
                }

                newChatHandler(msg.Chat.Id, msg.MessageId, msg.From?.Username, fullArg);
            }
        }
    }

    bool CommandCheck(ChatType type, string command)
    {
        switch (type)
        {
            case ChatType.Private:
            case ChatType.Sender:
                return command == Config.Instance.ChatCommand;
            case ChatType.Group:
            case ChatType.Supergroup:
                return command == $"{Config.Instance.ChatCommand}@{Instance.botInfo.Username}";
        }

        return false;
    }
}
