using System.Text.RegularExpressions;
using NLog;
using OpenAI;
using OpenAI.Managers;
using OpenAI.ObjectModels;
using OpenAI.ObjectModels.RequestModels;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Nagase;

public class Bot
{
    static Bot? instance;
    static readonly object instanceLock = new();
    readonly List<Chat> chatList;
    readonly object chatListLock;
    readonly TelegramBotClient client;
    readonly long lastCallOpenAIApiTime;
    readonly Logger logger;
    readonly OpenAIService openAI;
    readonly Queue<OpenAIChatMessage> pendingOpenAIChat;
    readonly object pendingOpenAIChatLock;
    readonly Queue<TelegramMessage> pendingTelegramMessage;
    readonly object pendingTelegramMessageLock;
    User botInfo;
    long lastCallTelegramApiTime;

    Bot()
    {
        pendingTelegramMessageLock = new object();
        pendingOpenAIChatLock = new object();
        chatListLock = new object();

        logger = LogManager.GetCurrentClassLogger();
        lastCallTelegramApiTime = lastCallOpenAIApiTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        openAI = new OpenAIService(new OpenAiOptions { ApiKey = Config.Instance.OpenAIApiKey });
        client = new TelegramBotClient(Config.Instance.TelegramBotApiToken);

        pendingTelegramMessage = new Queue<TelegramMessage>();
        chatList = new List<Chat>();
        pendingOpenAIChat = new Queue<OpenAIChatMessage>();
    }

    public static Bot Instance
    {
        get
        {
            if (instance == null)
            {
                lock (instanceLock)
                {
                    instance ??= new Bot();
                }
            }

            return instance;
        }
    }

    public async Task StartAsync()
    {
        ReceiverOptions receiverOptions = new() { AllowedUpdates = new[] { UpdateType.Message } };
        client.StartReceiving(HandleUpdateAsync, HandlePollingErrorAsync, receiverOptions);
        botInfo = client.GetMeAsync().Result;
        logger.Info(
            $"The bot has started successfully. Hello world! I'm {botInfo.Username} and my ID is {botInfo.Id}.");
        var task1 = Task.Run(OpenAIApiCallAsync);
        var task2 = Task.Run(TelegramApiCallAsync);
        var task3 = ChatListGCAsync();
        await Task.WhenAll(task1, task2, task3);
    }

