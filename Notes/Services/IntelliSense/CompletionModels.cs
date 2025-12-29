namespace Notes.Services.IntelliSense;

/// <summary>
/// Completion data structure for Monaco editor
/// </summary>
public class CompletionData
{
    /// <summary>
    /// Script globals (NoteContent, Print, etc.)
    /// </summary>
    public List<CompletionItem> Globals { get; set; } = new();

    /// <summary>
    /// Type definitions (string, List, etc.)
    /// </summary>
    public List<TypeCompletion> Types { get; set; } = new();

    /// <summary>
    /// Module-contributed extensions
    /// </summary>
    public List<ExtensionCompletion> Extensions { get; set; } = new();

    /// <summary>
    /// Code snippets (foreach, if, etc.)
    /// </summary>
    public List<SnippetCompletion> Snippets { get; set; } = new();
}

/// <summary>
/// A single completion item
/// </summary>
public class CompletionItem
{
    public string Label { get; set; } = string.Empty;
    public string Kind { get; set; } = "Property"; // Property, Method, Class, etc.
    public string? Detail { get; set; }
    public string? Documentation { get; set; }
    public string? InsertText { get; set; }
    public bool IsSnippet { get; set; }
    public bool IsFromRoslyn { get; set; }
}

/// <summary>
/// Type completion with members
/// </summary>
public class TypeCompletion
{
    public string Name { get; set; } = string.Empty;
    public string? Namespace { get; set; }
    public string Kind { get; set; } = "Class"; // Class, Struct, Interface
    public string? Documentation { get; set; }
    public List<CompletionItem> StaticMembers { get; set; } = new();
    public List<CompletionItem> InstanceMembers { get; set; } = new();
    public List<CompletionItem> Constructors { get; set; } = new();
}

/// <summary>
/// Module extension completion
/// </summary>
public class ExtensionCompletion
{
    public string Prefix { get; set; } = string.Empty;
    public string ModuleId { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<CompletionItem> Methods { get; set; } = new();
}

/// <summary>
/// Code snippet completion
/// </summary>
public class SnippetCompletion
{
    public string Label { get; set; } = string.Empty;
    public string? Detail { get; set; }
    public string InsertText { get; set; } = string.Empty;
    public string? Documentation { get; set; }
}
