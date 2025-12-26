using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Notes.Data;
using Notes.Services;

namespace Notes;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
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

        // Services
        builder.Services.AddSingleton<IThemeService, ThemeService>();
        builder.Services.AddSingleton<SaveOnCloseService>();
#if WINDOWS
        builder.Services.AddSingleton<TrayService>();
        builder.Services.AddSingleton<ClipboardService>();
#endif
        builder.Services.AddScoped<INoteService, NoteService>();

        var app = builder.Build();

        // Run migrations on startup
        using (var scope = app.Services.CreateScope())
        {
            var factory = scope.ServiceProvider
                .GetRequiredService<IDbContextFactory<NotesDbContext>>();
            using var context = factory.CreateDbContext();
            context.Database.EnsureCreated();
        }

        return app;
    }
}
