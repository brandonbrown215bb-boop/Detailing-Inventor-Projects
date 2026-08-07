using System;
using System.IO;
using UnitProgressTracker.Core.Services;
using Xunit;
using Xunit.Abstractions;

namespace UnitProgressTracker.Tests;

public class InvestigateSurfaces0008And0009
{
    private readonly ITestOutputHelper _output;

    public InvestigateSurfaces0008And0009(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void ReadConfigFromIam0008And0009()
    {
        string iam8 = @"C:\Users\jbrow263\ISG\20138\Unit\391Z010115-0008\391Z010115-0008.IAM";
        string iam9 = @"C:\Users\jbrow263\ISG\20138\Unit\391Z010115-0009\391Z010115-0009.IAM";

        InspectIam(iam8, "0008");
        InspectIam(iam9, "0009");
    }

    private void InspectIam(string path, string label)
    {
        if (!File.Exists(path))
        {
            _output.WriteLine($"File not found: {path}");
            return;
        }

        string? json = InventorComReader.TryReadConfigJsonAttribute(path);
        if (string.IsNullOrEmpty(json))
        {
            _output.WriteLine($"Could not read JSON attribute from {path}");
            return;
        }

        var surface = GeometryScanner.ParseConfigJson(json, path, "", "iam");
        if (surface == null)
        {
            _output.WriteLine($"ParseConfigJson returned null for {label}");
            return;
        }

        _output.WriteLine($"=== SURFACE {label} (SideTag: '{surface.SideTag}', SurfaceNumber: '{surface.SurfaceNumber}') ===");
        _output.WriteLine($"Raw JSON length: {json.Length}");

        if (surface.BulkheadHolePatterns != null)
        {
            _output.WriteLine($"Bulkhead Hole Patterns count: {surface.BulkheadHolePatterns.Count}");
            foreach (var hp in surface.BulkheadHolePatterns)
            {
                _output.WriteLine($"  [HolePattern] Part: '{hp.BulkheadPartNumber}', Desc: '{hp.BulkheadDescription}', Side: '{hp.UnitSide}', Index: {hp.Index}, Doa: {hp.DoaOffset}, WidthOffset: {hp.WidthOffset}, Qty: {hp.WidthQty}, Spacing: {hp.WidthSpacing}");
            }
        }

        if (surface.BulkheadChannels != null)
        {
            _output.WriteLine($"Calculated 3D Bulkhead Channels count: {surface.BulkheadChannels.Count}");
            foreach (var c in surface.BulkheadChannels)
            {
                _output.WriteLine($"  [Channel Box] X: {c.X:F3}, Y: {c.Y:F3}, Z: {c.Z:F3}, XL: {c.XLength:F3}, YL: {c.YLength:F3}, ZL: {c.ZLength:F3}");
            }
        }
    }
}
