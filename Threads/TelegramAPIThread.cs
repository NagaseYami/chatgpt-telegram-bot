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

    public TelegramAPIThread()
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

    void Thread()
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (lastCallTime - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() >
                    Config.Instance.TelegramBotApiRateLimit)
                {
                    if (pendingEditMessageRequests.TryDequeue(out var editRequest))
                    {
                        TelegramService.Instance.EditMessageAsync(editRequest, editMessageHandler).Start();
                        lastCallTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    }
                    else if (pendingSendMessageRequests.TryDequeue(out var sendRequest))
                    {
                        TelegramService.Instance.SendMessageAsync(sendRequest, sendMessageHandler).Start();
                        lastCallTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    }
                }
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
