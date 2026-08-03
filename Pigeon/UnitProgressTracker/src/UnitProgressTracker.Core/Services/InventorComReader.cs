using System;
using System.Runtime.InteropServices;

namespace UnitProgressTracker.Core.Services;

public class InventorComReader
{
    [DllImport("oleaut32.dll", PreserveSig = false)]
    private static extern void GetActiveObject(ref Guid rclsid, IntPtr pvReserved, [MarshalAs(UnmanagedType.IUnknown)] out object ppunk);

    [DllImport("ole32.dll")]
    private static extern int CLSIDFromProgID([MarshalAs(UnmanagedType.LPWStr)] string lpszProgID, out Guid pclsid);

    public static object? GetActiveComObject(string progId)
    {
        int hr = CLSIDFromProgID(progId, out Guid clsid);
        if (hr < 0) return null;

        try
        {
            GetActiveObject(ref clsid, IntPtr.Zero, out object obj);
            return obj;
        }
        catch
        {
            return null;
        }
    }

    public static bool IsInventorRunning()
    {
        return GetActiveComObject("Inventor.Application") != null;
    }

    public static string? TryReadConfigJsonAttribute(string iamPath)
    {
        object? invObj = GetActiveComObject("Inventor.Application");
        if (invObj == null) return null;

        try
        {
            dynamic invApp = invObj;
            dynamic doc = invApp.Documents.Open(iamPath, false);

            try
            {
                dynamic attributeSets = doc.AttributeSets;
                if (attributeSets.NameExists["DOCUMENT_CONFIG_JSON"])
                {
                    dynamic set = attributeSets["DOCUMENT_CONFIG_JSON"];
                    if (set.NameExists["DOCUMENT_CONFIG_JSON"])
                    {
                        return set["DOCUMENT_CONFIG_JSON"].Value as string;
                    }
                }
            }
            finally
            {
                doc.Close(true);
            }
        }
        catch
        {
            // Inventor COM read failure
        }

        return null;
    }
}
