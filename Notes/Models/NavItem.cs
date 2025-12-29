namespace Notes.Models;

/// <summary>
/// Represents a navigation item in the icon rail
/// </summary>
public class NavItem
{
    /// <summary>
    /// Unique identifier for this navigation item
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Display label shown when rail is expanded
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Icon content (SVG markup or emoji)
    /// </summary>
    public string Icon { get; set; } = string.Empty;

    /// <summary>
    /// Type of icon: "svg" or "emoji"
    /// </summary>
    public string IconType { get; set; } = "svg";

    /// <summary>
    /// Sort order (lower values appear first)
    /// </summary>
    public int Order { get; set; } = 100;

    /// <summary>
    /// Whether this nav item is enabled
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Optional: The Blazor component type to render in sidebar when selected
    /// </summary>
    public Type? PanelComponent { get; set; }

    /// <summary>
    /// Optional: Parameters to pass to the panel component
    /// </summary>
    public Dictionary<string, object>? PanelParameters { get; set; }

    /// <summary>
    /// Source of this nav item: "builtin" or module ID
    /// </summary>
    public string Source { get; set; } = "builtin";
}
