using System;
using System.IO;
using System.Linq;
using Inventor;
using UnitConstructionVerifier.Operations;
using UnitConstructionVerifier.Extraction;

// Replays the verifier's highlight calls against a separate Inventor instance, logging before each call.
static class Helper
{
    static readonly Type T = typeof(IptPropertyReader).Assembly.GetType("UnitConstructionVerifier.Operations.InventorSelectionHelper", true);
    static object Call(string name, params object[] a) =>
        T.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
         .First(m => m.Name == name && m.GetParameters().Length == a.Length).Invoke(null, a);
    public static void HighlightByFilePath(Application app, string root, string target) => Call("HighlightByFilePath", app, root, target);
    public static void HighlightPartInSurface(Application app, string root, string surf, string pn, string file, bool x) => Call("HighlightPartInSurface", app, root, surf, pn, file, x);
    public static void ClearHighlight(Application app) => Call("ClearHighlight", app, null);
    public static void RestoreNormal(Application app) => Call("RestoreNormal", app);
}

static class Program
{
    static StreamWriter Log;
    static System.Diagnostics.Process Proc;
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hWnd, out int pid);
    static void Step(string s) { Log.WriteLine($"{DateTime.Now:HH:mm:ss.fff} {s}"); Log.Flush(); Console.WriteLine(s); }

    [STAThread]
    static int Main(string[] args)
    {
        string iam = args[0], mode = args[1], logPath = args[2];
        bool visible = args.Length > 3 && args[3] == "visible";
        Log = new StreamWriter(logPath, append: true);
        Application app = null;
        Document doc = null;
        try
        {
            Step($"=== mode {mode}, visible={visible}: starting new Inventor instance");
            var before = new System.Collections.Generic.HashSet<int>(System.Diagnostics.Process.GetProcessesByName("Inventor").Select(p => p.Id));
            app = (Application)Activator.CreateInstance(Type.GetTypeFromProgID("Inventor.Application"));
            GetWindowThreadProcessId(new IntPtr(app.MainFrameHWND), out int pid);
            Step($"connected to Inventor pid {pid}; already running before: {string.Join(",", before)}");
            if (before.Contains(pid)) { Step("ABORT: attached to an existing Inventor; not touching it"); app = null; return 2; }
            Proc = System.Diagnostics.Process.GetProcessById(pid);
            app.Visible = visible;
            app.SilentOperation = true;
            Step("open " + iam);
            doc = app.Documents.Open(iam, true);
            var asm = (AssemblyDocument)doc;
            Step($"activeView null? {app.ActiveView == null}");
            var parts = new IptPropertyReader().ScanAssembly(asm).Parts;
            Step($"{parts.Count} parts");

            if (mode == "old" || mode == "new" || mode == "boxclicks")
            {
                Step("surface select -> box around surface (root == surface)");
                Helper.HighlightByFilePath(app, iam, iam);
                if (mode == "new") { Step("mode switch to X-Ray -> box around surface again"); Helper.HighlightByFilePath(app, iam, iam); }
                else { Step("old X-Ray checkbox -> ClearHighlight"); Helper.ClearHighlight(app); }
            }
            bool xray = mode != "boxclicks";
            for (int round = 0; round < (xray ? 1 : 3); round++)
            foreach (var p in parts)
            {
                Step($"{(xray ? "xray" : "box")} part {p.PartNumber} {System.IO.Path.GetFileName(p.FilePath)}");
                Helper.HighlightPartInSurface(app, iam, iam, p.PartNumber, p.FilePath, xray);
                Proc.Refresh();
                Step($"  ok  inventor private MB {Proc.PrivateMemorySize64 / 1048576}");
            }
            Step("restore normal");
            Helper.RestoreNormal(app);
            Step("DONE no crash");
            return 0;
        }
        catch (Exception ex)
        {
            Step("EXCEPTION " + ex.GetType().Name + ": " + ex.Message);
            return 1;
        }
        finally
        {
            try { doc?.Close(true); } catch { }
            try { app?.Quit(); } catch { }
            Log.Dispose();
        }
    }
}
