using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnitProgressTracker.Core.Models;

namespace UnitProgressTracker.Core.Services;

public class BomShellEngine
{
    public static readonly string[] ExclusionPatterns = new[]
    {
        "DRAIN PAN NIPPLE KIT",
        "ASY F GA-SPC",
        "ISO PLT",
        "OS LATCH ASSY",
        "IS LATCH ASSY",
        "TEST COVER",
        "SUMP DRAIN",
        "FLOOR DRAIN",
        "DOOR"
    };

    private static readonly Regex IllegalFolderCharsRegex = new(@"[\u0000-\u001F\\/:*?""<>|]", RegexOptions.Compiled);
    private const int MaxAssemblyFolderLength = 120;

    private static readonly HashSet<string> ReservedDeviceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    public static bool Is391Part(string partNumber)
    {
        return !string.IsNullOrWhiteSpace(partNumber) && partNumber.Trim().StartsWith("391-", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsExcludedFromShellMaker(BomRow row)
    {
        string text = row.CombinedDescription;
        if (string.IsNullOrWhiteSpace(text)) return false;

        foreach (string pattern in ExclusionPatterns)
        {
            if (pattern.Equals("DOOR", StringComparison.OrdinalIgnoreCase))
            {
                if (Regex.IsMatch(text, @"\bDOORS?\b", RegexOptions.IgnoreCase))
                {
                    return true;
                }
            }
            else if (text.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    public static bool IsMisplacedCoilPanel(BomRow row)
    {
        return Is391Part(row.PartNumber) && string.Equals(row.Segment.Trim(), "<--", StringComparison.Ordinal);
    }

    public static bool IsCustomSqAssembly(BomRow row)
    {
        string text = row.CombinedDescription;
        return Regex.IsMatch(text, @"\bSQ\b", RegexOptions.IgnoreCase);
    }

    public static string? ParseSkidNumber(string skid)
    {
        if (string.IsNullOrWhiteSpace(skid)) return null;
        var match = Regex.Match(skid.Trim(), @"^(\d+)");
        return match.Success ? match.Groups[1].Value.PadLeft(2, '0') : null;
    }

    public static string NormalizeSegmentCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return string.Empty;
        return Regex.Replace(code, @"[^A-Za-z0-9]", "").ToUpperInvariant();
    }

    public record SegmentOrderToken(int Order, string Code, string FolderPrefix, string Normalized);

    public static List<SegmentOrderToken> ParseSkidSegmentOrder(string skid)
    {
        if (string.IsNullOrWhiteSpace(skid)) return new();
        var match = Regex.Match(skid, @"\[([^\]]*)\]");
        if (!match.Success) return new();
        string raw = match.Groups[1].Value.Trim();
        if (string.IsNullOrWhiteSpace(raw)) return new();

        var tokens = raw.Split('-')
            .Select(t => t.Trim())
            .Where(t => t.Length > 0)
            .Select(t => IllegalFolderCharsRegex.Replace(t, "").Replace("..", "").Trim())
            .Where(t => t.Length > 0)
            .ToList();

        // Bracket tokens are listed in reverse physical segment order (e.g. FR-MB -> 01 MB, 02 FR)
        tokens.Reverse();

        var result = new List<SegmentOrderToken>();
        for (int i = 0; i < tokens.Count; i++)
        {
            int order = i + 1;
            string code = tokens[i];
            string prefix = $"{order:D2} {code}";
            result.Add(new SegmentOrderToken(order, code, prefix, NormalizeSegmentCode(code)));
        }
        return result;
    }

    public static string? ResolveSegmentFolder(string skid, string segment)
    {
        if (string.IsNullOrWhiteSpace(segment) || segment.Trim() == "<--") return null;
        string prefix = segment.Split(" - ")[0].Trim();
        string normalized = NormalizeSegmentCode(prefix);
        var orderTokens = ParseSkidSegmentOrder(skid);
        var hit = orderTokens.FirstOrDefault(t => t.Normalized == normalized);
        return hit?.FolderPrefix;
    }

    public static string SanitizeAssemblyFolderName(string description, string extDescription = "")
    {
        string combined = string.IsNullOrWhiteSpace(extDescription)
            ? description.Trim()
            : $"{description.Trim()} {extDescription.Trim()}";

        string sanitized = Regex.Replace(combined, @"\.\.+", " ");
        sanitized = IllegalFolderCharsRegex.Replace(sanitized, " ");
        sanitized = Regex.Replace(sanitized, @"\s+", " ").Trim();
        if (string.IsNullOrWhiteSpace(sanitized)) sanitized = "Assembly";

        if (sanitized.Length > MaxAssemblyFolderLength)
        {
            sanitized = sanitized[..MaxAssemblyFolderLength].Trim();
        }

        sanitized = sanitized.TrimEnd('.', ' ');
        if (string.IsNullOrWhiteSpace(sanitized)) sanitized = "Assembly";

        if (ReservedDeviceNames.Contains(sanitized))
        {
            sanitized += "_";
        }

        return sanitized;
    }

    public static string BuildEntryKey(string partNumber, string skid, string segment, string description, string extDescription = "")
    {
        return string.Join("|", partNumber, skid, segment, description, extDescription ?? "");
    }

    public ShellFolderPlan BuildPlan(IEnumerable<BomRow> rows, string? shellRoot = null)
    {
        var p391Rows = rows.Where(r => Is391Part(r.PartNumber)).ToList();

        var excluded = new List<BomRow>();
        var misplaced = new List<BomRow>();
        var skipped = new List<(BomRow Row, string Reason)>();
        var entries = new List<ShellFolderEntry>();

        var seenDedupeKeys = new HashSet<string>();
        var folderNameUse = new Dictionary<string, string>();

        foreach (var row in p391Rows)
        {
            if (IsMisplacedCoilPanel(row))
            {
                misplaced.Add(row);
                continue;
            }
            if (IsExcludedFromShellMaker(row))
            {
                excluded.Add(row);
                continue;
            }

            string? skidNum = ParseSkidNumber(row.Skid);
            string? segmentFolder = ResolveSegmentFolder(row.Skid, row.Segment);

            if (skidNum == null || segmentFolder == null)
            {
                skipped.Add((row, skidNum == null ? "unrecognized skid" : "unmatched segment"));
                continue;
            }

            string assemblyFolder = SanitizeAssemblyFolderName(row.Description, row.ExtDescription);
            string partNumber = row.PartNumber.Trim();
            string dedupeKey = $"{partNumber}|{row.Skid}|{segmentFolder}|{assemblyFolder}";

            if (seenDedupeKeys.Contains(dedupeKey)) continue;
            seenDedupeKeys.Add(dedupeKey);

            string segmentPath = $"Shell/Skid {skidNum}/{segmentFolder}";
            string useKey = $"{segmentPath}|{assemblyFolder}";

            string relativePath = $"{segmentPath}/{assemblyFolder}";
            if (folderNameUse.TryGetValue(useKey, out string? priorPart) && priorPart != partNumber)
            {
                relativePath = $"{segmentPath}/{assemblyFolder} [{partNumber}]";
            }
            else
            {
                folderNameUse[useKey] = partNumber;
            }

            string finalAssemblyFolderName = relativePath.Split('/').Last();
            string? absolutePath = !string.IsNullOrWhiteSpace(shellRoot)
                ? Path.Combine(shellRoot, relativePath.Replace('/', Path.DirectorySeparatorChar))
                : null;

            entries.Add(new ShellFolderEntry
            {
                EntryKey = BuildEntryKey(partNumber, row.Skid, row.Segment, row.Description, row.ExtDescription),
                PartNumber = partNumber,
                Quantity = row.Quantity,
                Unit = row.Unit,
                Skid = row.Skid,
                Segment = row.Segment,
                Description = row.Description,
                ExtDescription = row.ExtDescription,
                SegmentFolder = segmentFolder,
                AssemblyFolder = finalAssemblyFolderName,
                RelativePath = relativePath,
                AbsolutePath = absolutePath,
                IsCustomSq = IsCustomSqAssembly(row)
            });
        }

        entries.Sort((a, b) =>
        {
            string skidA = ParseSkidNumber(a.Skid) ?? "";
            string skidB = ParseSkidNumber(b.Skid) ?? "";
            int cmp = string.Compare(skidA, skidB, StringComparison.OrdinalIgnoreCase);
            if (cmp != 0) return cmp;

            cmp = string.Compare(a.SegmentFolder, b.SegmentFolder, StringComparison.OrdinalIgnoreCase);
            if (cmp != 0) return cmp;

            return string.Compare(a.AssemblyFolder, b.AssemblyFolder, StringComparison.OrdinalIgnoreCase);
        });

        return new ShellFolderPlan
        {
            Entries = entries,
            Excluded = excluded,
            Misplaced = misplaced,
            Skipped = skipped,
            Stats = new ShellFolderPlanStats
            {
                Total391Rows = p391Rows.Count,
                FolderCount = entries.Count,
                ExcludedCount = excluded.Count,
                MisplacedCount = misplaced.Count,
                SkippedCount = skipped.Count,
                CustomSqCount = entries.Count(e => e.IsCustomSq)
            }
        };
    }

    public static int CreateShellFolders(string rootPath, IEnumerable<ShellFolderEntry> entries)
    {
        return CreateShellFolders(rootPath, entries.Select(e => e.RelativePath));
    }

    public static int CreateShellFolders(string rootPath, IEnumerable<string> relativePaths)
    {
        if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
        {
            throw new DirectoryNotFoundException("Shell root folder not found.");
        }

        string fullRoot = Path.GetFullPath(rootPath);
        string rootWithSep = fullRoot.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.OrdinalIgnoreCase)
            ? fullRoot
            : fullRoot + Path.DirectorySeparatorChar;

        int count = 0;
        foreach (string relativePath in relativePaths)
        {
            if (string.IsNullOrWhiteSpace(relativePath)) continue;

            string normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);

            if (Path.IsPathRooted(normalized) || normalized.StartsWith(@"\\"))
            {
                throw new ArgumentException($"Path traversal attempt rejected: '{relativePath}' is an absolute path.");
            }

            string safeRelative = normalized.TrimStart(Path.DirectorySeparatorChar);

            string fullPath = Path.GetFullPath(Path.Combine(fullRoot, safeRelative));

            if (!fullPath.Equals(fullRoot, StringComparison.OrdinalIgnoreCase) &&
                !fullPath.StartsWith(rootWithSep, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"Path traversal attempt rejected: '{relativePath}' resolves to '{fullPath}', outside root '{rootPath}'.");
            }

            Directory.CreateDirectory(fullPath);
            count++;
        }
        return count;
    }
}

