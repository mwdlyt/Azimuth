using System.Windows.Media;

namespace Azimuth.Models;

/// <summary>
/// Application-wide constants and configuration values.
/// </summary>
public static class AppConfig
{
    // ── Theme Colors ──────────────────────────────────────────
    public const string BackgroundHex = "#0F0F17";
    public const string SurfaceHex = "#1A1A28";
    public const string CanvasBackgroundHex = "#0C0C14";
    public const string AccentHex = "#2DD4BF";
    public const string TextPrimaryHex = "#F0F0F5";
    public const string TextMutedHex = "#5A5A72";

    // ── Source Node Colors ────────────────────────────────────
    public static readonly string[] SourceColors =
    {
        "#2DD4BF", // teal
        "#F87171", // coral
        "#60A5FA", // blue
        "#FBBF24", // amber
        "#A78BFA", // lavender
        "#34D399", // green
    };

    // ── Canvas ────────────────────────────────────────────────
    public const double DefaultCanvasRadius = 400.0;
    public static readonly double[] RingPercentages = { 0.25, 0.50, 0.75, 1.00 };
    public const double SourceNodeRadius = 22.0;
    public const double ListenerIconRadius = 18.0;

    // ── Grid / Snap ───────────────────────────────────────────
    public const double GridSize = 30.0;
    public const string GridColorHex = "#1E1E2E";

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
