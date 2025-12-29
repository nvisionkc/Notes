namespace Notes.Modules.Abstractions;

/// <summary>
/// Allows modules to contribute methods and properties to the scripting system.
/// These become available to user scripts via a prefix (e.g., "Http.Get()").
/// </summary>
public interface IScriptGlobalsExtension
{
    /// <summary>
    /// Unique prefix for this extension's methods (e.g., "Http" for Http.Get())
    /// This becomes the property name in the script context.
    /// </summary>
    string Prefix { get; }

    /// <summary>
    /// Get metadata about all methods this extension provides.
    /// Used for IntelliSense and documentation generation.
    /// </summary>
    IEnumerable<ScriptMethodMetadata> GetMethodMetadata();

    /// <summary>
    /// Create an instance of this extension to be used during script execution.
    /// The instance will be added to the script context as a property named {Prefix}.
    /// </summary>
    object CreateInstance(IServiceProvider services, ScriptExecutionContext context);
}

/// <summary>
/// Metadata describing a script method for IntelliSense support.
/// </summary>
public class ScriptMethodMetadata
{
    /// <summary>
    /// Method name (without prefix)
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Human-readable description for documentation
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Return type as a string (e.g., "string", "Task&lt;string&gt;")
    /// </summary>
    public required string ReturnType { get; init; }

    /// <summary>
    /// Method parameters
    /// </summary>
    public IReadOnlyList<ScriptParameterMetadata> Parameters { get; init; } = Array.Empty<ScriptParameterMetadata>();

    /// <summary>
    /// Optional example usage
    /// </summary>
    public string? Example { get; init; }
}

/// <summary>
/// Metadata for a method parameter
/// </summary>
public class ScriptParameterMetadata
{
    /// <summary>
    /// Parameter name
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Parameter type as a string
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Optional description
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Whether this parameter is optional
    /// </summary>
    public bool IsOptional { get; init; }

    /// <summary>
    /// Default value if optional (as string representation)
    /// </summary>
    public string? DefaultValue { get; init; }
}

/// <summary>
/// Context provided to script extensions during execution.
/// </summary>
public class ScriptExecutionContext
{
    /// <summary>
    /// The base script globals (NoteContent, ClipboardText, etc.)
    /// </summary>
    public required object Globals { get; init; }

    /// <summary>
    /// Cancellation token for the script execution
    /// </summary>
    public CancellationToken CancellationToken { get; init; }

    /// <summary>
    /// Service provider for resolving dependencies
    /// </summary>
    public required IServiceProvider Services { get; init; }
}
