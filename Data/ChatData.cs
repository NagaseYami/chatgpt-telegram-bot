using System.Collections.Concurrent;

namespace Nagase.Data;

public class ChatData
{
    public ChatData(Message firstMessage)
    {
        ID = Guid.NewGuid();
        Messages = new ConcurrentQueue<Message>();
        LastAccessTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        Messages.Enqueue(firstMessage);
    }

    public Guid ID { get; }

    public ConcurrentQueue<Message> Messages { get; }

    public long LastAccessTime { get; private set; }

    public void SetLastAccessTime(long t)
    {
        LastAccessTime = t;
    }

    public class Message
    {
        public Message(string text, bool isUser, int id, int? replayToId = null)
        {
            Text = text;
            IsUser = isUser;
            ID = id;
            ReplayToID = replayToId;
        }

        public string Text { get; private set; }

        public bool IsUser { get; }

        public int ID { get; }

        public int? ReplayToID { get; }

        public void EditText(string text)
        {
            Text = text;
        }
    }
}
