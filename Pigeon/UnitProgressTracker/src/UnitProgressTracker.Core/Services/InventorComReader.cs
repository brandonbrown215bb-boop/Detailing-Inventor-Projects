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
        object? invObj = GetActiveComObject("Inventor.Application");
        if (invObj != null)
        {
            SafeReleaseComObject(invObj);
            return true;
        }
        return false;
    }

    public static string? TryReadConfigJsonAttribute(string iamPath)
    {
        object? invObj = null;
        object? docsObj = null;
        object? docObj = null;
        object? attrSetsObj = null;
        object? attrSetObj = null;
        object? attrObj = null;

        try
        {
            invObj = GetActiveComObject("Inventor.Application");
            if (invObj == null) return null;

            dynamic invApp = invObj;
            docsObj = invApp.Documents;
            dynamic docs = docsObj;

            docObj = docs.Open(iamPath, false);
            if (docObj == null) return null;

            dynamic doc = docObj;
            attrSetsObj = doc.AttributeSets;
            dynamic attributeSets = attrSetsObj;

            if (attributeSets.NameExists["DOCUMENT_CONFIG_JSON"])
            {
                attrSetObj = attributeSets["DOCUMENT_CONFIG_JSON"];
                dynamic set = attrSetObj;
                if (set.NameExists["DOCUMENT_CONFIG_JSON"])
                {
                    attrObj = set["DOCUMENT_CONFIG_JSON"];
                    dynamic attr = attrObj;
                    return attr.Value as string;
                }
            }
        }
        catch
        {
            // Inventor COM read failure
        }
        finally
        {
            SafeReleaseComObject(attrObj);
            SafeReleaseComObject(attrSetObj);
            SafeReleaseComObject(attrSetsObj);
            if (docObj != null)
            {
                try
                {
                    dynamic doc = docObj;
                    doc.Close(true);
                }
                catch { }
                SafeReleaseComObject(docObj);
            }
            SafeReleaseComObject(docsObj);
            SafeReleaseComObject(invObj);
        }

        return null;
    }

    private static void SafeReleaseComObject(object? comObj)
    {
        if (comObj != null && Marshal.IsComObject(comObj))
        {
            try
            {
#pragma warning disable CA1416
                Marshal.ReleaseComObject(comObj);
#pragma warning restore CA1416
            }
            catch
            {
                // Ignore release errors during teardown
            }
        }
    }
}

