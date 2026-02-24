using System.Text.Json.Serialization;

namespace Azimuth.Models;

/// <summary>
/// Represents a single audio source positioned on the spatial canvas.
/// </summary>
public class AudioSource
{
    /// <summary>Unique identifier for this source.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Absolute path to the audio file on disk.</summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>Display name (derived from filename).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>X position relative to canvas center (-1 to 1 normalized, or pixel offset).</summary>
    public double X { get; set; }

    /// <summary>Y position relative to canvas center.</summary>
    public double Y { get; set; }

    /// <summary>User-adjustable base volume (0.0 to 1.0).</summary>
    public float BaseVolume { get; set; } = 1.0f;

    /// <summary>Whether this source is muted.</summary>
    public bool IsMuted { get; set; }

    /// <summary>Whether this source is soloed.</summary>
    [JsonIgnore]
    public bool IsSoloed { get; set; }

    /// <summary>Hex color string for the node visual.</summary>
    public string Color { get; set; } = AppConfig.SourceColors[0];
}
