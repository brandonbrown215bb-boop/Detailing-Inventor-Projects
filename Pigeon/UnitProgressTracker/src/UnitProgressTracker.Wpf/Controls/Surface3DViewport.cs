using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf;
using UnitProgressTracker.Core.Models;

namespace UnitProgressTracker.Wpf.Controls;

public class Surface3DViewport : Grid
{
    private readonly HelixViewport3D _helixViewport;

    // Per-surface visual groups keyed by SurfaceNumber
    private readonly Dictionary<string, ModelVisual3D> _surfaceVisuals = new(StringComparer.OrdinalIgnoreCase);

    // Per-surface wireframe container groups
    private readonly Dictionary<string, ModelVisual3D> _wireframeGroups = new(StringComparer.OrdinalIgnoreCase);

    // Skid floor grid markers
    private readonly List<Visual3D> _skidFloorVisuals = new();

    // Map surface numbers to original models for hover lookup
    private readonly Dictionary<string, SurfaceModel> _surfaceModels = new(StringComparer.OrdinalIgnoreCase);

    // Track current highlight
    private string? _highlightedSurfaceNumber;

    // State mirrors
    private bool _wireframeVisible = true;
    private bool _showSkidGrid = true;
    private double _globalOpacity = 1.0;
    private StickerOptions _stickerOptions = new();

    public event Action<string>? SurfacePicked;
    public event Action<SurfaceModel?, Point>? SurfaceHovered;

    public Surface3DViewport()
    {
        _helixViewport = new HelixViewport3D
        {
            ShowFrameRate = false,
            ShowCoordinateSystem = true,
            ShowViewCube = true,
            ViewCubeHorizontalPosition = HorizontalAlignment.Right,
            ViewCubeVerticalPosition = VerticalAlignment.Bottom,
            ModelUpDirection = new Vector3D(0, 1, 0),
            ZoomExtentsWhenLoaded = true,
            IsInertiaEnabled = true,
            Background = new SolidColorBrush(Color.FromRgb(11, 15, 25))
        };

        _helixViewport.Children.Add(new DefaultLights());
        Children.Add(_helixViewport);
        _helixViewport.MouseDown += OnViewportMouseDown;
        _helixViewport.MouseMove += OnViewportMouseMove;
    }

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    public void SetStickerOptions(StickerOptions options)
    {
        if (options != null) _stickerOptions = options;
    }

