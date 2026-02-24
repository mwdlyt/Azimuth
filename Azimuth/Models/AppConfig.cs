using System.Windows.Media;

namespace Azimuth.Models;

/// <summary>
/// Application-wide constants and configuration values.
/// </summary>
public static class AppConfig
{
    // ── Theme Colors ──────────────────────────────────────────
    public const string BackgroundHex = "#0D0D0F";
    public const string SurfaceHex = "#161622";
    public const string CanvasBackgroundHex = "#0A0A12";
    public const string AccentHex = "#7C5CFC";
    public const string TextPrimaryHex = "#E8E8F0";
    public const string TextMutedHex = "#6B6B80";

    // ── Source Node Colors ────────────────────────────────────
    public static readonly string[] SourceColors =
    {
        "#7C5CFC", // violet
        "#FF6B6B", // coral
        "#4ECDC4", // teal
        "#FFD93D", // amber
        "#6BCB77", // green
    };

    // ── Canvas ────────────────────────────────────────────────
    public const double DefaultCanvasRadius = 400.0;
    public static readonly double[] RingPercentages = { 0.25, 0.50, 0.75, 1.00 };
    public const double SourceNodeRadius = 22.0;
    public const double ListenerIconRadius = 18.0;

    // ── Spatial Audio ─────────────────────────────────────────
    public const float MinVolume = 0.05f;
    public const float DistanceFalloff = 8.0f;
    public const int SampleRate = 44100;
    public const int Channels = 2;

    // ── File Extensions ───────────────────────────────────────
    public const string ProjectExtension = ".azimuth";
    public const string ProjectFilter = "Azimuth Scene (*.azimuth)|*.azimuth";
    public const string AudioFilter = "Audio Files (*.wav;*.mp3;*.flac;*.ogg;*.aac;*.wma;*.m4a;*.aiff;*.aif;*.opus)|*.wav;*.mp3;*.flac;*.ogg;*.aac;*.wma;*.m4a;*.aiff;*.aif;*.opus|All Files (*.*)|*.*";
    public const string ExportWavFilter = "WAV Audio (*.wav)|*.wav";
    public const string ExportMp3Filter = "MP3 Audio (*.mp3)|*.mp3";

    /// <summary>
    /// Returns the next source color by cycling through the palette.
    /// </summary>
    public static string GetSourceColor(int index) =>
        SourceColors[index % SourceColors.Length];
}
