using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Azimuth.Commands;
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

    /// <summary>When true, source nodes snap to the grid on drag.</summary>
    public bool IsSnapToGridEnabled { get; set; }

    /// <summary>When true, the grid dots are drawn on the canvas.</summary>
    public bool IsGridVisible { get; set; }

    public SpatialCanvas()
    {
        AllowDrop = true;
        ClipToBounds = true;
        Background = BrushFromHex(AppConfig.CanvasBackgroundHex);

        Drop += OnFileDrop;
        DragOver += OnDragOver;
        SizeChanged += OnSizeChanged;
        Loaded += OnLoaded;
        MouseLeftButtonDown += OnCanvasMouseDown;
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
    /// Click on empty canvas space deselects all sources.
    /// </summary>
    private void OnCanvasMouseDown(object sender, MouseButtonEventArgs e)
    {
        // Only deselect if the click is on the canvas itself (not on a SourceNode)
        if (e.OriginalSource == this || e.OriginalSource is Ellipse || e.OriginalSource is TextBlock)
        {
            // Check that we didn't hit a SourceNode
            var hit = e.OriginalSource as DependencyObject;
            while (hit != null && hit != this)
            {
                if (hit is SourceNode) return;
                hit = VisualTreeHelper.GetParent(hit);
            }

            _viewModel?.DeselectAll();
        }
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

        // Draw grid dots (only when grid is visible)
        if (IsGridVisible)
        {
            DrawGrid(cx, cy);
        }

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
        node.DragStarted += OnSourceNodeDragStarted;
        node.DragEnded += OnSourceNodeDragEnded;
        node.RemoveRequested += OnSourceNodeRemoveRequested;
        node.Clicked += OnSourceNodeClicked;
        Children.Add(node);
        PositionNode(node);

        // Draw connection line
        UpdateConnectionLine(node);

        return node;
    }

    /// <summary>
    /// Removes a source node from the canvas.
    /// </summary>
    private void OnSourceNodeRemoveRequested(SourceNode node)
    {
        _viewModel?.RemoveSource(node.SourceVm.Id);
    }

    /// <summary>
    /// Handles a click (non-drag) on a source node — selects it.
    /// </summary>
    private void OnSourceNodeClicked(SourceNode node)
    {
        _viewModel?.SelectSource(node.SourceVm);
    }

    public void RemoveSourceNode(Guid sourceId)
    {
        var node = Children.OfType<SourceNode>().FirstOrDefault(n => n.SourceVm.Id == sourceId);
        if (node != null)
        {
            node.SourceDragged -= OnSourceNodeDragged;
            node.DragStarted -= OnSourceNodeDragStarted;
            node.DragEnded -= OnSourceNodeDragEnded;
            node.RemoveRequested -= OnSourceNodeRemoveRequested;
            node.Clicked -= OnSourceNodeClicked;
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
            n.DragStarted -= OnSourceNodeDragStarted;
            n.DragEnded -= OnSourceNodeDragEnded;
            n.RemoveRequested -= OnSourceNodeRemoveRequested;
            n.Clicked -= OnSourceNodeClicked;
            Children.Remove(n);
        }

        var lines = Children.OfType<Line>().Where(l => l.Tag is Guid).ToList();
        foreach (var l in lines) Children.Remove(l);
    }

    /// <summary>
    /// Repositions a source node on the canvas and updates its connection line.
    /// Called by MainWindow when an undo/redo changes a source's position.
    /// </summary>
    public void RefreshNodePosition(Guid sourceId)
    {
        var node = Children.OfType<SourceNode>().FirstOrDefault(n => n.SourceVm.Id == sourceId);
        if (node == null) return;
        PositionNode(node);
        UpdateConnectionLine(node);
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

    // ── Drag-start position tracking for undo ────────────────

    private double _dragStartX;
    private double _dragStartY;

    private void OnSourceNodeDragStarted(SourceNode node)
    {
        _dragStartX = node.SourceVm.X;
        _dragStartY = node.SourceVm.Y;

        // Select the node being dragged
        _viewModel?.SelectSource(node.SourceVm);
    }

    private void OnSourceNodeDragEnded(SourceNode node)
    {
        if (_viewModel == null) return;

        double newX = node.SourceVm.X;
        double newY = node.SourceVm.Y;

        // Only create a move command if the position actually changed
        if (Math.Abs(newX - _dragStartX) > 0.01 || Math.Abs(newY - _dragStartY) > 0.01)
        {
            var cmd = new MoveSourceCommand(
                node.SourceVm,
                _dragStartX, _dragStartY,
                newX, newY,
                vm => _viewModel.UpdateSourcePosition(vm));

            // Push to undo stack without re-executing (position is already set)
            _viewModel.UndoRedo.Execute(cmd);
            // The Execute call sets it again (no-op since values are the same)
        }
    }

    private void OnSourceNodeDragged(SourceNode node, Point canvasPos)
    {
        double cx = ActualWidth / 2.0;
        double cy = ActualHeight / 2.0;

        // Convert canvas position to offset from center
        double relX = canvasPos.X - cx;
        double relY = canvasPos.Y - cy;

        // Snap to grid if enabled (snap before clamping so grid aligns naturally)
        if (IsSnapToGridEnabled)
        {
            relX = SnapToGrid(relX);
            relY = SnapToGrid(relY);
        }

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
            if (!Services.AudioReaderFactory.IsSupported(ext)) continue;

            // Node creation is handled by MainWindow.OnSourcesChanged via CollectionChanged.
            // Do NOT call AddSourceNode here — that causes the duplication bug.
            _viewModel.AddSourceFromFile(file, offsetX, offsetY);

            // Offset slightly for multiple files dropped at once
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

    /// <summary>Draws a dot grid across the full canvas area.</summary>
    private void DrawGrid(double cx, double cy)
    {
        double gridSize = AppConfig.GridSize;
        var dotBrush = BrushFromHex(AppConfig.GridColorHex);

        // Start from the center and work outward so grid aligns to center
        double startX = cx % gridSize;
        double startY = cy % gridSize;

        for (double x = startX; x < ActualWidth; x += gridSize)
        {
            for (double y = startY; y < ActualHeight; y += gridSize)
            {
                var dot = new Ellipse
                {
                    Width = 2,
                    Height = 2,
                    Fill = dotBrush,
                    IsHitTestVisible = false,
                };
                SetLeft(dot, x - 1);
                SetTop(dot, y - 1);
                Children.Add(dot);
            }
        }
    }

    /// <summary>Snaps a value to the nearest grid increment relative to the canvas center.</summary>
    private static double SnapToGrid(double value)
    {
        double g = AppConfig.GridSize;
        return Math.Round(value / g) * g;
    }

    /// <summary>Toggles grid visibility and snap, then redraws the canvas.</summary>
    public void SetGridEnabled(bool visible, bool snap)
    {
        IsGridVisible = visible;
        IsSnapToGridEnabled = snap;
        Rebuild();
    }

    private static SolidColorBrush BrushFromHex(string hex)
    {
        var color = (Color)ColorConverter.ConvertFromString(hex);
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
