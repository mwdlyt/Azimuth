using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Azimuth.Models;
using Azimuth.Services;
using NAudio.Wave;

namespace Azimuth.ViewModels;

/// <summary>
/// ViewModel wrapper around an AudioSource for data binding.
/// </summary>
public class AudioSourceViewModel : INotifyPropertyChanged
{
    private readonly AudioSource _model;
    private double _canvasRadius = AppConfig.DefaultCanvasRadius;
    private bool _isPlaying;
    private TimeSpan _duration;
    private TimeSpan _currentPosition;
    private double[]? _waveformSamples;
    private bool _isSelected;
    private bool _isOrbitDragPaused;

    public AudioSourceViewModel(AudioSource model)
    {
        _model = model;
        _ = LoadWaveformAsync(model.FilePath);
    }

    public Guid Id => _model.Id;
    public AudioSource Model => _model;

    public string Name
    {
        get => _model.Name;
        set { _model.Name = value; OnPropertyChanged(); }
    }

    public string FilePath => _model.FilePath;

    public double X
    {
        get => _model.X;
        set
        {
            _model.X = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DistancePercent));
            OnPropertyChanged(nameof(PanDisplay));
        }
    }

    public double Y
    {
        get => _model.Y;
        set
        {
            _model.Y = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DistancePercent));
            OnPropertyChanged(nameof(PanDisplay));
        }
    }

    public float BaseVolume
    {
        get => _model.BaseVolume;
        set
        {
            _model.BaseVolume = Math.Clamp(value, 0f, 1f);
            OnPropertyChanged();
        }
    }

    public bool IsMuted
    {
        get => _model.IsMuted;
        set
        {
            _model.IsMuted = value;
            OnPropertyChanged();
        }
    }

    public bool IsSoloed
    {
        get => _model.IsSoloed;
        set
        {
            _model.IsSoloed = value;
            OnPropertyChanged();
        }
    }

    public string Color
    {
        get => _model.Color;
        set { _model.Color = value; OnPropertyChanged(); }
    }

    public double CanvasRadius
    {
        get => _canvasRadius;
        set
        {
            _canvasRadius = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DistancePercent));
            OnPropertyChanged(nameof(PanDisplay));
        }
    }

    // ── Selection ────────────────────────────────────────────

    /// <summary>Whether this source is currently selected on the canvas.</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            OnPropertyChanged();
        }
    }

    // ── Orbit ────────────────────────────────────────────────

    /// <summary>Whether orbital motion is enabled for this source.</summary>
    public bool OrbitEnabled
    {
        get => _model.OrbitEnabled;
        set
        {
            if (_model.OrbitEnabled == value) return;
            if (value && !_model.OrbitEnabled)
            {
                // Initialize orbit center to current position when first enabled
                _model.OrbitCenterX = _model.X;
                _model.OrbitCenterY = _model.Y;
                OnPropertyChanged(nameof(OrbitCenterX));
                OnPropertyChanged(nameof(OrbitCenterY));
            }
            _model.OrbitEnabled = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Horizontal radius of the elliptical orbit path in pixels.</summary>
    public double OrbitRadiusX
    {
        get => _model.OrbitRadiusX;
        set
        {
            _model.OrbitRadiusX = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsCircularOrbit));
        }
    }

    /// <summary>Vertical radius of the elliptical orbit path in pixels.</summary>
    public double OrbitRadiusY
    {
        get => _model.OrbitRadiusY;
        set
        {
            _model.OrbitRadiusY = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsCircularOrbit));
        }
    }

    /// <summary>Orbit speed in degrees per second.</summary>
    public double OrbitSpeed
    {
        get => _model.OrbitSpeed;
        set
        {
            _model.OrbitSpeed = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(OrbitSpeedDisplay));
        }
    }

    /// <summary>Whether the orbit rotates clockwise.</summary>
    public bool OrbitClockwise
    {
        get => _model.OrbitClockwise;
        set { _model.OrbitClockwise = value; OnPropertyChanged(); }
    }

    /// <summary>X coordinate of the orbit center relative to canvas center.</summary>
    public double OrbitCenterX
    {
        get => _model.OrbitCenterX;
        set { _model.OrbitCenterX = value; OnPropertyChanged(); }
    }

    /// <summary>Y coordinate of the orbit center relative to canvas center.</summary>
    public double OrbitCenterY
    {
        get => _model.OrbitCenterY;
        set { _model.OrbitCenterY = value; OnPropertyChanged(); }
    }

    /// <summary>Current angle in degrees along the orbit path.</summary>
    public double OrbitAngle
    {
        get => _model.OrbitAngle;
        set { _model.OrbitAngle = value; OnPropertyChanged(); }
    }

    /// <summary>Whether the orbit is circular (RadiusX == RadiusY).</summary>
    public bool IsCircularOrbit => Math.Abs(OrbitRadiusX - OrbitRadiusY) < 0.01;

    /// <summary>Formatted orbit speed display string.</summary>
    public string OrbitSpeedDisplay => $"{(int)OrbitSpeed} deg/s";

    /// <summary>Whether orbit is temporarily paused due to user dragging the node.</summary>
    public bool IsOrbitDragPaused
    {
        get => _isOrbitDragPaused;
        set { _isOrbitDragPaused = value; OnPropertyChanged(); }
    }

    // ── Per-Source Playback ──────────────────────────────────

    public bool IsPlaying
    {
        get => _isPlaying;
        set
        {
            _isPlaying = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PlayPauseIcon));
        }
    }

    public string PlayPauseIcon => _isPlaying ? "Pause24" : "Play24";

    // ── Timeline / Scrubber ─────────────────────────────────

    public TimeSpan Duration
    {
        get => _duration;
        set { _duration = value; OnPropertyChanged(); }
    }

    public TimeSpan CurrentPosition
    {
        get => _currentPosition;
        set
        {
            _currentPosition = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PositionFraction));
        }
    }

    public double PositionFraction =>
        _duration.TotalSeconds > 0 ? _currentPosition.TotalSeconds / _duration.TotalSeconds : 0.0;

    public double[]? WaveformSamples
    {
        get => _waveformSamples;
        private set { _waveformSamples = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Converts a fractional position (0.0 to 1.0) to a TimeSpan within the source duration.
    /// </summary>
    public TimeSpan FractionToTime(double fraction)
    {
        fraction = Math.Clamp(fraction, 0.0, 1.0);
        return TimeSpan.FromSeconds(_duration.TotalSeconds * fraction);
    }

    /// <summary>
    /// Samples audio file to generate waveform data for timeline display.
    /// </summary>
    public async Task LoadWaveformAsync(string filePath)
    {
        try
        {
            var samples = await Task.Run(() =>
            {
                const int bucketCount = 200;
                var result = new double[bucketCount];

                using var reader = AudioReaderFactory.CreateReader(filePath);
                ISampleProvider raw = reader.ToSampleProvider();
                if (raw.WaveFormat.Channels == 2)
                    raw = new Services.StereoToMonoSampleProvider(raw);

                long totalSamples = reader.Length / (reader.WaveFormat.BitsPerSample / 8);
                if (reader.WaveFormat.Channels > 1)
                    totalSamples /= reader.WaveFormat.Channels;

                int samplesPerBucket = Math.Max(1, (int)(totalSamples / bucketCount));
                var buffer = new float[samplesPerBucket];

                for (int i = 0; i < bucketCount; i++)
                {
                    int read = raw.Read(buffer, 0, samplesPerBucket);
                    if (read == 0) break;
                    float max = 0;
                    for (int j = 0; j < read; j++)
                        max = Math.Max(max, Math.Abs(buffer[j]));
                    result[i] = max;
                }

                return result;
            });

            WaveformSamples = samples;
        }
        catch
        {
            WaveformSamples = new double[200];
        }
    }

    /// <summary>
    /// Distance from center as a 0-100% string.
    /// </summary>
    public string DistancePercent
    {
        get
        {
            float d = SpatialMath.NormalizedDistance(X, Y, _canvasRadius);
            return $"{(int)(d * 100)}%";
        }
    }

    /// <summary>
    /// Pan display string (e.g., "32L", "C", "45R").
    /// </summary>
    public string PanDisplay
    {
        get
        {
            float pan = SpatialMath.PanValue(X, _canvasRadius);
            if (Math.Abs(pan) < 0.02f) return "C";
            int pct = (int)(Math.Abs(pan) * 100);
            return pan < 0 ? $"{pct}L" : $"{pct}R";
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
