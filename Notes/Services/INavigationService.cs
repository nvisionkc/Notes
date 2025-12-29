using Notes.Models;

namespace Notes.Services;

/// <summary>
/// Service for managing navigation state and the icon rail
/// </summary>
public interface INavigationService
{
    /// <summary>
    /// All registered navigation items, sorted by Order
    /// </summary>
    IReadOnlyList<NavItem> NavItems { get; }

    /// <summary>
    /// Currently active navigation item ID
    /// </summary>
    string ActiveItemId { get; }

    /// <summary>
    /// Whether the icon rail is expanded
    /// </summary>
    bool IsRailExpanded { get; }

    /// <summary>
    /// Fired when navigation state changes (active item, items list)
    /// </summary>
    event Action? NavigationChanged;

    /// <summary>
    /// Fired when rail expanded state changes
    /// </summary>
    event Action? RailExpandedChanged;

    /// <summary>
    /// Register a navigation item
    /// </summary>
    void RegisterNavItem(NavItem item);

    /// <summary>
    /// Unregister a navigation item by ID
    /// </summary>
    void UnregisterNavItem(string itemId);

    /// <summary>
    /// Set the active navigation item
    /// </summary>
    void SetActiveItem(string itemId);

    /// <summary>
    /// Toggle rail expanded/collapsed state
    /// </summary>
    void ToggleRailExpanded();

    /// <summary>
    /// Set rail expanded state explicitly
    /// </summary>
    void SetRailExpanded(bool expanded);

    /// <summary>
    /// Load persisted state
    /// </summary>
    Task LoadStateAsync();

    /// <summary>
    /// Save current state
    /// </summary>
    Task SaveStateAsync();
}
