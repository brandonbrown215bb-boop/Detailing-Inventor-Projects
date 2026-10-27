using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Inventor;
using UnitConstructionVerifier.Engine;
using Path = System.IO.Path;
using File = System.IO.File;

namespace UnitConstructionVerifier.Operations
{
    /// <summary>
    /// Reads the "BOM" tables from a surface's paperwork IDW. The IDW is named after the surface IAM and sits next to
    /// it, or only inside the surface's upload ZIP (YCShell keeps the loose copy only sometimes). Read only: a drawing
    /// UCV opens is closed without saving.
    /// </summary>
    public sealed class IdwBomReader
    {
        private const string BomTableTitle = "BOM";
        private readonly Inventor.Application _app;

        public IdwBomReader(Inventor.Application app) => _app = app;

        public SurfacePaperwork Read(string surfaceIamPath)
        {
            var result = new SurfacePaperwork { SurfaceIamPath = surfaceIamPath };
            string? tempFolder = null;
            try
            {
                string? idwPath = FindIdw(surfaceIamPath, out tempFolder, out string source);
                if (idwPath == null)
                {
                    result.Problem = "no IDW next to the IAM or in its ZIP";
                    return result;
                }
                result.Source = source;
                ReadTables(idwPath, result);
                if (result.Rows.Count == 0 && result.Problem.Length == 0) result.Problem = "IDW has no BOM tables";
            }
            catch (Exception ex)
            {
                DebugLogger.Log(ex, $"IdwBomReader.Read: {surfaceIamPath}");
                result.Problem = "could not read the IDW: " + ex.Message;
            }
            finally
            {
                if (tempFolder != null)
                {
                    try { Directory.Delete(tempFolder, recursive: true); } catch { /* still open or locked; temp is cleaned later */ }
                }
            }
            return result;
        }

        private static string? FindIdw(string surfaceIamPath, out string? tempFolder, out string source)
        {
            tempFolder = null;
            string folder = Path.GetDirectoryName(surfaceIamPath) ?? string.Empty;
            string baseName = Path.GetFileNameWithoutExtension(surfaceIamPath);

            string loose = Path.Combine(folder, baseName + ".idw");
            if (File.Exists(loose))
            {
                source = loose;
                return loose;
            }

            source = Path.Combine(folder, baseName + ".ZIP");
            if (!File.Exists(source)) return null;

            using (ZipArchive zip = ZipFile.OpenRead(source))
            {
                ZipArchiveEntry? entry = zip.Entries.FirstOrDefault(e =>
                    string.Equals(e.FullName, baseName + ".idw", StringComparison.OrdinalIgnoreCase));
                if (entry == null) return null;

                tempFolder = Path.Combine(Path.GetTempPath(), "UnitConstructionVerifier", "paperwork", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempFolder);
                string extracted = Path.Combine(tempFolder, entry.Name);
                entry.ExtractToFile(extracted);
                return extracted;
            }
        }

        private void ReadTables(string idwPath, SurfacePaperwork result)
        {
            DrawingDocument? drawing = null;
            bool opened = false;
            bool silent = _app.SilentOperation;
            try
            {
                try { drawing = _app.Documents.get_ItemByName(idwPath) as DrawingDocument; }
                catch { drawing = null; }

                if (drawing == null)
                {
                    // Silent: an IDW taken from the ZIP can't find its models, and the tables don't need them.
                    _app.SilentOperation = true;
                    drawing = _app.Documents.Open(idwPath, OpenVisible: false) as DrawingDocument;
                    opened = true;
                }
                if (drawing == null)
                {
                    result.Problem = "file is not a drawing";
                    return;
                }

                foreach (Sheet sheet in drawing.Sheets)
                {
                    foreach (CustomTable table in sheet.CustomTables)
                    {
                        if (!string.Equals(table.Title?.Trim(), BomTableTitle, StringComparison.OrdinalIgnoreCase)) continue;
                        ReadTable(sheet.Name, table, result);
                    }
                }
            }
            finally
            {
                if (opened && drawing != null)
                {
                    try { drawing.Close(SkipSave: true); } catch { }
                }
                _app.SilentOperation = silent;
            }
        }

        private static void ReadTable(string sheetName, CustomTable table, SurfacePaperwork result)
        {
            int seq = -1, partNumber = -1, desc = -1;
            for (int c = 1; c <= table.Columns.Count; c++)
            {
                string title = (table.Columns[c].Title ?? string.Empty).Trim().ToUpperInvariant();
                if (title == "SEQ") seq = c;
                else if (title.StartsWith("PART")) partNumber = c;
                else if (title.StartsWith("DESC")) desc = c;
            }
            if (partNumber < 0 || desc < 0) return;   // e.g. insulation: SEQ | TYPE | DESCRIPTION, no part numbers

            foreach (Row row in table.Rows)
            {
                result.Rows.Add(new BomRow
                {
                    Sheet       = sheetName,
                    Seq         = seq > 0 ? Cell(row, seq) : string.Empty,
                    PartNumber  = Cell(row, partNumber),
                    Description = Cell(row, desc),
                });
            }
        }

        private static string Cell(Row row, int column)
        {
            try { return row[column].Value ?? string.Empty; }
            catch { return string.Empty; }
        }
    }
}
