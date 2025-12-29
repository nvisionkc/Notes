using Notes.Data.Entities;
using Notes.Services.Scripting;

namespace Notes.Services;

public interface IScriptingService
{
    // CRUD Operations
    Task<Script> CreateScriptAsync(string name = "New Script");
    Task<Script?> GetScriptAsync(int id);
    Task<List<Script>> GetAllScriptsAsync();
    Task SaveScriptAsync(Script script);
    Task DeleteScriptAsync(int id);

    // Execution
    Task<ScriptResult> ExecuteAsync(string code, ScriptGlobals globals,
        CancellationToken cancellationToken = default);
}
