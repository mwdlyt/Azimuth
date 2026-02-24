using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Azimuth.Models;
using Azimuth.ViewModels;

namespace Azimuth.Controls;

/// <summary>
/// Custom canvas control that renders the spatial audio stage: concentric rings, labels,
/// listener icon, and manages drag-drop of audio files and dragging of source nodes.
/// </summary>
public class SpatialCanvas : Canvas
{
    private MainViewModel? _viewModel;
    private double _radius;

    public SpatialCanvas()
    {
        AllowDrop = true;
        ClipToBounds = true;
        Background = BrushFromHex(AppConfig.CanvasBackgroundHex);

        Drop += OnFileDrop;
        DragOver += OnDragOver;
        SizeChanged += OnSizeChanged;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _viewModel = DataContext as MainViewModel;
        Rebuild();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        Rebuild();
    }

    /// <summary>
    /// Rebuilds all static visual elements (rings, labels, listener icon).
    /// Source nodes are managed separately.
    /// </summary>
    public void Rebuild()
    {
        // Remove only the decorative elements (keep SourceNodes)
        var nodesToKeep = Children.OfType<SourceNode>().ToList();
        Children.Clear();

        _radius = Math.Min(ActualWidth, ActualHeight) / 2.0 - 20;
        if (_radius <= 0) return;

        double cx = ActualWidth / 2.0;
        double cy = ActualHeight / 2.0;

        if (_viewModel != null)
            _viewModel.CanvasRadius = _radius;

        // Draw ring guides
        foreach (double pct in AppConfig.RingPercentages)
        {
            double r = _radius * pct;
            var ellipse = new Ellipse
            {
                Width = r * 2,
                Height = r * 2,
                Stroke = BrushFromHex("#1A1A2E"),
                StrokeThickness = 1,
                StrokeDashArray = pct < 1.0 ? new DoubleCollection { 4, 4 } : new DoubleCollection(),
                IsHitTestVisible = false,
            };
            SetLeft(ellipse, cx - r);
            SetTop(ellipse, cy - r);
            Children.Add(ellipse);
        }

        // Draw crosshairs (subtle)
        var crossColor = BrushFromHex("#1A1A2E");
        Children.Add(MakeLine(cx, cy - _radius, cx, cy + _radius, crossColor));
        Children.Add(MakeLine(cx - _radius, cy, cx + _radius, cy, crossColor));

        // Direction labels
        AddLabel("Front", cx, cy - _radius - 18, 12);
        AddLabel("Back", cx, cy + _radius + 6, 12);
        AddLabel("Left", cx - _radius - 30, cy - 8, 12);
        AddLabel("Right", cx + _radius + 8, cy - 8, 12);

        // Listener icon at center (simple circle + headphone shape)
        var listenerOuter = new Ellipse
        {
            Width = AppConfig.ListenerIconRadius * 2,
            Height = AppConfig.ListenerIconRadius * 2,
            Fill = BrushFromHex("#1E1E30"),
            Stroke = BrushFromHex("#3A3A50"),
            StrokeThickness = 2,
            IsHitTestVisible = false,
        };
        SetLeft(listenerOuter, cx - AppConfig.ListenerIconRadius);
        SetTop(listenerOuter, cy - AppConfig.ListenerIconRadius);
        Children.Add(listenerOuter);

        // Headphone icon - simple text glyph
        var headphone = new TextBlock
        {
            Text = "\U0001F3A7",
            FontSize = 16,
            Foreground = Brushes.White,
            IsHitTestVisible = false,
        };
        headphone.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        SetLeft(headphone, cx - headphone.DesiredSize.Width / 2);
        SetTop(headphone, cy - headphone.DesiredSize.Height / 2);
        Children.Add(headphone);

        // Re-add source nodes
        foreach (var node in nodesToKeep)
        {
            Children.Add(node);
            PositionNode(node);
        }
    }

    /// <summary>
    /// Adds a source node to the canvas for the given view model.
    /// </summary>
    public SourceNode AddSourceNode(AudioSourceViewModel sourceVm)
    {
        var node = new SourceNode(sourceVm);
        node.SourceDragged += OnSourceNodeDragged;
        Children.Add(node);
        PositionNode(node);

        // Draw connection line
        UpdateConnectionLine(node);

        return node;
    }

