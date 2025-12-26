using Microsoft.Win32;

namespace Notes.Services;

public class ThemeService : IThemeService, IDisposable
{
    private const string RegistryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string RegistryValueName = "AppsUseLightTheme";

    public event Action<bool>? ThemeChanged;
    public bool IsDarkMode { get; private set; }

    private readonly System.Timers.Timer _pollTimer;

    public ThemeService()
    {
        _pollTimer = new System.Timers.Timer(1000);
        _pollTimer.Elapsed += (_, _) => CheckTheme();
    }

    public Task InitializeAsync()
    {
        CheckTheme();
        _pollTimer.Start();
        return Task.CompletedTask;
    }

    private void CheckTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath);
            var value = key?.GetValue(RegistryValueName);
            var isDark = value is int intValue && intValue == 0;

            if (isDark != IsDarkMode)
            {
                IsDarkMode = isDark;
                ThemeChanged?.Invoke(isDark);
            }
        }
        catch
        {
            // Default to light if registry read fails
        }
    }

    public void Dispose()
    {
        _pollTimer.Stop();
        _pollTimer.Dispose();
    }
}
