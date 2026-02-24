namespace Azimuth.Models;

/// <summary>
/// Represents a complete Azimuth scene that can be serialized to/from JSON.
/// </summary>
public class AzimuthScene
{
    /// <summary>File format version.</summary>
    public int Version { get; set; } = 1;

    /// <summary>User-assigned scene name.</summary>
    public string Name { get; set; } = "Untitled Scene";

    /// <summary>Radius of the spatial canvas in pixels.</summary>
    public double CanvasRadius { get; set; } = 400;

    /// <summary>All audio sources in the scene.</summary>
    public List<AudioSource> Sources { get; set; } = new();
}
