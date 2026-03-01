using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Azimuth.ViewModels;

namespace Azimuth.Controls;

/// <summary>
/// Timeline panel control showing per-source waveform scrubbers.
/// </summary>
public partial class TimelinePanel : UserControl
{
    private bool _isSeeking;

    public TimelinePanel()
    {
        InitializeComponent();
    }

    private void WaveformCanvas_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is Canvas canvas && canvas.Tag is AudioSourceViewModel vm)
        {
            vm.PropertyChanged += (s, args) =>
            {
                if (args.PropertyName is nameof(AudioSourceViewModel.WaveformSamples)
                    or nameof(AudioSourceViewModel.PositionFraction))
                {
                    RedrawCanvas(canvas, vm);
                }
            };
            RedrawCanvas(canvas, vm);
        }
    }

    private void WaveformCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is Canvas canvas && canvas.Tag is AudioSourceViewModel vm)
            RedrawCanvas(canvas, vm);
    }

    private void RedrawCanvas(Canvas canvas, AudioSourceViewModel vm)
    {
        canvas.Children.Clear();

        double w = canvas.ActualWidth;
        double h = canvas.ActualHeight;
        if (w <= 0 || h <= 0) return;

        // Draw waveform bars
        var samples = vm.WaveformSamples;
        if (samples is { Length: > 0 })
        {
            double barWidth = w / samples.Length;
            var barBrush = GetSourceBrush(vm.Color, 0.4);

            for (int i = 0; i < samples.Length; i++)
            {
                double amplitude = samples[i];
                double barHeight = Math.Max(1, amplitude * (h - 4));
                double x = i * barWidth;
                double y = (h - barHeight) / 2.0;

                var rect = new Rectangle
                {
                    Width = Math.Max(1, barWidth - 0.5),
                    Height = barHeight,
                    Fill = barBrush,
                    IsHitTestVisible = false,
                };
                Canvas.SetLeft(rect, x);
                Canvas.SetTop(rect, y);
                canvas.Children.Add(rect);
            }
        }

        // Draw playhead
        double fraction = vm.PositionFraction;
        if (fraction > 0)
        {
            double px = fraction * w;

            // Played region overlay
            var playedRect = new Rectangle
            {
                Width = px,
                Height = h,
                Fill = GetSourceBrush(vm.Color, 0.12),
                IsHitTestVisible = false,
            };
            Canvas.SetLeft(playedRect, 0);
            Canvas.SetTop(playedRect, 0);
            canvas.Children.Add(playedRect);

            // Playhead line
            var playhead = new Line
            {
                X1 = px, Y1 = 0,
                X2 = px, Y2 = h,
                Stroke = GetSourceBrush(vm.Color, 1.0),
                StrokeThickness = 2,
                IsHitTestVisible = false,
            };
            canvas.Children.Add(playhead);
        }

        // Duration label
        if (vm.Duration.TotalSeconds > 0)
        {
            var durationText = new TextBlock
            {
                Text = FormatTime(vm.CurrentPosition) + " / " + FormatTime(vm.Duration),
                FontSize = 9,
                Foreground = new SolidColorBrush(Color.FromArgb(153, 240, 240, 245)),
                IsHitTestVisible = false,
            };
            Canvas.SetRight(durationText, 4);
            Canvas.SetBottom(durationText, 2);

            // Use right-aligned positioning: manually place from right edge
            durationText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(durationText, w - durationText.DesiredSize.Width - 4);
            Canvas.SetTop(durationText, h - durationText.DesiredSize.Height - 2);
            canvas.Children.Add(durationText);
        }
    }

    private void WaveformCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Canvas canvas && canvas.Tag is AudioSourceViewModel vm)
        {
            _isSeeking = true;
            canvas.CaptureMouse();
            SeekToMouse(canvas, vm, e);
        }
    }

    private void WaveformCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isSeeking && sender is Canvas canvas && canvas.Tag is AudioSourceViewModel vm)
        {
            SeekToMouse(canvas, vm, e);
        }
    }

    private void WaveformCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is Canvas canvas)
        {
            _isSeeking = false;
            canvas.ReleaseMouseCapture();
        }
    }

    private void SeekToMouse(Canvas canvas, AudioSourceViewModel vm, MouseEventArgs e)
    {
        double x = e.GetPosition(canvas).X;
        double fraction = Math.Clamp(x / canvas.ActualWidth, 0.0, 1.0);

        var mainVm = DataContext as MainViewModel;
        mainVm?.SeekSource(vm.Id, fraction);
    }

    private static SolidColorBrush GetSourceBrush(string hex, double opacity)
    {
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            var brush = new SolidColorBrush(color) { Opacity = opacity };
            brush.Freeze();
            return brush;
        }
        catch
        {
            return new SolidColorBrush(Color.FromRgb(45, 212, 191)) { Opacity = opacity };
        }
    }

    private static string FormatTime(TimeSpan ts)
    {
        return ts.TotalHours >= 1
            ? ts.ToString(@"h\:mm\:ss")
            : ts.ToString(@"m\:ss");
    }
}
