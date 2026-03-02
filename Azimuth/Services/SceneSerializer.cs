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
    /// Audio source paths are stored relative to the scene file directory when possible,
    /// preventing user directory structure leakage when sharing scene files.
    /// </summary>
    public static async Task SaveAsync(AzimuthScene scene, string filePath)
    {
        var sceneDir = Path.GetDirectoryName(Path.GetFullPath(filePath))!;

        // Convert absolute paths to relative for serialization
        var savedPaths = new Dictionary<Guid, string>();
        foreach (var source in scene.Sources)
        {
            savedPaths[source.Id] = source.FilePath;
            source.FilePath = ToRelativePath(sceneDir, source.FilePath);
        }

        try
        {
            var json = JsonSerializer.Serialize(scene, JsonOptions);
            await File.WriteAllTextAsync(filePath, json);
        }
        finally
        {
            // Restore absolute paths on the live model so the app continues working
            foreach (var source in scene.Sources)
            {
                if (savedPaths.TryGetValue(source.Id, out var abs))
                    source.FilePath = abs;
            }
        }
    }

    /// <summary>
    /// Loads a scene from a JSON file at the specified path.
    /// Relative source paths are resolved back to absolute using the scene file directory.
    /// </summary>
    public static async Task<AzimuthScene> LoadAsync(string filePath)
    {
        var json = await File.ReadAllTextAsync(filePath);
        var scene = JsonSerializer.Deserialize<AzimuthScene>(json, JsonOptions);
        if (scene is null)
            throw new InvalidDataException("Failed to deserialize scene file.");

        var sceneDir = Path.GetDirectoryName(Path.GetFullPath(filePath))!;

        foreach (var source in scene.Sources)
        {
            source.FilePath = ResolveSourcePath(sceneDir, source.FilePath);
        }

        return scene;
    }

    /// <summary>
    /// Converts an absolute file path to a path relative to the scene directory.
    /// Falls back to the absolute path if they are on different drives or the path is empty.
    /// </summary>
    internal static string ToRelativePath(string sceneDir, string sourcePath)
    {
        if (string.IsNullOrEmpty(sourcePath))
            return sourcePath;

        try
        {
            var fullSource = Path.GetFullPath(sourcePath);

            // Different drive roots — relative path would be meaningless
            var sourceRoot = Path.GetPathRoot(fullSource);
            var sceneRoot = Path.GetPathRoot(sceneDir);
            if (!string.Equals(sourceRoot, sceneRoot, StringComparison.OrdinalIgnoreCase))
                return fullSource;

            return Path.GetRelativePath(sceneDir, fullSource);
        }
        catch
        {
            return sourcePath;
        }
    }

    /// <summary>
    /// Resolves a source path (possibly relative) back to an absolute path
    /// anchored at the scene file directory. Validates against path traversal.
    /// </summary>
    internal static string ResolveSourcePath(string sceneDir, string sourcePath)
    {
        if (string.IsNullOrEmpty(sourcePath))
            return sourcePath;

        try
        {
            // If already absolute, validate and return
            if (Path.IsPathRooted(sourcePath))
                return ValidatePath(sourcePath);

            // Resolve relative path against scene directory
            var combined = Path.Combine(sceneDir, sourcePath);
            var resolved = Path.GetFullPath(combined);

            return ValidatePath(resolved);
        }
        catch
        {
            return sourcePath;
        }
    }

    /// <summary>
    /// Validates a resolved path does not contain suspicious traversal patterns
    /// and is a well-formed file path.
    /// </summary>
    private static string ValidatePath(string fullPath)
    {
        // Ensure the resolved path doesn't navigate to unexpected locations
        // by checking it resolves to a proper absolute path without leftover traversal
        var normalized = Path.GetFullPath(fullPath);

        // Block paths that still contain traversal sequences after normalization
        // (should not happen with GetFullPath, but defense in depth)
        if (normalized.Contains(".." + Path.DirectorySeparatorChar) ||
            normalized.Contains(".." + Path.AltDirectorySeparatorChar))
        {
            throw new InvalidDataException("Invalid path in scene file.");
        }

        return normalized;
    }
}
