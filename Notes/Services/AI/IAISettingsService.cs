using Notes.Models.AI;

namespace Notes.Services.AI;

public interface IAISettingsService
{
    AISettings Settings { get; }
    bool HasApiKey { get; }

    event Action? SettingsChanged;

    Task LoadAsync();
    Task SaveSettingsAsync(AISettings settings);
    Task<string?> GetApiKeyAsync();
    Task SetApiKeyAsync(string apiKey);
    Task ClearApiKeyAsync();
}
