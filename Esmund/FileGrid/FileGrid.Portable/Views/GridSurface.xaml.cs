using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using FileGrid.Portable.Helpers;
using FileGrid.Portable.Models;

namespace FileGrid.Portable.Views;

public partial class GridSurface : UserControl
{
    private LayoutState _layout = LayoutState.CreateDefault();
    private AppSettings _settings = new();
    private string? _selectedBoxId;
    private string? _selectedLinkId;
    private bool _linking;
    private string? _linkFromId;
    private bool _panning;
    private Point _panStart;
    private double _panStartX;
    private double _panStartY;
    private string? _dragBoxId;
    private int _dragStartGx, _dragStartGy, _pointerGx, _pointerGy;
    private string? _resizeBoxId;
    private int _resizeStartGw, _resizeStartGh;

    public event Action<LayoutState>? LayoutChanged;
    public event Action<string?>? BoxSelected;
    public event Action<string?>? LinkSelected;
    public event Action<string, string[]>? FilesDroppedOnBox;
    public event Action<string>? LinkBoxPicked;

    public GridSurface()
    {
        InitializeComponent();
        PreviewMouseWheel += OnWheel;
        PreviewMouseDown += OnMouseDown;
        PreviewMouseMove += OnMouseMove;
        PreviewMouseUp += OnMouseUp;
        DragOver += (_, e) => { e.Effects = DragDropEffects.Copy; e.Handled = true; };
        Drop += OnDrop;
    }

    public void Bind(LayoutState layout, AppSettings settings, string? selectedBoxId, string? selectedLinkId, bool linking, string? linkFromId)
    {
        _layout = layout;
        _settings = settings;
        _selectedBoxId = selectedBoxId;
        _selectedLinkId = selectedLinkId;
        _linking = linking;
        _linkFromId = linkFromId;
        Background = ColorHelper.Brush(settings.BackgroundColor);
        Redraw();
    }

    private void Redraw()
    {
        LinkCanvas.Children.Clear();
        DrawGrid();
        DrawLinks();
        DrawBoxes();
    }

    private void DrawGrid()
    {
        if (!_settings.ShowGrid) return;
        var step = GridMath.Step(_layout);
        if (step < 4) return;
        var brush = ColorHelper.Brush(_settings.GridColor, _settings.GridOpacity);
        var w = ActualWidth;
        var h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        var firstGx = (int)Math.Floor(-_layout.View.PanX / step) - 1;
        var lastGx = (int)Math.Ceiling((w - _layout.View.PanX) / step) + 1;
        var firstGy = (int)Math.Floor(-_layout.View.PanY / step) - 1;
        var lastGy = (int)Math.Ceiling((h - _layout.View.PanY) / step) + 1;

        for (var gx = firstGx; gx <= lastGx; gx++)
        {
            var x = Math.Round(_layout.View.PanX + gx * step) + 0.5;
            LinkCanvas.Children.Add(new Line
            {
                Tag = "grid",
                X1 = x, X2 = x, Y1 = 0, Y2 = h,
                Stroke = brush, StrokeThickness = 1, IsHitTestVisible = false
            });
        }
        for (var gy = firstGy; gy <= lastGy; gy++)
        {
            var y = Math.Round(_layout.View.PanY + gy * step) + 0.5;
            LinkCanvas.Children.Add(new Line
            {
                Tag = "grid",
                X1 = 0, X2 = w, Y1 = y, Y2 = y,
                Stroke = brush, StrokeThickness = 1, IsHitTestVisible = false
            });
        }
    }

