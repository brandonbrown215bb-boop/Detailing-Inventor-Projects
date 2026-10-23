using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using UnitConstructionVerifier.Operations;

namespace UnitConstructionVerifier.UI
{
    /// <summary>
    /// Step 0 spike for Docs/batch_regenerate_upload_plan.md: connects to YCShell by reflection, lists the surfaces it
    /// has loaded against the active unit, and regenerates one surface on request. Throwaway; it never uploads, and it
    /// only regenerates surfaces under a "Spike Copy" folder.
    /// </summary>
    internal sealed class YcShellSpikeWindow : Window
    {
        private const string SpikeFolderMarker = "Spike Copy";

        private readonly Inventor.Application _app;
        private readonly string _logPath;
        private readonly ListBox _surfaceList = new ListBox { FontFamily = new FontFamily("Consolas"), FontSize = 12 };
        private readonly TextBox _report = new TextBox
        {
            IsReadOnly = true,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            TextWrapping = TextWrapping.NoWrap,
        };
        private readonly Button _regenerateButton = new Button { Content = "Regenerate selected", Padding = new Thickness(10, 3, 10, 3), Margin = new Thickness(0, 0, 6, 0) };
        private YcShellBridge? _bridge;

        internal YcShellSpikeWindow(Inventor.Application app)
        {
            _app = app;
            _logPath = Path.Combine(DebugLogger.LogDirectory, $"ycshell_spike_{DateTime.Now:yyyyMMdd_HHmmss}.txt");

            Title = "UCV - YCShell spike (Step 0)";
            Width = 1100;
            Height = 760;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            new WindowInteropHelper(this).Owner = new IntPtr(app.MainFrameHWND);

            var refresh = new Button { Content = "Refresh", Padding = new Thickness(10, 3, 10, 3), Margin = new Thickness(0, 0, 6, 0) };
            var openLog = new Button { Content = "Open log folder", Padding = new Thickness(10, 3, 10, 3), Margin = new Thickness(0, 0, 6, 0) };
            var copy = new Button { Content = "Copy report", Padding = new Thickness(10, 3, 10, 3) };
            refresh.Click += (_, _) => Refresh();
            _regenerateButton.Click += (_, _) => RegenerateSelected();
            openLog.Click += (_, _) => Process.Start("explorer.exe", DebugLogger.LogDirectory);
            copy.Click += (_, _) => Clipboard.SetText(_report.Text);

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            buttons.Children.Add(refresh);
            buttons.Children.Add(_regenerateButton);
            buttons.Children.Add(openLog);
            buttons.Children.Add(copy);

            var hint = new TextBlock
            {
                Text = "Changed surfaces are listed first. Regenerate only runs on surfaces under a \"" + SpikeFolderMarker +
                       "\" folder. This window never uploads. Log: " + _logPath,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 6),
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(2, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(6) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(3, GridUnitType.Star) });
            Grid.SetRow(_surfaceList, 0);
            Grid.SetRow(_report, 2);
            grid.Children.Add(_surfaceList);
            grid.Children.Add(_report);

            var root = new DockPanel { Margin = new Thickness(10) };
            DockPanel.SetDock(buttons, Dock.Top);
            DockPanel.SetDock(hint, Dock.Top);
            root.Children.Add(buttons);
            root.Children.Add(hint);
            root.Children.Add(grid);
            Content = root;

            Loaded += (_, _) => Refresh();
        }

