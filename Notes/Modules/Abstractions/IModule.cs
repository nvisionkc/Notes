using Microsoft.Extensions.DependencyInjection;

namespace Notes.Modules.Abstractions;

/// <summary>
/// Core interface that all modules must implement to be loaded by the application.
/// </summary>
public interface IModule
{
    /// <summary>
    /// Unique identifier for the module (e.g., "com.example.mymodule")
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Human-readable module name
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Module version
    /// </summary>
    Version Version { get; }

    /// <summary>
    /// Optional description of module functionality
    /// </summary>
    string? Description { get; }

    /// <summary>
    /// Optional author information
    /// </summary>
    string? Author { get; }

    /// <summary>
    /// Module dependencies (other module IDs this module requires)
    /// </summary>
    IReadOnlyList<string> Dependencies { get; }

    /// <summary>
    /// Register services with the DI container during startup.
    /// Called before the app is built.
    /// </summary>
    void ConfigureServices(IServiceCollection services);

    /// <summary>
    /// Initialize the module after all services are available.
    /// Called after the app is built.
    /// </summary>
    Task InitializeAsync(IServiceProvider services);

    /// <summary>
    /// Cleanup when the module is unloaded or app shuts down.
    /// </summary>
    Task ShutdownAsync();
}
