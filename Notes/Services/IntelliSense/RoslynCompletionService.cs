using System.Collections.Immutable;
using System.Composition.Hosting;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Text;
using Notes.Services.Scripting;

namespace Notes.Services.IntelliSense;

/// <summary>
/// Provides Roslyn-based IntelliSense completions for C# scripts
/// </summary>
public class RoslynCompletionService : IDisposable
{
    private readonly AdhocWorkspace _workspace;
    private readonly ProjectId _projectId;
    private DocumentId? _documentId;
    private readonly object _lock = new();

    // Default imports matching ScriptingService
    private static readonly string[] DefaultImports =
    {
        "System",
        "System.Linq",
        "System.Text",
        "System.Text.RegularExpressions",
        "System.Collections.Generic",
        "System.Net",
        "System.Net.Http",
        "System.Text.Json",
        "System.Threading.Tasks",
        "System.Dynamic",
        "System.IO"
    };

    // Default references matching ScriptingService
    private static readonly MetadataReference[] DefaultReferences;

    static RoslynCompletionService()
    {
        // Build metadata references from the same assemblies used in ScriptingService
        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Text.RegularExpressions.Regex).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Net.Http.HttpClient).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Text.Json.JsonDocument).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Dynamic.ExpandoObject).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.IO.File).Assembly.Location),
            // ScriptGlobals type for our custom API
            MetadataReference.CreateFromFile(typeof(ScriptGlobals).Assembly.Location),
        };

        // Add runtime assemblies for full BCL support
        var runtimeDir = System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory();
        var runtimeAssemblies = new[]
        {
            "System.Runtime.dll",
            "System.Collections.dll",
            "System.Console.dll",
            "netstandard.dll",
            "System.Private.CoreLib.dll"
        };

        foreach (var asm in runtimeAssemblies)
        {
            var path = Path.Combine(runtimeDir, asm);
            if (File.Exists(path))
            {
                references.Add(MetadataReference.CreateFromFile(path));
            }
        }

        DefaultReferences = references.ToArray();
    }

    public RoslynCompletionService()
    {
        // Create MEF host with Roslyn's default assemblies
        var assemblies = MefHostServices.DefaultAssemblies;
        var hostServices = MefHostServices.Create(assemblies);

        _workspace = new AdhocWorkspace(hostServices);

        // Create compilation options for scripting
        var compilationOptions = new CSharpCompilationOptions(
            OutputKind.DynamicallyLinkedLibrary,
            usings: DefaultImports,
            allowUnsafe: false,
            checkOverflow: false,
            optimizationLevel: OptimizationLevel.Debug,
            nullableContextOptions: NullableContextOptions.Enable
        );

        var parseOptions = new CSharpParseOptions(
            languageVersion: LanguageVersion.Latest,
            kind: SourceCodeKind.Script
        );

        // Create project
        var projectInfo = ProjectInfo.Create(
            ProjectId.CreateNewId("ScriptProject"),
            VersionStamp.Create(),
            "ScriptProject",
            "ScriptProject",
            LanguageNames.CSharp,
            compilationOptions: compilationOptions,
            parseOptions: parseOptions,
            metadataReferences: DefaultReferences
        );

        var project = _workspace.AddProject(projectInfo);
        _projectId = project.Id;
    }

    /// <summary>
    /// Get completions at the specified position in the code
    /// </summary>
    public async Task<List<CompletionItem>> GetCompletionsAsync(
        string code,
        int position,
        CancellationToken cancellationToken = default)
    {
        var results = new List<CompletionItem>();

        try
        {
            // Build script with globals wrapper
            var wrappedCode = WrapScriptWithGlobals(code);
            var adjustedPosition = GetAdjustedPosition(code, position);

            // Update or create document
            var document = UpdateDocument(wrappedCode);

            // Get completion service (fully qualified to avoid conflict with MAUI's GetService)
            var completionService = Microsoft.CodeAnalysis.Completion.CompletionService.GetService(document);
            if (completionService == null)
            {
                System.Diagnostics.Debug.WriteLine("CompletionService not available");
                return results;
            }

            // Get completions
            var completions = await completionService.GetCompletionsAsync(
                document,
                adjustedPosition,
                cancellationToken: cancellationToken
            );

            if (completions == null)
            {
                return results;
            }

            // Convert to our format
            foreach (var item in completions.ItemsList)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var completionItem = ConvertCompletionItem(item);
                if (completionItem != null)
                {
                    results.Add(completionItem);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Roslyn completion error: {ex.Message}");
        }

        return results;
    }

    /// <summary>
    /// Wrap the script code to include ScriptGlobals members as available variables
    /// </summary>
    private static string WrapScriptWithGlobals(string code)
    {
        // Create a script preamble that makes ScriptGlobals members available
        // This simulates the scripting environment where these are globals
        var preamble = """
            #nullable enable
            // ScriptGlobals simulation for IntelliSense
            string NoteContent = "";
            string NotePlainText = "";
            string NoteTitle = "";
            string ClipboardText = "";
            System.Collections.Generic.List<string> ClipboardHistory = new();
            string? OutputContent = null;
            dynamic Extensions = new System.Dynamic.ExpandoObject();

            void Print(object? value) { }
            void Print(string format, params object[] args) { }
            string StripHtml(string html) => "";
            string ToHtml(string text) => "";
            string TransformLines(string text, System.Func<string, string> transform) => "";
            string TransformLines(string text, System.Func<string, int, string> transform) => "";

            // User code starts here

            """;

        return preamble + code;
    }

    /// <summary>
    /// Adjust the cursor position to account for the preamble
    /// </summary>
    private static int GetAdjustedPosition(string originalCode, int originalPosition)
    {
        var preamble = WrapScriptWithGlobals("").Length;
        return preamble + originalPosition;
    }

    private Document UpdateDocument(string code)
    {
        lock (_lock)
        {
            var sourceText = SourceText.From(code);

            if (_documentId != null)
            {
                // Update existing document
                var currentDoc = _workspace.CurrentSolution.GetDocument(_documentId);
                if (currentDoc != null)
                {
                    var newSolution = currentDoc.WithText(sourceText).Project.Solution;
                    _workspace.TryApplyChanges(newSolution);
                    return _workspace.CurrentSolution.GetDocument(_documentId)!;
                }
            }

            // Create new document
            var project = _workspace.CurrentSolution.GetProject(_projectId)!;
            var documentInfo = DocumentInfo.Create(
                DocumentId.CreateNewId(project.Id, "Script.csx"),
                "Script.csx",
                loader: TextLoader.From(TextAndVersion.Create(sourceText, VersionStamp.Create())),
                filePath: "Script.csx"
            );

            var newProject = project.Solution.AddDocument(documentInfo);
            _workspace.TryApplyChanges(newProject);

            _documentId = documentInfo.Id;
            return _workspace.CurrentSolution.GetDocument(_documentId)!;
        }
    }

    private static CompletionItem? ConvertCompletionItem(Microsoft.CodeAnalysis.Completion.CompletionItem roslynItem)
    {
        // Filter out some items we don't want
        var displayText = roslynItem.DisplayText;

        // Skip compiler-generated or internal items
        if (displayText.StartsWith("__") || displayText.StartsWith("<"))
            return null;

        // Map Roslyn tags to our completion kinds
        var kind = MapCompletionKind(roslynItem.Tags);

        // Build insert text - use the display text or filter text
        var insertText = roslynItem.FilterText ?? displayText;

        // Check if it's a method that needs parentheses
        var isMethod = roslynItem.Tags.Contains("Method") ||
                       roslynItem.Tags.Contains("ExtensionMethod");

        return new CompletionItem
        {
            Label = displayText,
            Kind = kind,
            Detail = GetDetail(roslynItem),
            Documentation = roslynItem.InlineDescription,
            InsertText = insertText,
            IsSnippet = false,
            IsFromRoslyn = true
        };
    }

    private static string MapCompletionKind(ImmutableArray<string> tags)
    {
        // Use string literals instead of WellKnownTags to avoid assembly reference issues
        if (tags.Contains("Method") || tags.Contains("ExtensionMethod"))
            return "Method";
        if (tags.Contains("Property"))
            return "Property";
        if (tags.Contains("Field"))
            return "Field";
        if (tags.Contains("Class"))
            return "Class";
        if (tags.Contains("Structure"))
            return "Struct";
        if (tags.Contains("Interface"))
            return "Interface";
        if (tags.Contains("Enum"))
            return "Enum";
        if (tags.Contains("EnumMember"))
            return "EnumMember";
        if (tags.Contains("Keyword"))
            return "Keyword";
        if (tags.Contains("Namespace"))
            return "Module";
        if (tags.Contains("Local") || tags.Contains("Parameter"))
            return "Variable";
        if (tags.Contains("Snippet"))
            return "Snippet";
        if (tags.Contains("Event"))
            return "Event";
        if (tags.Contains("Delegate"))
            return "Function";

        return "Text";
    }

    private static string? GetDetail(Microsoft.CodeAnalysis.Completion.CompletionItem item)
    {
        // Try to get detail from properties
        if (item.Properties.TryGetValue("SymbolName", out var symbolName))
            return symbolName;

        if (!string.IsNullOrEmpty(item.InlineDescription))
            return item.InlineDescription;

        return null;
    }

    public void Dispose()
    {
        _workspace?.Dispose();
    }
}
