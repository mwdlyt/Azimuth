using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Azimuth.Models;
using Azimuth.ViewModels;

namespace Azimuth.Controls;

/// <summary>
/// Orbit settings panel that displays and edits orbital motion properties
/// for the currently selected audio source.
/// </summary>
public partial class OrbitPanel : UserControl
{
    private AudioSourceViewModel? _source;
    private bool _isRadiiLinked = true;
    private bool _isSuppressingSliderEvents;

    public OrbitPanel()
    {
        InitializeComponent();
        UpdateLinkButtonVisual();
        UpdateDirectionButtons();
    }

    /// <summary>
    /// Binds the panel to the given source view model, or null to clear.
    /// </summary>
    public void BindSource(AudioSourceViewModel? source)
    {
        // Unsubscribe from previous source
        if (_source != null)
            _source.PropertyChanged -= OnSourcePropertyChanged;

        _source = source;

        if (_source != null)
        {
            _source.PropertyChanged += OnSourcePropertyChanged;
            RefreshAllControls();
        }
    }

    private void OnSourcePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(AudioSourceViewModel.OrbitEnabled):
            case nameof(AudioSourceViewModel.OrbitRadiusX):
            case nameof(AudioSourceViewModel.OrbitRadiusY):
            case nameof(AudioSourceViewModel.OrbitSpeed):
            case nameof(AudioSourceViewModel.OrbitClockwise):
            case nameof(AudioSourceViewModel.OrbitCenterX):
            case nameof(AudioSourceViewModel.OrbitCenterY):
            case nameof(AudioSourceViewModel.OrbitAngle):
                RefreshAllControls();
                break;
        }
    }

    /// <summary>
    /// Refreshes all panel controls from the bound source.
    /// </summary>
    private void RefreshAllControls()
    {
        if (_source == null) return;

        _isSuppressingSliderEvents = true;

        // Header
        SourceNameText.Text = _source.Name;
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(_source.Color);
            ColorDot.Fill = new SolidColorBrush(color);
        }
        catch
        {
            ColorDot.Fill = Brushes.Gray;
        }

        // Toggle
        OrbitToggle.IsChecked = _source.OrbitEnabled;
        OrbitSettingsPanel.Visibility = _source.OrbitEnabled ? Visibility.Visible : Visibility.Collapsed;

        // Shape
        RadiusXSlider.Value = _source.OrbitRadiusX;
        RadiusYSlider.Value = _source.OrbitRadiusY;
        RadiusXValue.Text = $"{(int)_source.OrbitRadiusX} px";
        RadiusYValue.Text = $"{(int)_source.OrbitRadiusY} px";

        // Motion
        SpeedSlider.Value = _source.OrbitSpeed;
        SpeedValue.Text = _source.OrbitSpeedDisplay;
        UpdateDirectionButtons();

        // Position
        CenterXValue.Text = $"{(int)_source.OrbitCenterX}";
        CenterYValue.Text = $"{(int)_source.OrbitCenterY}";

        _isSuppressingSliderEvents = false;
    }

    // ── Event Handlers ──────────────────────────────────────

    private void OrbitToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_source == null) return;
        _source.OrbitEnabled = OrbitToggle.IsChecked == true;
        OrbitSettingsPanel.Visibility = _source.OrbitEnabled ? Visibility.Visible : Visibility.Collapsed;

        // Notify parent to refresh orbit timer
        OrbitChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RadiusXSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_source == null || _isSuppressingSliderEvents) return;

        _source.OrbitRadiusX = RadiusXSlider.Value;
        RadiusXValue.Text = $"{(int)RadiusXSlider.Value} px";

        if (_isRadiiLinked)
        {
            _isSuppressingSliderEvents = true;
            _source.OrbitRadiusY = RadiusXSlider.Value;
            RadiusYSlider.Value = RadiusXSlider.Value;
            RadiusYValue.Text = $"{(int)RadiusXSlider.Value} px";
            _isSuppressingSliderEvents = false;
        }
    }

    private void RadiusYSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_source == null || _isSuppressingSliderEvents) return;

        _source.OrbitRadiusY = RadiusYSlider.Value;
        RadiusYValue.Text = $"{(int)RadiusYSlider.Value} px";

        if (_isRadiiLinked)
        {
            _isSuppressingSliderEvents = true;
            _source.OrbitRadiusX = RadiusYSlider.Value;
            RadiusXSlider.Value = RadiusYSlider.Value;
            RadiusXValue.Text = $"{(int)RadiusYSlider.Value} px";
            _isSuppressingSliderEvents = false;
        }
    }

    private void SpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_source == null || _isSuppressingSliderEvents) return;
        _source.OrbitSpeed = SpeedSlider.Value;
        SpeedValue.Text = _source.OrbitSpeedDisplay;
    }

    private void LinkButton_Click(object sender, RoutedEventArgs e)
    {
        _isRadiiLinked = !_isRadiiLinked;
        UpdateLinkButtonVisual();

        // If linking, sync Y to X
        if (_isRadiiLinked && _source != null)
        {
            _source.OrbitRadiusY = _source.OrbitRadiusX;
            _isSuppressingSliderEvents = true;
            RadiusYSlider.Value = _source.OrbitRadiusX;
            RadiusYValue.Text = $"{(int)_source.OrbitRadiusX} px";
            _isSuppressingSliderEvents = false;
        }
    }

    private void UpdateLinkButtonVisual()
    {
        if (LinkButton == null) return;
        LinkButton.Appearance = _isRadiiLinked
            ? Wpf.Ui.Controls.ControlAppearance.Primary
            : Wpf.Ui.Controls.ControlAppearance.Secondary;
        LinkButton.Content = _isRadiiLinked ? "\U0001F517 Linked" : "\U0001F517 Unlinked";
    }

    private void CwButton_Click(object sender, RoutedEventArgs e)
    {
        if (_source == null) return;
        _source.OrbitClockwise = true;
        UpdateDirectionButtons();
    }

    private void CcwButton_Click(object sender, RoutedEventArgs e)
    {
        if (_source == null) return;
        _source.OrbitClockwise = false;
        UpdateDirectionButtons();
    }

    private void UpdateDirectionButtons()
    {
        if (CwButton == null || CcwButton == null) return;
        bool cw = _source?.OrbitClockwise ?? true;
        CwButton.Appearance = cw
            ? Wpf.Ui.Controls.ControlAppearance.Primary
            : Wpf.Ui.Controls.ControlAppearance.Secondary;
        CcwButton.Appearance = !cw
            ? Wpf.Ui.Controls.ControlAppearance.Primary
            : Wpf.Ui.Controls.ControlAppearance.Secondary;
    }

    private void ResetCenterButton_Click(object sender, RoutedEventArgs e)
    {
        if (_source == null) return;
        _source.OrbitCenterX = _source.X;
        _source.OrbitCenterY = _source.Y;
        CenterXValue.Text = $"{(int)_source.OrbitCenterX}";
        CenterYValue.Text = $"{(int)_source.OrbitCenterY}";
    }

    /// <summary>
    /// Raised when orbit enabled/disabled changes, so the parent can refresh the orbit timer.
    /// </summary>
    public event EventHandler? OrbitChanged;
}
