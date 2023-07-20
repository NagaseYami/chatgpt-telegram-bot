namespace Nagase;

public class Config
{
    public const string EnvDebugMode = "DEBUG_MODE";
    public const string EnvOpenAIApiKey = "OPENAI_API_KEY";
    public const string EnvOpenAIApiRateLimit = "OPENAI_API_RATE_LIMIT";
    public const string EnvTelegramBotApiToken = "TELEGRAM_BOT_API_TOKEN";
    public const string EnvTelegramBotApiRateLimit = "TELEGRAM_BOT_API_RATE_LIMIT";
    public const string EnvChatLifeTime = "CHAT_LIFE_TIME";
    public const string EnvChatIdWhiteList = "CHAT_ID_WHITE_LIST";
    public const string EnvUsernameWhiteList = "USERNAME_WHITE_LIST";
    public const string EnvChatCommand = "CHAT_COMMAND";

    const int DefaultChatLifeTime = 300;
    const string DefaultChatCommand = "chat";
    
    static Config? instance;

    static readonly object lockObject = new();
    readonly long[] chatIdWhiteList;
    readonly long chatLifeTime;

    readonly bool debugMode;
    readonly string openAIApiKey;
    readonly long openAIApiRateLimit;
    readonly long telegramBotApiRateLimit;
    readonly string telegramBotApiToken;
    readonly string[] usernameWhiteList;
    readonly string chatCommand;

    Config()
    {
        GetBoolEnvironmentVariable(out debugMode, EnvDebugMode);
        GetStringEnvironmentVariable(out openAIApiKey, EnvOpenAIApiKey);
        GetLongEnvironmentVariable(out openAIApiRateLimit, EnvOpenAIApiRateLimit);
        GetStringEnvironmentVariable(out telegramBotApiToken, EnvTelegramBotApiToken);
        GetLongEnvironmentVariable(out telegramBotApiRateLimit, EnvTelegramBotApiRateLimit);
        GetLongEnvironmentVariable(out chatLifeTime, EnvChatLifeTime, DefaultChatLifeTime);
        GetLongArrayEnvironmentVariable(out chatIdWhiteList, EnvChatIdWhiteList);
        GetStringArrayEnvironmentVariable(out usernameWhiteList, EnvUsernameWhiteList);
        GetStringEnvironmentVariable(out chatCommand, EnvChatCommand, DefaultChatCommand);
    }

    public static Config Instance
    {
        get
        {
            if (instance == null)
            {
                lock (lockObject)
                {
                    instance ??= new Config();
                }
            }

            return instance;
        }
    }

    public bool DebugMode => debugMode;

    public string OpenAIApiKey => openAIApiKey;

    public long OpenAIApiRateLimit => openAIApiRateLimit;

    public string TelegramBotApiToken => telegramBotApiToken;

    public long TelegramBotApiRateLimit => telegramBotApiRateLimit;

    public long ChatLifeTime => chatLifeTime;

    public long[] ChatIdWhiteList => chatIdWhiteList;

    public string[] UsernameWhiteList => usernameWhiteList;

    public string ChatCommand => chatCommand;

    void GetStringEnvironmentVariable(out string field, string env, string defaultValue = "")
    {
        var nullableStr = Environment.GetEnvironmentVariable(env);
        if (!string.IsNullOrWhiteSpace(nullableStr))
        {
            field = nullableStr;
            return;
        }

        field = defaultValue;
    }

    void GetStringArrayEnvironmentVariable(out string[] field, string env)
    {
        GetStringEnvironmentVariable(out var str, env);
        field = str.Split(",");
    }

    void GetIntEnvironmentVariable(out int field, string env, int defaultValue = default)
    {
        var nullableStr = Environment.GetEnvironmentVariable(env);
        if (!string.IsNullOrWhiteSpace(nullableStr))
        {
            if (int.TryParse(nullableStr, out var value))
            {
                field = value;
                return;
            }
        }

        field = defaultValue;
    }

    void GetLongArrayEnvironmentVariable(out long[] field, string env)
    {
        GetStringEnvironmentVariable(out var strEnv, env);
        field = strEnv.Split(",").Select(s =>
        {
            long.TryParse(s, out var o);
            return o;
        }).ToArray();
    }

    void GetLongEnvironmentVariable(out long field, string env, long defaultValue = default)
    {
        var nullableStr = Environment.GetEnvironmentVariable(env);
        if (!string.IsNullOrWhiteSpace(nullableStr))
        {
            if (int.TryParse(nullableStr, out var value))
            {
                field = value;
                return;
            }
        }

        field = defaultValue;
    }

    void GetBoolEnvironmentVariable(out bool field, string env, bool defaultValue = default)
    {
        var nullableStr = Environment.GetEnvironmentVariable(env);
        if (!string.IsNullOrWhiteSpace(nullableStr))
        {
            if (bool.TryParse(nullableStr, out var value))
            {
                field = value;
                return;
            }
        }

        field = defaultValue;
    }
}
