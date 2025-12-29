namespace Notes.Modules.Abstractions;

/// <summary>
/// Allows modules to contribute navigation items to the icon rail.
/// Implement this interface on your IModule class to add navigation.
/// </summary>
public interface INavigationContributor
{
    /// <summary>
    /// Get navigation items this module wants to add to the icon rail.
    /// </summary>
    IEnumerable<ModuleNavigationItem> GetNavigationItems();
}

/// <summary>
/// Represents a navigation item that a module contributes.
/// </summary>
public class ModuleNavigationItem
{
    /// <summary>
    /// Unique key for this navigation item (used as tab ID)
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// Display label for the navigation item
    /// </summary>
    public required string Label { get; init; }

    /// <summary>
    /// Icon content (SVG markup or emoji)
    /// </summary>
    public string? Icon { get; init; }

    /// <summary>
    /// Type of icon: "svg" or "emoji"
    /// </summary>
    public string IconType { get; init; } = "svg";

    /// <summary>
    /// Sort order (lower values appear first, built-in items use 10-30)
    /// </summary>
    public int Order { get; init; } = 100;

    /// <summary>
    /// Type of the Razor component to render when this item is selected.
    /// Must be a Blazor component type from the module's assembly.
    /// </summary>
    public required Type ComponentType { get; init; }

    /// <summary>
    /// Optional parameters to pass to the component
    /// </summary>
    public IDictionary<string, object>? Parameters { get; init; }
}
