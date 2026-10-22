using System;
using System.Reflection;
using System.Windows;

// Loads the add-in DLL with Assembly.LoadFrom and constructs VerifierWindow with no WPF Application,
// the way Inventor hosts it.
static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        System.Windows.FrameworkCompatibilityPreferences.AreInactiveSelectionHighlightBrushKeysSupported = false; // as in Inventor
        try
        {
            var dir = System.IO.Path.GetDirectoryName(args[0]); AppDomain.CurrentDomain.AssemblyResolve += (s, e) => { var p = System.IO.Path.Combine(dir, new AssemblyName(e.Name).Name + ".dll"); return System.IO.File.Exists(p) && !e.Name.StartsWith("UnitConstructionVerifier") ? Assembly.LoadFile(p) : null; }; var asm = Assembly.LoadFile(args[0]);
            var dataType = asm.GetType("UnitConstructionVerifier.Models.UnitConstructionData", true);
            var winType = asm.GetType("UnitConstructionVerifier.UI.VerifierWindow", true);
            var data = Activator.CreateInstance(dataType);
            var w = (Window)Activator.CreateInstance(winType, data, @"C:\Units\X.iam", null);
            w.Left = -4000; w.ShowActivated = false; w.Show();
            Console.WriteLine("constructed and shown OK");
            w.Close();
            return 0;
        }
        catch (Exception ex)
        {
            for (Exception e = ex; e != null; e = e.InnerException) Console.WriteLine(e.GetType().Name + ": " + e.Message);
            return 1;
        }
    }
}
