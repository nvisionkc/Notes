using System.Diagnostics;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.EntityFrameworkCore;
using Notes.Data;
using Notes.Services.Scripting;
using Script = Notes.Data.Entities.Script;

namespace Notes.Services;

public class ScriptingService : IScriptingService
{
    private readonly IDbContextFactory<NotesDbContext> _contextFactory;
    private readonly ScriptOptions _scriptOptions;
    private static readonly TimeSpan ExecutionTimeout = TimeSpan.FromSeconds(30);

    public ScriptingService(IDbContextFactory<NotesDbContext> contextFactory)
    {
        _contextFactory = contextFactory;

        // Configure script options with common imports
        _scriptOptions = ScriptOptions.Default
            .WithImports(
                "System",
                "System.Linq",
                "System.Text",
                "System.Text.RegularExpressions",
                "System.Collections.Generic",
                "System.Net",
                "System.Net.Http",
                "System.Text.Json",
                "System.Threading.Tasks"
            )
            .WithReferences(
                typeof(object).Assembly,
                typeof(Enumerable).Assembly,
                typeof(System.Text.RegularExpressions.Regex).Assembly,
                typeof(System.Net.Http.HttpClient).Assembly,
                typeof(System.Text.Json.JsonDocument).Assembly
            );
    }

    // CRUD Operations
    public Task<Script> CreateScriptAsync(string name = "New Script")
    {
        return Task.FromResult(new Script
        {
            Name = name,
            Code = GetDefaultScriptTemplate(),
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow
        });
    }

    public async Task<Script?> GetScriptAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Scripts.FindAsync(id);
    }

    public async Task<List<Script>> GetAllScriptsAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Scripts
            .OrderByDescending(s => s.ModifiedAt)
            .ToListAsync();
    }

    public async Task SaveScriptAsync(Script script)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        script.ModifiedAt = DateTime.UtcNow;

        if (script.Id == 0)
        {
            context.Scripts.Add(script);
        }
        else
        {
            context.Scripts.Update(script);
        }

        await context.SaveChangesAsync();
    }

    public async Task DeleteScriptAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var script = await context.Scripts.FindAsync(id);
        if (script != null)
        {
            script.IsDeleted = true;
            await context.SaveChangesAsync();
        }
    }

    // Execution
    public async Task<ScriptResult> ExecuteAsync(string code, ScriptGlobals globals,
        CancellationToken cancellationToken = default)
    {
        var result = new ScriptResult();
        var sw = Stopwatch.StartNew();

        try
        {
            // Create timeout token
            using var timeoutCts = new CancellationTokenSource(ExecutionTimeout);
            using var linkedCts = CancellationTokenSource
                .CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            // Execute script
            var scriptResult = await CSharpScript.EvaluateAsync<object?>(
                code,
                _scriptOptions,
                globals,
                typeof(ScriptGlobals),
                linkedCts.Token
            );

            result.Success = true;
            result.ReturnValue = scriptResult;
            result.OutputContent = globals.OutputContent;
            result.ConsoleOutput = globals.ConsoleOutput.ToList();
        }
        catch (OperationCanceledException)
        {
            result.Success = false;
            result.ErrorMessage = "Script execution timed out (30 second limit)";
        }
        catch (CompilationErrorException ex)
        {
            result.Success = false;
            result.ErrorMessage = string.Join("\n",
                ex.Diagnostics.Select(d => d.ToString()));
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = $"{ex.GetType().Name}: {ex.Message}";
        }
        finally
        {
            sw.Stop();
            result.ExecutionTime = sw.Elapsed;
            result.ConsoleOutput = globals.ConsoleOutput.ToList();
        }

        return result;
    }

    private static string GetDefaultScriptTemplate() => """
        // Script globals available:
        // - NoteContent (HTML), NotePlainText, NoteTitle
        // - ClipboardText, ClipboardHistory
        // - Print(msg), StripHtml(html), ToHtml(text)

        // Example: Transform note text to uppercase
        var text = StripHtml(NoteContent);
        Print($"Original: {text.Length} characters");

        var result = text.ToUpper();
        OutputContent = ToHtml(result);

        Print($"Transformed to uppercase");
        """;
}
