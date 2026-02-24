using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azimuth.Models;

namespace Azimuth.Services;

/// <summary>
/// Handles serialization and deserialization of Azimuth scene files (.azimuth JSON).
/// </summary>
public static class SceneSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Saves the scene to a JSON file at the specified path.
    /// </summary>
    public static async Task SaveAsync(AzimuthScene scene, string filePath)
    {
        var json = JsonSerializer.Serialize(scene, JsonOptions);
        await File.WriteAllTextAsync(filePath, json);
    }

    /// <summary>
    /// Loads a scene from a JSON file at the specified path.
    /// </summary>
    public static async Task<AzimuthScene> LoadAsync(string filePath)
    {
        var json = await File.ReadAllTextAsync(filePath);
        var scene = JsonSerializer.Deserialize<AzimuthScene>(json, JsonOptions);
        return scene ?? throw new InvalidDataException("Failed to deserialize scene file.");
    }
}
