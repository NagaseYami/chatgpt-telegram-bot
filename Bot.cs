using Nagase.Data;
using Nagase.Services;
using Nagase.Threads;
using NLog;

namespace Nagase;

public class Bot
{
    static Bot? instance;
    static readonly object instanceLock = new();
    readonly Logger logger;
    List<ChatData> chatDataList;

    Bot()
    {
        logger = LogManager.GetCurrentClassLogger();
        chatDataList = new List<ChatData>();
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

    public void Start()
    {
        TelegramService.Instance.Init(HandleNewChat, HandleReply);

        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        OpenAIAPIThread.Instance.InitAndStart(cancellationToken, HandleOpenAIResponse);
        TelegramAPIThread.Instance.InitAndStart(HandleTelegramSendMessageResponse, HandleTelegramEditMessageResponse,
            cancellationToken);
        ChatDataManageThread.Instance.InitAndStart(cancellationToken);

        OpenAIAPIThread.Instance.Join();
        TelegramAPIThread.Instance.Join();
        ChatDataManageThread.Instance.Join();
    }

    void HandleOpenAIResponse(OpenAIChatCompletionResponse response)
    {
        TelegramAPIThread.Instance.AddEditMessageRequest(new TelegramEditMessageRequest(response.Request.ChatID,
            response.Request.TelegramChatID, response.Request.TelegramMessageID, response.Text));
    }

    void HandleTelegramSendMessageResponse(TelegramSendMessageResponse response)
    {
        if (response.Request.IsErrorMessage)
        {
            return;
        }

        OpenAIAPIThread.Instance.AddRequest(new OpenAIChatCompletionRequest(response.Request.ChatID.Value,
            response.Request.TelegramChatId, response.TelegramMessageID,
            ChatDataManageThread.Instance.GenerateOpenAIChatMessages(response.Request.ChatID.Value)));

        ChatDataManageThread.Instance.AddChatMessage(response.Request.ChatID.Value,
            new ChatData.Message(string.Empty, false, response.TelegramMessageID, response.TelegramReplyToMessageID));
    }

    void HandleTelegramEditMessageResponse(TelegramEditMessageResponse response)
    {
        ChatDataManageThread.Instance.EditMessage(response.Request.ChatID, response.Request.TelegramMessageID,
            response.Request.Text);
    }

    void HandleNewChat(long telegramChatID, int telegramMessageID, string? senderUserName, string text)
    {
        logger.Info($"Recived a new chat from {senderUserName}. Message : {text}");
        var id = ChatDataManageThread.Instance.CreateChatMessage(new ChatData.Message(text, true, telegramMessageID));
        TelegramAPIThread.Instance.AddSendMessageRequest(new TelegramSendMessageRequest(id,
            telegramChatID, "请等待……⏳", telegramMessageID));
    }

    void HandleReply(long telegramChatID, int telegramMessageID, int telegramReplyToMessageID, string? senderUserName,
        string text)
    {
        var chatID = ChatDataManageThread.Instance.GetIDByMessageID(telegramReplyToMessageID);
        if (chatID.HasValue)
        {
            logger.Info($"Recived a reply from {senderUserName}. Message : {text}");
            ChatDataManageThread.Instance.AddChatMessage(chatID.Value,
                new ChatData.Message(text, true, telegramMessageID, telegramReplyToMessageID));
            TelegramAPIThread.Instance.AddSendMessageRequest(new TelegramSendMessageRequest(chatID.Value,
                telegramChatID, "请等待……⏳", telegramMessageID));
        }
        else
        {
            logger.Warn($"{senderUserName} try to reply to a nonexistent talk.");
            TelegramAPIThread.Instance.AddSendMessageRequest(new TelegramSendMessageRequest(null, telegramChatID,
                "对话已超时。\n请重新开始新的对话。", telegramMessageID));
        }
    }
}
