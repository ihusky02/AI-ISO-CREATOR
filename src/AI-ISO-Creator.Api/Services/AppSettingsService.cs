using System.Text.Json;
using Microsoft.Extensions.Configuration;
using AI_ISO_Creator.Core.Configuration;

namespace AI_ISO_Creator.Api.Services;

/// <summary>
/// Manages user application preferences and persists Gemini API credentials to local storage (user_settings.json).
/// </summary>
public class AppSettingsService
{
    private readonly string _settingsFilePath;
    private readonly GeminiSettings _currentSettings;
    private readonly object _lock = new();

    public AppSettingsService(IConfiguration config)
    {
        _settingsFilePath = Path.Combine(Directory.GetCurrentDirectory(), "user_settings.json");
        
        _currentSettings = new GeminiSettings
        {
            ApiKey = config["Gemini:ApiKey"] ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? string.Empty,
            Model = config["Gemini:Model"] ?? "gemini-2.5-flash",
            ApiUrl = config["Gemini:ApiUrl"] ?? "https://generativelanguage.googleapis.com/v1beta"
        };

        LoadFromFile();
    }

    /// <summary>
    /// Returns a snapshot of the current in-memory settings.
    /// </summary>
    public GeminiSettings GetSettings()
    {
        lock (_lock)
        {
            return new GeminiSettings
            {
                ApiKey = _currentSettings.ApiKey,
                Model = _currentSettings.Model,
                ApiUrl = _currentSettings.ApiUrl
            };
        }
    }

    /// <summary>
    /// Updates the API key and active model, persisting changes to user_settings.json.
    /// </summary>
    public void UpdateSettings(string? apiKey, string? model)
    {
        lock (_lock)
        {
            if (apiKey != null)
            {
                _currentSettings.ApiKey = apiKey.Trim();
            }
            if (!string.IsNullOrWhiteSpace(model))
            {
                _currentSettings.Model = model.Trim();
            }

            SaveToFile();
        }
    }

    /// <summary>
    /// Reads saved user settings from user_settings.json if present.
    /// </summary>
    private void LoadFromFile()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                var json = File.ReadAllText(_settingsFilePath);
                var loaded = JsonSerializer.Deserialize<GeminiSettings>(json);
                if (loaded != null)
                {
                    if (!string.IsNullOrWhiteSpace(loaded.ApiKey))
                        _currentSettings.ApiKey = loaded.ApiKey;
                    if (!string.IsNullOrWhiteSpace(loaded.Model))
                        _currentSettings.Model = loaded.Model;
                    if (!string.IsNullOrWhiteSpace(loaded.ApiUrl))
                        _currentSettings.ApiUrl = loaded.ApiUrl;
                }
            }
        }
        catch { /* Ignore unparseable or corrupted settings file */ }
    }

    /// <summary>
    /// Writes current settings to user_settings.json.
    /// </summary>
    private void SaveToFile()
    {
        try
        {
            var json = JsonSerializer.Serialize(_currentSettings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsFilePath, json);
        }
        catch { /* Ignore write errors */ }
    }
}
