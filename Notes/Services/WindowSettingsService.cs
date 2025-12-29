#if WINDOWS
using System.Text.Json;

namespace Notes.Services;

public class WindowSettings
{
    public bool IsPinned { get; set; } = true;
    public int? UnpinnedX { get; set; }
    public int? UnpinnedY { get; set; }
    public int? UnpinnedWidth { get; set; }
    public int? UnpinnedHeight { get; set; }
}

public class WindowSettingsService
{
    private readonly string _settingsPath;
    private WindowSettings _settings;

    public WindowSettingsService()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Notes");
        Directory.CreateDirectory(appDataPath);
        _settingsPath = Path.Combine(appDataPath, "window-settings.json");
        _settings = Load();
    }

    public WindowSettings Settings => _settings;

    public bool IsPinned
    {
        get => _settings.IsPinned;
        set
        {
            _settings.IsPinned = value;
            Save();
        }
    }

    public void SaveUnpinnedPosition(int x, int y, int width, int height)
    {
        _settings.UnpinnedX = x;
        _settings.UnpinnedY = y;
        _settings.UnpinnedWidth = width;
        _settings.UnpinnedHeight = height;
        Save();
    }

    public (int? X, int? Y, int? Width, int? Height) GetUnpinnedPosition()
    {
        return (_settings.UnpinnedX, _settings.UnpinnedY,
                _settings.UnpinnedWidth, _settings.UnpinnedHeight);
    }

    private WindowSettings Load()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                return JsonSerializer.Deserialize<WindowSettings>(json) ?? new WindowSettings();
            }
        }
        catch
        {
            // Ignore errors, use defaults
        }
        return new WindowSettings();
    }

    private void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(_settingsPath, json);
        }
        catch
        {
            // Ignore save errors
        }
    }
}
#endif
