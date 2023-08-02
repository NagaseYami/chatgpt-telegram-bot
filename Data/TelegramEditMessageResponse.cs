namespace Nagase.Data;

public class TelegramEditMessageResponse
{
    public TelegramEditMessageResponse(TelegramEditMessageRequest request)
    {
        Request = request;
    }

    public TelegramEditMessageRequest Request { get; }
}