        private void Refresh()
        {
            _surfaceList.Items.Clear();
            Log("");
            Log($"===== Refresh {DateTime.Now:yyyy-MM-dd HH:mm:ss}  UCV {typeof(YcShellSpikeWindow).Assembly.GetName().Version} =====");

            try
            {
                var trace = new List<string>();
                _bridge = YcShellBridge.Connect(_app, trace, out string problem, out IReadOnlyList<MemberCheck> checks);
                foreach (string line in trace) Log("  " + line);
                foreach (MemberCheck check in checks) Log("  " + check);
                if (_bridge is null)
                {
                    Log("NOT CONNECTED: " + problem);
                    _regenerateButton.IsEnabled = false;
                    return;
                }

                Log($"Connected to YCShell {_bridge.Version} at {_bridge.AssemblyLocation}");
                _regenerateButton.IsEnabled = true;

                Inventor._Document? active = _app.ActiveDocument;
                Log($"Inventor active document: {active?.FullFileName ?? "(none)"}");
                Log($"Inventor active edit document: {ActiveEditName()}");
                Log($"YCShell active assembly (what its buttons act on): {_bridge.ActiveAssemblyFileName ?? "(none)"}");

                IReadOnlyList<YcSurface> surfaces = _bridge.GetOpenSurfaces();
                Dictionary<string, Inventor.Document> openDocs = OpenDocumentsByPath();

                var rows = surfaces.Select(s => SpikeRow.Create(s, openDocs)).ToList();
                foreach (SpikeRow row in rows.OrderByDescending(r => r.ChangedSq).ThenBy(r => r.Surface.FullFileName, StringComparer.OrdinalIgnoreCase))
                {
                    _surfaceList.Items.Add(row);
                }

                Log($"YCShell open assembly list: {surfaces.Count} entries, {rows.Count(r => r.ChangedSq > 0)} with Changed_SQ parts, " +
                    $"{rows.Sum(r => r.ChangedSq)} Changed_SQ parts in all");
                foreach (SpikeRow row in rows) Log("  " + row);

                CompareWithActiveUnit(active, surfaces);
            }
            catch (Exception ex)
            {
                Log("REFRESH FAILED: " + ex);
                DebugLogger.Log(ex, "YcShellSpikeWindow.Refresh");
            }
        }

        /// <summary>Open question 1: does YCShell's list hold every surface the unit references?</summary>
        private void CompareWithActiveUnit(Inventor._Document? active, IReadOnlyList<YcSurface> surfaces)
        {
            if (active is not Inventor.AssemblyDocument unit)
            {
                Log("Active document is not an assembly; open the spike copy's FINAL.IAM to compare.");
                return;
            }

            var referenced = new List<string>();
            foreach (Inventor.Document doc in unit.ReferencedDocuments)
            {
                if (doc.DocumentType == Inventor.DocumentTypeEnum.kAssemblyDocumentObject) referenced.Add(doc.FullFileName);
            }

            List<string> missing = referenced.Where(path => !surfaces.Any(s => s.Matches(path))).ToList();
            Log($"Active unit references {referenced.Count} sub-assemblies directly; {referenced.Count - missing.Count} are in YCShell's list.");
            foreach (string path in missing) Log("  NOT IN YCSHELL LIST: " + path);

            YcSurface? unitEntry = surfaces.FirstOrDefault(s => s.Matches(unit.FullFileName));
            Log(unitEntry is null
                ? "Active unit itself is NOT in YCShell's list."
                : $"Active unit is in YCShell's list: loaded={unitEntry.IsLoaded}, composite={Show(unitEntry.IsComposite)}");
        }

