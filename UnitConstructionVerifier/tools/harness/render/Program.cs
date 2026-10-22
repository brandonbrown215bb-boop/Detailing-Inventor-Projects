using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using UnitConstructionVerifier.Models;
using UnitConstructionVerifier.UI;

// Renders VerifierWindow with sample data (no Inventor) and saves PNG screenshots.
static class Program
{
    static string Out = "";

    [STAThread]
    static int Main(string[] args)
    {
        FrameworkCompatibilityPreferences.AreInactiveSelectionHighlightBrushKeysSupported = false; // as in Inventor
        Out = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();
        Directory.CreateDirectory(Out);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        int code = 0;
        app.Dispatcher.BeginInvoke(new Action(() =>
        {
            try { Run(args.Skip(1).FirstOrDefault() ?? "1180x800"); }
            catch (Exception ex) { Console.WriteLine(ex); code = 1; }
            app.Shutdown();
        }));
        app.Run();
        return code;
    }

    static void Run(string size)
    {
        var data = new UnitConstructionData { IamPath = @"C:\Units\UNIT-4471_MAIN.iam" };
        string[] roofs = { "391Z010111-0151", "391Z010111-0152", "391Z010111-0153", "391Z010111-0154" };
        for (int i = 0; i < roofs.Length; i++)
            data.RoofRows.Add(new RoofSurfaceRow
            {
                PartNumber = roofs[i], Segments = i == 1 ? "3, 4, 5" : $"{2 * i + 1}, {2 * i + 2}", SourceSurfaceIam = $@"C:\Units\R{i}.iam",
                Thickness = "2", ExteriorSkinGauge = "18", ExteriorSkinMaterial = "STL GALV PPC", InteriorLinerGauge = "22", InteriorLinerMaterial = "STL GALV",
                ChannelSkinGauge = i == 1 ? "16.5" : "16", ChannelSkinMaterial = "STL GALV2", TrimSkinGauge = "20", TrimSkinMaterial = "STL GALV",
                InsulationThicknessAndMaterial = "2\" FG", ThermalBreak = i == 3 ? "" : "No"
            });
        string[] walls = { "391Z010111-0201", "391Z010111-0202", "391Z010111-0203" };
        for (int i = 0; i < walls.Length; i++)
            data.WallRows.Add(new WallSurfaceRow
            {
                PartNumber = walls[i], Segments = $"CC, XA-{i + 1}", SourceSurfaceIam = $@"C:\Units\W{i}.iam",
                Thickness = "2", ExteriorPaint = "PPC", ExteriorSkinGauge = "18", ExteriorSkinMaterial = "STL GALV PPC",
                InteriorLinerGauge = "22", InteriorLinerMaterial = "STL GALV", ChannelSkinGauge = "16", ChannelSkinMaterial = "STL GALV2",
                InsulationThicknessAndMaterial = "2\" FG", ThermalBreak = "Yes"
            });
        data.BaseRows.Add(new BaseSurfaceRow
        {
            PartNumber = "391Z010111-0301", Segments = "1", SourceSurfaceIam = @"C:\Units\B0.iam", BaseHeight = "8\"", BaseMaterial = "STL C CHNL",
            FormedChannelGauge = "10", FormedChannelMaterial = "STL HOT ROLL", FloorGauge = "0.125", FloorMaterial = "ALM DIA",
            SubFloorGauge = "16", SubFloorMaterial = "STL GALV", PerimeterAngleGauge = "10", PerimeterAngleMaterial = "ALM SHT", FloorThermalBreak = "No"
        });
        data.OtherConstruction.UpturnedLip = true;
        data.OtherConstruction.UpturnedLipHeight = "2.5";

        var scan = new IptScanResult();
        int n = 104300;
        void Part(string iam, string model, string gauge, string mat, string desc) =>
            scan.Parts.Add(new IptProperties { PartNumber = $"091Z00{n++}-0001", OwnerIamPath = iam, ModelNumber = model, MtlGauge = gauge, YCMATL = mat, Description = desc, FilePath = $@"C:\Units\parts\{n}.ipt" });
        foreach (var w in data.WallRows.Concat<object>(data.RoofRows))
        {
            string iam = SurfaceStatusBoard.SurfaceIamOf(w);
            Part(iam, "091-30117-083", "18", "STL GALV PPC", "PANEL EXT SKIN");
            Part(iam, "091-30117-083", iam.EndsWith("R1.iam") || iam.EndsWith("W2.iam") ? "20" : "18", "STL GALV PPC", "PANEL EXT SKIN, SHORT");
            Part(iam, "091-30117-082", "20", "STL GALV", "TRIM CORNER");
            Part(iam, "091-30117-075", "10", "ALM SHT", "ANGLE, CUSTOM");
            if (iam.EndsWith("R1.iam")) Part(iam, "091-30117-083", "22", "ALM SHT", "PANEL EXT SKIN, DOOR");
        }
        Part(@"C:\Units\B0.iam", "091-30117-056", "0.125", "ALM DIA", "FLOOR SHEET");

        var v0 = new VerifierView(data, data.IamPath, null);
        var w0 = new VerifierWindow(v0);
        v0.SetIptScanResult(scan);
        var dims = size.Split('x').Select(double.Parse).ToArray();
        w0.Width = dims[0]; w0.Height = dims[1];
        w0.Left = -4000; w0.Top = 0; w0.ShowActivated = false;
        w0.WindowStartupLocation = WindowStartupLocation.Manual;
        w0.Show();
        Pump();
        Shot(w0, "01-open");

        var roofList = (ListBox)v0.FindName("RoofSurfaceList");
        roofList.SelectedIndex = 1;
        Pump();
        var grid = (DataGrid)v0.FindName("RoofPartsGrid");
        grid.SelectedIndex = 1;
        Pump();
        Shot(w0, "02-roof-selected");

        ((ButtonBase)v0.FindName("BannerNextIssueButton")).GetType();
        v0.GetType().GetMethod("OnVerify", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(v0, new object[] { v0, new RoutedEventArgs() });
        ((RadioButton)v0.FindName("RoofFilterIssues")).IsChecked = true;
        ((CheckBox)v0.FindName("EditModeCheckBox")).IsChecked = true;
        Pump();
        var rows = grid.ItemsSource.Cast<IptVerificationRow>().ToList();
        var mm = rows.FirstOrDefault(r => r.IsMismatch && r.IsWritable);
        foreach (var r in rows.Where(r => r.IsMismatch && r.IsWritable)) r.NewValue = r.Expected;
        Pump();
        Shot(w0, "03-recheck-edit-issues");

        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        v0.GetType().GetMethod("OnWriteChanges", flags)!.Invoke(v0, new object[] { v0, new RoutedEventArgs() });
        Pump();
        var review = (System.Collections.Generic.List<WriteReviewItem>)v0.GetType().GetField("_reviewItems", flags)!.GetValue(v0)!;
        Console.WriteLine($"review items: {review.Count}");
        if (review.Count > 1) review[1].IsIncluded = false;
        Pump();
        Shot(w0, "06-write-review");
        v0.GetType().GetMethod("OnWriteReviewCancel", flags)!.Invoke(v0, new object[] { v0, new RoutedEventArgs() });
        Pump();

        ((TabControl)v0.FindName("MainTabControl")).SelectedIndex = 2;
        Pump();
        ((ListBox)v0.FindName("BaseSurfaceList")).SelectedIndex = 0;
        Pump();
        Shot(w0, "04-base");

        ((TabControl)v0.FindName("MainTabControl")).SelectedIndex = 1;
        ((TextBox)v0.FindName("WallSearchBox")).Text = "0203";
        Pump();
        ((ListBox)v0.FindName("WallSurfaceList")).SelectedIndex = 0;
        ((RadioButton)v0.FindName("HighlightXRayRadio")).IsChecked = true;
        Pump();
        Shot(w0, "05-walls-search");

        // Re-check staged edits then close prompt text is out of scope here; skip closing prompt.
        v0.GetType().GetField("_overridesDirty", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(v0, false);
        v0.GetType().GetField("_pendingEdits", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .FieldType.GetMethod("Clear")!.Invoke(v0.GetType().GetField("_pendingEdits", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(v0), null);
        w0.Close();
        Console.WriteLine("ok");
    }

    static void Pump()
    {
        for (int i = 0; i < 4; i++)
            Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(() => { }));
    }

    static void Shot(Window w, string name)
    {
        var root = (FrameworkElement)w.Content;
        root.UpdateLayout();
        var bmp = new RenderTargetBitmap((int)root.ActualWidth, (int)root.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        var bg = new DrawingVisual();
        using (var dc = bg.RenderOpen())
            dc.DrawRectangle(w.Background, null, new Rect(0, 0, root.ActualWidth, root.ActualHeight));
        bmp.Render(bg);
        bmp.Render(root);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(bmp));
        using var fs = File.Create(Path.Combine(Out, name + ".png"));
        enc.Save(fs);
    }
}
