using Notes.Models;
using Notes.Modules.Abstractions;
using Notes.Services;

namespace Notes.Modules.Services;

/// <summary>
/// Interface for centralized module management
/// </summary>
public interface IModuleManager
{
    /// <summary>
    /// All loaded modules
    /// </summary>
    IReadOnlyList<LoadedModule> Modules { get; }

    /// <summary>
    /// Get all navigation items contributed by modules
    /// </summary>
    IEnumerable<NavItem> GetModuleNavigationItems();

    /// <summary>
    /// Get all script extensions from modules
    /// </summary>
    IEnumerable<IScriptGlobalsExtension> GetScriptExtensions();

    /// <summary>
    /// Register module navigation items with the navigation service
    /// </summary>
    void RegisterModuleNavigationItems(INavigationService navigationService);
}

/// <summary>
/// Centralized management of loaded modules and their contributions
/// </summary>
public class ModuleManager : IModuleManager
{
    private readonly IModuleLoader _loader;
    private readonly List<NavItem> _navigationItems = new();
    private readonly List<IScriptGlobalsExtension> _scriptExtensions = new();

    public IReadOnlyList<LoadedModule> Modules => _loader.LoadedModules;

    public ModuleManager(IModuleLoader loader)
    {
        _loader = loader;
        CollectContributions();
    }

    private void CollectContributions()
    {
        foreach (var loaded in _loader.LoadedModules.Where(m => m.Status == ModuleLoadStatus.Initialized))
        {
            // Collect navigation items from modules implementing INavigationContributor
            if (loaded.Module is INavigationContributor navContributor)
            {
                foreach (var moduleNavItem in navContributor.GetNavigationItems())
                {
                    _navigationItems.Add(new NavItem
                    {
                        Id = moduleNavItem.Key,
                        Label = moduleNavItem.Label,
                        Icon = moduleNavItem.Icon ?? "",
                        IconType = moduleNavItem.IconType,
                        Order = moduleNavItem.Order,
                        PanelComponent = moduleNavItem.ComponentType,
                        PanelParameters = moduleNavItem.Parameters?.ToDictionary(x => x.Key, x => x.Value),
                        Source = loaded.Module.Id
                    });
                }
            }

            // Collect script extensions
            var extensionTypes = loaded.Assembly.GetExportedTypes()
                .Where(t => typeof(IScriptGlobalsExtension).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface);

            foreach (var extType in extensionTypes)
            {
                try
                {
                    var extension = (IScriptGlobalsExtension)Activator.CreateInstance(extType)!;
                    _scriptExtensions.Add(extension);
                }
                catch
                {
                    // Skip extensions that can't be instantiated
                }
            }
        }

        // Sort navigation items by order
        _navigationItems.Sort((a, b) => a.Order.CompareTo(b.Order));
    }

    public IEnumerable<NavItem> GetModuleNavigationItems() => _navigationItems;

    public IEnumerable<IScriptGlobalsExtension> GetScriptExtensions() => _scriptExtensions;

    public void RegisterModuleNavigationItems(INavigationService navigationService)
    {
        foreach (var navItem in _navigationItems)
        {
            navigationService.RegisterNavItem(navItem);
        }
    }
}