    public void LoadSurfaces(IEnumerable<SurfaceModel> surfaces, Func<string, string> getStatusColorHex)
    {
        ClearSurfaces();
        _highlightedSurfaceNumber = null;

        Rect3D totalBounds = Rect3D.Empty;
        var surfacesBySkid = new Dictionary<int, List<SurfaceModel>>();

        foreach (var surface in surfaces)
        {
            _surfaceModels[surface.SurfaceNumber] = surface;

            if (surface.IsHidden || surface.Boxes.Count == 0) continue;

            if (!surfacesBySkid.ContainsKey(surface.SkidId))
                surfacesBySkid[surface.SkidId] = new List<SurfaceModel>();
            surfacesBySkid[surface.SkidId].Add(surface);

            var surfaceGroupVisual = new ModelVisual3D();
            surfaceGroupVisual.SetValue(SurfaceNumberProperty, surface.SurfaceNumber);

            var wireframeGroup = new ModelVisual3D();
            wireframeGroup.SetValue(SurfaceNumberProperty, surface.SurfaceNumber);

            string colorHex = getStatusColorHex(surface.StateId);
            Color baseColor = ParseHexColor(colorHex);

            foreach (var box in surface.Boxes)
            {
                var boxVisual = new BoxVisual3D
                {
                    Center = new Point3D(box.X + box.XLength / 2.0, box.Y + box.YLength / 2.0, box.Z + box.ZLength / 2.0),
                    Length = box.XLength,
                    Width = box.YLength,
                    Height = box.ZLength,
                };
                ApplyBoxMaterial(boxVisual, baseColor, _globalOpacity);

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

            // On-surface 3D sticker placement (depth-tested 3D mesh planes on outer faces)
            var bounds = GetSurfaceBounds(surface.Boxes);
            if (!bounds.IsEmpty)
            {
                AddOnSurfaceStickers(surfaceGroupVisual, surface.ShortLabel, bounds, _stickerOptions);
            }

            _helixViewport.Children.Add(surfaceGroupVisual);
            if (_wireframeVisible) _helixViewport.Children.Add(wireframeGroup);
            _surfaceVisuals[surface.SurfaceNumber] = surfaceGroupVisual;
            _wireframeGroups[surface.SurfaceNumber] = wireframeGroup;
        }

        // Build Skid Ground Footprint Grid Markers & 3D Skid Labels
        if (!totalBounds.IsEmpty)
        {
            double minY = totalBounds.Y;

            foreach (var kv in surfacesBySkid.OrderBy(k => k.Key))
            {
                int skidId = kv.Key;
                Rect3D skidBounds = Rect3D.Empty;
                foreach (var s in kv.Value)
                {
                    foreach (var b in s.Boxes)
                        skidBounds.Union(new Rect3D(b.X, b.Y, b.Z, b.XLength, b.YLength, b.ZLength));
                }

                if (skidBounds.IsEmpty) continue;

                // Skid Floor Grid Bounding Frame
                var floorFrame = new BoundingBoxVisual3D
                {
                    BoundingBox = new Rect3D(skidBounds.X - 5, minY - 1, skidBounds.Z - 5, skidBounds.SizeX + 10, 1, skidBounds.SizeZ + 10),
                    Fill = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
                    Diameter = 1.2
                };
                _skidFloorVisuals.Add(floorFrame);

                // 3D Skid Ground Label Sticker
                var skidLabel = new BillboardTextVisual3D
                {
                    Text = $"[ Skid {skidId} ]",
                    Position = new Point3D(skidBounds.X + skidBounds.SizeX / 2.0, minY - 2, skidBounds.Z - 8),
                    Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
                    Background = new SolidColorBrush(Color.FromArgb(240, 15, 23, 42)),
                    Padding = new Thickness(6),
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    DepthOffset = 0.1
                };
                _skidFloorVisuals.Add(skidLabel);

                if (_showSkidGrid)
                {
                    _helixViewport.Children.Add(floorFrame);
                    _helixViewport.Children.Add(skidLabel);
                }
            }

            _helixViewport.ModelUpDirection = new Vector3D(0, 1, 0);
            if (_helixViewport.Camera is ProjectionCamera camera)
            {
                camera.UpDirection = new Vector3D(0, 1, 0);
            }
            _helixViewport.ZoomExtents(totalBounds, 500);
        }
    }

    public void HighlightSurface(string surfaceNumber)
    {
        _highlightedSurfaceNumber = surfaceNumber;

        foreach (var kv in _surfaceVisuals)
        {
            bool isHighlighted = kv.Key.Equals(surfaceNumber, StringComparison.OrdinalIgnoreCase);
            double targetOpacity = isHighlighted ? 1.0 : Math.Max(0.2, _globalOpacity * 0.4);
            SetGroupOpacity(kv.Value, targetOpacity);
        }
    }

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

    public void SetShowSkidGrid(bool visible)
    {
        _showSkidGrid = visible;
        foreach (var v in _skidFloorVisuals)
        {
            if (visible)
            {
                if (!_helixViewport.Children.Contains(v))
                    _helixViewport.Children.Add(v);
            }
            else
            {
                _helixViewport.Children.Remove(v);
            }
        }
    }

    public void SetGlobalOpacity(double opacity)
    {
        _globalOpacity = Math.Clamp(opacity, 0.1, 1.0);
        foreach (var kv in _surfaceVisuals)
        {
            bool isHighlighted = kv.Key.Equals(_highlightedSurfaceNumber, StringComparison.OrdinalIgnoreCase);
            double targetOpacity = isHighlighted ? 1.0 : _globalOpacity;
            SetGroupOpacity(kv.Value, targetOpacity);
        }
    }

    public void SetSurfaceVisibility(bool isHidden, string surfaceNumber)
    {
        if (_surfaceVisuals.TryGetValue(surfaceNumber, out var visual))
        {
            if (isHidden)
            {
                if (_helixViewport.Children.Contains(visual))
                    _helixViewport.Children.Remove(visual);
            }
            else
            {
                if (!_helixViewport.Children.Contains(visual))
                    _helixViewport.Children.Add(visual);
            }
        }

        if (_wireframeGroups.TryGetValue(surfaceNumber, out var wireframeGroup))
        {
            if (isHidden)
            {
                if (_helixViewport.Children.Contains(wireframeGroup))
                    _helixViewport.Children.Remove(wireframeGroup);
            }
            else
            {
                if (_wireframeVisible && !_helixViewport.Children.Contains(wireframeGroup))
                    _helixViewport.Children.Add(wireframeGroup);
            }
        }
    }

    public void ClearSurfaces()
    {
        foreach (var visual in _surfaceVisuals.Values)
            _helixViewport.Children.Remove(visual);
        foreach (var wg in _wireframeGroups.Values)
            _helixViewport.Children.Remove(wg);
        foreach (var sfv in _skidFloorVisuals)
            _helixViewport.Children.Remove(sfv);

        _surfaceVisuals.Clear();
        _wireframeGroups.Clear();
        _skidFloorVisuals.Clear();
        _surfaceModels.Clear();
        _highlightedSurfaceNumber = null;
    }

    // -----------------------------------------------------------------------
    // On-Surface 3D Label Mesh Placement (Depth-Tested, Standard 3D Occlusion)
    // -----------------------------------------------------------------------

    private static Material CreateStickerMaterial(string text, string fontFamily, string textColorHex, string bgHex, string borderHex)
    {
        int px = 256;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            Color bgColor = ParseHexColor(bgHex);
            Color borderCol = ParseHexColor(borderHex);
            Color textCol = ParseHexColor(textColorHex);

            var bgBrush = new SolidColorBrush(bgColor) { Opacity = 0.94 };
            var borderPen = new Pen(new SolidColorBrush(borderCol), 6);

            dc.DrawRoundedRectangle(bgBrush, borderPen, new Rect(12, 12, px - 24, px - 24), 16, 16);

            var textBrush = new SolidColorBrush(textCol);
            var formattedText = new FormattedText(
                text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily(fontFamily), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                72,
                textBrush,
                96.0)
            {
                TextAlignment = TextAlignment.Center
            };

            dc.DrawText(formattedText, new Point(px / 2.0, (px - formattedText.Height) / 2.0));
        }

        var rtb = new RenderTargetBitmap(px, px, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(visual);
        rtb.Freeze();

        var imageBrush = new ImageBrush(rtb);
        return MaterialHelper.CreateMaterial(imageBrush);
    }

    private static void AddOnSurfaceStickers(ModelVisual3D group, string labelText, Rect3D bounds, StickerOptions options)
    {
        if (bounds.IsEmpty) return;

        var stickerMaterial = CreateStickerMaterial(
            labelText,
            options.FontFamily,
            options.TextColorHex,
            options.BackgroundColorHex,
            options.BorderColorHex);

        double lenX = bounds.SizeX;
        double lenY = bounds.SizeY;
        double lenZ = bounds.SizeZ;

        Point3D center = new Point3D(bounds.X + lenX / 2.0, bounds.Y + lenY / 2.0, bounds.Z + lenZ / 2.0);

        double areaX = lenY * lenZ;
        double areaY = lenX * lenZ;
        double areaZ = lenX * lenY;
        double offset = 0.05;

        if (areaX >= areaY && areaX >= areaZ)
        {
            double size = Math.Min(Math.Max(Math.Min(lenY, lenZ) * 0.4, 4), 14);

            var posSticker = new RectangleVisual3D
            {
                Origin = new Point3D(center.X + lenX / 2.0 + offset, center.Y, center.Z),
                Normal = new Vector3D(1, 0, 0),
                LengthDirection = new Vector3D(0, 0, 1),
                Width = size,
                Length = size,
                Material = stickerMaterial
            };
            group.Children.Add(posSticker);

            var negSticker = new RectangleVisual3D
            {
                Origin = new Point3D(center.X - lenX / 2.0 - offset, center.Y, center.Z),
                Normal = new Vector3D(-1, 0, 0),
                LengthDirection = new Vector3D(0, 0, -1),
                Width = size,
                Length = size,
                Material = stickerMaterial
            };
            group.Children.Add(negSticker);
        }
        else if (areaY >= areaX && areaY >= areaZ)
        {
            double size = Math.Min(Math.Max(Math.Min(lenX, lenZ) * 0.4, 4), 14);

            var posSticker = new RectangleVisual3D
            {
                Origin = new Point3D(center.X, center.Y + lenY / 2.0 + offset, center.Z),
                Normal = new Vector3D(0, 1, 0),
                LengthDirection = new Vector3D(1, 0, 0),
                Width = size,
                Length = size,
                Material = stickerMaterial
            };
            group.Children.Add(posSticker);

            var negSticker = new RectangleVisual3D
            {
                Origin = new Point3D(center.X, center.Y - lenY / 2.0 - offset, center.Z),
                Normal = new Vector3D(0, -1, 0),
                LengthDirection = new Vector3D(1, 0, 0),
                Width = size,
                Length = size,
                Material = stickerMaterial
            };
            group.Children.Add(negSticker);
        }
        else
        {
            double size = Math.Min(Math.Max(Math.Min(lenX, lenY) * 0.4, 4), 14);

            var posSticker = new RectangleVisual3D
            {
                Origin = new Point3D(center.X, center.Y, center.Z + lenZ / 2.0 + offset),
                Normal = new Vector3D(0, 0, 1),
                LengthDirection = new Vector3D(1, 0, 0),
                Width = size,
                Length = size,
                Material = stickerMaterial
            };
            group.Children.Add(posSticker);

            var negSticker = new RectangleVisual3D
            {
                Origin = new Point3D(center.X, center.Y, center.Z - lenZ / 2.0 - offset),
                Normal = new Vector3D(0, 0, -1),
                LengthDirection = new Vector3D(-1, 0, 0),
                Width = size,
                Length = size,
                Material = stickerMaterial
            };
            group.Children.Add(negSticker);
        }
    }

    // -----------------------------------------------------------------------
    // Mouse Interaction
    // -----------------------------------------------------------------------

    private void OnViewportMouseMove(object sender, MouseEventArgs e)
    {
        try
        {
            Point mousePos = e.GetPosition(_helixViewport);
            string? foundSurfaceNum = HitTestSurface(mousePos);

            if (!string.IsNullOrEmpty(foundSurfaceNum) && _surfaceModels.TryGetValue(foundSurfaceNum, out var surface))
            {
                SurfaceHovered?.Invoke(surface, mousePos);
            }
            else
            {
                SurfaceHovered?.Invoke(null, default);
            }
        }
        catch
        {
            SurfaceHovered?.Invoke(null, default);
        }
    }

    private void OnViewportMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            Point mousePos = e.GetPosition(_helixViewport);
            string? foundSurfaceNum = HitTestSurface(mousePos);

            if (!string.IsNullOrEmpty(foundSurfaceNum))
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    SurfacePicked?.Invoke(foundSurfaceNum);
                }));
            }
        }
    }

    private string? HitTestSurface(Point mousePos)
    {
        string? foundSurfaceNum = null;
        VisualTreeHelper.HitTest(_helixViewport.Viewport, null, hitResult =>
        {
            if (hitResult is RayMeshGeometry3DHitTestResult rayHit)
            {
                DependencyObject? current = rayHit.VisualHit;
                while (current != null)
                {
                    try
                    {
                        string? surfaceNum = current.GetValue(SurfaceNumberProperty) as string;
                        if (!string.IsNullOrEmpty(surfaceNum))
                        {
                            foundSurfaceNum = surfaceNum;
                            return HitTestResultBehavior.Stop;
                        }

                        if (current is Visual || current is Visual3D)
                        {
                            current = VisualTreeHelper.GetParent(current);
                        }
                        else
                        {
                            break;
                        }
                    }
                    catch
                    {
                        break;
                    }
                }
            }
            return HitTestResultBehavior.Continue;
        }, new PointHitTestParameters(mousePos));

        return foundSurfaceNum;
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static void ApplyBoxMaterial(BoxVisual3D box, Color baseColor, double opacity)
    {
        var brush = new SolidColorBrush(baseColor) { Opacity = opacity };
        var material = MaterialHelper.CreateMaterial(brush, specularPower: 15);
        box.Material = material;
        box.BackMaterial = material;
    }

    private static void SetGroupOpacity(ModelVisual3D group, double opacity)
    {
        foreach (var child in group.Children)
        {
            if (child is BoxVisual3D box)
            {
                Color? baseColor = GetMaterialColor(box.Material);
                if (baseColor.HasValue)
                {
                    ApplyBoxMaterial(box, baseColor.Value, opacity);
                }
            }
        }
    }

    private static Color? GetMaterialColor(Material? material)
    {
        if (material is MaterialGroup mg)
        {
            foreach (var m in mg.Children)
            {
                var color = GetMaterialColor(m);
                if (color.HasValue) return color;
            }
        }
        else if (material is DiffuseMaterial dm && dm.Brush is SolidColorBrush b)
        {
            return b.Color;
        }
        return null;
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

    public static readonly DependencyProperty SurfaceNumberProperty =
        DependencyProperty.RegisterAttached("SurfaceNumber", typeof(string), typeof(Surface3DViewport), new PropertyMetadata(null));
}
