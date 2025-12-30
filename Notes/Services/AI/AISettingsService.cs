using System.Text.Json;
using Notes.Models.AI;

namespace Notes.Services.AI;

public class AISettingsService : IAISettingsService
{
    private const string ApiKeyStorageKey = "claude_api_key";
    private const string SettingsFileName = "ai-settings.json";
    private const string DevKeyFileName = ".claude-key"; // Fallback for dev (not in SecureStorage)

    private readonly string _settingsPath;
    private readonly string _devKeyPath;
    private AISettings _settings = new();
    private bool _hasApiKey;
    private bool _isLoaded;

    public AISettings Settings => _settings;
    public bool HasApiKey => _hasApiKey;

    public event Action? SettingsChanged;

    public AISettingsService()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Notes");
        Directory.CreateDirectory(appDataPath);
        _settingsPath = Path.Combine(appDataPath, SettingsFileName);
        _devKeyPath = Path.Combine(appDataPath, DevKeyFileName);

        // Load settings file synchronously (this is safe, just file I/O)
        LoadSettingsFromFile();

        // Check for dev fallback key synchronously
        _hasApiKey = HasDevFallbackKey();
    }

    private bool HasDevFallbackKey()
    {
        // Check environment variable first
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CLAUDE_API_KEY")))
            return true;

        // Check dev key file
        if (File.Exists(_devKeyPath))
        {
            try
            {
                var key = File.ReadAllText(_devKeyPath).Trim();
                return !string.IsNullOrEmpty(key);
            }
            catch { }
        }

        return false;
    }

    private void LoadSettingsFromFile()
    {
        if (File.Exists(_settingsPath))
        {
            try
            {
                var json = File.ReadAllText(_settingsPath);
                _settings = JsonSerializer.Deserialize<AISettings>(json) ?? new AISettings();
            }
            catch
            {
                _settings = new AISettings();
            }
        }
    }

    public async Task LoadAsync()
    {
        if (_isLoaded) return;

        // Check if API key exists in SecureStorage
        try
        {
            var key = await SecureStorage.Default.GetAsync(ApiKeyStorageKey);
            if (!string.IsNullOrEmpty(key))
            {
                _hasApiKey = true;
            }
        }
        catch
        {
            // SecureStorage failed, rely on fallback check from constructor
        }

        _isLoaded = true;
    }

    public async Task SaveSettingsAsync(AISettings settings)
    {
        _settings = settings;

        try
        {
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            await File.WriteAllTextAsync(_settingsPath, json);
            SettingsChanged?.Invoke();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save AI settings: {ex.Message}");
        }
    }

    public async Task<string?> GetApiKeyAsync()
    {
        // For dev: prioritize file/env over SecureStorage since SecureStorage can be unreliable
        // Fallback: dev key file (check first for dev reliability)
        if (File.Exists(_devKeyPath))
        {
            try
            {
                var fileKey = File.ReadAllText(_devKeyPath).Trim();
                if (!string.IsNullOrEmpty(fileKey))
                {
                    System.Diagnostics.Debug.WriteLine($"Using API key from file: {fileKey[..20]}...");
                    return fileKey;
                }
            }
            catch { }
        }

        // Fallback: environment variable
        var envKey = Environment.GetEnvironmentVariable("CLAUDE_API_KEY");
        if (!string.IsNullOrEmpty(envKey))
        {
            System.Diagnostics.Debug.WriteLine($"Using API key from env: {envKey[..20]}...");
            return envKey;
        }

        // Try SecureStorage last (can be unreliable in dev)
        try
        {
            var key = await SecureStorage.Default.GetAsync(ApiKeyStorageKey);
            if (!string.IsNullOrEmpty(key))
            {
                System.Diagnostics.Debug.WriteLine($"Using API key from SecureStorage: {key[..20]}...");
                return key;
            }
        }
        catch { }

        return null;
    }

    public async Task SetApiKeyAsync(string apiKey)
    {
        bool saved = false;

        // Try SecureStorage first
        try
        {
            await SecureStorage.Default.SetAsync(ApiKeyStorageKey, apiKey);
            saved = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SecureStorage failed, using fallback: {ex.Message}");
        }

        // Fallback: save to dev key file
        if (!saved)
        {
            try
            {
                await File.WriteAllTextAsync(_devKeyPath, apiKey);
                saved = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save API key to fallback: {ex.Message}");
                throw;
            }
        }

        _hasApiKey = !string.IsNullOrEmpty(apiKey);
        SettingsChanged?.Invoke();
    }

    public Task ClearApiKeyAsync()
    {
        // Clear SecureStorage
        try
        {
            SecureStorage.Default.Remove(ApiKeyStorageKey);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to clear SecureStorage API key: {ex.Message}");
        }

        // Clear fallback file
        try
        {
            if (File.Exists(_devKeyPath))
                File.Delete(_devKeyPath);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to clear fallback API key: {ex.Message}");
        }

        _hasApiKey = false;
        SettingsChanged?.Invoke();
        return Task.CompletedTask;
    }
}
