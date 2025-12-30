using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Notes.Data;
using Notes.Modules.Services;
using Notes.Services;
using Notes.Services.AI;
using Notes.Services.IntelliSense;

namespace Notes;

public static class MauiProgram
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Notes", "startup.log");

    private static void Log(string message)
    {
        try { File.AppendAllText(LogPath, $"{DateTime.Now}: [MauiProgram] {message}\n"); } catch { }
    }

    public static MauiApp CreateMauiApp()
    {
        Log("CreateMauiApp started");
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddMauiBlazorWebView();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        // Database configuration
        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Notes", "notes.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

        builder.Services.AddDbContextFactory<NotesDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath}"));

        // ==========================================
        // Module System - Load modules before building
        // ==========================================
        var modulesPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Notes", "Modules");

        // Create module loader and discover modules
        var moduleLoader = new ModuleLoader();
        moduleLoader.DiscoverAndLoadModulesAsync(modulesPath).GetAwaiter().GetResult();

        // Let modules register their services
        foreach (var loaded in moduleLoader.LoadedModules)
        {
            try
            {
                loaded.Module.ConfigureServices(builder.Services);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Module {loaded.Module.Name} failed to configure services: {ex.Message}");
            }
        }

        // Register module infrastructure as singletons
        builder.Services.AddSingleton<IModuleLoader>(moduleLoader);
        builder.Services.AddSingleton<IModuleManager, ModuleManager>();

        // ==========================================
        // Core Services
        // ==========================================
        builder.Services.AddSingleton<INavigationService, NavigationService>();
        builder.Services.AddSingleton<IThemeService, ThemeService>();
        builder.Services.AddSingleton<SaveOnCloseService>();
#if WINDOWS
        builder.Services.AddSingleton<WindowSettingsService>();
        builder.Services.AddSingleton<TrayService>();
        builder.Services.AddSingleton<ClipboardService>();
#endif
        builder.Services.AddScoped<INoteService, NoteService>();
        builder.Services.AddScoped<IScriptingService, ScriptingService>();
        builder.Services.AddSingleton<ICompletionService, CompletionService>();
        builder.Services.AddScoped<CompletionInterop>();
        builder.Services.AddSingleton<IDataFormatterService, DataFormatterService>();
        builder.Services.AddSingleton<IEncoderService, EncoderService>();
        builder.Services.AddScoped<IScriptTemplateService, ScriptTemplateService>();
        builder.Services.AddSingleton<IDiffService, DiffService>();
        builder.Services.AddSingleton<IPortService, PortService>();
        builder.Services.AddSingleton<ICommandService, CommandService>();

        // AI Services
        Log("Registering AI services");
        builder.Services.AddSingleton<IAISettingsService, AISettingsService>();
        builder.Services.AddSingleton<IClaudeService, ClaudeService>();
        builder.Services.AddSingleton<IScriptEditorState, ScriptEditorState>();

        Log("Building app");
        var app = builder.Build();
        Log("App built successfully");

        // ==========================================
        // Post-Build Initialization
        // ==========================================

        // Initialize database
        using (var scope = app.Services.CreateScope())
        {
            var factory = scope.ServiceProvider
                .GetRequiredService<IDbContextFactory<NotesDbContext>>();
            using var context = factory.CreateDbContext();

            // Check if Scripts table exists, if not recreate the database
            try
            {
                context.Database.ExecuteSqlRaw("SELECT 1 FROM Scripts LIMIT 1");
            }
            catch
            {
                // Scripts table doesn't exist - recreate database
                context.Database.EnsureDeleted();
                context.Database.EnsureCreated();
            }
        }

        // Initialize modules after app is built
        moduleLoader.InitializeModulesAsync(app.Services).GetAwaiter().GetResult();

        // Register module navigation items with the navigation service
        var moduleManager = app.Services.GetRequiredService<IModuleManager>();
        var navigationService = app.Services.GetRequiredService<INavigationService>();
        moduleManager.RegisterModuleNavigationItems(navigationService);

        // AI settings will load lazily when first accessed
        Log("CreateMauiApp completed successfully");
        return app;
    }
}
