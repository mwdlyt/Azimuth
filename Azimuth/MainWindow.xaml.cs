using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
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

        Closed += (_, _) =>
        {
            _viewModel.Dispose();
        };
    }

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
        // For programmatic adds (e.g., scene load), we handle here:
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

    /// <summary>
    /// Handles volume slider changes to update the audio engine.
    /// </summary>
    private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (sender is Slider slider && slider.Tag is AudioSourceViewModel sourceVm)
        {
            _viewModel.UpdateSourceVolume(sourceVm);
        }
    }
}
