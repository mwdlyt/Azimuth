using System.Windows;
using System.Windows.Controls;
using Azimuth.Services;
using Wpf.Ui.Controls;

namespace Azimuth.Views;

/// <summary>
/// Modal settings dialog. Reads from <see cref="UserSettings"/> on open,
/// writes back only when the user clicks OK.
/// </summary>
public partial class SettingsWindow : FluentWindow
{
    private readonly UserSettings _settings;
    private bool _cleared;

    public SettingsWindow()
    {
        _settings = UserSettings.Instance;
        InitializeComponent();
        LoadFromSettings();
    }

    /// <summary>Whether the user cleared recent files while the dialog was open.</summary>
    public bool RecentFilesCleared => _cleared;

    private void LoadFromSettings()
    {
        // Sample rate
        foreach (ComboBoxItem item in SampleRateCombo.Items)
        {
            if (item.Tag is string tag && int.TryParse(tag, out int rate) && rate == _settings.SampleRate)
            {
                SampleRateCombo.SelectedItem = item;
                break;
            }
        }

        if (SampleRateCombo.SelectedItem is null)
            SampleRateCombo.SelectedIndex = 1; // default 44100

        // Falloff
        FalloffSlider.Value = _settings.DistanceFalloff;
        FalloffValueText.Text = _settings.DistanceFalloff.ToString("F1");

        // Grid
        GridSizeSlider.Value = _settings.GridSize;
        GridSizeValueText.Text = ((int)_settings.GridSize).ToString();
        SnapDefaultCheck.IsChecked = _settings.SnapToGridDefault;

        // General
        OpenLastSceneCheck.IsChecked = _settings.OpenLastScene;
    }

    private void FalloffSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (FalloffValueText is not null)
            FalloffValueText.Text = e.NewValue.ToString("F1");
    }

    private void GridSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (GridSizeValueText is not null)
            GridSizeValueText.Text = ((int)e.NewValue).ToString();
    }

    private void ClearRecents_Click(object sender, RoutedEventArgs e)
    {
        _settings.ClearRecentFiles();
        _cleared = true;
    }

    private void OK_Click(object sender, RoutedEventArgs e)
    {
        // Sample rate
        if (SampleRateCombo.SelectedItem is ComboBoxItem item
            && item.Tag is string tag
            && int.TryParse(tag, out int rate))
        {
            _settings.SampleRate = rate;
        }

        _settings.DistanceFalloff = FalloffSlider.Value;
        _settings.GridSize = GridSizeSlider.Value;
        _settings.SnapToGridDefault = SnapDefaultCheck.IsChecked == true;
        _settings.OpenLastScene = OpenLastSceneCheck.IsChecked == true;

        _settings.Save();
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
