using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using Azimuth.Models;
using Azimuth.Services;

namespace Azimuth.ViewModels;

/// <summary>
/// Primary ViewModel for the Azimuth application window.
/// </summary>
public class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly SpatialAudioEngine _engine;
    private readonly DispatcherTimer _positionTimer;
    private string _sceneName = "Untitled Scene";
    private string? _currentFilePath;
    private bool _isPlaying;
    private double _canvasRadius = AppConfig.DefaultCanvasRadius;
    private int _sourceColorIndex;
    private string _statusText = "Ready";
    private bool _hasUnsavedChanges;
    private bool _isTimelinePanelVisible;
    private bool _isSnapToGridEnabled;

    public MainViewModel()
    {
        _engine = new SpatialAudioEngine();
        Sources = new ObservableCollection<AudioSourceViewModel>();

        NewSceneCommand = new RelayCommand(NewScene);
        OpenCommand = new RelayCommand(async () => await OpenSceneAsync());
        SaveCommand = new RelayCommand(async () => await SaveSceneAsync());
        SaveAsCommand = new RelayCommand(async () => await SaveSceneAsAsync());
        ExportWavCommand = new RelayCommand(async () => await ExportAsync(false));
        ExportMp3Command = new RelayCommand(async () => await ExportAsync(true));
        PlayAllCommand = new RelayCommand(PlayAll);
        StopAllCommand = new RelayCommand(StopAll);
        RemoveSourceCommand = new RelayCommand(obj => RemoveSource(obj as AudioSourceViewModel));
        ToggleMuteCommand = new RelayCommand(obj => ToggleMute(obj as AudioSourceViewModel));
        ToggleSoloCommand = new RelayCommand(obj => ToggleSolo(obj as AudioSourceViewModel));
        ToggleSourcePlayPauseCommand = new RelayCommand(obj => ToggleSourcePlayPause(obj as AudioSourceViewModel));
        ToggleTimelinePanelCommand = new RelayCommand(() => IsTimelinePanelVisible = !IsTimelinePanelVisible);
        ToggleSnapToGridCommand = new RelayCommand(() => IsSnapToGridEnabled = !IsSnapToGridEnabled);
        SeekSourceCommand = new RelayCommand(obj => HandleSeek(obj));

        _positionTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _positionTimer.Tick += OnPositionTimerTick;
    }

    // ── Properties ──────────────────────────────────────────

    public ObservableCollection<AudioSourceViewModel> Sources { get; }

    public string SceneName
    {
        get => _sceneName;
        set { _sceneName = value; OnPropertyChanged(); }
    }

    public bool IsPlaying
    {
        get => _isPlaying;
        private set { _isPlaying = value; OnPropertyChanged(); }
    }

    public double CanvasRadius
    {
        get => _canvasRadius;
        set
        {
            _canvasRadius = value;
            OnPropertyChanged();
            foreach (var s in Sources) s.CanvasRadius = value;
        }
    }

    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; OnPropertyChanged(); }
    }

    public bool HasUnsavedChanges
    {
        get => _hasUnsavedChanges;
        set { _hasUnsavedChanges = value; OnPropertyChanged(); OnPropertyChanged(nameof(WindowTitle)); }
    }

    public string WindowTitle =>
        HasUnsavedChanges ? $"Azimuth - {SceneName} *" : $"Azimuth - {SceneName}";

    public bool IsTimelinePanelVisible
    {
        get => _isTimelinePanelVisible;
        set { _isTimelinePanelVisible = value; OnPropertyChanged(); }
    }

    public bool IsSnapToGridEnabled
    {
        get => _isSnapToGridEnabled;
        set { _isSnapToGridEnabled = value; OnPropertyChanged(); }
    }

    // ── Commands ─────────────────────────────────────────────

    public ICommand NewSceneCommand { get; }
    public ICommand OpenCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand SaveAsCommand { get; }
    public ICommand ExportWavCommand { get; }
    public ICommand ExportMp3Command { get; }
    public ICommand PlayAllCommand { get; }
    public ICommand StopAllCommand { get; }
    public ICommand RemoveSourceCommand { get; }
    public ICommand ToggleMuteCommand { get; }
    public ICommand ToggleSoloCommand { get; }
    public ICommand ToggleSourcePlayPauseCommand { get; }
    public ICommand ToggleTimelinePanelCommand { get; }
    public ICommand SeekSourceCommand { get; }
    public ICommand ToggleSnapToGridCommand { get; }

    // ── Source Management ────────────────────────────────────

    /// <summary>
    /// Adds audio files dropped onto the canvas at the given position.
    /// </summary>
    public void AddSourceFromFile(string filePath, double x, double y)
    {
        if (!File.Exists(filePath)) return;

        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        if (!Services.AudioReaderFactory.IsSupported(ext)) return;

        var source = new AudioSource
        {
            FilePath = filePath,
            Name = Path.GetFileNameWithoutExtension(filePath),
            X = x,
            Y = y,
            Color = AppConfig.GetSourceColor(_sourceColorIndex++),
        };

        var vm = new AudioSourceViewModel(source) { CanvasRadius = _canvasRadius };
        Sources.Add(vm);

        _engine.AddSource(source, _canvasRadius);

        // Set duration from engine
        vm.Duration = _engine.GetSourceDuration(vm.Id);

        if (IsPlaying)
        {
            // Source is already in the mixer, engine handles it
        }

        HasUnsavedChanges = true;
        StatusText = $"Added: {source.Name}";
    }

    /// <summary>
    /// Notifies the engine that a source has been dragged to a new position.
    /// </summary>
    public void UpdateSourcePosition(AudioSourceViewModel sourceVm)
    {
        _engine.UpdateSourcePosition(
            sourceVm.Id, sourceVm.X, sourceVm.Y,
            _canvasRadius, sourceVm.BaseVolume, sourceVm.IsMuted);
        HasUnsavedChanges = true;
    }

    public void UpdateSourceVolume(AudioSourceViewModel sourceVm)
    {
        _engine.UpdateSourcePosition(
            sourceVm.Id, sourceVm.X, sourceVm.Y,
            _canvasRadius, sourceVm.BaseVolume, sourceVm.IsMuted);
        HasUnsavedChanges = true;
    }

    /// <summary>Remove a source by its ID (called from canvas right-click).</summary>
    public void RemoveSource(Guid sourceId)
    {
        var vm = Sources.FirstOrDefault(s => s.Id == sourceId);
        RemoveSource(vm);
    }

    private void RemoveSource(AudioSourceViewModel? sourceVm)
    {
        if (sourceVm is null) return;
        _engine.RemoveSource(sourceVm.Id);
        Sources.Remove(sourceVm);
        HasUnsavedChanges = true;
        StatusText = $"Removed: {sourceVm.Name}";
    }

    private void ToggleMute(AudioSourceViewModel? sourceVm)
    {
        if (sourceVm is null) return;
        sourceVm.IsMuted = !sourceVm.IsMuted;
        _engine.UpdateSourcePosition(
            sourceVm.Id, sourceVm.X, sourceVm.Y,
            _canvasRadius, sourceVm.BaseVolume, sourceVm.IsMuted);
    }

    private void ToggleSolo(AudioSourceViewModel? sourceVm)
    {
        if (sourceVm is null) return;
        sourceVm.IsSoloed = !sourceVm.IsSoloed;

        bool anySoloed = Sources.Any(s => s.IsSoloed);
        foreach (var s in Sources)
        {
            bool effectiveMute = anySoloed ? !s.IsSoloed : s.IsMuted;
            _engine.UpdateSourcePosition(
                s.Id, s.X, s.Y,
                _canvasRadius, s.BaseVolume, effectiveMute);
        }
    }

    // ── Per-Source Playback ──────────────────────────────────

    private void ToggleSourcePlayPause(AudioSourceViewModel? sourceVm)
    {
        if (sourceVm is null) return;

        if (sourceVm.IsPlaying)
        {
            _engine.PauseSource(sourceVm.Id);
            sourceVm.IsPlaying = false;
        }
        else
        {
            _engine.PlaySource(sourceVm.Id);
            sourceVm.IsPlaying = true;
            IsPlaying = true;
            _positionTimer.Start();
        }
    }

    /// <summary>
    /// Handles seek from the timeline panel. Parameter is a Tuple(Guid, double).
    /// </summary>
    public void SeekSource(Guid sourceId, double fraction)
    {
        var sourceVm = Sources.FirstOrDefault(s => s.Id == sourceId);
        if (sourceVm is null) return;

        var time = sourceVm.FractionToTime(fraction);
        _engine.SeekSource(sourceId, time);
        sourceVm.CurrentPosition = time;
    }

    private void HandleSeek(object? obj)
    {
        if (obj is Tuple<Guid, double> t)
            SeekSource(t.Item1, t.Item2);
    }

    // ── Playback ─────────────────────────────────────────────

    private void PlayAll()
    {
        _engine.Play();
        IsPlaying = true;
        foreach (var s in Sources)
            s.IsPlaying = true;
        _positionTimer.Start();
        StatusText = "Playing";
    }

    private void StopAll()
    {
        _engine.Stop();
        IsPlaying = false;
        _positionTimer.Stop();
        foreach (var s in Sources)
        {
            s.IsPlaying = false;
            s.CurrentPosition = TimeSpan.Zero;
        }
        StatusText = "Stopped";
    }

    // ── Position Timer ───────────────────────────────────────

    private void OnPositionTimerTick(object? sender, EventArgs e)
    {
        if (!_engine.IsPlaying)
        {
            _positionTimer.Stop();
            return;
        }

        foreach (var s in Sources)
        {
            if (s.IsPlaying)
            {
                s.CurrentPosition = _engine.GetSourcePosition(s.Id);
                if (s.Duration == TimeSpan.Zero)
                    s.Duration = _engine.GetSourceDuration(s.Id);
            }
        }
    }

    // ── File Operations ──────────────────────────────────────

    private void NewScene()
    {
        _engine.StopAll();
        _positionTimer.Stop();
        Sources.Clear();
        IsPlaying = false;
        _currentFilePath = null;
        _sourceColorIndex = 0;
        SceneName = "Untitled Scene";
        HasUnsavedChanges = false;
        StatusText = "New scene";
        OnPropertyChanged(nameof(WindowTitle));
    }

    private async Task OpenSceneAsync()
    {
        var dlg = new OpenFileDialog
        {
            Filter = AppConfig.ProjectFilter,
            Title = "Open Azimuth Scene"
        };

        if (dlg.ShowDialog() != true) return;

        try
        {
            _engine.StopAll();
            _positionTimer.Stop();
            Sources.Clear();
            IsPlaying = false;
            _sourceColorIndex = 0;

            var scene = await SceneSerializer.LoadAsync(dlg.FileName);
            _currentFilePath = dlg.FileName;
            SceneName = scene.Name;
            CanvasRadius = scene.CanvasRadius;

            foreach (var source in scene.Sources)
            {
                if (!File.Exists(source.FilePath)) continue;
                var vm = new AudioSourceViewModel(source) { CanvasRadius = _canvasRadius };
                Sources.Add(vm);
                _engine.AddSource(source, _canvasRadius);
                vm.Duration = _engine.GetSourceDuration(vm.Id);
                _sourceColorIndex++;
            }

            HasUnsavedChanges = false;
            StatusText = $"Opened: {Path.GetFileName(dlg.FileName)}";
            OnPropertyChanged(nameof(WindowTitle));
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to open scene:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task SaveSceneAsync()
    {
        if (_currentFilePath is null)
        {
            await SaveSceneAsAsync();
            return;
        }

        await SaveToFileAsync(_currentFilePath);
    }

    private async Task SaveSceneAsAsync()
    {
        var dlg = new SaveFileDialog
        {
            Filter = AppConfig.ProjectFilter,
            Title = "Save Azimuth Scene",
            FileName = SceneName
        };

        if (dlg.ShowDialog() != true) return;
        await SaveToFileAsync(dlg.FileName);
    }

    private async Task SaveToFileAsync(string path)
    {
        try
        {
            var scene = new AzimuthScene
            {
                Name = SceneName,
                CanvasRadius = _canvasRadius,
                Sources = Sources.Select(s => s.Model).ToList()
            };

            await SceneSerializer.SaveAsync(scene, path);
            _currentFilePath = path;
            HasUnsavedChanges = false;
            StatusText = $"Saved: {Path.GetFileName(path)}";
            OnPropertyChanged(nameof(WindowTitle));
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save scene:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task ExportAsync(bool mp3)
    {
        var dlg = new SaveFileDialog
        {
            Filter = mp3 ? AppConfig.ExportMp3Filter : AppConfig.ExportWavFilter,
            Title = $"Export as {(mp3 ? "MP3" : "WAV")}",
            FileName = SceneName
        };

        if (dlg.ShowDialog() != true) return;

        try
        {
            StatusText = "Exporting...";
            var scene = new AzimuthScene
            {
                Name = SceneName,
                CanvasRadius = _canvasRadius,
                Sources = Sources.Select(s => s.Model).ToList()
            };

            var progress = new Progress<double>(p =>
            {
                StatusText = $"Exporting... {(int)(p * 100)}%";
            });

            if (mp3)
                await AudioExporter.ExportMp3Async(scene, dlg.FileName, _canvasRadius, progress);
            else
                await AudioExporter.ExportWavAsync(scene, dlg.FileName, _canvasRadius, progress);

            StatusText = $"Exported: {Path.GetFileName(dlg.FileName)}";
        }
        catch (Exception ex)
        {
            StatusText = "Export failed";
            MessageBox.Show($"Failed to export:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ── INotifyPropertyChanged ───────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public void Dispose()
    {
        _positionTimer.Stop();
        _engine.Dispose();
    }
}
