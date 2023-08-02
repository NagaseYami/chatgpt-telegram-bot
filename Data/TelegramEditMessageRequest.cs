namespace Nagase.Data;

public class TelegramEditMessageRequest
{
    public TelegramEditMessageRequest(Guid chatId, long telegramChatId, int telegramMessageId, string text)
    {
        ChatID = chatId;
        TelegramChatID = telegramChatId;
        TelegramMessageID = telegramMessageId;
        Text = text;
    }

    public Guid ChatID { get; }

    public long TelegramChatID { get; }

    public int TelegramMessageID { get; }

    public string Text { get; }
}
