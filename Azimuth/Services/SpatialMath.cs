namespace Azimuth.Services;

/// <summary>
/// Calculates spatial audio parameters (volume, pan, left/right gain) from 2D position.
/// </summary>
public static class SpatialMath
{
    /// <summary>
    /// Computes the spatial audio parameters for a source at position (x, y) relative to the canvas center.
    /// </summary>
    /// <param name="x">Horizontal offset from center (negative = left, positive = right).</param>
    /// <param name="y">Vertical offset from center (negative = front/up, positive = back/down).</param>
    /// <param name="maxRadius">The maximum radius of the canvas in the same units as x/y.</param>
    /// <returns>A tuple of (leftGain, rightGain) with distance attenuation and panning applied.</returns>
    public static (float LeftGain, float RightGain) CalculateGains(double x, double y, double maxRadius)
    {
        if (maxRadius <= 0) return (0f, 0f);

        float distance = (float)Math.Min(Math.Sqrt(x * x + y * y) / maxRadius, 1.0);
        float volume = Math.Max(1.0f / (1.0f + distance * distance * Models.AppConfig.DistanceFalloff), Models.AppConfig.MinVolume);
        float pan = Math.Clamp((float)(x / maxRadius), -1f, 1f);

        float leftGain = volume * (pan <= 0 ? 1f : 1f - pan);
        float rightGain = volume * (pan >= 0 ? 1f : 1f + pan);

        return (leftGain, rightGain);
    }

    /// <summary>
    /// Computes the normalized distance (0..1) from center.
    /// </summary>
    public static float NormalizedDistance(double x, double y, double maxRadius)
    {
        if (maxRadius <= 0) return 0f;
        return (float)Math.Min(Math.Sqrt(x * x + y * y) / maxRadius, 1.0);
    }

    /// <summary>
    /// Computes the pan value (-1 left .. +1 right).
    /// </summary>
    public static float PanValue(double x, double maxRadius)
    {
        if (maxRadius <= 0) return 0f;
        return Math.Clamp((float)(x / maxRadius), -1f, 1f);
    }
}
