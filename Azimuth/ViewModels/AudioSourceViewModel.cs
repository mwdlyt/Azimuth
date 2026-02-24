using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Azimuth.Models;
using Azimuth.Services;

namespace Azimuth.ViewModels;

/// <summary>
/// ViewModel wrapper around an AudioSource for data binding.
/// </summary>
public class AudioSourceViewModel : INotifyPropertyChanged
{
    private readonly AudioSource _model;
    private double _canvasRadius = AppConfig.DefaultCanvasRadius;

    public AudioSourceViewModel(AudioSource model)
    {
        _model = model;
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
