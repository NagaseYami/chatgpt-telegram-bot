using NLog;
using NLog.Config;
using NLog.Targets;

namespace Nagase;

public static class Program
{
    static async Task Main()
    {
        InitNLog();
        var logger = LogManager.GetCurrentClassLogger();

        if (Config.Instance.DebugMode)
        {
            logger.Debug($"{Config.EnvDebugMode} has been set to true.");
        }

        if (string.IsNullOrEmpty(Config.Instance.OpenAIApiKey))
        {
            logger.Fatal($"{Config.EnvOpenAIApiKey} not set, the program will now terminate.");
        }

        if (string.IsNullOrEmpty(Config.Instance.TelegramBotApiToken))
        {
            logger.Fatal($"{Config.EnvTelegramBotApiToken} not set, the program will now terminate.");
        }

        await Bot.Instance.StartAsync();
    }

    static void InitNLog()
    {
        var config = new LoggingConfiguration();

        var consoleTarget = new ConsoleTarget("chatgpt-telegram-bot");

        if (Config.Instance.DebugMode)
        {
            config.AddRule(LogLevel.Debug, LogLevel.Fatal, consoleTarget);
        }
        else
        {
            config.AddRule(LogLevel.Info, LogLevel.Fatal, consoleTarget);
        }

        LogManager.Configuration = config;
    }
}