    async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        switch (update.Type)
        {
            case UpdateType.Message:
                if (update.Message is { Text: not null } )
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
                    logger.Warn($"Received a message from a non-whitelisted group.\nGroup Title : {msg.Chat.Title}\nGroup Chat ID : {msg.Chat.Id}");
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

        if (msg.ReplyToMessage?.From?.Id == botInfo.Id)
        {
            ReceiveReply(msg);
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
                NewChat(msg, fullArg);
            }
        }
    }

    bool CommandCheck(ChatType type, string command)
    {
        switch (type)
        {
            case ChatType.Private:
            case ChatType.Sender:
                return command == "chat";
            case ChatType.Group:
            case ChatType.Supergroup:
                return command == $"chat@{botInfo.Username}";
        }

        return false;
    }

    void ReceiveReply(Message msg)
    {
        lock (chatListLock)
        {

            var chat = chatList.FirstOrDefault(c => c.Contains(msg.ReplyToMessage?.MessageId));
            if (chat != default)
            {
                if (chat.IsTimeout)
                {
                    logger.Warn($"{msg.From?.Username} try to reply to a timeout talk.");
                    AddPendingMessage(new TelegramMessage(msg.Chat.Id, "对话已超时。\n请重新开始新的对话。", msg.MessageId, true));
                }
                else
                {
                    logger.Info($"Recived a reply from {msg.From?.Username}. Message : {msg.Text}");
                    chat.AddUserMessage(msg.MessageId, msg.Text);
                    pendingOpenAIChat.Enqueue(new OpenAIChatMessage(chat.ChatMessages, chat.ChatId, msg.MessageId));
                }
            }
            else
            {
                logger.Warn($"{msg.From?.Username} try to reply to a timeout talk.");
                AddPendingMessage(new TelegramMessage(msg.Chat.Id, "对话已超时，又或是您Reply的消息并不是对话的一环。\n请重新开始新的对话。",
                    msg.MessageId, true));
            }
        }
    }

    void NewChat(Message msg, string fullArg)
    {
        var chat = new Chat(msg.Chat.Id, msg.MessageId, true, fullArg);
        lock (chatListLock)
        {
            chatList.Add(chat);
        }

        lock (pendingOpenAIChatLock)
        {
            pendingOpenAIChat.Enqueue(new OpenAIChatMessage(chat.ChatMessages, msg.Chat.Id, msg.MessageId));
        }
    }

    async Task ChatListGCAsync()
    {
        logger.Debug("Chat list GC started.");
        while (true)
        {
            lock (chatListLock)
            {

                var count = chatList.RemoveAll(c => c.IsTimeout);
                if (count > 0)
                {
                    logger.Info($"{count} timeout talks have been cleared.");
                }
            }

            await Task.Delay(5000);
        }
    }

    void OpenAIApiCallAsync()
    {
        logger.Debug("OpenAI API Call Sycle Started.");
        while (true)
        {
            var timeOffset = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - lastCallOpenAIApiTime;
            if (timeOffset > Config.Instance.OpenAIApiRateLimit)
            {
                lock (pendingOpenAIChatLock)
                {
                    if (pendingOpenAIChat.Count > 0)
                    {
                        var pendingChat = pendingOpenAIChat.Dequeue();
                        var result = OpenAIChatCompletionAsync(pendingChat.ChatMessage).Result;
                        logger.Debug($"Recived response from OpenAI :\n{result}");
                        AddPendingMessage(new TelegramMessage(pendingChat.ChatId, result,
                            pendingChat.ReplyToMessageId));
                        lastCallTelegramApiTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    }
                }
            }
        }
    }

    async Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception,
        CancellationToken cancellationToken)
    {
        var ErrorMessage = exception switch
        {
            ApiRequestException apiRequestException =>
                $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
            _ => exception.ToString()
        };

        logger.Error(ErrorMessage);
    }

    void AddPendingMessage(TelegramMessage message)
    {
        lock (pendingTelegramMessageLock)
        {
            pendingTelegramMessage.Enqueue(message);
        }
    }

    void TelegramApiCallAsync()
    {
        logger.Debug("Telegram API Call Sycle Started.");
        while (true)
        {
            var timeOffset = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - lastCallTelegramApiTime;
            if (timeOffset > Config.Instance.TelegramBotApiRateLimit)
            {
                lock (pendingTelegramMessageLock)
                {
                    if (pendingTelegramMessage.Count > 0)
                    {
                        var msg = pendingTelegramMessage.Dequeue();

                        var sended = client.SendTextMessageAsync(msg.ChatId, msg.Text, null, null, null, null, null,
                            null, msg.ReplyToMessageId).Result;

                        if (!msg.IsErrorMessage)
                        {
                            lock (chatListLock)
                            {
                                var chat = chatList.First(c => c.Contains(sended.ReplyToMessage.MessageId));
                                chat.AddAssistantMessage(sended.MessageId, sended.Text);
                            }
                        }

                        lastCallTelegramApiTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    }
                }
            }
        }
    }

    async Task<string> OpenAIChatCompletionAsync(ChatMessage[] messages)
    {
        var completionResult = await openAI.ChatCompletion.CreateCompletion(new ChatCompletionCreateRequest
        {
            Messages = messages, Model = Models.ChatGpt3_5Turbo
        });

        if (completionResult.Successful)
        {
            return completionResult.Choices.First().Message.Content;
        }

        if (completionResult.Error == null)
        {
            logger.Error("An unknown error occurred while calling the OpenAI API.");
        }
        else
        {
            logger.Error(
                $"An error occurred while calling the OpenAI API.\nError code: {completionResult.Error.Code}\nError message: {completionResult.Error.Message}");
        }

        return string.Empty;
    }

    class OpenAIChatMessage
    {
        public OpenAIChatMessage(ChatMessage[] chatMessage, ChatId chatId, int? replyToMessageId)
        {
            ChatMessage = chatMessage;
            ChatId = chatId;
            ReplyToMessageId = replyToMessageId;
        }

        public ChatMessage[] ChatMessage { get; }

        public ChatId ChatId { get; }

        public int? ReplyToMessageId { get; }
    }

    class TelegramMessage
    {
        public TelegramMessage(ChatId chatId, string text, int? replyToMessageId, bool isErrorMessage = false)
        {
            ChatId = chatId;
            Text = text;
            ReplyToMessageId = replyToMessageId;
            IsErrorMessage = isErrorMessage;
        }

        public ChatId ChatId { get; }

        public string Text { get; }

        public int? ReplyToMessageId { get; }

        public bool IsErrorMessage { get; }
    }
}
