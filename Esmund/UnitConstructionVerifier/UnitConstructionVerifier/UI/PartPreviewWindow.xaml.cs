using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using UnitConstructionVerifier.Operations;

namespace UnitConstructionVerifier.UI
{
    public partial class PartPreviewWindow : Window
    {
        private WireframeData? _wireframe;
        private double _yaw;
        private double _pitch;
        private double _zoom = 1.0;
        private double _panX;
        private double _panY;
        private bool _isDragging;
        private bool _isPanning;
        private Point _lastMouse;
        private byte _lineColorR = XRayHighlightColors.Default.R;
        private byte _lineColorG = XRayHighlightColors.Default.G;
        private byte _lineColorB = XRayHighlightColors.Default.B;

        /// <summary>
        /// True after the user interacts with the preview while it is in front of Inventor.
        /// </summary>
        private bool _keepAboveInventor;

        private bool _pendingStackRestore;

        private bool _pendingFocusRestore;

        public bool KeepAboveInventor => _keepAboveInventor;

        public PartPreviewWindow()
        {
            InitializeComponent();
            InitializeColorPicker();
            Loaded += (_, __) => Redraw();
            SizeChanged += (_, __) => Redraw();
            Activated += (_, __) => _keepAboveInventor = true;
            Deactivated += (_, __) => { /* keep until explicitly dismissed */ };
        }

        public void MarkKeepAboveInventor()
        {
            _keepAboveInventor = true;
        }

        public void ClearKeepAboveInventor()
        {
            _keepAboveInventor = false;
        }

        public void CaptureStackState()
        {
            InventorWindowStackHelper.Capture(
                this,
                ref _keepAboveInventor,
                ref _pendingStackRestore,
                ref _pendingFocusRestore);
        }

        public bool TakePendingStackRestore(out bool restoreFocus)
        {
            return InventorWindowStackHelper.TakePending(
                ref _pendingStackRestore,
                ref _pendingFocusRestore,
                out restoreFocus);
        }

        public void RestoreStackAboveInventor(bool restoreFocus)
        {
            InventorWindowStackHelper.RestoreAboveInventor(this, restoreFocus);
        }

        public void ScheduleStackRestore(bool restoreFocus)
        {
            InventorWindowStackHelper.ScheduleRestore(this, restoreFocus);
        }

        public void ShowWireframe(string title, WireframeData? wireframe, bool resetView)
        {
            TitleLabel.Text = title;
            _wireframe = wireframe;

            if (resetView)
            {
                _yaw = 0.45;
                _pitch = 0.35;
                _zoom = 1.0;
                _panX = 0;
                _panY = 0;
            }

            Redraw();
        }

        private void InitializeColorPicker()
        {
            if (PreviewWireframeColorSettings.TryLoad(out byte r, out byte g, out byte b))
            {
                _lineColorR = r;
                _lineColorG = g;
                _lineColorB = b;
            }

            UpdateColorSwatch();

            foreach (XRayHighlightColor option in XRayHighlightColors.Options)
            {
                XRayHighlightColor captured = option;
                var row = new Button
                {
                    Height = 26,
                    Margin = new Thickness(0, 0, 0, 2),
                    Padding = new Thickness(6, 0, 8, 0),
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    Background = new SolidColorBrush(Color.FromRgb(captured.R, captured.G, captured.B)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(68, 68, 90)),
                    BorderThickness = new Thickness(1),
                    Cursor = Cursors.Hand,
                    Tag = captured
                };

                var content = new StackPanel { Orientation = Orientation.Horizontal };
                content.Children.Add(new Border
                {
                    Width = 14,
                    Height = 14,
                    Margin = new Thickness(0, 0, 8, 0),
                    Background = row.Background,
                    BorderBrush = new SolidColorBrush(Color.FromRgb(68, 68, 90)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(2)
                });
                content.Children.Add(new TextBlock
                {
                    Text = captured.Name,
                    Foreground = GetContrastForeground(captured.R, captured.G, captured.B),
                    FontSize = 12,
                    VerticalAlignment = VerticalAlignment.Center
                });
                row.Content = content;
                row.Click += OnPreviewColorOptionClick;
                PreviewColorListPanel.Children.Add(row);
            }
        }

        private void OnPreviewColorButtonClick(object sender, RoutedEventArgs e)
        {
            PreviewColorPopup.IsOpen = !PreviewColorPopup.IsOpen;
        }

        private void OnPreviewColorOptionClick(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not XRayHighlightColor color)
            {
                return;
            }

            _lineColorR = color.R;
            _lineColorG = color.G;
            _lineColorB = color.B;
            PreviewWireframeColorSettings.Save(color.R, color.G, color.B);
            UpdateColorSwatch();
            PreviewColorPopup.IsOpen = false;
            Redraw();
        }

