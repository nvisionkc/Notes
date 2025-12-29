using System.Reflection;
using Microsoft.Extensions.Logging;
using Notes.Modules.Abstractions;

namespace Notes.Modules.Services;

/// <summary>
/// Represents a loaded module with its metadata
/// </summary>
public class LoadedModule
{
    public required IModule Module { get; init; }
    public required Assembly Assembly { get; init; }
    public required ModuleAssemblyLoadContext LoadContext { get; init; }
    public ModuleLoadStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
}

public enum ModuleLoadStatus
{
    Discovered,
    Loaded,
    Initialized,
    Failed,
    Disabled
}

/// <summary>
/// Service for discovering and loading module DLLs
/// </summary>
public interface IModuleLoader
{
    IReadOnlyList<LoadedModule> LoadedModules { get; }
    Task<IReadOnlyList<LoadedModule>> DiscoverAndLoadModulesAsync(string modulesPath);
    Task InitializeModulesAsync(IServiceProvider services);
    Task ShutdownModulesAsync();
}

public class ModuleLoader : IModuleLoader
{
    private readonly List<LoadedModule> _loadedModules = new();
    private readonly ILogger<ModuleLoader>? _logger;

    public IReadOnlyList<LoadedModule> LoadedModules => _loadedModules.AsReadOnly();

    public ModuleLoader(ILogger<ModuleLoader>? logger = null)
    {
        _logger = logger;
    }

    public Task<IReadOnlyList<LoadedModule>> DiscoverAndLoadModulesAsync(string modulesPath)
    {
        if (!Directory.Exists(modulesPath))
        {
            Directory.CreateDirectory(modulesPath);
            _logger?.LogInformation("Created modules directory: {Path}", modulesPath);
            return Task.FromResult<IReadOnlyList<LoadedModule>>(_loadedModules);
        }

        // Find all DLL files in the Modules folder (look in subdirectories too)
        var dllFiles = Directory.GetFiles(modulesPath, "*.dll", SearchOption.AllDirectories);

        foreach (var dllPath in dllFiles)
        {
            try
            {
                LoadModuleFromDll(dllPath);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to load module from {Path}", dllPath);
            }
        }

        // Sort by dependencies (topological sort)
        SortByDependencies();

        _logger?.LogInformation("Discovered {Count} module(s)", _loadedModules.Count);

        return Task.FromResult<IReadOnlyList<LoadedModule>>(_loadedModules);
    }

    private void LoadModuleFromDll(string dllPath)
    {
        // Create isolated load context for the module
        var loadContext = new ModuleAssemblyLoadContext(dllPath);
        Assembly assembly;

        try
        {
            assembly = loadContext.LoadFromAssemblyPath(dllPath);
        }
        catch (Exception ex)
        {
            _logger?.LogDebug("Skipping {Path}: {Error}", dllPath, ex.Message);
            return;
        }

        // Find types implementing IModule
        Type[] exportedTypes;
        try
        {
            exportedTypes = assembly.GetExportedTypes();
        }
        catch (Exception ex)
        {
            _logger?.LogDebug("Cannot get types from {Path}: {Error}", dllPath, ex.Message);
            return;
        }

        var moduleTypes = exportedTypes
            .Where(t => typeof(IModule).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface);

        foreach (var moduleType in moduleTypes)
        {
            try
            {
                var module = (IModule)Activator.CreateInstance(moduleType)!;

                // Check if module with same ID already loaded
                if (_loadedModules.Any(m => m.Module.Id == module.Id))
                {
                    _logger?.LogWarning("Module {Id} already loaded, skipping duplicate from {Path}", module.Id, dllPath);
                    continue;
                }

                _loadedModules.Add(new LoadedModule
                {
                    Module = module,
                    Assembly = assembly,
                    LoadContext = loadContext,
                    Status = ModuleLoadStatus.Loaded
                });

                _logger?.LogInformation("Loaded module: {Name} v{Version} from {Path}", module.Name, module.Version, dllPath);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to instantiate module type {Type} from {Path}", moduleType.Name, dllPath);
            }
        }
    }

    private void SortByDependencies()
    {
        // Topological sort to ensure dependencies are initialized first
        var sorted = new List<LoadedModule>();
        var visited = new HashSet<string>();

        void Visit(LoadedModule module)
        {
            if (visited.Contains(module.Module.Id)) return;
            visited.Add(module.Module.Id);

            foreach (var depId in module.Module.Dependencies)
            {
                var dep = _loadedModules.FirstOrDefault(m => m.Module.Id == depId);
                if (dep != null) Visit(dep);
            }

            sorted.Add(module);
        }

        foreach (var module in _loadedModules)
        {
            Visit(module);
        }

        _loadedModules.Clear();
        _loadedModules.AddRange(sorted);
    }

    public async Task InitializeModulesAsync(IServiceProvider services)
    {
        foreach (var loaded in _loadedModules.Where(m => m.Status == ModuleLoadStatus.Loaded))
        {
            try
            {
                await loaded.Module.InitializeAsync(services);
                loaded.Status = ModuleLoadStatus.Initialized;
                _logger?.LogInformation("Initialized module: {Name}", loaded.Module.Name);
            }
            catch (Exception ex)
            {
                loaded.Status = ModuleLoadStatus.Failed;
                loaded.ErrorMessage = ex.Message;
                _logger?.LogError(ex, "Failed to initialize module: {Name}", loaded.Module.Name);
            }
        }
    }

    public async Task ShutdownModulesAsync()
    {
        // Shutdown in reverse order
        foreach (var loaded in _loadedModules.AsEnumerable().Reverse())
        {
            try
            {
                await loaded.Module.ShutdownAsync();
                _logger?.LogInformation("Shutdown module: {Name}", loaded.Module.Name);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error shutting down module: {Name}", loaded.Module.Name);
            }
        }
    }
}
