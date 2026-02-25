using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Azimuth.Models;
using Azimuth.ViewModels;

namespace Azimuth.Controls;

/// <summary>
/// A draggable audio source node displayed on the SpatialCanvas.
/// </summary>
public partial class SourceNode : UserControl
{
    private static readonly SolidColorBrush SelectionGlowBrush;

    static SourceNode()
    {
        var color = (Color)ColorConverter.ConvertFromString(AppConfig.AccentHex);
        SelectionGlowBrush = new SolidColorBrush(color);
        SelectionGlowBrush.Freeze();
    }

    public AudioSourceViewModel SourceVm { get; }

    /// <summary>
    /// Raised when the user drags this node. Provides the new canvas-space position.
    /// </summary>
    public event Action<SourceNode, Point>? SourceDragged;

    /// <summary>Raised when a drag operation begins (mouse down before move).</summary>
    public event Action<SourceNode>? DragStarted;

    /// <summary>Raised when a drag operation ends (mouse up after drag).</summary>
    public event Action<SourceNode>? DragEnded;

    /// <summary>Raised when the user requests removal via right-click context menu.</summary>
    public event Action<SourceNode>? RemoveRequested;

    /// <summary>Raised when the node is clicked without dragging (for selection).</summary>
    public event Action<SourceNode>? Clicked;

    private bool _isDragging;
    private bool _hasMoved;
    private Point _dragStartMouse;
    private Point _dragStartPosition;
    private Color _nodeColor;

    public SourceNode(AudioSourceViewModel sourceVm)
    {
        InitializeComponent();
        SourceVm = sourceVm;

        NameLabel.Text = TruncateName(sourceVm.Name);
        ApplyColor(sourceVm.Color);

        MouseLeftButtonDown += OnMouseDown;
        MouseLeftButtonUp += OnMouseUp;
        MouseMove += OnMouseMove;
        MouseRightButtonUp += OnRightClick;

        // Listen for IsSelected changes to update the selection glow
        sourceVm.PropertyChanged += OnSourceVmPropertyChanged;
        UpdateSelectionVisual();
    }

    private void OnSourceVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AudioSourceViewModel.IsSelected))
        {
            UpdateSelectionVisual();
        }
    }

    /// <summary>
    /// Updates the glow ellipse to show an accent-colored glow when selected.
    /// </summary>
    private void UpdateSelectionVisual()
    {
        if (SourceVm.IsSelected)
        {
            // Show accent glow for selected state
            GlowEllipse.Opacity = 0.6;
            GlowEllipse.Fill = new RadialGradientBrush(
                new GradientStopCollection
                {
                    new GradientStop((Color)ColorConverter.ConvertFromString(AppConfig.AccentHex), 0.0),
                    new GradientStop(Colors.Transparent, 1.0),
                });
        }
        else
        {
            // Restore normal glow
            GlowEllipse.Opacity = 0.25;
            GlowEllipse.Fill = new RadialGradientBrush(
                new GradientStopCollection
                {
                    new GradientStop(_nodeColor, 0.2),
                    new GradientStop(Colors.Transparent, 1.0),
                });
        }
    }

    private void OnRightClick(object sender, MouseButtonEventArgs e)
    {
        var menu = new ContextMenu();

        var header = new MenuItem
        {
            Header = SourceVm.Name,
            IsEnabled = false,
            FontWeight = System.Windows.FontWeights.SemiBold,
        };

        var remove = new MenuItem { Header = "\U0001F5D1  Remove source" };
        remove.Click += (_, _) => RemoveRequested?.Invoke(this);

        menu.Items.Add(header);
        menu.Items.Add(new Separator());
        menu.Items.Add(remove);

        ContextMenu = menu;
        menu.IsOpen = true;
        e.Handled = true;
    }

    private void ApplyColor(string hex)
    {
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            _nodeColor = color;
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
        return name.Length > 6 ? name[..5] + "\u2026" : name;
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        _isDragging = true;
        _hasMoved = false;
        _dragStartMouse = e.GetPosition(Parent as UIElement);

        if (Parent is Canvas)
        {
            _dragStartPosition = new Point(
                Canvas.GetLeft(this) + Width / 2,
                Canvas.GetTop(this) + Height / 2);
        }

        DragStarted?.Invoke(this);
        CaptureMouse();
        e.Handled = true;
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            ReleaseMouseCapture();

            if (_hasMoved)
            {
                DragEnded?.Invoke(this);
            }
            else
            {
                // Click without drag — fire Clicked for selection
                Clicked?.Invoke(this);
            }

            e.Handled = true;
        }
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging) return;

        var currentMouse = e.GetPosition(Parent as UIElement);
        double dx = currentMouse.X - _dragStartMouse.X;
        double dy = currentMouse.Y - _dragStartMouse.Y;

        // Only count as a move if we've moved more than a small threshold
        if (!_hasMoved && (Math.Abs(dx) > 3 || Math.Abs(dy) > 3))
        {
            _hasMoved = true;
        }

        if (_hasMoved)
        {
            var newCenter = new Point(
                _dragStartPosition.X + dx,
                _dragStartPosition.Y + dy);

            SourceDragged?.Invoke(this, newCenter);
        }
    }
}
