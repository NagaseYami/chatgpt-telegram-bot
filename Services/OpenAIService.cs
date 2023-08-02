using Nagase.Data;
using NLog;
using OpenAI;
using OpenAI.ObjectModels;
using OpenAI.ObjectModels.RequestModels;

namespace Nagase.Services;

public class OpenAIService
{
    static readonly object instanceLock = new();
    static OpenAIService instance;
    readonly Logger logger;
    readonly OpenAI.Managers.OpenAIService openAI;

    OpenAIService()
    {
        logger = LogManager.GetCurrentClassLogger();
        openAI = new OpenAI.Managers.OpenAIService(new OpenAiOptions { ApiKey = Config.Instance.OpenAIApiKey });
    }

    public static OpenAIService Instance
    {
        get
        {
            if (instance == null)
            {
                lock (instanceLock)
                {
                    if (instance == null)
                    {
                        instance = new OpenAIService();
                    }
                }
            }

            return instance;
        }
    }

    public async Task OpenAIChatCompletionAsync(OpenAIChatCompletionRequest req,
        Action<OpenAIChatCompletionResponse> onSuccess)
    {
        var messages = req.Messages;
        var completionResult = await openAI.ChatCompletion.CreateCompletion(new ChatCompletionCreateRequest
        {
            Messages = messages, Model = Models.ChatGpt3_5Turbo
        });

        if (completionResult.Successful)
        {
            var result = completionResult.Choices.First().Message.Content;
            logger.Debug($"Recived response from OpenAI :\n{result}");
            onSuccess(new OpenAIChatCompletionResponse(req, result));
        }
        else if (completionResult.Error == null)
        {
            logger.Error("An unknown error occurred while calling the OpenAI API.");
        }
        else
        {
            logger.Error(
                $"An error occurred while calling the OpenAI API.\nError code: {completionResult.Error.Code}\nError message: {completionResult.Error.Message}");
        }
    }
}