        private void RegenerateSelected()
        {
            if (_bridge is null || _surfaceList.SelectedItem is not SpikeRow row) return;

            string path = row.Surface.FullFileName;
            if (path.IndexOf(SpikeFolderMarker, StringComparison.OrdinalIgnoreCase) < 0)
            {
                MessageBox.Show(this, $"Refusing to regenerate outside a \"{SpikeFolderMarker}\" folder:\n\n{path}",
                    "YCShell spike", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show(this, $"Run YCShell Regenerate Channel Features on\n\n{path}\n\n({row.ChangedSq} Changed_SQ parts)?",
                    "YCShell spike", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }

            Dictionary<string, Inventor.Document> openDocs = OpenDocumentsByPath();
            openDocs.TryGetValue(path, out Inventor.Document? doc);

            Log("");
            Log($"===== Regenerate {DateTime.Now:yyyy-MM-dd HH:mm:ss}: {path} =====");
            Log($"Before: active edit document = {ActiveEditName()}");
            Log($"Before: YCShell active assembly = {_bridge.ActiveAssemblyFileName ?? "(none)"}");
            Log($"Before: surface dirty = {Show(doc?.Dirty)}, part status = {Statuses(doc)}");

            var watch = Stopwatch.StartNew();
            try
            {
                _bridge.Regenerate(row.Surface);
                Log($"Regenerate returned after {watch.Elapsed.TotalSeconds:F1} s (includes time spent on YCShell message boxes)");
            }
            catch (Exception ex)
            {
                Log($"Regenerate FAILED after {watch.Elapsed.TotalSeconds:F1} s: {ex}");
                DebugLogger.Log(ex, "YcShellSpikeWindow.RegenerateSelected");
            }

            Log($"After: active edit document = {ActiveEditName()}");
            Log($"After: YCShell active assembly = {_bridge.ActiveAssemblyFileName ?? "(none)"}");
            Log($"After: surface dirty = {Show(doc?.Dirty)}, part status = {Statuses(doc)}");
            Log("YCShell saves new channel parts and the surface as it regenerates; closing without saving does not undo it.");
        }

        private string ActiveEditName()
        {
            try { return (_app.ActiveEditDocument as Inventor._Document)?.FullFileName ?? "(none)"; }
            catch { return "(unknown)"; }
        }

        private Dictionary<string, Inventor.Document> OpenDocumentsByPath()
        {
            var docs = new Dictionary<string, Inventor.Document>(StringComparer.OrdinalIgnoreCase);
            foreach (Inventor.Document doc in _app.Documents)
            {
                try { docs[doc.FullFileName.Trim()] = doc; }
                catch { /* unnamed or closing document */ }
            }

            return docs;
        }

        private static string Statuses(Inventor.Document? doc) =>
            doc is Inventor.AssemblyDocument assembly
                ? string.Join(" ", PartChangeStatus.CountByStatus(assembly).Select(kv => $"{kv.Key}={kv.Value}"))
                : "(document not open)";

        private static string Show(bool? value) => value?.ToString() ?? "unknown";

        private void Log(string line)
        {
            _report.AppendText(line + Environment.NewLine);
            _report.ScrollToEnd();
            try
            {
                Directory.CreateDirectory(DebugLogger.LogDirectory);
                File.AppendAllText(_logPath, line + Environment.NewLine);
            }
            catch { /* the on-screen report still has it */ }
        }

        private sealed class SpikeRow
        {
            private SpikeRow(YcSurface surface, int changedSq, string statuses)
            {
                Surface = surface;
                ChangedSq = changedSq;
                Statuses = statuses;
            }

            public YcSurface Surface { get; }
            public int ChangedSq { get; }
            private string Statuses { get; }

            public static SpikeRow Create(YcSurface surface, Dictionary<string, Inventor.Document> openDocs)
            {
                if (!openDocs.TryGetValue(surface.FullFileName, out Inventor.Document? doc) || doc is not Inventor.AssemblyDocument assembly)
                {
                    return new SpikeRow(surface, 0, "(not open in Inventor)");
                }

                IDictionary<string, int> counts = PartChangeStatus.CountByStatus(assembly);
                int changed = counts.Where(kv => PartChangeStatus.IsChangedSq(kv.Key)).Sum(kv => kv.Value);
                return new SpikeRow(surface, changed, string.Join(" ", counts.Select(kv => $"{kv.Key}={kv.Value}")));
            }

            public override string ToString() =>
                $"{(ChangedSq > 0 ? $"CHANGED {ChangedSq,3}" : "           ")}  " +
                $"loaded={(Surface.IsLoaded ? "yes" : "no ")}  composite={Show(Surface.IsComposite),-7}  " +
                $"{Surface.FullFileName}   [{Statuses}]";
        }
    }
}
