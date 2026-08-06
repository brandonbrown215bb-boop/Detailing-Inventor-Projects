using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace UnitProgressTracker.Core.Services;

[SupportedOSPlatform("windows")]
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
        if (string.IsNullOrWhiteSpace(iamPath) || !File.Exists(iamPath))
            return null;

        object? apprenticeObj = null;
        object? docObj = null;

        try
        {
            Type? apprenticeType = Type.GetTypeFromProgID("Inventor.ApprenticeServer");
            if (apprenticeType == null) return null;

            apprenticeObj = Activator.CreateInstance(apprenticeType);
            if (apprenticeObj == null) return null;

            dynamic apprentice = apprenticeObj;
            docObj = apprentice.Open(iamPath);
            if (docObj == null) return null;

            dynamic doc = docObj;
            dynamic attributeSets = doc.AttributeSets;

            // 1. Try MOM_DATA attribute set (standard for 391Z/ICG surface assemblies)
            try
            {
                if (attributeSets.NameIsUsed["MOM_DATA"])
                {
                    dynamic momSet = attributeSets["MOM_DATA"];
                    if (momSet.NameIsUsed["DOCUMENT_CONFIG_JSON"])
                    {
                        dynamic attr = momSet["DOCUMENT_CONFIG_JSON"];
                        string? val = attr.Value as string;
                        if (!string.IsNullOrWhiteSpace(val)) return val;
                    }
                }
            }
            catch { }

            // 2. Try direct DOCUMENT_CONFIG_JSON attribute set
            try
            {
                if (attributeSets.NameIsUsed["DOCUMENT_CONFIG_JSON"])
                {
                    dynamic jsonSet = attributeSets["DOCUMENT_CONFIG_JSON"];
                    if (jsonSet.NameIsUsed["DOCUMENT_CONFIG_JSON"])
                    {
                        dynamic attr = jsonSet["DOCUMENT_CONFIG_JSON"];
                        string? val = attr.Value as string;
                        if (!string.IsNullOrWhiteSpace(val)) return val;
                    }
                }
            }
            catch { }

            // 3. Fallback: scan all attribute sets
            try
            {
                foreach (dynamic set in attributeSets)
                {
                    foreach (dynamic attr in set)
                    {
                        if (string.Equals((string)attr.Name, "DOCUMENT_CONFIG_JSON", StringComparison.OrdinalIgnoreCase))
                        {
                            string? val = attr.Value as string;
                            if (!string.IsNullOrWhiteSpace(val)) return val;
                        }
                    }
                }
            }
            catch { }
        }
        catch
        {
            // Apprentice Server COM read failure
        }
        finally
        {
            if (docObj != null)
            {
                try
                {
                    dynamic doc = docObj;
                    doc.Close();
                }
                catch { }
                SafeReleaseComObject(docObj);
            }
            if (apprenticeObj != null)
            {
                try
                {
                    dynamic apprentice = apprenticeObj;
                    apprentice.Close();
                }
                catch { }
                SafeReleaseComObject(apprenticeObj);
            }
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


