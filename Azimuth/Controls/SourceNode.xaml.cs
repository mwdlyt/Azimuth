using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Azimuth.ViewModels;

namespace Azimuth.Controls;

/// <summary>
/// A draggable audio source node displayed on the SpatialCanvas.
/// </summary>
public partial class SourceNode : UserControl
{
    public AudioSourceViewModel SourceVm { get; }

    /// <summary>
    /// Raised when the user drags this node. Provides the new canvas-space position.
    /// </summary>
    public event Action<SourceNode, Point>? SourceDragged;

    private bool _isDragging;
    private Point _dragStartMouse;
    private Point _dragStartPosition;

    public SourceNode(AudioSourceViewModel sourceVm)
    {
        InitializeComponent();
        SourceVm = sourceVm;

        NameLabel.Text = TruncateName(sourceVm.Name);
        ApplyColor(sourceVm.Color);

        MouseLeftButtonDown += OnMouseDown;
        MouseLeftButtonUp += OnMouseUp;
        MouseMove += OnMouseMove;
    }

    private void ApplyColor(string hex)
    {
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            StrokeBrush.Color = color;
            MainEllipse.Fill = new SolidColorBrush(color) { Opacity = 0.3 };

            GlowEllipse.Fill = new RadialGradientBrush(
                new GradientStopCollection
                {
                    new GradientStop(color, 0.2),
                    new GradientStop(Colors.Transparent, 1.0),
                });
        }
        catch { }
    }

    private static string TruncateName(string name)
    {
        return name.Length > 6 ? name[..5] + "…" : name;
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        _isDragging = true;
        _dragStartMouse = e.GetPosition(Parent as UIElement);

        var canvas = Parent as Canvas;
        if (canvas != null)
        {
            _dragStartPosition = new Point(
                Canvas.GetLeft(this) + Width / 2,
                Canvas.GetTop(this) + Height / 2);
        }

        CaptureMouse();
        e.Handled = true;
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            ReleaseMouseCapture();
            e.Handled = true;
        }
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging) return;

        var currentMouse = e.GetPosition(Parent as UIElement);
        double dx = currentMouse.X - _dragStartMouse.X;
        double dy = currentMouse.Y - _dragStartMouse.Y;

        var newCenter = new Point(
            _dragStartPosition.X + dx,
            _dragStartPosition.Y + dy);

        SourceDragged?.Invoke(this, newCenter);
    }
}
