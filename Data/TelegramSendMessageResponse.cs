namespace Nagase.Data;

public class TelegramSendMessageResponse
{
    public TelegramSendMessageResponse(TelegramSendMessageRequest request, int telegramMessageId,
        int? telegramReplyToMessageId)
    {
        Request = request;
        TelegramMessageID = telegramMessageId;
        TelegramReplyToMessageID = telegramReplyToMessageId;
    }

    public TelegramSendMessageRequest Request { get; }

    public int TelegramMessageID { get; }

    public int? TelegramReplyToMessageID { get; }
}
