namespace Nagase.Data;

public class TelegramSendMessageRequest
{
    public TelegramSendMessageRequest(Guid chatId, long telegramChatId, string text, int? telegramReplyToMessageId,
        bool isErrorMessage = false)
    {
        ChatID = chatId;
        TelegramChatId = telegramChatId;
        Text = text;
        TelegramReplyToMessageId = telegramReplyToMessageId;
        IsErrorMessage = isErrorMessage;
    }

    public Guid ChatID { get; }

    public long TelegramChatId { get; }

    public string Text { get; }

    public int? TelegramReplyToMessageId { get; }

    public bool IsErrorMessage { get; }
}
