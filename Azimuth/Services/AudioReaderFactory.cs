using System.IO;
using NAudio.Vorbis;
using NAudio.Wave;

namespace Azimuth.Services;

/// <summary>
/// Creates the appropriate NAudio WaveStream for a given audio file based on its extension.
/// Supports WAV, MP3, FLAC, OGG, AAC, WMA, M4A, AIFF, and OPUS.
/// </summary>
public static class AudioReaderFactory
{
    /// <summary>
    /// All audio file extensions supported by Azimuth.
    /// </summary>
    public static readonly IReadOnlyList<string> SupportedExtensions = new[]
    {
        ".wav", ".mp3", ".flac", ".ogg", ".aac", ".wma", ".m4a", ".aiff", ".aif", ".opus"
    };

    /// <summary>
    /// Returns true if the given file extension is supported.
    /// </summary>
    public static bool IsSupported(string extension) =>
        SupportedExtensions.Contains(extension.ToLowerInvariant());

    /// <summary>
    /// Creates the best WaveStream for the given file path.
    /// </summary>
    /// <exception cref="NotSupportedException">Thrown if the file format is not supported.</exception>
    /// <exception cref="FileNotFoundException">Thrown if the file does not exist.</exception>
    public static WaveStream CreateReader(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Audio file not found: {Path.GetFileName(filePath)}");

        var ext = Path.GetExtension(filePath).ToLowerInvariant();

        if (ext == ".ogg")
            return new VorbisWaveReader(filePath);

        if (ext == ".wav")
            return new WaveFileReader(filePath);

        if (ext == ".mp3")
            return new Mp3FileReader(filePath);

        if (ext == ".flac" || ext == ".aac" || ext == ".wma" ||
            ext == ".m4a" || ext == ".aiff" || ext == ".aif" || ext == ".opus")
            return new MediaFoundationReader(filePath);

        throw new NotSupportedException(
            $"Format '{ext}' is not supported. Supported: {string.Join(", ", SupportedExtensions)}");
    }
}
