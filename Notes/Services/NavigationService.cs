using Notes.Models;

namespace Notes.Services;

/// <summary>
/// Manages navigation state for the icon rail
/// </summary>
public class NavigationService : INavigationService
{
    private readonly List<NavItem> _navItems = new();
    private string _activeItemId = "notes";
    private bool _isRailExpanded = false;

    private const string ExpandedStateKey = "NavRailExpanded";
    private const string ActiveItemKey = "NavActiveItem";

    public IReadOnlyList<NavItem> NavItems => _navItems
        .Where(x => x.IsEnabled)
        .OrderBy(x => x.Order)
        .ToList();

    public string ActiveItemId => _activeItemId;
    public bool IsRailExpanded => _isRailExpanded;

    public event Action? NavigationChanged;
    public event Action? RailExpandedChanged;

    public void RegisterNavItem(NavItem item)
    {
        // Don't add duplicates
        if (_navItems.Any(x => x.Id == item.Id))
        {
            return;
        }

        _navItems.Add(item);
        NavigationChanged?.Invoke();
    }

    public void UnregisterNavItem(string itemId)
    {
        var item = _navItems.FirstOrDefault(x => x.Id == itemId);
        if (item != null)
        {
            _navItems.Remove(item);

            // If we removed the active item, switch to first available
            if (_activeItemId == itemId && _navItems.Count > 0)
            {
                _activeItemId = _navItems.OrderBy(x => x.Order).First().Id;
            }

            NavigationChanged?.Invoke();
        }
    }

    public void SetActiveItem(string itemId)
    {
        if (_activeItemId != itemId && _navItems.Any(x => x.Id == itemId))
        {
            _activeItemId = itemId;
            NavigationChanged?.Invoke();
            _ = SaveStateAsync();
        }
    }

    public void ToggleRailExpanded()
    {
        _isRailExpanded = !_isRailExpanded;
        RailExpandedChanged?.Invoke();
        _ = SaveStateAsync();
    }

    public void SetRailExpanded(bool expanded)
    {
        if (_isRailExpanded != expanded)
        {
            _isRailExpanded = expanded;
            RailExpandedChanged?.Invoke();
        }
    }

    public Task LoadStateAsync()
    {
        try
        {
            _isRailExpanded = Preferences.Default.Get(ExpandedStateKey, false);
            var savedActiveItem = Preferences.Default.Get(ActiveItemKey, "notes");

            // Only use saved active item if it exists in our nav items
            if (_navItems.Any(x => x.Id == savedActiveItem))
            {
                _activeItemId = savedActiveItem;
            }
        }
        catch
        {
            // Ignore preference errors, use defaults
        }

        return Task.CompletedTask;
    }

    public Task SaveStateAsync()
    {
        try
        {
            Preferences.Default.Set(ExpandedStateKey, _isRailExpanded);
            Preferences.Default.Set(ActiveItemKey, _activeItemId);
        }
        catch
        {
            // Ignore preference errors
        }

        return Task.CompletedTask;
    }
}