    private void DrawLinks()
    {
        var map = new Dictionary<string, GridBox>();
        foreach (var b in _layout.Boxes)
        {
            if (!string.IsNullOrEmpty(b.Id) && !map.ContainsKey(b.Id))
                map[b.Id] = b;
        }
        foreach (var link in _layout.Links)
        {
            if (!map.TryGetValue(link.FromBoxId, out var a) || !map.TryGetValue(link.ToBoxId, out var b)) continue;
            var ra = GridMath.GridRectToScreen(_layout, a);
            var rb = GridMath.GridRectToScreen(_layout, b);
            var ac = new Point(ra.X + ra.Width / 2, ra.Y + ra.Height / 2);
            var bc = new Point(rb.X + rb.Width / 2, rb.Y + rb.Height / 2);
            var p1 = GridMath.BorderPoint(ra, bc);
            var p2 = GridMath.BorderPoint(rb, ac);

            var hit = new Line
            {
                X1 = p1.X, Y1 = p1.Y, X2 = p2.X, Y2 = p2.Y,
                Stroke = Brushes.Transparent,
                StrokeThickness = Math.Max(link.Thickness, 10),
                Tag = link.Id,
                Cursor = Cursors.Hand
            };
            hit.MouseDown += (_, e) =>
            {
                e.Handled = true;
                LinkSelected?.Invoke(link.Id);
            };
            LinkCanvas.Children.Add(hit);

            var vis = new Line
            {
                X1 = p1.X, Y1 = p1.Y, X2 = p2.X, Y2 = p2.Y,
                Stroke = ColorHelper.Brush(link.Color),
                StrokeThickness = link.Thickness,
                IsHitTestVisible = false
            };
            if (link.Id == _selectedLinkId)
                vis.Stroke = Brushes.DeepSkyBlue;
            LinkCanvas.Children.Add(vis);
        }
    }

