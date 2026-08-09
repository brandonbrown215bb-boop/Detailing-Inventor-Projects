using System;
using System.IO;
using System.Runtime.CompilerServices;
using UnitProgressTracker.Core.Services;

namespace UnitProgressTracker.Tests;

internal static class TestAssemblySetup
{
    private static readonly string DataRoot = Path.Combine(
        Path.GetTempPath(),
        "UnitProgressTracker.Tests",
        Environment.ProcessId.ToString());

    [ModuleInitializer]
    public static void Initialize()
    {
        Directory.CreateDirectory(DataRoot);
        Environment.SetEnvironmentVariable(AppSettingsService.DataRootEnvironmentVariable, DataRoot);
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            try
            {
                if (Directory.Exists(DataRoot)) Directory.Delete(DataRoot, recursive: true);
            }
            catch
            {
                // Test cleanup must not mask the test result.
            }
        };
    }
}