        private void UpdateColorSwatch()
        {
            PreviewColorButton.Background = new SolidColorBrush(Color.FromRgb(_lineColorR, _lineColorG, _lineColorB));
        }

        private static Brush GetContrastForeground(byte r, byte g, byte b)
        {
            double luminance = (0.299 * r + 0.587 * g + 0.114 * b) / 255.0;
            return luminance > 0.62
                ? new SolidColorBrush(Color.FromRgb(30, 30, 46))
                : new SolidColorBrush(Colors.White);
        }

        private void Redraw()
        {
            WireCanvas.Children.Clear();
            if (_wireframe == null || _wireframe.Segments.Count == 0)
            {
                return;
            }

            double width = WireCanvas.ActualWidth;
            double height = WireCanvas.ActualHeight;
            if (width < 10 || height < 10)
            {
                return;
            }

            double baseScale = Math.Min(width, height) * 0.42 / Math.Max(_wireframe.Radius, 0.001);
            double scale = baseScale * _zoom;
            double centerScreenX = width * 0.5 + _panX;
            double centerScreenY = height * 0.5 + _panY;

            var lineBrush = new SolidColorBrush(Color.FromRgb(_lineColorR, _lineColorG, _lineColorB));

            foreach ((double x1, double y1, double z1, double x2, double y2, double z2) in _wireframe.Segments)
            {
                ProjectSegment(
                    x1 - _wireframe.CenterX,
                    y1 - _wireframe.CenterY,
                    z1 - _wireframe.CenterZ,
                    x2 - _wireframe.CenterX,
                    y2 - _wireframe.CenterY,
                    z2 - _wireframe.CenterZ,
                    scale,
                    centerScreenX,
                    centerScreenY,
                    out double sx1,
                    out double sy1,
                    out double sx2,
                    out double sy2);

                WireCanvas.Children.Add(new Line
                {
                    X1 = sx1,
                    Y1 = sy1,
                    X2 = sx2,
                    Y2 = sy2,
                    Stroke = lineBrush,
                    StrokeThickness = 1
                });
            }
        }

        private void ProjectSegment(
            double x1,
            double y1,
            double z1,
            double x2,
            double y2,
            double z2,
            double scale,
            double centerScreenX,
            double centerScreenY,
            out double sx1,
            out double sy1,
            out double sx2,
            out double sy2)
        {
            Rotate(ref x1, ref y1, ref z1);
            Rotate(ref x2, ref y2, ref z2);

            sx1 = centerScreenX + x1 * scale;
            sy1 = centerScreenY - y1 * scale;
            sx2 = centerScreenX + x2 * scale;
            sy2 = centerScreenY - y2 * scale;
        }

        private void Rotate(ref double x, ref double y, ref double z)
        {
            double cosY = Math.Cos(_yaw);
            double sinY = Math.Sin(_yaw);
            double nx = x * cosY + z * sinY;
            double nz = -x * sinY + z * cosY;
            x = nx;
            z = nz;

            double cosX = Math.Cos(_pitch);
            double sinX = Math.Sin(_pitch);
            double ny = y * cosX - z * sinX;
            nz = y * sinX + z * cosX;
            y = ny;
            z = nz;
        }

        private void OnCanvasMouseDown(object sender, MouseButtonEventArgs e)
        {
            MarkKeepAboveInventor();
            WireCanvas.Focus();
            _lastMouse = e.GetPosition(WireCanvas);

            if (e.ChangedButton == MouseButton.Middle)
            {
                _isPanning = true;
            }
            else if (e.ChangedButton == MouseButton.Left)
            {
                _isDragging = true;
            }
            else
            {
                return;
            }

            WireCanvas.CaptureMouse();
        }

        private void OnCanvasMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDragging && !_isPanning)
            {
                return;
            }

            Point current = e.GetPosition(WireCanvas);
            double dx = current.X - _lastMouse.X;
            double dy = current.Y - _lastMouse.Y;
            _lastMouse = current;

            if (_isPanning)
            {
                _panX += dx;
                _panY += dy;
            }
            else
            {
                _yaw += dx * 0.01;
                _pitch += dy * 0.01;
                _pitch = Math.Max(-1.4, Math.Min(1.4, _pitch));
            }

            Redraw();
        }

        private void OnCanvasMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle)
            {
                _isPanning = false;
            }
            else if (e.ChangedButton == MouseButton.Left)
            {
                _isDragging = false;
            }

            if (!_isDragging && !_isPanning)
            {
                WireCanvas.ReleaseMouseCapture();
            }
        }

        private void OnCanvasMouseWheel(object sender, MouseWheelEventArgs e)
        {
            MarkKeepAboveInventor();
            double factor = e.Delta > 0 ? 1.1 : 0.9;
            _zoom = Math.Max(0.2, Math.Min(8.0, _zoom * factor));
            Redraw();
        }
    }
}
