namespace Nagomi.Api.Features.HelpChat;

/// <summary>Server-side configuration for the in-app help chat. The API key never leaves the backend.</summary>
public sealed class HelpChatOptions
{
    public const string SectionName = "HelpChat";

    public bool Enabled { get; set; }
    public string? BaseUrl { get; set; }
    public string? ApiKey { get; set; }
    public string? Model { get; set; }
    public string? SystemPrompt { get; set; }
    /// <summary>Send Nagomi's domain tools to the model (function calling). Default true.</summary>
    public bool EnableTools { get; set; } = true;

    public bool IsConfigured =>
        Enabled &&
        !string.IsNullOrWhiteSpace(BaseUrl) &&
        !string.IsNullOrWhiteSpace(ApiKey) &&
        !string.IsNullOrWhiteSpace(Model);
}

public sealed record HelpChatStatusResponse(bool Enabled);

public sealed class HelpChatHistoryItem
{
    public string Role { get; set; } = "user";
    public string Content { get; set; } = "";

    public HelpChatHistoryItem() { }
    public HelpChatHistoryItem(string role, string content) { Role = role; Content = content; }
}

public sealed class HelpChatMessageRequest
{
    public string Message { get; set; } = "";
    public List<HelpChatHistoryItem> History { get; set; } = new();

    public HelpChatMessageRequest() { }
    public HelpChatMessageRequest(string message, List<HelpChatHistoryItem>? history)
    {
        Message = message;
        History = history ?? new();
    }
}

public sealed record HelpChatMessageResponse(string Reply);
