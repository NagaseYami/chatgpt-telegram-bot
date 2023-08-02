using OpenAI.ObjectModels.RequestModels;

namespace Nagase.Data;

public class OpenAIChatCompletionRequest
{
    public OpenAIChatCompletionRequest(Guid chatId, long telegramChatId, int telegramMessageId, ChatMessage[] messages)
    {
        ChatID = chatId;
        TelegramChatID = telegramChatId;
        TelegramMessageID = telegramMessageId;
        Messages = messages;
    }

    public Guid ChatID { get; }

    public long TelegramChatID { get; }

    public int TelegramMessageID { get; }

    public ChatMessage[] Messages { get; }
}
