using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Notes.Models.AI;

namespace Notes.Services.AI;

public class ClaudeService : IClaudeService
{
    private const string ApiBaseUrl = "https://api.anthropic.com/v1/messages";
    private const string AnthropicVersion = "2023-06-01";

    private readonly IAISettingsService _settingsService;
    private readonly HttpClient _httpClient;

    public bool IsConfigured => _settingsService.HasApiKey;

    public ClaudeService(IAISettingsService settingsService)
    {
        _settingsService = settingsService;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("anthropic-version", AnthropicVersion);
    }

    public async Task<ChatResponse> SendMessageAsync(
        string message,
        List<ChatMessage>? history = null,
        string? additionalSystemPrompt = null,
        CancellationToken cancellationToken = default)
    {
        var apiKey = await _settingsService.GetApiKeyAsync();
        if (string.IsNullOrEmpty(apiKey))
        {
            return new ChatResponse
            {
                Success = false,
                ErrorMessage = "API key not configured"
            };
        }

        try
        {
            var settings = _settingsService.Settings;
            var systemPrompt = BuildSystemPrompt(settings.SystemPrompt, additionalSystemPrompt);
            var messages = BuildMessages(message, history);

            var request = new ClaudeRequest
            {
                Model = settings.Model,
                MaxTokens = settings.MaxTokens,
                System = systemPrompt,
                Messages = messages,
                Stream = false
            };

            var json = JsonSerializer.Serialize(request, JsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, ApiBaseUrl);
            httpRequest.Headers.Add("x-api-key", apiKey);
            httpRequest.Content = content;

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = JsonSerializer.Deserialize<ClaudeErrorResponse>(responseJson, JsonOptions);
                return new ChatResponse
                {
                    Success = false,
                    ErrorMessage = error?.Error?.Message ?? $"API error: {response.StatusCode}"
                };
            }

            var result = JsonSerializer.Deserialize<ClaudeResponse>(responseJson, JsonOptions);
            var textContent = result?.Content?.FirstOrDefault(c => c.Type == "text")?.Text;

            return new ChatResponse
            {
                Success = true,
                Content = textContent,
                ModelUsed = result?.Model,
                InputTokens = result?.Usage?.InputTokens ?? 0,
                OutputTokens = result?.Usage?.OutputTokens ?? 0
            };
        }
        catch (OperationCanceledException)
        {
            return new ChatResponse
            {
                Success = false,
                ErrorMessage = "Request cancelled"
            };
        }
        catch (Exception ex)
        {
            return new ChatResponse
            {
                Success = false,
                ErrorMessage = $"Error: {ex.Message}"
            };
        }
    }

    public async IAsyncEnumerable<string> StreamMessageAsync(
        string message,
        List<ChatMessage>? history = null,
        string? additionalSystemPrompt = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var apiKey = await _settingsService.GetApiKeyAsync();
        if (string.IsNullOrEmpty(apiKey))
        {
            yield return "[Error: API key not configured]";
            yield break;
        }

        var settings = _settingsService.Settings;
        var systemPrompt = BuildSystemPrompt(settings.SystemPrompt, additionalSystemPrompt);
        var messages = BuildMessages(message, history);

        var request = new ClaudeRequest
        {
            Model = settings.Model,
            MaxTokens = settings.MaxTokens,
            System = systemPrompt,
            Messages = messages,
            Stream = true
        };

        var json = JsonSerializer.Serialize(request, JsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, ApiBaseUrl);
        httpRequest.Headers.Add("x-api-key", apiKey);
        httpRequest.Content = content;

        using var response = await _httpClient.SendAsync(
            httpRequest,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            System.Diagnostics.Debug.WriteLine($"Claude API error: {response.StatusCode} - {errorBody}");
            yield return $"[API Error: {response.StatusCode}]";
            yield break;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) != null && !cancellationToken.IsCancellationRequested)
        {
            if (string.IsNullOrEmpty(line)) continue;

            if (line.StartsWith("data: "))
            {
                var eventData = line[6..];
                if (eventData == "[DONE]") break;

                var streamEvent = JsonSerializer.Deserialize<ClaudeStreamEvent>(eventData, JsonOptions);
                if (streamEvent?.Type == "content_block_delta" &&
                    streamEvent.Delta?.Type == "text_delta" &&
                    !string.IsNullOrEmpty(streamEvent.Delta.Text))
                {
                    yield return streamEvent.Delta.Text;
                }
            }
        }
    }

    public async Task<bool> ValidateApiKeyAsync(string apiKey)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, ApiBaseUrl);
            request.Headers.Add("x-api-key", apiKey);

            var body = new ClaudeRequest
            {
                Model = ClaudeModels.Haiku, // Use cheapest model for validation
                MaxTokens = 10,
                Messages = [new ClaudeMessage { Role = "user", Content = "Hi" }]
            };

            request.Content = new StringContent(
                JsonSerializer.Serialize(body, JsonOptions),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.SendAsync(request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static string BuildSystemPrompt(string basePrompt, string? additionalPrompt)
    {
        if (string.IsNullOrEmpty(additionalPrompt))
            return basePrompt;

        return $"{basePrompt}\n\n{additionalPrompt}";
    }

    private static List<ClaudeMessage> BuildMessages(string message, List<ChatMessage>? history)
    {
        var messages = new List<ClaudeMessage>();

        if (history != null)
        {
            foreach (var msg in history)
            {
                messages.Add(new ClaudeMessage
                {
                    Role = msg.Role == ChatRole.User ? "user" : "assistant",
                    Content = msg.Content
                });
            }
        }

        messages.Add(new ClaudeMessage
        {
            Role = "user",
            Content = message
        });

        return messages;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    #region API Models

    private class ClaudeRequest
    {
        public string Model { get; set; } = "";
        public int MaxTokens { get; set; }
        public string? System { get; set; }
        public List<ClaudeMessage> Messages { get; set; } = [];
        public bool Stream { get; set; }
    }

    private class ClaudeMessage
    {
        public string Role { get; set; } = "";
        public string Content { get; set; } = "";
    }

    private class ClaudeResponse
    {
        public string? Id { get; set; }
        public string? Model { get; set; }
        public List<ClaudeContent>? Content { get; set; }
        public ClaudeUsage? Usage { get; set; }
    }

    private class ClaudeContent
    {
        public string Type { get; set; } = "";
        public string? Text { get; set; }
    }

    private class ClaudeUsage
    {
        public int InputTokens { get; set; }
        public int OutputTokens { get; set; }
    }

    private class ClaudeErrorResponse
    {
        public ClaudeError? Error { get; set; }
    }

    private class ClaudeError
    {
        public string? Type { get; set; }
        public string? Message { get; set; }
    }

    private class ClaudeStreamEvent
    {
        public string? Type { get; set; }
        public ClaudeStreamDelta? Delta { get; set; }
    }

    private class ClaudeStreamDelta
    {
        public string? Type { get; set; }
        public string? Text { get; set; }
    }

    #endregion
}
