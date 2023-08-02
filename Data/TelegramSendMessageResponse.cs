namespace Nagase.Data;

public class TelegramSendMessageResponse
{
    public TelegramSendMessageResponse(TelegramSendMessageRequest req, int telegramMessageId)
    {
        TelegramMessageID = telegramMessageId;
    }

    public TelegramSendMessageRequest Request { get; }

    public int TelegramMessageID { get; }
}
