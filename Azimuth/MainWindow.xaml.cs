using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Azimuth.Controls;
using Azimuth.ViewModels;
using Wpf.Ui.Controls;

namespace Azimuth;

/// <summary>
/// Main application window. Minimal code-behind — delegates to MainViewModel.
/// </summary>
public partial class MainWindow : FluentWindow
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        _viewModel = new MainViewModel();
        DataContext = _viewModel;

        InitializeComponent();

        // Watch source collection for add/remove to update canvas nodes
        _viewModel.Sources.CollectionChanged += OnSourcesChanged;

        // Sync grid/snap state to canvas
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        // Keyboard shortcuts via PreviewKeyDown (WPF eats some keys in InputBindings)
        PreviewKeyDown += OnPreviewKeyDown;

        Closed += (_, _) =>
        {
            _viewModel.Dispose();
        };
    }

    // ── Keyboard Shortcuts ──────────────────────────────────

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
        bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;

        // Don't intercept when focus is in a text input
        if (e.OriginalSource is System.Windows.Controls.TextBox) return;

        if (ctrl && !shift && e.Key == Key.Z)
        {
            _viewModel.Undo();
            e.Handled = true;
        }
        else if (ctrl && e.Key == Key.Y)
        {
            _viewModel.Redo();
            e.Handled = true;
        }
        else if (ctrl && shift && e.Key == Key.Z)
        {
            _viewModel.Redo();
            e.Handled = true;
        }
        else if (ctrl && !shift && e.Key == Key.S)
        {
            _viewModel.SaveCommand.Execute(null);
            e.Handled = true;
        }
        else if (ctrl && shift && e.Key == Key.S)
        {
            _viewModel.SaveAsCommand.Execute(null);
            e.Handled = true;
        }
        else if (ctrl && e.Key == Key.O)
        {
            _viewModel.OpenCommand.Execute(null);
            e.Handled = true;
        }
        else if (ctrl && e.Key == Key.N)
        {
            _viewModel.NewSceneCommand.Execute(null);
            e.Handled = true;
        }
        else if (!ctrl && e.Key == Key.Space)
        {
            _viewModel.TogglePlayStop();
            e.Handled = true;
        }
        else if (!ctrl && e.Key == Key.Delete)
        {
            _viewModel.RemoveSelectedSource();
            e.Handled = true;
        }
        else if (!ctrl && e.Key == Key.Escape)
        {
            _viewModel.DeselectAll();
            e.Handled = true;
        }
        else if (!ctrl && !shift && e.Key == Key.G)
        {
            _viewModel.ToggleSnapToGridCommand.Execute(null);
            e.Handled = true;
        }
    }

    // ── Source Collection Changes ────────────────────────────

    private void OnSourcesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            SpatialCanvas.ClearSourceNodes();
            return;
        }

        if (e.Action == NotifyCollectionChangedAction.Remove && e.OldItems != null)
        {
            foreach (AudioSourceViewModel vm in e.OldItems)
            {
                SpatialCanvas.RemoveSourceNode(vm.Id);
            }
        }

        // Note: Add is handled in SpatialCanvas.OnFileDrop directly for drop positioning.
        // For programmatic adds (e.g., scene load, undo), we handle here:
        if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null)
        {
            foreach (AudioSourceViewModel vm in e.NewItems)
            {
                // Check if node already exists (from drag-drop path)
                var existing = SpatialCanvas.Children.OfType<SourceNode>()
                    .Any(n => n.SourceVm.Id == vm.Id);
                if (!existing)
                {
                    SpatialCanvas.AddSourceNode(vm);
                }
            }
        }
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsSnapToGridEnabled))
        {
            bool snap = _viewModel.IsSnapToGridEnabled;
            SpatialCanvas.SetGridEnabled(visible: snap, snap: snap);
        }
    }

    // ── Sidebar Source Row Click ─────────────────────────────

    /// <summary>
    /// Clicking a sidebar source row selects it on the canvas too.
    /// </summary>
    private void SourceRow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border border && border.Tag is AudioSourceViewModel sourceVm)
        {
            _viewModel.SelectSource(sourceVm);
        }
    }

    // ── Volume Slider with Undo ─────────────────────────────

    /// <summary>
    /// Handles volume slider changes to update the audio engine in real-time.
    /// </summary>
    private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (sender is Slider slider && slider.Tag is AudioSourceViewModel sourceVm)
        {
            _viewModel.UpdateSourceVolume(sourceVm);
        }
    }
}