    private void DrawBoxes()
    {
        BoxCanvas.Children.Clear();
        foreach (var box in _layout.Boxes)
        {
            var rect = GridMath.GridRectToScreen(_layout, box);
            var shortSide = Math.Min(rect.Width, rect.Height);
            var scale = _settings.TextScale;
            var titleSize = _settings.ShowFilePath
                ? Math.Min(18 * scale, Math.Max(9 * scale, shortSide * 0.2 * scale))
                : Math.Min(28 * scale, Math.Max(11 * scale, shortSide * 0.24 * scale));

            var border = new Border
            {
                Width = rect.Width,
                Height = rect.Height,
                Background = ColorHelper.Brush(box.Color),
                BorderBrush = box.Id == _selectedBoxId ? Brushes.DeepSkyBlue : new SolidColorBrush(Color.FromArgb(115, 255, 255, 255)),
                BorderThickness = box.Id == _linkFromId ? new Thickness(2) : new Thickness(1),
                Tag = box.Id,
                AllowDrop = true
            };

            if (box.Id == _linkFromId)
                border.BorderBrush = Brushes.Gold;

            var stack = new StackPanel();
            var title = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(box.Title) ? "Untitled" : box.Title,
                FontWeight = FontWeights.SemiBold,
                FontSize = titleSize,
                Foreground = ColorHelper.Brush(_settings.FontColor),
                FontFamily = FontHelper.Safe(_settings.FontFamily),
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(6, 4, 6, 4),
                Background = _settings.ShowFilePath ? new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)) : Brushes.Transparent,
                TextWrapping = _settings.ShowFilePath ? TextWrapping.NoWrap : TextWrapping.Wrap,
                VerticalAlignment = _settings.ShowFilePath ? VerticalAlignment.Top : VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                TextAlignment = _settings.ShowFilePath ? TextAlignment.Left : TextAlignment.Center
            };
            stack.Children.Add(title);
            if (_settings.ShowFilePath)
            {
                stack.Children.Add(new TextBlock
                {
                    Text = box.FolderPath,
                    FontSize = Math.Max(8, titleSize * 0.8),
                    Foreground = new SolidColorBrush(Color.FromArgb(220, 255, 255, 255)),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Margin = new Thickness(6, 0, 6, 4),
                    ToolTip = box.FolderPath
                });
            }

            var handle = new Rectangle
            {
                Width = 12,
                Height = 12,
                Fill = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 2, 2)
            };
            var handleHost = new Grid();
            handleHost.Children.Add(stack);
            handleHost.Children.Add(handle);
            border.Child = handleHost;

            Canvas.SetLeft(border, rect.X);
            Canvas.SetTop(border, rect.Y);
            BoxCanvas.Children.Add(border);

            border.MouseDown += (s, e) =>
            {
                if (e.ChangedButton != MouseButton.Left) return;
                var id = (string)((Border)s!).Tag;
                if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0)
                {
                    TryOpenFolder(box.FolderPath);
                    e.Handled = true;
                    return;
                }
                if (_linking)
                {
                    LinkBoxPicked?.Invoke(id);
                    e.Handled = true;
                    return;
                }
                BoxSelected?.Invoke(id);
                if (e.ClickCount == 1)
                {
                    _dragBoxId = id;
                    var (gx, gy) = GridMath.ScreenToGrid(_layout, e.GetPosition(this).X, e.GetPosition(this).Y);
                    _dragStartGx = box.Gx;
                    _dragStartGy = box.Gy;
                    _pointerGx = gx;
                    _pointerGy = gy;
                    border.CaptureMouse();
                }
                e.Handled = true;
            };

            border.Drop += (_, e) =>
            {
                if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths) return;
                FilesDroppedOnBox?.Invoke(box.Id, paths);
                e.Handled = true;
            };

            handle.MouseDown += (_, e) =>
            {
                _resizeBoxId = box.Id;
                var (gx, gy) = GridMath.ScreenToGrid(_layout, e.GetPosition(this).X, e.GetPosition(this).Y);
                _resizeStartGw = box.Gw;
                _resizeStartGh = box.Gh;
                _pointerGx = gx;
                _pointerGy = gy;
                handle.CaptureMouse();
                e.Handled = true;
            };
        }
    }

    private static void TryOpenFolder(string path)
    {
        try { System.Diagnostics.Process.Start("explorer.exe", path); } catch { /* ignore */ }
    }

    private void OnWheel(object sender, MouseWheelEventArgs e)
    {
        var pos = e.GetPosition(this);
        var factor = e.Delta > 0 ? 1.1 : 0.9;
        var newZoom = Math.Clamp(_layout.View.Zoom * factor, 0.25, 3);
        var wx = (pos.X - _layout.View.PanX) / _layout.View.Zoom;
        var wy = (pos.Y - _layout.View.PanY) / _layout.View.Zoom;
        _layout.View.Zoom = newZoom;
        _layout.View.PanX = Math.Round(pos.X - wx * newZoom);
        _layout.View.PanY = Math.Round(pos.Y - wy * newZoom);
        LayoutChanged?.Invoke(_layout);
        Redraw();
        e.Handled = true;
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Middle || e.ChangedButton == MouseButton.Right ||
            (e.ChangedButton == MouseButton.Left && (Keyboard.Modifiers & ModifierKeys.Alt) != 0))
        {
            _panning = true;
            _panStart = e.GetPosition(this);
            _panStartX = _layout.View.PanX;
            _panStartY = _layout.View.PanY;
            CaptureMouse();
            e.Handled = true;
            return;
        }
        if (e.ChangedButton == MouseButton.Left && e.OriginalSource == this)
        {
            BoxSelected?.Invoke(null);
            LinkSelected?.Invoke(null);
        }
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_panning)
        {
            var p = e.GetPosition(this);
            _layout.View.PanX = Math.Round(_panStartX + (p.X - _panStart.X));
            _layout.View.PanY = Math.Round(_panStartY + (p.Y - _panStart.Y));
            LayoutChanged?.Invoke(_layout);
            Redraw();
            return;
        }

        var (gx, gy) = GridMath.ScreenToGrid(_layout, e.GetPosition(this).X, e.GetPosition(this).Y);
        if (_dragBoxId != null && _layout.Boxes.FirstOrDefault(b => b.Id == _dragBoxId) is { } dragBox)
        {
            var box = dragBox;
            box.Gx = _dragStartGx + (gx - _pointerGx);
            box.Gy = _dragStartGy + (gy - _pointerGy);
            LayoutChanged?.Invoke(_layout);
            Redraw();
        }
        if (_resizeBoxId != null && _layout.Boxes.FirstOrDefault(b => b.Id == _resizeBoxId) is { } resizeBox)
        {
            var box = resizeBox;
            box.Gw = Math.Max(1, _resizeStartGw + (gx - _pointerGx));
            box.Gh = Math.Max(1, _resizeStartGh + (gy - _pointerGy));
            LayoutChanged?.Invoke(_layout);
            Redraw();
        }
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        _panning = false;
        _dragBoxId = null;
        _resizeBoxId = null;
        if (IsMouseCaptured) ReleaseMouseCapture();
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        // Drops on empty canvas ignored; box handles drops
        e.Handled = true;
    }
}
