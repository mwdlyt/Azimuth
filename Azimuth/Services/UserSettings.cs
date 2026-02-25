using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Azimuth.Services;

/// <summary>
/// Singleton that persists user preferences to %APPDATA%/Azimuth/settings.json.
/// </summary>
public sealed class UserSettings
{
    private static readonly Lazy<UserSettings> _instance = new(() => Load());

    private static readonly string SettingsDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Azimuth");

    private static readonly string SettingsPath =
        Path.Combine(SettingsDir, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Gets the singleton instance.</summary>
    public static UserSettings Instance => _instance.Value;

    // ── Persisted Properties ─────────────────────────────────

    /// <summary>Most-recently-opened scene file paths (max 10).</summary>
    public List<string> RecentFiles { get; set; } = new();

    /// <summary>Canvas grid spacing in pixels.</summary>
    public double GridSize { get; set; } = 30.0;

    /// <summary>Audio engine sample rate.</summary>
    public int SampleRate { get; set; } = 44100;

    /// <summary>Spatial distance falloff exponent.</summary>
    public double DistanceFalloff { get; set; } = 8.0;

    /// <summary>Whether snap-to-grid is enabled by default on startup.</summary>
    public bool SnapToGridDefault { get; set; }

    /// <summary>Whether to auto-open the last scene on startup.</summary>
    public bool OpenLastScene { get; set; }

    /// <summary>Remembered window width.</summary>
    public double WindowWidth { get; set; } = 1280;

    /// <summary>Remembered window height.</summary>
    public double WindowHeight { get; set; } = 800;

    // ── Methods ──────────────────────────────────────────────

    /// <summary>
    /// Loads settings from disk, returning defaults if the file is missing or corrupt.
    /// </summary>
    public static UserSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var settings = JsonSerializer.Deserialize<UserSettings>(json, JsonOptions);
                if (settings is not null)
                    return settings;
            }
        }
        catch
        {
            // Corrupt file — fall through to defaults
        }

        return new UserSettings();
    }

    /// <summary>
    /// Persists current settings to disk.
    /// </summary>
    public void Save()
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);
            var json = JsonSerializer.Serialize(this, JsonOptions);
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // Best-effort — don't crash the app over settings
        }
    }

    /// <summary>
    /// Adds a file path to the top of the recent files list (max 10, no duplicates).
    /// </summary>
    public void AddRecentFile(string path)
    {
        RecentFiles.Remove(path);
        RecentFiles.Insert(0, path);
        if (RecentFiles.Count > 10)
            RecentFiles.RemoveRange(10, RecentFiles.Count - 10);
        Save();
    }

    /// <summary>
    /// Removes a file path from the recent files list.
    /// </summary>
    public void RemoveRecentFile(string path)
    {
        RecentFiles.Remove(path);
        Save();
    }

    /// <summary>
    /// Clears all recent file entries.
    /// </summary>
    public void ClearRecentFiles()
    {
        RecentFiles.Clear();
        Save();
    }
}
