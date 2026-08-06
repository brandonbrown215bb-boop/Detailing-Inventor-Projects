using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Media;
using UnitProgressTracker.Core.Models;
using UnitProgressTracker.Wpf.Controls;
using Xunit;

namespace UnitProgressTracker.Tests;

public class ViewportSelectionTests
{
    private static void RunOnStaThread(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception != null)
        {
            throw exception;
        }
    }

    [Fact]
    public void Surface3DViewport_LoadAndHighlightSurface_DoesNotCrashOnFrozenBrushes()
    {
        RunOnStaThread(() =>
        {
            var viewport = new Surface3DViewport();
            var surfaces = new List<SurfaceModel>
            {
                new SurfaceModel
                {
                    SurfaceNumber = "391Z010142-0001",
                    SurfaceUnitSide = "Roof",
                    StateId = "done",
                    Boxes = new List<GeometryBox> { new(0, 0, 0, 10, 10, 10) }
                },
                new SurfaceModel
                {
                    SurfaceNumber = "391Z010142-0002",
                    SurfaceUnitSide = "Wall",
                    StateId = "built",
                    Boxes = new List<GeometryBox> { new(20, 0, 0, 10, 10, 10) }
                }
            };

            viewport.LoadSurfaces(surfaces, id => "#38bdf8");

            // Verify highlight surface does not throw when setting opacity
            viewport.HighlightSurface("391Z010142-0001");
            viewport.HighlightSurface("391Z010142-0002");
            viewport.SetGlobalOpacity(0.5);
            viewport.HighlightSurface("non-existent-surface");
        });
    }

    [Fact]
    public void Surface3DViewport_VisibilityAndWireframe_DoesNotCrash()
    {
        RunOnStaThread(() =>
        {
            var viewport = new Surface3DViewport();
            var surfaces = new List<SurfaceModel>
            {
                new SurfaceModel
                {
                    SurfaceNumber = "391Z010142-0001",
                    SurfaceUnitSide = "Roof",
                    StateId = "done",
                    Boxes = new List<GeometryBox> { new(0, 0, 0, 10, 10, 10) }
                }
            };

            viewport.LoadSurfaces(surfaces, id => "#38bdf8");
            viewport.SetWireframeVisible(false);
            viewport.SetWireframeVisible(true);
            viewport.SetSurfaceVisibility(true, "391Z010142-0001");
            viewport.SetSurfaceVisibility(false, "391Z010142-0001");
            viewport.ClearSurfaces();
        });
    }
}
