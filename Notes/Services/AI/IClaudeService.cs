using Notes.Models.AI;

namespace Notes.Services.AI;

public interface IClaudeService
{
    bool IsConfigured { get; }

    Task<ChatResponse> SendMessageAsync(
        string message,
        List<ChatMessage>? history = null,
        string? additionalSystemPrompt = null,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<string> StreamMessageAsync(
        string message,
        List<ChatMessage>? history = null,
        string? additionalSystemPrompt = null,
        CancellationToken cancellationToken = default);

    Task<bool> ValidateApiKeyAsync(string apiKey);
}
