using System.Collections.Concurrent;
using Nagase.Data;
using NLog;
using OpenAI.ObjectModels.RequestModels;

namespace Nagase.Threads;

public class ChatDataManageThread
{
    static readonly object instanceLock = new();
    static ChatDataManageThread instance;

    readonly ConcurrentDictionary<Guid, ChatData> ChatDatas;

    readonly Logger logger;
    readonly Thread thread;

    CancellationToken cancellationToken;

    public ChatDataManageThread()
    {
        logger = LogManager.GetCurrentClassLogger();
        ChatDatas = new ConcurrentDictionary<Guid, ChatData>();
        thread = new Thread(Thread);
    }

    public static ChatDataManageThread Instance
    {
        get
        {
            if (instance == null)
            {
                lock (instanceLock)
                {
                    if (instance == null)
                    {
                        instance = new ChatDataManageThread();
                    }
                }
            }

            return instance;
        }
    }

    public void InitAndStart(CancellationToken cancellationToken)
    {
        this.cancellationToken = cancellationToken;
        thread.Start();
    }

    public void Join()
    {
        thread.Join();
    }

    public Guid? GetIDByMessageID(int messageID)
    {
        var kvp = ChatDatas.FirstOrDefault(d => d.Value.Messages.Any(m => m.ID == messageID));
        if (kvp.Equals(default(KeyValuePair<Guid, ChatData>)))
        {
            return null;
        }

        return kvp.Key;
    }

    public Guid CreateChatMessage(ChatData.Message message)
    {
        var data = new ChatData(message);
        ChatDatas[data.ID] = data;
        return data.ID;
    }

    public void AddChatMessage(Guid guid, ChatData.Message message)
    {
        ChatDatas[guid].Messages.Enqueue(message);
        ChatDatas[guid].SetLastAccessTime(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
    }

    public void EditMessage(Guid guid, int messageID, string text)
    {
        ChatDatas[guid].Messages.First(m => m.ID == messageID).EditText(text);
    }

    public ChatMessage[] GenerateOpenAIChatMessages(Guid guid)
    {
        return ChatDatas[guid].Messages.Select(msg => new ChatMessage(msg.IsUser ? "user" : "assistant", msg.Text))
            .ToArray();
    }

    void Thread()
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var count = 0;
                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                foreach (var kvp in ChatDatas)
                {
                    if (now - kvp.Value.LastAccessTime > Config.Instance.ChatLifeTime)
                    {
                        ChatDatas.Remove(kvp.Key, out _);
                        count++;
                    }
                }

                if (count > 0)
                {
                    logger.Info($"{count} timeout talks has been cleared.");
                }

                Task.Delay(1000).Wait();
            }
        }
        catch (Exception e)
        {
            if (e is OperationCanceledException)
            {
                logger.Warn(e);
            }
            else
            {
                logger.Error(e);
            }
        }

    }
}
