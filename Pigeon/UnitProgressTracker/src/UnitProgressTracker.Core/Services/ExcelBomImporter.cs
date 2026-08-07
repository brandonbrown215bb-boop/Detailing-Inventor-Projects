using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
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
    public Dictionary<string, int> KeptCountByPrefix { get; set; } = new();
    public int TotalRowCount => AllRows.Count;
    public int KeptCount => KeptRows.Count;
    public int DroppedCount => DroppedRows.Count;
}

public class ExcelBomImporter
{
    private static readonly BomFilterConfig Config = LoadConfig();

    private static BomFilterConfig LoadConfig()
    {
        try
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string configPath = Path.Combine(baseDir, "Services", "bom_filter_config.json");
            if (!File.Exists(configPath))
            {
                configPath = Path.Combine(baseDir, "bom_filter_config.json");
            }
            if (!File.Exists(configPath))
            {
                string asmLoc = Path.GetDirectoryName(typeof(ExcelBomImporter).Assembly.Location) ?? string.Empty;
                configPath = Path.Combine(asmLoc, "Services", "bom_filter_config.json");
                if (!File.Exists(configPath))
                {
                    configPath = Path.Combine(asmLoc, "bom_filter_config.json");
                }
            }

            if (File.Exists(configPath))
            {
                string json = File.ReadAllText(configPath);
                var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var loaded = JsonSerializer.Deserialize<BomFilterConfig>(json, opts);
                if (loaded != null) return loaded;
            }
        }
        catch
        {
            // Fallback to default in-memory config if loading fails
        }
        return new BomFilterConfig();
    }

    public static bool ShouldKeepRow(string partNumber, string segment)
    {
        return ShouldKeepRow(partNumber, segment, string.Empty);
    }

    public static bool ShouldKeepRow(string partNumber, string segment, string description)
    {
        if (string.IsNullOrWhiteSpace(partNumber)) return false;

        string pn = partNumber.Trim();
        string pnUpper = pn.ToUpperInvariant();
        string descUpper = description?.Trim().ToUpperInvariant() ?? string.Empty;

        // 1. Check AlwaysDropDescriptionKeywords
        if (!string.IsNullOrEmpty(descUpper))
        {
            if (Config.AlwaysDropDescriptionKeywords.Any(kw => descUpper.Contains(kw.ToUpperInvariant())))
            {
                return false;
            }
        }

        // 2. Check AlwaysKeepDescriptionKeywords
        if (!string.IsNullOrEmpty(descUpper))
        {
            if (Config.AlwaysKeepDescriptionKeywords.Any(kw => descUpper.Contains(kw.ToUpperInvariant())))
            {
                return true;
            }
        }

        // 3. Check DroppedPrefixes
        if (Config.DroppedPrefixes.Any(p => pnUpper.StartsWith(p.ToUpperInvariant())))
        {
            return false;
        }

        // 4. Check KeptPrefixes
        if (Config.KeptPrefixes.Any(p => pnUpper.StartsWith(p.ToUpperInvariant())))
        {
            return true;
        }

        return false;
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

        int colPn = 0, colQty = 1, colUnit = 2, colSkid = 3, colSeg = 4, colDesc = 5, colExtDesc = 6;
        bool headerFound = false;

        string currentSkid = string.Empty;
        string currentSegment = string.Empty;

        do
        {
            while (reader.Read())
            {
                int fieldCount = reader.FieldCount;
                if (fieldCount == 0) continue;

                // Inspect section headers for hierarchical context (Skid & Segment)
                string cellA = GetValueSafe(reader, 0, fieldCount);
                string cellB = GetValueSafe(reader, 1, fieldCount);

                if (cellA.Contains("SKID #") || cellA.Contains("SKID SHP") || cellB.Contains("SKID #") || cellB.Contains("SKID SHP"))
                {
                    string text = cellA.Length > 0 ? cellA : cellB;
                    var bracketMatch = System.Text.RegularExpressions.Regex.Match(text, @"\[([^\]]+)\]");
                    var skidNumMatch = System.Text.RegularExpressions.Regex.Match(text, @"SKID #?(\d+)");

                    if (skidNumMatch.Success)
                    {
                        string num = skidNumMatch.Groups[1].Value.PadLeft(2, '0');
                        currentSkid = bracketMatch.Success
                            ? $"{num} - [{bracketMatch.Groups[1].Value}]"
                            : num;
                    }
                    else if (text.Contains("SKID SHP", StringComparison.OrdinalIgnoreCase))
                    {
                        var numMatch = System.Text.RegularExpressions.Regex.Match(text, @"491-\d+-(\d+)");
                        string num = numMatch.Success ? numMatch.Groups[1].Value : "101";
                        currentSkid = bracketMatch.Success
                            ? $"{num} - [{bracketMatch.Groups[1].Value}]"
                            : $"{num} - []";
                    }
                }

                string segCandidate = cellB.Length > 0 ? cellB : cellA;
                if (segCandidate.StartsWith("Seg #", StringComparison.OrdinalIgnoreCase))
                {
                    string rawSeg = segCandidate;
                    int itemsIdx = rawSeg.LastIndexOf('(');
                    if (itemsIdx > 0)
                    {
                        rawSeg = rawSeg.Substring(0, itemsIdx).Trim();
                    }
                    if (rawSeg.StartsWith("Seg #", StringComparison.OrdinalIgnoreCase))
                    {
                        rawSeg = rawSeg.Substring(5).Trim();
                    }
                    currentSegment = rawSeg;
                }

                // Find the first non-empty cell in columns 0..3 for part number (handles tree-indented Grouped formats)
                int pnColIndex = -1;
                string partNumber = string.Empty;

                for (int c = 0; c < Math.Min(4, fieldCount); c++)
                {
                    string val = reader.GetValue(c)?.ToString()?.Trim() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(val)) continue;
                    if (val.StartsWith("[") || val.StartsWith("Seg #", StringComparison.OrdinalIgnoreCase)) break;

                    if (val.Equals("Part Number", StringComparison.OrdinalIgnoreCase) ||
                        val.Equals("PartNumber", StringComparison.OrdinalIgnoreCase) ||
                        val.Equals("Part #", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!headerFound)
                        {
                            for (int h = 0; h < fieldCount; h++)
                            {
                                string colVal = reader.GetValue(h)?.ToString()?.Trim().ToLowerInvariant() ?? string.Empty;
                                if (colVal.Contains("part number") || colVal.Contains("partnumber") || colVal.Equals("part #")) colPn = h;
                                else if (colVal.Equals("quantity") || colVal.Equals("qty")) colQty = h;
                                else if (colVal.Equals("unit")) colUnit = h;
                                else if (colVal.Equals("skid")) colSkid = h;
                                else if (colVal.Equals("segment") || colVal.Equals("seg")) colSeg = h;
                                else if (colVal.Contains("ext. description") || colVal.Contains("ext description")) colExtDesc = h;
                                else if (colVal.Contains("description") || colVal.Equals("desc")) colDesc = h;
                            }
                            headerFound = true;
                        }
                        pnColIndex = -1;
                        break;
                    }

                    partNumber = val;
                    pnColIndex = c;
                    break;
                }

                if (pnColIndex < 0 || string.IsNullOrWhiteSpace(partNumber))
                {
                    continue;
                }

                // Determine relative column offsets based on part number cell position
                int effectiveQtyCol = (pnColIndex == colPn) ? colQty : (pnColIndex + 1);
                int effectiveUnitCol = (pnColIndex == colPn) ? colUnit : -1;
                int effectiveSkidCol = (pnColIndex == colPn) ? colSkid : -1;
                int effectiveSegCol = (pnColIndex == colPn) ? colSeg : -1;
                int effectiveDescCol = (pnColIndex == colPn) ? colDesc : (pnColIndex + 3);
                int effectiveExtDescCol = (pnColIndex == colPn) ? colExtDesc : (pnColIndex + 4);

                string quantity = GetValueSafe(reader, effectiveQtyCol, fieldCount);
                string unit = GetValueSafe(reader, effectiveUnitCol, fieldCount);
                string skid = GetValueSafe(reader, effectiveSkidCol, fieldCount);
                string segment = GetValueSafe(reader, effectiveSegCol, fieldCount);
                string description = GetValueSafe(reader, effectiveDescCol, fieldCount);
                string extDescription = GetValueSafe(reader, effectiveExtDescCol, fieldCount);

                // Fallback to active hierarchical state context for Skid & Segment when absent or numeric
                if (string.IsNullOrWhiteSpace(skid) || System.Text.RegularExpressions.Regex.IsMatch(skid, @"^\d+$"))
                {
                    if (!string.IsNullOrWhiteSpace(currentSkid))
                    {
                        skid = currentSkid;
                    }
                }

                if (string.IsNullOrWhiteSpace(segment) || System.Text.RegularExpressions.Regex.IsMatch(segment, @"^\d+$"))
                {
                    if (!string.IsNullOrWhiteSpace(currentSegment))
                    {
                        segment = currentSegment;
                    }
                }

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

                string combinedDesc = row.CombinedDescription;
                if (ShouldKeepRow(partNumber, segment, combinedDesc))
                {
                    result.KeptRows.Add(row);

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
        } while (reader.NextResult());

        return result;
    }

    private static string GetValueSafe(IExcelDataReader reader, int index, int fieldCount)
    {
        if (index >= 0 && index < fieldCount)
        {
            return reader.GetValue(index)?.ToString()?.Trim() ?? string.Empty;
        }
        return string.Empty;
    }

    private static string GetPrefixKey(string partNumber)
    {
        int dashIdx = partNumber.IndexOf('-');
        if (dashIdx > 0) return partNumber.Substring(0, dashIdx + 1).ToUpperInvariant();
        return partNumber.Length >= 4 ? partNumber.Substring(0, 4).ToUpperInvariant() : partNumber.ToUpperInvariant();
    }
}
