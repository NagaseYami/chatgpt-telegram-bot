using OpenAI.ObjectModels.RequestModels;

namespace Nagase.Data;

public class OpenAIChatCompletionRequest
{
    public OpenAIChatCompletionRequest(Guid chatId, ChatMessage[] messages)
    {
        ChatID = chatId;
        Messages = messages;
    }

    public Guid ChatID { get; }

    public ChatMessage[] Messages { get; }
}
