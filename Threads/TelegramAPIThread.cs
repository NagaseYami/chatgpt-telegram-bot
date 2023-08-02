using System.Collections.Concurrent;
using Nagase.Data;
using Nagase.Services;
using NLog;

namespace Nagase.Threads;

public class TelegramAPIThread
{
    static readonly object instanceLock = new();

    static TelegramAPIThread instance;
    readonly Logger logger;
    readonly ConcurrentQueue<TelegramEditMessageRequest> pendingEditMessageRequests;
    readonly ConcurrentQueue<TelegramSendMessageRequest> pendingSendMessageRequests;
    readonly Thread thread;
    CancellationToken cancellationToken;
    Action<TelegramEditMessageResponse> editMessageHandler;
    long lastCallTime;
    Action<TelegramSendMessageResponse> sendMessageHandler;

    TelegramAPIThread()
    {
        logger = LogManager.GetCurrentClassLogger();
        thread = new Thread(Thread);
        pendingSendMessageRequests = new ConcurrentQueue<TelegramSendMessageRequest>();
        pendingEditMessageRequests = new ConcurrentQueue<TelegramEditMessageRequest>();
        lastCallTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    public static TelegramAPIThread Instance
    {
        get
        {
            if (instance == null)
            {
                lock (instanceLock)
                {
                    if (instance == null)
                    {
                        instance = new TelegramAPIThread();
                    }

                }
            }

            return instance;
        }
    }

    public void InitAndStart(Action<TelegramSendMessageResponse> sendMessageHandler,
        Action<TelegramEditMessageResponse> editMessageHandler, CancellationToken token)
    {
        this.sendMessageHandler = sendMessageHandler;
        this.editMessageHandler = editMessageHandler;
        cancellationToken = token;
        thread.Start();
    }

    public void Join()
    {
        thread.Join();
    }

    public void AddEditMessageRequest(TelegramEditMessageRequest req)
    {
        pendingEditMessageRequests.Enqueue(req);
    }

    public void AddSendMessageRequest(TelegramSendMessageRequest req)
    {
        pendingSendMessageRequests.Enqueue(req);
    }

    void Thread()
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - lastCallTime >
                    Config.Instance.TelegramBotApiRateLimit)
                {
                    if (pendingEditMessageRequests.TryDequeue(out var editRequest))
                    {
                        logger.Debug($"There are {pendingEditMessageRequests.Count + 1} edit request(s) left in TelegramAPIThread.");
                        TelegramService.Instance.EditMessageAsync(editRequest, editMessageHandler);
                        lastCallTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    }
                    else if (pendingSendMessageRequests.TryDequeue(out var sendRequest))
                    {
                        logger.Debug($"There are {pendingSendMessageRequests.Count + 1} send request(s) left in TelegramAPIThread.");
                        TelegramService.Instance.SendMessageAsync(sendRequest, sendMessageHandler);
                        lastCallTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    }
                }

                Task.Delay(100).Wait();
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
