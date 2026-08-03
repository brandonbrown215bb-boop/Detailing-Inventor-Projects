using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ExcelDataReader;
using UnitProgressTracker.Core.Models;

namespace UnitProgressTracker.Core.Services;

public class BomImportResult
{
    public string SourceFilePath { get; set; } = string.Empty;
    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
    public List<BomRow> AllRows { get; set; } = new();
    public List<BomRow> KeptRows { get; set; } = new();
    public List<BomRow> DroppedRows { get; set; } = new();
    public BomRow? UnitHeader { get; set; }
    public Dictionary<string, int> KeptCountByPrefix { get; set; } = new();
    public int TotalRowCount => AllRows.Count;
    public int KeptCount => KeptRows.Count;
    public int DroppedCount => DroppedRows.Count;
}

public class ExcelBomImporter
{
    public static bool ShouldKeepRow(string partNumber, string segment)
    {
        if (string.IsNullOrWhiteSpace(partNumber)) return false;

        string pn = partNumber.Trim();
        string seg = segment?.Trim() ?? string.Empty;

        // Drop Hardware / Conduit / Stock Prefixes
        string[] dropHardwarePrefixes = new[] { "007-", "025-", "026-", "028-", "035-", "091-" };
        if (dropHardwarePrefixes.Any(p => pn.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        // Drop MAPICS Factor Multipliers
        if (pn.StartsWith("491-", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Keep Unit Header Root
        if (pn.StartsWith("5E", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Segment Placeholder Rule (<--)
        if (string.Equals(seg, "<--", StringComparison.Ordinal))
        {
            return pn.StartsWith("391-", StringComparison.OrdinalIgnoreCase);
        }

        // Keep Main Assembly & Subassembly Prefixes
        string[] keptPrefixes = new[] { "391-", "291-", "386-", "486-", "251-" };
        return keptPrefixes.Any(p => pn.StartsWith(p, StringComparison.OrdinalIgnoreCase));
    }

    public BomImportResult ImportBom(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            throw new FileNotFoundException($"BOM file not found: {filePath}", filePath ?? string.Empty);
        }

        using var stream = File.OpenRead(filePath);
        var result = ImportBom(stream, Path.GetFileName(filePath));
        result.SourceFilePath = filePath;
        return result;
    }

    public BomImportResult ImportBom(Stream stream, string fileName)
    {
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

        var result = new BomImportResult
        {
            SourceFilePath = fileName,
            ImportedAt = DateTime.UtcNow
        };

        if (stream == null || stream.Length == 0)
        {
            return result;
        }

        using var reader = fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)
            ? ExcelReaderFactory.CreateCsvReader(stream)
            : ExcelReaderFactory.CreateReader(stream);

        bool isFirstRow = true;
        while (reader.Read())
        {
            string partNumber = reader.GetValue(0)?.ToString()?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(partNumber)) continue;

            // Header row detection
            if (isFirstRow && (partNumber.Equals("Part Number", StringComparison.OrdinalIgnoreCase) ||
                               partNumber.Equals("PartNumber", StringComparison.OrdinalIgnoreCase) ||
                               partNumber.Equals("Part #", StringComparison.OrdinalIgnoreCase)))
            {
                isFirstRow = false;
                continue;
            }
            isFirstRow = false;

            string quantity = reader.FieldCount > 1 ? reader.GetValue(1)?.ToString()?.Trim() ?? string.Empty : string.Empty;
            string unit = reader.FieldCount > 2 ? reader.GetValue(2)?.ToString()?.Trim() ?? string.Empty : string.Empty;
            string skid = reader.FieldCount > 3 ? reader.GetValue(3)?.ToString()?.Trim() ?? string.Empty : string.Empty;
            string segment = reader.FieldCount > 4 ? reader.GetValue(4)?.ToString()?.Trim() ?? string.Empty : string.Empty;
            string description = reader.FieldCount > 5 ? reader.GetValue(5)?.ToString()?.Trim() ?? string.Empty : string.Empty;
            string extDescription = reader.FieldCount > 6 ? reader.GetValue(6)?.ToString()?.Trim() ?? string.Empty : string.Empty;

            var row = new BomRow
            {
                PartNumber = partNumber,
                Quantity = quantity,
                Unit = unit,
                Skid = skid,
                Segment = segment,
                Description = description,
                ExtDescription = extDescription
            };

            result.AllRows.Add(row);

            if (ShouldKeepRow(partNumber, segment))
            {
                result.KeptRows.Add(row);

                if (partNumber.StartsWith("5E", StringComparison.OrdinalIgnoreCase) && result.UnitHeader == null)
                {
                    result.UnitHeader = row;
                }

                string prefix = GetPrefixKey(partNumber);
                if (!result.KeptCountByPrefix.ContainsKey(prefix))
                {
                    result.KeptCountByPrefix[prefix] = 0;
                }
                result.KeptCountByPrefix[prefix]++;
            }
            else
            {
                result.DroppedRows.Add(row);
            }
        }

        return result;
    }

    private static string GetPrefixKey(string partNumber)
    {
        if (partNumber.StartsWith("5E", StringComparison.OrdinalIgnoreCase)) return "5E";
        int dashIdx = partNumber.IndexOf('-');
        if (dashIdx > 0) return partNumber.Substring(0, dashIdx + 1).ToUpperInvariant();
        return partNumber.Length >= 3 ? partNumber.Substring(0, 3).ToUpperInvariant() : partNumber.ToUpperInvariant();
    }
}
