using System;
using System.Collections.Generic;
using UnitConstructionVerifier.Extraction;

namespace UnitConstructionVerifier.Operations
{
    /// <summary>
    /// The two checks YCShell's Upload button makes before it opens the upload window (MOM_Upload_OnExecute), applied to
    /// every surface of a batch: suppressed parts, and structural parts with holes. Any blocker stops the whole upload,
    /// as it does in YCShell.
    /// </summary>
    public static class UploadBlockers
    {
        /// <param name="hasHoles">YCShell's own hole test (<see cref="YcShellBridge.HasHoles"/>).</param>
        public static List<string> Find(Inventor.AssemblyDocument surface, Func<object, bool> hasHoles)
        {
            var blockers = new List<string>();
            var checkedParts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (Inventor.ComponentOccurrence occ in surface.ComponentDefinition.Occurrences)
            {
                if (occ.Suppressed)
                {
                    blockers.Add($"suppressed: {occ.Name}");
                    continue;
                }

                bool structural = string.Equals(SurfaceChangeReader.ReadMomAttribute(occ.AttributeSets, "CONSTRUCTION_TYPE").Trim(),
                    "STRUCTURAL", StringComparison.OrdinalIgnoreCase);
                if (!structural || occ.Definition?.Document is not Inventor.PartDocument part) continue;
                if (!checkedParts.Add(part.FullFileName)) continue;

                if (hasHoles(part)) blockers.Add($"structural part with holes: {occ.Name}");
            }

            return blockers;
        }
    }
}
