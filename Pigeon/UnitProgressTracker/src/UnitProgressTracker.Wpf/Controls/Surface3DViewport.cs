using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf;
using UnitProgressTracker.Core.Models;

namespace UnitProgressTracker.Wpf.Controls;

public class Surface3DViewport : Grid
{
    private readonly HelixViewport3D _helixViewport;

    // Per-surface visual groups keyed by SurfaceNumber
    private readonly Dictionary<string, ModelVisual3D> _surfaceVisuals = new(StringComparer.OrdinalIgnoreCase);

    // Per-surface wireframe container groups — added/removed from parent to toggle visibility
    private readonly Dictionary<string, ModelVisual3D> _wireframeGroups = new(StringComparer.OrdinalIgnoreCase);

    // Track current highlight so we can restore the previous surface
    private string? _highlightedSurfaceNumber;

    // State mirrors
    private bool _wireframeVisible = true;
    private double _globalOpacity = 1.0;

    public event Action<string>? SurfacePicked;

    public Surface3DViewport()
    {
        _helixViewport = new HelixViewport3D
        {
            ShowFrameRate = false,
            ShowCoordinateSystem = true,
            ShowViewCube = true,
            ViewCubeHorizontalPosition = HorizontalAlignment.Right,
            ViewCubeVerticalPosition = VerticalAlignment.Bottom,
            ZoomExtentsWhenLoaded = true,
            IsInertiaEnabled = true,
            Background = new SolidColorBrush(Color.FromRgb(11, 15, 25))
        };

        _helixViewport.Children.Add(new DefaultLights());
        Children.Add(_helixViewport);
        _helixViewport.MouseDown += OnViewportMouseDown;
    }

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    public void LoadSurfaces(IEnumerable<SurfaceModel> surfaces, Func<string, string> getStatusColorHex)
    {
        ClearSurfaces();
        _highlightedSurfaceNumber = null;

        Rect3D totalBounds = Rect3D.Empty;

        foreach (var surface in surfaces)
        {
            if (surface.IsHidden || surface.Boxes.Count == 0) continue;

            var surfaceGroupVisual = new ModelVisual3D();
            var wireframeGroup = new ModelVisual3D();

            string colorHex = getStatusColorHex(surface.StateId);
            Color baseColor = ParseHexColor(colorHex);
            var brush = new SolidColorBrush(baseColor) { Opacity = _globalOpacity };
            var material = MaterialHelper.CreateMaterial(brush, specularPower: 15);

            foreach (var box in surface.Boxes)
            {
                var boxVisual = new BoxVisual3D
                {
                    Center = new Point3D(box.X + box.XLength / 2.0, box.Y + box.YLength / 2.0, box.Z + box.ZLength / 2.0),
                    Length = box.XLength,
                    Width = box.ZLength,
                    Height = box.YLength,
                    Material = material,
                    BackMaterial = material
                };
                boxVisual.SetValue(SurfaceNumberProperty, surface.SurfaceNumber);
                surfaceGroupVisual.Children.Add(boxVisual);

                var wireframe = new BoundingBoxVisual3D
                {
                    BoundingBox = new Rect3D(box.X, box.Y, box.Z, box.XLength, box.YLength, box.ZLength),
                    Fill = new SolidColorBrush(Color.FromRgb(50, 70, 90)),
                    Diameter = 0.5
                };
                wireframeGroup.Children.Add(wireframe);

                var boxBounds = new Rect3D(box.X, box.Y, box.Z, box.XLength, box.YLength, box.ZLength);
                totalBounds.Union(boxBounds);
            }

            // Billboard label
            var bounds = GetSurfaceBounds(surface.Boxes);
            if (!bounds.IsEmpty)
            {
                var labelSticker = new BillboardTextVisual3D
                {
                    Text = surface.ShortLabel,
                    Position = new Point3D(bounds.X + bounds.SizeX / 2.0, bounds.Y + bounds.SizeY + 2.0, bounds.Z + bounds.SizeZ / 2.0),
                    Foreground = Brushes.White,
                    Background = new SolidColorBrush(Color.FromArgb(200, 15, 23, 42)),
                    Padding = new Thickness(4),
                    FontSize = 12,
                    FontWeight = FontWeights.Bold
                };
                surfaceGroupVisual.Children.Add(labelSticker);
            }

            _helixViewport.Children.Add(surfaceGroupVisual);
            if (_wireframeVisible) _helixViewport.Children.Add(wireframeGroup);
            _surfaceVisuals[surface.SurfaceNumber] = surfaceGroupVisual;
            _wireframeGroups[surface.SurfaceNumber] = wireframeGroup;
        }

        if (!totalBounds.IsEmpty)
            _helixViewport.ZoomExtents(totalBounds, 500);
    }

