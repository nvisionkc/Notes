namespace Notes.Services.AI;

/// <summary>
/// Simple singleton to share script editor state with AI chat panel.
/// </summary>
public interface IScriptEditorState
{
    string? CurrentContent { get; set; }
    string? CurrentScriptName { get; set; }
}

public class ScriptEditorState : IScriptEditorState
{
    public string? CurrentContent { get; set; }
    public string? CurrentScriptName { get; set; }
}
