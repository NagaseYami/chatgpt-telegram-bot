using System.Collections;
using System.Collections.Specialized;
using OpenAI.ObjectModels;
using OpenAI.ObjectModels.RequestModels;

namespace Nagase;

public class Chat
{
    readonly object lifeLock;
    readonly OrderedDictionary messageList;
    long lastActive;

    public Chat(long chatId, int messageId, bool isUserMessage, string message)
    {
        ChatId = chatId;
        lastActive = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        lifeLock = new object();
        messageList = new OrderedDictionary();
        var chatMessage =
            new ChatMessage(
                isUserMessage ? StaticValues.ChatMessageRoles.User : StaticValues.ChatMessageRoles.Assistant, message);
        messageList[messageId.ToString()] = chatMessage;
        IsTimeout = false;
        HealthCheck();
    }

    public long ChatId { get; }

    public bool IsTimeout { get; private set; }

    public ChatMessage[] ChatMessages =>
        messageList.Cast<DictionaryEntry>().Select(e => e.Value).Cast<ChatMessage>().ToArray();

    public bool Contains(int? chatId)
    {
        return messageList.Contains(chatId.ToString());
    }

    async void HealthCheck()
    {
        while (true)
        {
            lock (lifeLock)
            {
                if (!IsTimeout && DateTimeOffset.UtcNow.ToUnixTimeSeconds() - lastActive > Config.Instance.ChatLifeTime)
                {
                    IsTimeout = true;
                    return;
                }
            }

            await Task.Delay(1000);
        }
    }

    public void AddUserMessage(int id, string message)
    {
        lock (lifeLock)
        {
            if (!IsTimeout)
            {
                lastActive = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                messageList.Add(id.ToString(), new ChatMessage(StaticValues.ChatMessageRoles.User, message));
            }
        }
    }

    public void AddAssistantMessage(int id, string message)
    {
        lock (lifeLock)
        {
            lastActive = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            messageList.Add(id.ToString(), new ChatMessage(StaticValues.ChatMessageRoles.Assistant, message));
        }
    }
}