    /// <summary>
    /// Removes a source node from the canvas.
    /// </summary>
    public void RemoveSourceNode(Guid sourceId)
    {
        var node = Children.OfType<SourceNode>().FirstOrDefault(n => n.SourceVm.Id == sourceId);
        if (node != null)
        {
            node.SourceDragged -= OnSourceNodeDragged;
            Children.Remove(node);
        }

        // Remove the associated line
        var line = Children.OfType<Line>().FirstOrDefault(l => l.Tag is Guid id && id == sourceId);
        if (line != null) Children.Remove(line);
    }

    /// <summary>
    /// Clears all source nodes.
    /// </summary>
    public void ClearSourceNodes()
    {
        var nodes = Children.OfType<SourceNode>().ToList();
        foreach (var n in nodes)
        {
            n.SourceDragged -= OnSourceNodeDragged;
            Children.Remove(n);
        }

        var lines = Children.OfType<Line>().Where(l => l.Tag is Guid).ToList();
        foreach (var l in lines) Children.Remove(l);
    }

    private void PositionNode(SourceNode node)
    {
        double cx = ActualWidth / 2.0;
        double cy = ActualHeight / 2.0;

        double left = cx + node.SourceVm.X - AppConfig.SourceNodeRadius;
        double top = cy + node.SourceVm.Y - AppConfig.SourceNodeRadius;

        SetLeft(node, left);
        SetTop(node, top);
    }

    private void UpdateConnectionLine(SourceNode node)
    {
        double cx = ActualWidth / 2.0;
        double cy = ActualHeight / 2.0;

        // Remove old line
        var oldLine = Children.OfType<Line>().FirstOrDefault(l => l.Tag is Guid id && id == node.SourceVm.Id);
        if (oldLine != null) Children.Remove(oldLine);

        var line = new Line
        {
            X1 = cx,
            Y1 = cy,
            X2 = cx + node.SourceVm.X,
            Y2 = cy + node.SourceVm.Y,
            Stroke = BrushFromHex(node.SourceVm.Color),
            StrokeThickness = 1,
            Opacity = 0.3,
            IsHitTestVisible = false,
            Tag = node.SourceVm.Id,
        };

        // Insert line behind nodes (at start)
        Children.Insert(0, line);
    }

    private void OnSourceNodeDragged(SourceNode node, Point canvasPos)
    {
        double cx = ActualWidth / 2.0;
        double cy = ActualHeight / 2.0;

        // Convert canvas position to offset from center
        double relX = canvasPos.X - cx;
        double relY = canvasPos.Y - cy;

        // Clamp within the circle
        double dist = Math.Sqrt(relX * relX + relY * relY);
        if (dist > _radius)
        {
            relX = relX / dist * _radius;
            relY = relY / dist * _radius;
        }

        node.SourceVm.X = relX;
        node.SourceVm.Y = relY;

        PositionNode(node);
        UpdateConnectionLine(node);

        _viewModel?.UpdateSourcePosition(node.SourceVm);
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }
    }

    private void OnFileDrop(object sender, DragEventArgs e)
    {
        if (_viewModel is null) return;
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

        var files = e.Data.GetData(DataFormats.FileDrop) as string[];
        if (files is null) return;

        var dropPos = e.GetPosition(this);
        double cx = ActualWidth / 2.0;
        double cy = ActualHeight / 2.0;

        double offsetX = dropPos.X - cx;
        double offsetY = dropPos.Y - cy;

        foreach (var file in files)
        {
            var ext = System.IO.Path.GetExtension(file).ToLowerInvariant();
            if (ext is not ".wav" and not ".mp3") continue;

            _viewModel.AddSourceFromFile(file, offsetX, offsetY);

            // Find the newly added source VM and create a node for it
            var newVm = _viewModel.Sources.LastOrDefault();
            if (newVm != null)
            {
                AddSourceNode(newVm);
            }

            // Offset slightly for multiple files
            offsetX += 30;
            offsetY += 30;
        }
    }

    private void AddLabel(string text, double x, double y, double fontSize)
    {
        var tb = new TextBlock
        {
            Text = text,
            FontSize = fontSize,
            Foreground = BrushFromHex(AppConfig.TextMutedHex),
            IsHitTestVisible = false,
        };
        SetLeft(tb, x);
        SetTop(tb, y);
        Children.Add(tb);
    }

    private static Line MakeLine(double x1, double y1, double x2, double y2, Brush stroke)
    {
        return new Line
        {
            X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
            Stroke = stroke,
            StrokeThickness = 1,
            IsHitTestVisible = false,
        };
    }

    private static SolidColorBrush BrushFromHex(string hex)
    {
        var color = (Color)ColorConverter.ConvertFromString(hex);
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
