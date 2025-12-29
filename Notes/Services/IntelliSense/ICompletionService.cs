namespace Notes.Services.IntelliSense;

/// <summary>
/// Service for providing IntelliSense completion data
/// </summary>
public interface ICompletionService
{
    /// <summary>
    /// Get all completion data as JSON for Monaco editor
    /// </summary>
    Task<string> GetCompletionDataJsonAsync();

    /// <summary>
    /// Get completion data object
    /// </summary>
    Task<CompletionData> GetCompletionDataAsync();

    /// <summary>
    /// Get Roslyn-based completions at a specific position
    /// </summary>
    Task<List<CompletionItem>> GetRoslynCompletionsAsync(string code, int position, CancellationToken cancellationToken = default);

    /// <summary>
    /// Register completions from a module extension
    /// </summary>
    void RegisterModuleCompletions(ExtensionCompletion extension);

    /// <summary>
    /// Refresh module completions (call when modules change)
    /// </summary>
    void RefreshModuleCompletions();
}
