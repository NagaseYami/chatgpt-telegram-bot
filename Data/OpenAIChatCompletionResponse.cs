namespace Nagase.Data;

public class OpenAIChatCompletionResponse
{
    public OpenAIChatCompletionResponse(OpenAIChatCompletionRequest request, string text)
    {
        Request = request;
        Text = text;
    }

    public OpenAIChatCompletionRequest Request { get; }

    public string Text { get; }
}
