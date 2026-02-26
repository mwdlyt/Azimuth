using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using Azimuth.Commands;
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
    private readonly DispatcherTimer _orbitTimer;
    private readonly UndoRedoManager _undoRedo = new();
    private DateTime _lastOrbitTick;
    private string _sceneName = "Untitled Scene";
    private string? _currentFilePath;
    private bool _isPlaying;
    private double _canvasRadius = AppConfig.DefaultCanvasRadius;
    private int _sourceColorIndex;
    private string _statusText = "Ready";
    private bool _hasUnsavedChanges;
    private bool _isTimelinePanelVisible;
    private bool _isSnapToGridEnabled;
    private bool _isOrbitPanelVisible;
    private AudioSourceViewModel? _selectedSource;

    public MainViewModel()
    {
        _engine = new SpatialAudioEngine();
        Sources = new ObservableCollection<AudioSourceViewModel>();
        RecentFiles = new ObservableCollection<string>(UserSettings.Instance.RecentFiles);

        NewSceneCommand = new RelayCommand(NewScene);
        OpenCommand = new RelayCommand(async () => await OpenSceneAsync());
        SaveCommand = new RelayCommand(async () => await SaveSceneAsync());
        SaveAsCommand = new RelayCommand(async () => await SaveSceneAsAsync());
        ExportWavCommand = new RelayCommand(async () => await ExportAsync(false));
        ExportMp3Command = new RelayCommand(async () => await ExportAsync(true));
        PlayAllCommand = new RelayCommand(PlayAll);
        StopAllCommand = new RelayCommand(StopAll);
        RemoveSourceCommand = new RelayCommand(obj => RemoveSourceUndoable(obj as AudioSourceViewModel));
        ToggleMuteCommand = new RelayCommand(obj => ToggleMute(obj as AudioSourceViewModel));
        ToggleSoloCommand = new RelayCommand(obj => ToggleSolo(obj as AudioSourceViewModel));
        ToggleSourcePlayPauseCommand = new RelayCommand(obj => ToggleSourcePlayPause(obj as AudioSourceViewModel));
        ToggleTimelinePanelCommand = new RelayCommand(() => IsTimelinePanelVisible = !IsTimelinePanelVisible);
        ToggleSnapToGridCommand = new RelayCommand(() => IsSnapToGridEnabled = !IsSnapToGridEnabled);
        SeekSourceCommand = new RelayCommand(obj => HandleSeek(obj));
        SelectSourceCommand = new RelayCommand(obj => SelectSource(obj as AudioSourceViewModel));
        UndoCommand = new RelayCommand(Undo, () => _undoRedo.CanUndo);
        RedoCommand = new RelayCommand(Redo, () => _undoRedo.CanRedo);
        OpenRecentCommand = new RelayCommand(obj => _ = OpenRecentAsync(obj as string));
        OpenSettingsCommand = new RelayCommand(OpenSettings);
        ToggleOrbitPanelCommand = new RelayCommand(() => IsOrbitPanelVisible = !IsOrbitPanelVisible);
        ToggleOrbitCommand = new RelayCommand(ToggleOrbitOnSelected);

        _undoRedo.StateChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
        };

        _positionTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _positionTimer.Tick += OnPositionTimerTick;

        _orbitTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _orbitTimer.Tick += OnOrbitTimerTick;

        // Apply snap default from settings
        _isSnapToGridEnabled = UserSettings.Instance.SnapToGridDefault;

        // Auto-open last scene if configured
        if (UserSettings.Instance.OpenLastScene && RecentFiles.Count > 0 && File.Exists(RecentFiles[0]))
        {
            _ = OpenSceneFromPathAsync(RecentFiles[0]);
        }
    }

    // ── Properties ──────────────────────────────────────────

    public ObservableCollection<AudioSourceViewModel> Sources { get; }

    /// <summary>Gets the undo/redo manager for external command registration (e.g. drag moves).</summary>
    public UndoRedoManager UndoRedo => _undoRedo;

    /// <summary>Recent file paths surfaced from <see cref="UserSettings"/>.</summary>
    public ObservableCollection<string> RecentFiles { get; }

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

    /// <summary>Whether the orbit settings panel is visible.</summary>
    public bool IsOrbitPanelVisible
    {
        get => _isOrbitPanelVisible;
        set { _isOrbitPanelVisible = value; OnPropertyChanged(); }
    }

    /// <summary>The currently selected source on the canvas, or null if none.</summary>
    public AudioSourceViewModel? SelectedSource
    {
        get => _selectedSource;
        set
        {
            if (_selectedSource == value) return;
            if (_selectedSource != null) _selectedSource.IsSelected = false;
            _selectedSource = value;
            if (_selectedSource != null) _selectedSource.IsSelected = true;
            OnPropertyChanged();
        }
    }

    /// <summary>Whether an undo operation is available.</summary>
    public bool CanUndo => _undoRedo.CanUndo;

    /// <summary>Whether a redo operation is available.</summary>
    public bool CanRedo => _undoRedo.CanRedo;

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

    /// <summary>Command to select a source (parameter: AudioSourceViewModel or null).</summary>
    public ICommand SelectSourceCommand { get; }

    /// <summary>Undoes the last command.</summary>
    public ICommand UndoCommand { get; }

    /// <summary>Redoes the last undone command.</summary>
    public ICommand RedoCommand { get; }

    /// <summary>Opens a scene from a recent file path (string parameter).</summary>
    public ICommand OpenRecentCommand { get; }

    /// <summary>Opens the settings dialog.</summary>
    public ICommand OpenSettingsCommand { get; }

    /// <summary>Toggles the orbit settings panel visibility.</summary>
    public ICommand ToggleOrbitPanelCommand { get; }

    /// <summary>Toggles orbit on the selected source.</summary>
    public ICommand ToggleOrbitCommand { get; }

    // ── Selection ────────────────────────────────────────────

    /// <summary>Selects a source, deselecting all others. Pass null to deselect all.</summary>
    public void SelectSource(AudioSourceViewModel? sourceVm)
    {
        SelectedSource = sourceVm;
    }

    /// <summary>Deselects all sources.</summary>
    public void DeselectAll()
    {
        SelectedSource = null;
    }

    /// <summary>Removes the currently selected source via the undo system.</summary>
    public void RemoveSelectedSource()
    {
        if (_selectedSource != null)
            RemoveSourceUndoable(_selectedSource);
    }

    // ── Undo/Redo ────────────────────────────────────────────

    /// <summary>Undoes the most recent command.</summary>
    public void Undo()
    {
        _undoRedo.Undo();
        HasUnsavedChanges = true;
    }

    /// <summary>Redoes the most recently undone command.</summary>
    public void Redo()
    {
        _undoRedo.Redo();
        HasUnsavedChanges = true;
    }

    // ── Source Management ────────────────────────────────────

    /// <summary>
    /// Adds audio files dropped onto the canvas at the given position (with undo support).
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

        var cmd = new AddSourceCommand(source, vm, AddSourceInternal, RemoveSourceInternal);
        _undoRedo.Execute(cmd);

        StatusText = $"Added: {source.Name}";
    }

    /// <summary>
    /// Internal add: adds the VM to the collection and source to the engine.
    /// Used by AddSourceCommand.Execute and RemoveSourceCommand.Undo.
    /// </summary>
    private void AddSourceInternal(AudioSourceViewModel vm)
    {
        Sources.Add(vm);
        _engine.AddSource(vm.Model, _canvasRadius);
        vm.Duration = _engine.GetSourceDuration(vm.Id);
        HasUnsavedChanges = true;
    }

    /// <summary>
    /// Internal remove: removes the VM from the collection and source from the engine.
    /// Used by RemoveSourceCommand.Execute and AddSourceCommand.Undo.
    /// </summary>
    private void RemoveSourceInternal(AudioSourceViewModel vm)
    {
        if (SelectedSource == vm) SelectedSource = null;
        _engine.RemoveSource(vm.Id);
        Sources.Remove(vm);
        HasUnsavedChanges = true;
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

    /// <summary>
    /// Notifies the engine that a source volume has changed.
    /// </summary>
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
        RemoveSourceUndoable(vm);
    }

    /// <summary>Removes a source via the undo system.</summary>
    private void RemoveSourceUndoable(AudioSourceViewModel? sourceVm)
    {
        if (sourceVm is null) return;
        var cmd = new RemoveSourceCommand(sourceVm.Model, sourceVm, AddSourceInternal, RemoveSourceInternal);
        _undoRedo.Execute(cmd);
        StatusText = $"Removed: {sourceVm.Name}";
    }

    /// <summary>
    /// Moves a source from one index to another in the sidebar list.
    /// </summary>
    public void MoveSource(int oldIndex, int newIndex)
    {
        if (oldIndex < 0 || oldIndex >= Sources.Count) return;
        if (newIndex < 0 || newIndex >= Sources.Count) return;
        if (oldIndex == newIndex) return;

        Sources.Move(oldIndex, newIndex);
        HasUnsavedChanges = true;
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

    /// <summary>Toggles between play and stop.</summary>
    public void TogglePlayStop()
    {
        if (IsPlaying)
            StopAll();
        else
            PlayAll();
    }

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

    // ── Orbit Animation ──────────────────────────────────────

    /// <summary>
    /// Toggles orbit on the currently selected source.
    /// </summary>
    public void ToggleOrbitOnSelected()
    {
        if (_selectedSource == null) return;
        _selectedSource.OrbitEnabled = !_selectedSource.OrbitEnabled;
        RefreshOrbitTimer();
        HasUnsavedChanges = true;
    }

    /// <summary>
    /// Starts or stops the orbit timer based on whether any source has orbit enabled.
    /// </summary>
    public void RefreshOrbitTimer()
    {
        bool anyOrbiting = Sources.Any(s => s.OrbitEnabled);
        if (anyOrbiting && !_orbitTimer.IsEnabled)
        {
            _lastOrbitTick = DateTime.UtcNow;
            _orbitTimer.Start();
        }
        else if (!anyOrbiting && _orbitTimer.IsEnabled)
        {
            _orbitTimer.Stop();
        }
    }

    /// <summary>
    /// Called when a source is dragged manually while orbit is enabled.
    /// Updates the orbit center to the new drag-end position.
    /// </summary>
    public void UpdateOrbitCenterFromDrag(AudioSourceViewModel sourceVm)
    {
        if (!sourceVm.OrbitEnabled) return;
        sourceVm.OrbitCenterX = sourceVm.X;
        sourceVm.OrbitCenterY = sourceVm.Y;
    }

    private void OnOrbitTimerTick(object? sender, EventArgs e)
    {
        var now = DateTime.UtcNow;
        double deltaSeconds = (now - _lastOrbitTick).TotalSeconds;
        _lastOrbitTick = now;

        // Clamp to avoid large jumps (e.g., after suspend)
        if (deltaSeconds > 0.1) deltaSeconds = 0.016;

        foreach (var source in Sources)
        {
            if (!source.OrbitEnabled || source.IsOrbitDragPaused) continue;

            double deltaAngle = source.OrbitSpeed * deltaSeconds * (source.OrbitClockwise ? -1.0 : 1.0);
            double newAngle = source.OrbitAngle + deltaAngle;

            // Wrap at 360 to avoid overflow
            newAngle %= 360.0;
            if (newAngle < 0) newAngle += 360.0;

            source.OrbitAngle = newAngle;

            double angleRad = newAngle * Math.PI / 180.0;
            double newX = source.OrbitCenterX + source.OrbitRadiusX * Math.Cos(angleRad);
            double newY = source.OrbitCenterY + source.OrbitRadiusY * Math.Sin(angleRad);

            // Clamp to 2x canvas radius so orbits can go beyond visible area
            // but not infinitely far (allows recovery by reducing radius)
            double maxBound = _canvasRadius * 2.0;
            source.X = Math.Clamp(newX, -maxBound, maxBound);
            source.Y = Math.Clamp(newY, -maxBound, maxBound);

            _engine.UpdateSourcePosition(
                source.Id, source.X, source.Y,
                _canvasRadius, source.BaseVolume, source.IsMuted);
        }

        OrbitTick?.Invoke(this, EventArgs.Empty);
    }

    // ── Recent Files ─────────────────────────────────────────

    /// <summary>
    /// Refreshes the observable RecentFiles collection from <see cref="UserSettings"/>.
    /// </summary>
    private void RefreshRecentFiles()
    {
        RecentFiles.Clear();
        foreach (var path in UserSettings.Instance.RecentFiles)
            RecentFiles.Add(path);
    }

    /// <summary>
    /// Records a path in settings and refreshes the UI collection.
    /// </summary>
    private void TrackRecentFile(string path)
    {
        UserSettings.Instance.AddRecentFile(path);
        RefreshRecentFiles();
    }

    private async Task OpenRecentAsync(string? path)
    {
        if (string.IsNullOrEmpty(path)) return;
        if (!File.Exists(path))
        {
            UserSettings.Instance.RemoveRecentFile(path);
            RefreshRecentFiles();
            StatusText = "File not found — removed from recents";
            return;
        }

        await OpenSceneFromPathAsync(path);
    }

    // ── Settings ─────────────────────────────────────────────

    private void OpenSettings()
    {
        var win = new Views.SettingsWindow { Owner = Application.Current.MainWindow };
        win.ShowDialog();

        if (win.RecentFilesCleared)
            RefreshRecentFiles();
    }

    // ── File Operations ──────────────────────────────────────

    private void NewScene()
    {
        _engine.StopAll();
        _positionTimer.Stop();
        _orbitTimer.Stop();
        Sources.Clear();
        IsPlaying = false;
        _currentFilePath = null;
        _sourceColorIndex = 0;
        _undoRedo.Clear();
        SelectedSource = null;
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

        await OpenSceneFromPathAsync(dlg.FileName);
    }

    /// <summary>
    /// Opens a scene file by path (shared by Open dialog and recent files).
    /// </summary>
    private async Task OpenSceneFromPathAsync(string filePath)
    {
        try
        {
            _engine.StopAll();
            _positionTimer.Stop();
            _orbitTimer.Stop();
            Sources.Clear();
            IsPlaying = false;
            _sourceColorIndex = 0;
            _undoRedo.Clear();
            SelectedSource = null;

            var scene = await SceneSerializer.LoadAsync(filePath);
            _currentFilePath = filePath;
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
            StatusText = $"Opened: {Path.GetFileName(filePath)}";
            OnPropertyChanged(nameof(WindowTitle));

            RefreshOrbitTimer();
            TrackRecentFile(filePath);
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

            TrackRecentFile(path);
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

    /// <summary>
    /// Raised on each orbit animation tick so the UI can refresh canvas visuals.
    /// </summary>
    public event EventHandler? OrbitTick;

    // ── INotifyPropertyChanged ───────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public void Dispose()
    {
        _positionTimer.Stop();
        _orbitTimer.Stop();
        _engine.Dispose();
    }
}
