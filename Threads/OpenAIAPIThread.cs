using System.Collections.Concurrent;
using Nagase.Data;
using Nagase.Services;
using NLog;

namespace Nagase.Threads;

public class OpenAIAPIThread
{
    static readonly object instanceLock = new();
    static OpenAIAPIThread instance;
    readonly Logger logger;
    readonly ConcurrentQueue<OpenAIChatCompletionRequest> pendingRequests;
    readonly Thread thread;
    CancellationToken cancellationToken;

    long lastCallTime;
    Action<OpenAIChatCompletionResponse> openAIResponseHandler;

    public OpenAIAPIThread()
    {
        logger = LogManager.GetCurrentClassLogger();
        lastCallTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        pendingRequests = new ConcurrentQueue<OpenAIChatCompletionRequest>();
        thread = new Thread(Thread);
    }

    public static OpenAIAPIThread Instance
    {
        get
        {
            if (instance == null)
            {
                lock (instanceLock)
                {
                    if (instance == null)
                    {
                        instance = new OpenAIAPIThread();
                    }
                }
            }

            return instance;
        }
    }

    public void InitAndStart(CancellationToken cancellationToken,
        Action<OpenAIChatCompletionResponse> openAIResponseHandler)
    {
        this.openAIResponseHandler = openAIResponseHandler;
        this.cancellationToken = cancellationToken;
        thread.Start();
    }

    public void Join()
    {
        thread.Join();
    }

    public void AddRequest(OpenAIChatCompletionRequest request)
    {
        pendingRequests.Enqueue(request);
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
                    if (pendingRequests.TryDequeue(out var request))
                    {
                        logger.Debug($"There are {pendingRequests.Count + 1} request(s) left in OpenAIAPIThread.");
                        OpenAIService.Instance.OpenAIChatCompletionAsync(request, openAIResponseHandler);
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
