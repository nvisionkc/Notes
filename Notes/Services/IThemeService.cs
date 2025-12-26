namespace Notes.Services;

public interface IThemeService
{
    event Action<bool>? ThemeChanged;
    bool IsDarkMode { get; }
    Task InitializeAsync();
}
