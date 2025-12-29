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

    public async ValueTask DisposeAsync()
    {
        _dotNetRef?.Dispose();
        await ValueTask.CompletedTask;
    }
}
