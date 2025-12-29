using System.Text.Json;
using Microsoft.JSInterop;

namespace Notes.Services.IntelliSense;

/// <summary>
/// JSInterop service to provide completion data to Monaco editor
/// </summary>
public class CompletionInterop : IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private readonly ICompletionService _completionService;
    private DotNetObjectReference<CompletionInterop>? _dotNetRef;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public CompletionInterop(IJSRuntime jsRuntime, ICompletionService completionService)
    {
        _jsRuntime = jsRuntime;
        _completionService = completionService;
    }

    /// <summary>
    /// Initialize completions in Monaco editor
    /// </summary>
    public async Task InitializeAsync()
    {
        _dotNetRef = DotNetObjectReference.Create(this);
        var completionJson = await _completionService.GetCompletionDataJsonAsync();
        await _jsRuntime.InvokeVoidAsync("initializeCompletions", completionJson, _dotNetRef);
    }

    /// <summary>
    /// Refresh completions (call when modules change)
    /// </summary>
    public async Task RefreshAsync()
    {
        _completionService.RefreshModuleCompletions();
        var completionJson = await _completionService.GetCompletionDataJsonAsync();
        await _jsRuntime.InvokeVoidAsync("updateCompletions", completionJson);
    }

    /// <summary>
    /// Get completion data JSON (called from JS if needed)
    /// </summary>
    [JSInvokable]
    public async Task<string> GetCompletionDataAsync()
    {
        return await _completionService.GetCompletionDataJsonAsync();
    }

    /// <summary>
    /// Get Roslyn-based completions at a specific position (called from JS)
    /// </summary>
    [JSInvokable]
    public async Task<string> GetRoslynCompletionsAsync(string code, int position)
    {
        try
        {
            var completions = await _completionService.GetRoslynCompletionsAsync(code, position);
            return JsonSerializer.Serialize(completions, JsonOptions);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Roslyn completions error: {ex.Message}");
            return "[]";
        }
    }

    public async ValueTask DisposeAsync()
    {
        _dotNetRef?.Dispose();
        await ValueTask.CompletedTask;
    }
}
