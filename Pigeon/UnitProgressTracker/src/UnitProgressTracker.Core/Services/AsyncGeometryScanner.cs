using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using UnitProgressTracker.Core.Models;

namespace UnitProgressTracker.Core.Services;

public class IamScanProgress
{
    public int Scanned { get; init; }
    public int Total { get; init; }
    public string CurrentFile { get; init; } = string.Empty;
    public double Percent => Total > 0 ? (double)Scanned / Total * 100 : 0;
}

[SupportedOSPlatform("windows")]
public class AsyncGeometryScanner
{
    /// <summary>
    /// Wrapper for backward compatibility delegating to GeometryScanner.ScanIamFolderAsync.
    /// </summary>
    public static async Task<List<SurfaceModel>> ScanFolderAsync(
        string folderPath,
        IProgress<IamScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var coreProgress = progress != null
            ? new Progress<ProgressReport>(pr => progress.Report(new IamScanProgress
              {
                  Scanned = pr.Scanned,
                  Total = pr.Total,
                  CurrentFile = pr.CurrentFile
              }))
            : null;

        return await GeometryScanner.ScanIamFolderAsync(folderPath, coreProgress, cancellationToken);
    }
}