    /// <summary>Highlight a surface in the viewport by applying a bright selection outline.</summary>
    public void HighlightSurface(string surfaceNumber)
    {
        // Restore previous highlight
        if (_highlightedSurfaceNumber != null && _surfaceVisuals.TryGetValue(_highlightedSurfaceNumber, out var prev))
        {
            SetGroupOpacity(prev, _globalOpacity);
        }

        _highlightedSurfaceNumber = surfaceNumber;

        if (_surfaceVisuals.TryGetValue(surfaceNumber, out var group))
        {
            // Fully opaque + brighter by setting opacity to 1.0 and boosting scale doesn't work in 3D simply;
            // instead we pulse the surface to full opacity and dim all others slightly.
            foreach (var kv in _surfaceVisuals)
            {
                SetGroupOpacity(kv.Value, kv.Key.Equals(surfaceNumber, StringComparison.OrdinalIgnoreCase)
                    ? 1.0
                    : Math.Max(0.25, _globalOpacity * 0.4));
            }
        }
    }

    /// <summary>Show or hide all wireframe bounding box overlays.</summary>
    public void SetWireframeVisible(bool visible)
    {
        _wireframeVisible = visible;
        foreach (var kv in _wireframeGroups)
        {
            if (visible)
            {
                if (!_helixViewport.Children.Contains(kv.Value))
                    _helixViewport.Children.Add(kv.Value);
            }
            else
            {
                _helixViewport.Children.Remove(kv.Value);
            }
        }
    }

    /// <summary>Apply a global opacity to all surface box materials.</summary>
    public void SetGlobalOpacity(double opacity)
    {
        _globalOpacity = Math.Clamp(opacity, 0.1, 1.0);
        foreach (var kv in _surfaceVisuals)
        {
            bool isHighlighted = kv.Key.Equals(_highlightedSurfaceNumber, StringComparison.OrdinalIgnoreCase);
            SetGroupOpacity(kv.Value, isHighlighted ? 1.0 : _globalOpacity);
        }
    }

    /// <summary>Toggle hidden state (remove/restore visual) for a single surface.</summary>
    public void SetSurfaceVisibility(bool isHidden, string surfaceNumber)
    {
        if (_surfaceVisuals.TryGetValue(surfaceNumber, out var visual))
        {
            visual.SetValue(UIElement.VisibilityProperty, isHidden ? Visibility.Collapsed : Visibility.Visible);
        }
    }

    public void ClearSurfaces()
    {
        foreach (var visual in _surfaceVisuals.Values)
            _helixViewport.Children.Remove(visual);
        foreach (var wg in _wireframeGroups.Values)
            _helixViewport.Children.Remove(wg);
        _surfaceVisuals.Clear();
        _wireframeGroups.Clear();
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static void SetGroupOpacity(ModelVisual3D group, double opacity)
    {
        foreach (var child in group.Children)
        {
            if (child is BoxVisual3D box)
            {
                if (box.Material is DiffuseMaterial dm && dm.Brush is SolidColorBrush b)
                    b.Opacity = opacity;
            }
        }
    }

    private static Rect3D GetSurfaceBounds(List<GeometryBox> boxes)
    {
        Rect3D bounds = Rect3D.Empty;
        foreach (var box in boxes)
            bounds.Union(new Rect3D(box.X, box.Y, box.Z, box.XLength, box.YLength, box.ZLength));
        return bounds;
    }

    private static Color ParseHexColor(string hex)
    {
        try
        {
            hex = hex.TrimStart('#');
            if (hex.Length == 6)
            {
                byte r = Convert.ToByte(hex.Substring(0, 2), 16);
                byte g = Convert.ToByte(hex.Substring(2, 2), 16);
                byte b = Convert.ToByte(hex.Substring(4, 2), 16);
                return Color.FromRgb(r, g, b);
            }
        }
        catch { }
        return Color.FromRgb(148, 163, 184);
    }

    private void OnViewportMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            Point mousePos = e.GetPosition(_helixViewport);
            var hitResult = VisualTreeHelper.HitTest(_helixViewport, mousePos) as RayMeshGeometry3DHitTestResult;

            if (hitResult?.ModelHit is GeometryModel3D hitModel)
            {
                string? surfaceNum = hitModel.GetValue(SurfaceNumberProperty) as string;
                if (!string.IsNullOrEmpty(surfaceNum))
                    SurfacePicked?.Invoke(surfaceNum);
            }
        }
    }

    public static readonly DependencyProperty SurfaceNumberProperty =
        DependencyProperty.RegisterAttached("SurfaceNumber", typeof(string), typeof(Surface3DViewport), new PropertyMetadata(null));
}
