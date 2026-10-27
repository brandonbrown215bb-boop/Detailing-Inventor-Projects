using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnitConstructionVerifier.Models;

namespace UnitConstructionVerifier.Engine
{
    /// <summary>One row of a "BOM" table on a surface IDW (SEQ | QTY | PART # | DESC).</summary>
    public sealed class BomRow
    {
        public string Sheet       { get; set; } = string.Empty;
        public string Seq         { get; set; } = string.Empty;
        public string PartNumber  { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    /// <summary>What reading one surface's IDW found: its BOM rows, or why there are none.</summary>
    public sealed class SurfacePaperwork
    {
        public string SurfaceIamPath { get; set; } = string.Empty;
        /// <summary>Where the IDW was read from ("…\0061.idw" or "…\0061.ZIP"), empty when none was found.</summary>
        public string Source { get; set; } = string.Empty;
        public string Problem { get; set; } = string.Empty;
        public List<BomRow> Rows { get; } = new List<BomRow>();
    }

    public sealed class PaperworkFinding
    {
        public BomRow Row { get; set; } = new BomRow();
        public string IdwValue  { get; set; } = string.Empty;
        public string PartValue { get; set; } = string.Empty;
        public string Note      { get; set; } = string.Empty;
    }

    /// <summary>
    /// Compares the material line YCShell prints on surface IDW BOMs ("GALVANIZED STEEL, 22 GA") with the part as
    /// modelled. YCShell builds that line from the surface configuration, not the part, so a part edited away from the
    /// configuration (an SQ edit, or a UCV write to a changed expectation) keeps the configured material on paper.
    /// </summary>
    public static class PaperworkCheck
    {
        /// <summary>
        /// YCShell's printed wording (E_MaterialType UI descriptions, upper-cased) to UCV material codes. The YCShell
        /// enum name with '_' read as a space is the UCV code.
        /// </summary>
        private static readonly Dictionary<string, string> MaterialByWording = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["GALVANIZED STEEL"]                                       = "STL GALV",
            ["GALVANIZED STEEL, PRE-PAINT CHAMPAGNE"]                  = "STL GALV PPC",
            ["GALVANIZED STEEL, PRE-PAINT WHITE"]                      = "STL GALV PPW",
            ["GALVANIZED STEEL, ROLL FORM COIL, 2\" HOUSING CHANNEL"]  = "STL GALV2",
            ["GALVANIZED STEEL, ROLL FORM COIL, 3\" HOUSING CHANNEL"]  = "STL GALV3",
            ["GALVANIZED STEEL, ROLL FORM COIL, 4\" HOUSING CHANNEL"]  = "STL GALV4",
            ["GALVANIZED STEEL, ROLL FORM COIL, XT RACEWAY"]           = "STL GALVR",
            ["GALVANIZED STEEL, ROLL FORM COIL, XT SEAM CAP"]          = "STL GALVS",
            ["GALVANIZED STEEL, STRUCTURAL"]                           = "STRUCT GALV",
            ["STEEL, SHEET, HOT ROLLED"]                               = "STL HOT ROLL",
            ["STEEL, DIAMOND PLATE, HOT ROLLED"]                       = "STL DIA HR",
            ["ALUMINUM"]                                               = "ALM SHT",
            ["ALUMINUM, DIAMOND PLATE"]                                = "ALM DIA",
            ["ALUMINUM, SHEET, EMBOSSED"]                              = "ALM EMB",
            ["ALUMINUM, PLATE"]                                        = "ALM PLATE",
            ["STAINLESS STEEL, 304"]                                   = "SST304",
            ["STAINLESS STEEL, 304L"]                                  = "SST304L",
            ["STAINLESS STEEL, 316L"]                                  = "SST316L",
        };

        /// <summary>
        /// Splits a DESC cell into description and material. The material is the last line when it reads
        /// "&lt;wording&gt;, &lt;gauge&gt; GA" with known wording; channel rows ("C:SC 1.5 x 3") have none.
        /// </summary>
        public static bool TryReadMaterial(string desc, out string material, out string gauge, out string printed)
        {
            material = gauge = printed = string.Empty;
            string[] lines = (desc ?? string.Empty).Replace("\r", "").Split('\n')
                .Select(l => l.Trim()).Where(l => l.Length > 0).ToArray();
            if (lines.Length < 2) return false;

            printed = lines[lines.Length - 1];
            int comma = printed.LastIndexOf(',');
            if (comma < 0) return false;

            string wording = printed.Substring(0, comma).Trim();
            string tail = printed.Substring(comma + 1).Trim();
            if (tail.EndsWith("GA", StringComparison.OrdinalIgnoreCase)) tail = tail.Substring(0, tail.Length - 2).Trim();

            if (!MaterialByWording.TryGetValue(wording, out string? code) ||
                !double.TryParse(tail, NumberStyles.Any, CultureInfo.InvariantCulture, out double g))
                return false;

            material = code;
            gauge = g.ToString("0.###", CultureInfo.InvariantCulture);
            return true;
        }

        /// <summary>BOM rows whose printed gauge or material differs from the part, plus rows that can't be checked.</summary>
        public static List<PaperworkFinding> Compare(SurfacePaperwork paperwork, IEnumerable<IptProperties> unitParts)
        {
            var findings = new List<PaperworkFinding>();
            var parts = unitParts.ToList();

            foreach (BomRow row in paperwork.Rows)
            {
                if (!TryReadMaterial(row.Description, out string bomMaterial, out string bomGauge, out string printed))
                    continue;   // no material printed (channels, insulation)

                IptProperties? part =
                    parts.FirstOrDefault(p => SamePartNumber(p, row) && SameIam(p.OwnerIamPath, paperwork.SurfaceIamPath)) ??
                    parts.FirstOrDefault(p => SamePartNumber(p, row));
                string idwValue = $"{bomGauge} GA {bomMaterial}";

                if (part == null)
                {
                    findings.Add(new PaperworkFinding { Row = row, IdwValue = idwValue, Note = "part not found in the unit" });
                    continue;
                }

                string partGauge = MaterialsConfig.MapGauge(part.MtlGauge, part.YCMATL);
                string partMaterial = MaterialsConfig.MapMaterial(part.YCMATL);
                string partValue = $"{partGauge} GA {partMaterial}";

                if (!string.Equals(Norm(partGauge), bomGauge, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(partMaterial, bomMaterial, StringComparison.OrdinalIgnoreCase))
                {
                    findings.Add(new PaperworkFinding { Row = row, IdwValue = idwValue, PartValue = partValue });
                }
            }
            return findings;
        }

        public static string Report(IEnumerable<(SurfacePaperwork Paperwork, List<PaperworkFinding> Findings)> surfaces)
        {
            var sb = new StringBuilder();
            int mismatches = 0, checkedRows = 0;
            var list = surfaces.ToList();

            foreach (var (paperwork, findings) in list)
            {
                string name = System.IO.Path.GetFileNameWithoutExtension(paperwork.SurfaceIamPath);
                if (paperwork.Problem.Length > 0)
                {
                    sb.AppendLine($"{name}: {paperwork.Problem}");
                    continue;
                }

                int rows = paperwork.Rows.Count(r => TryReadMaterial(r.Description, out _, out _, out _));
                checkedRows += rows;
                mismatches += findings.Count;
                sb.AppendLine(findings.Count == 0
                    ? $"{name}: OK ({rows} rows with a material checked, from {System.IO.Path.GetFileName(paperwork.Source)})"
                    : $"{name}: {findings.Count} of {rows} rows differ (from {System.IO.Path.GetFileName(paperwork.Source)})");
                foreach (PaperworkFinding f in findings)
                {
                    string where = $"   {f.Row.Sheet} {f.Row.Seq} {f.Row.PartNumber}:";
                    sb.AppendLine(f.Note.Length > 0
                        ? $"{where} IDW {f.IdwValue}, {f.Note}"
                        : $"{where} IDW {f.IdwValue}, part {f.PartValue}");
                }
            }

            sb.Insert(0, $"Paperwork check: {mismatches} mismatched rows in {checkedRows} checked, {list.Count} surfaces." +
                         Environment.NewLine +
                         "IDW materials come from the surface configuration, not the part." + Environment.NewLine +
                         Environment.NewLine);
            return sb.ToString().TrimEnd();
        }

        private static bool SamePartNumber(IptProperties p, BomRow row) =>
            string.Equals(p.PartNumber?.Trim(), row.PartNumber.Trim(), StringComparison.OrdinalIgnoreCase);

        private static bool SameIam(string a, string b) =>
            string.Equals(a ?? string.Empty, b ?? string.Empty, StringComparison.OrdinalIgnoreCase);

        private static string Norm(string gauge) =>
            double.TryParse(gauge, NumberStyles.Any, CultureInfo.InvariantCulture, out double g)
                ? g.ToString("0.###", CultureInfo.InvariantCulture)
                : (gauge ?? string.Empty).Trim();
    }
}
