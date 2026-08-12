using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using UnitProgressTracker.Core.Models;

namespace UnitProgressTracker.Core.Services;

[SupportedOSPlatform("windows")]
public class GeometryScanner
{
    private static readonly HashSet<string> SkipFolderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "oldversions", "archive", "archived", "backup", "backups", "temp", "tmp", "_restore", ".unit-surface-viewer"
    };

    public static string[] GetScannableFiles(string rootFolder)
    {
        var results = new List<string>();
        if (string.IsNullOrWhiteSpace(rootFolder) || !Directory.Exists(rootFolder))
            return Array.Empty<string>();

        void WalkDirectory(string currentDir, int depth)
        {
            if (depth > 12) return;

            string dirName = Path.GetFileName(currentDir);
            if (dirName.StartsWith(".") || SkipFolderNames.Contains(dirName))
                return;

            try
            {
                var files = Directory.GetFiles(currentDir);
                foreach (var file in files)
                {
                    string ext = Path.GetExtension(file);
                    string name = Path.GetFileName(file);

                    if (ext.Equals(".json", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!file.EndsWith(".uptproj", StringComparison.OrdinalIgnoreCase) &&
                            !file.Contains(".unit-surface-viewer", StringComparison.OrdinalIgnoreCase))
                        {
                            results.Add(file);
                        }
                    }
                    else if (ext.Equals(".iam", StringComparison.OrdinalIgnoreCase))
                    {
                        // Filter for surface assemblies (e.g. 391Z*.iam) and ignore 391-*.iam
                        if (name.StartsWith("391Z", StringComparison.OrdinalIgnoreCase) &&
                            !name.StartsWith("391-", StringComparison.OrdinalIgnoreCase))
                        {
                            results.Add(file);
                        }
                    }
                }

                var subDirs = Directory.GetDirectories(currentDir);
                foreach (var subDir in subDirs)
                {
                    WalkDirectory(subDir, depth + 1);
                }
            }
            catch
            {
                // Ignore inaccessible directories
            }
        }

        WalkDirectory(rootFolder, 0);
        return results.Distinct().ToArray();
    }

    /// <summary>
    /// Asynchronously scans a folder for surface assembly files (.iam / .json) via headless COM / Apprentice,
    /// without attaching to active Inventor GUI processes. Skipping OldVersions and backup folders.
    /// Progress reports and cancellation tokens are fully supported.
    /// </summary>
    public static async Task<List<SurfaceModel>> ScanIamFolderAsync(
        string folderPath,
        IProgress<ProgressReport>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var result = await ScanIamFolderWithDiagnosticsAsync(folderPath, progress, cancellationToken);
        return result.AcceptedSurfaces;
    }

    public static async Task<GeometryScanResult> ScanIamFolderWithDiagnosticsAsync(
        string folderPath,
        IProgress<ProgressReport>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            return new GeometryScanResult
            {
                FatalFailureKind = ScanFailureKind.InaccessibleFolder,
                Summary = "The selected scan folder is missing or inaccessible. Active project state was preserved."
            };
        }

        string[] allFilePaths;
        try
        {
            allFilePaths = GetScannableFiles(folderPath);
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            return new GeometryScanResult
            {
                FatalFailureKind = ScanFailureKind.InaccessibleFolder,
                Summary = "The selected scan folder could not be enumerated. Check access and try again. Active project state was preserved."
            };
        }

        var accepted = new List<SurfaceModel>();
        var failed = new List<ScanFileDiagnostic>();
        var skipped = new List<ScanFileDiagnostic>();
        var seenNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int total = allFilePaths.Length;

        for (int i = 0; i < allFilePaths.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string file = allFilePaths[i];
            string fileName = Path.GetFileName(file);
            progress?.Report(new ProgressReport(
                Scanned: i,
                Total: total,
                CurrentFile: fileName,
                StatusMessage: $"Scanning {i + 1} of {total}: {fileName}"
            ));

            SurfaceModel? model = null;
            ScanFileDiagnostic? failure = null;
            try
            {
                if (file.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    (model, failure) = await Task.Run(() => ScanJsonFileWithDiagnostic(file, folderPath), cancellationToken);
                }
                else
                {
                    model = await ScanIamFileAsync(file, folderPath, cancellationToken);
                    if (model == null)
                    {
                        failure = new ScanFileDiagnostic
                        {
                            FileIdentifier = fileName,
                            Kind = ScanFailureKind.InventorComFailure,
                            Message = $"{fileName}: Inventor/Apprentice did not return readable DOCUMENT_CONFIG_JSON geometry."
                        };
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                failure = new ScanFileDiagnostic
                {
                    FileIdentifier = fileName,
                    Kind = ScanFailureKind.FileReadFailure,
                    Message = $"{fileName}: the file could not be read."
                };
            }

            if (failure != null)
            {
                failed.Add(failure);
                continue;
            }

            if (model != null && !seenNumbers.Add(model.SurfaceNumber))
            {
                skipped.Add(new ScanFileDiagnostic
                {
                    FileIdentifier = fileName,
                    Kind = ScanFailureKind.DuplicateIdentity,
                    Message = $"{fileName}: duplicate surface identity '{model.SurfaceNumber}' was skipped for review."
                });
                continue;
            }

            if (model != null) accepted.Add(model);
        }

        progress?.Report(new ProgressReport(
            Scanned: total,
            Total: total,
            CurrentFile: string.Empty,
            StatusMessage: "Scan complete."
        ));

        accepted.Sort((a, b) => string.Compare(a.SurfaceNumber, b.SurfaceNumber, StringComparison.OrdinalIgnoreCase));
        return new GeometryScanResult
        {
            AcceptedSurfaces = accepted,
            FailedFiles = failed,
            SkippedFiles = skipped,
            DiscoveredFileCount = total,
            Summary = $"Scan reviewed {total} files: {accepted.Count} accepted, {skipped.Count} skipped, {failed.Count} failed."
        };
    }

    public static Task<SurfaceModel?> ScanIamFileAsync(
        string iamPath,
        string rootFolder = "",
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            // 1. Check for adjacent .json sidecar for instant lightweight scanning
            string jsonPath = Path.ChangeExtension(iamPath, ".json");
            if (File.Exists(jsonPath))
            {
                var jsonModel = ScanJsonFile(jsonPath, rootFolder);
                if (jsonModel != null) return jsonModel;
            }

            // 2. Fallback to background/invisible COM attribute reading
            string? json = InventorComReader.TryReadConfigJsonAttribute(iamPath);
            if (!string.IsNullOrWhiteSpace(json))
            {
                return ParseConfigJson(json, iamPath, rootFolder, "iam");
            }

            return null;
        }, cancellationToken);
    }

    public static List<SurfaceModel> ScanJsonFolder(string folderPath)
    {
        return ScanIamFolderAsync(folderPath).GetAwaiter().GetResult();
    }

    private static SurfaceModel? ScanJsonFile(string jsonPath, string rootFolder)
    {
        try
        {
            if (!File.Exists(jsonPath)) return null;
            string text = File.ReadAllText(jsonPath);
            return ParseConfigJson(text, jsonPath, rootFolder, "json");
        }
        catch
        {
            return null;
        }
    }

    private static (SurfaceModel? Model, ScanFileDiagnostic? Failure) ScanJsonFileWithDiagnostic(string jsonPath, string rootFolder)
    {
        string fileName = Path.GetFileName(jsonPath);
        string text;
        try
        {
            text = File.ReadAllText(jsonPath);
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            return (null, new ScanFileDiagnostic
            {
                FileIdentifier = fileName,
                Kind = ScanFailureKind.FileReadFailure,
                Message = $"{fileName}: the JSON file could not be read."
            });
        }

        try
        {
            using var document = JsonDocument.Parse(text);
            if (!document.RootElement.TryGetProperty("configuration", out _))
            {
                return (null, new ScanFileDiagnostic
                {
                    FileIdentifier = fileName,
                    Kind = ScanFailureKind.MissingGeometry,
                    Message = $"{fileName}: required configuration geometry is missing."
                });
            }
        }
        catch (JsonException)
        {
            return (null, new ScanFileDiagnostic
            {
                FileIdentifier = fileName,
                Kind = ScanFailureKind.JsonParseFailure,
                Message = $"{fileName}: invalid JSON could not be parsed."
            });
        }

        var model = ParseConfigJson(text, jsonPath, rootFolder, "json");
        return model != null
            ? (model, null)
            : (null, new ScanFileDiagnostic
            {
                FileIdentifier = fileName,
                Kind = ScanFailureKind.MissingGeometry,
                Message = $"{fileName}: no valid renderable geometry was found."
            });
    }

    public static SurfaceModel? ParseConfigJson(string jsonText, string filePath, string rootFolder, string sourceType)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonText);
            if (!doc.RootElement.TryGetProperty("configuration", out var conf)) return null;

            var boxes = ExtractBoxesFromConfig(conf);
            if (boxes.Count == 0) return null;

            string surfaceNumber = Path.GetFileNameWithoutExtension(filePath);
            string partNumber = conf.TryGetProperty("partNumber", out var pn) ? pn.GetString() ?? surfaceNumber : surfaceNumber;
            string surfaceType = conf.TryGetProperty("surfaceType", out var st) ? st.GetString() ?? "" : "";
            string side = conf.TryGetProperty("surfaceUnitSide", out var sus) ? sus.GetString() ?? "" : "";
            string configKind = conf.TryGetProperty("configurationKind", out var ck) ? ck.GetString() ?? "" : "";

            int rawSkid = 0;
            if (conf.TryGetProperty("skidId", out var skElem))
            {
                if (skElem.ValueKind == JsonValueKind.Number && skElem.TryGetInt32(out int idVal))
                    rawSkid = idVal;
                else if (skElem.ValueKind == JsonValueKind.String && int.TryParse(skElem.GetString(), out int parsedVal))
                    rawSkid = parsedVal;
            }
            else if (conf.TryGetProperty("skidNumber", out var skNumElem))
            {
                if (skNumElem.ValueKind == JsonValueKind.Number && skNumElem.TryGetInt32(out int numVal))
                    rawSkid = numVal;
                else if (skNumElem.ValueKind == JsonValueKind.String && int.TryParse(skNumElem.GetString(), out int parsedVal2))
                    rawSkid = parsedVal2;
            }

            // Skids start at 0 in config data: 0 or null -> 1, 1 -> 2, 2 -> 3...
            int displaySkidId = rawSkid >= 0 ? rawSkid + 1 : 1;

            var jobContext = ExtractJobContext(conf);
            var casingSpec = ExtractCasingSpec(conf);
            var openings = ExtractOpenings(conf);
            var bulkheadPatterns = ExtractBulkheadHolePatterns(conf);
            var bulkheadChannels = BulkheadChannelCalculator.CalculateChannels(bulkheadPatterns, boxes, side);

            return new SurfaceModel
            {
                SurfaceNumber = surfaceNumber,
                FilePath = filePath,
                RelativePath = !string.IsNullOrEmpty(rootFolder) ? Path.GetRelativePath(rootFolder, filePath) : filePath,
                SourceType = sourceType,
                PartNumber = partNumber,
                SurfaceType = surfaceType,
                SurfaceUnitSide = side,
                ConfigurationKind = configKind,
                SkidId = displaySkidId,
                SkidNumber = $"Skid {displaySkidId}",
                Boxes = boxes,
                JobContext = jobContext,
                CasingSpec = casingSpec,
                Openings = openings,
                BulkheadHolePatterns = bulkheadPatterns,
                BulkheadChannels = bulkheadChannels
            };
        }
        catch
        {
            return null;
        }
    }

    private static JobContextModel ExtractJobContext(JsonElement conf)
    {
        string salesOrder = conf.TryGetProperty("salesOrderNumber", out var so) ? so.GetString() ?? "" : "";
        string jobName = conf.TryGetProperty("jobName", out var jn) ? jn.GetString() ?? "" : "";
        string mfgLoc = conf.TryGetProperty("mfgLocation", out var ml) ? ml.GetString() ?? "" : "";
        string prodType = conf.TryGetProperty("productType", out var pt) ? pt.GetString() ?? "" : "";
        string unitType = conf.TryGetProperty("unitType", out var ut) ? ut.GetString() ?? "" : "";
        string housing = conf.TryGetProperty("housingStyle", out var hs) ? hs.GetString() ?? "" : "";
        string seq = conf.TryGetProperty("skidSegmentSequence", out var sss) ? sss.GetString() ?? "" : "";

        string comNumber = "";
        string unitTag = "";
        int unitNumber = 0;

        void ParseUnitDescElement(JsonElement ud)
        {
            if (ud.TryGetProperty("comNumber", out var cn))
            {
                if (cn.ValueKind == JsonValueKind.String) comNumber = cn.GetString() ?? "";
                else if (cn.ValueKind == JsonValueKind.Number) comNumber = cn.GetRawText();
            }
            if (ud.TryGetProperty("unitTag", out var utag)) unitTag = utag.GetString() ?? "";
            if (ud.TryGetProperty("unitNumber", out var un) && un.ValueKind == JsonValueKind.Number) unitNumber = un.GetInt32();
        }

        if (conf.TryGetProperty("unitDescriptorList", out var topUdList) && topUdList.ValueKind == JsonValueKind.Array)
        {
            foreach (var ud in topUdList.EnumerateArray())
            {
                ParseUnitDescElement(ud);
                if (!string.IsNullOrEmpty(comNumber)) break;
            }
        }

        if (string.IsNullOrEmpty(comNumber) && conf.TryGetProperty("surfaceSegmentList", out var segList) && segList.ValueKind == JsonValueKind.Array)
        {
            foreach (var seg in segList.EnumerateArray())
            {
                if (seg.TryGetProperty("unitDescriptorList", out var udList) && udList.ValueKind == JsonValueKind.Array)
                {
                    foreach (var ud in udList.EnumerateArray())
                    {
                        ParseUnitDescElement(ud);
                        if (!string.IsNullOrEmpty(comNumber)) break;
                    }
                }
                if (!string.IsNullOrEmpty(comNumber)) break;
            }
        }

        return new JobContextModel(
            SalesOrderNumber: salesOrder,
            JobName: jobName,
            ComNumber: comNumber,
            UnitTag: unitTag,
            UnitNumber: unitNumber,
            MfgLocation: mfgLoc,
            ProductType: prodType,
            UnitType: unitType,
            HousingStyle: housing,
            SkidSegmentSequence: seq
        );
    }

    private static CasingSpecModel ExtractCasingSpec(JsonElement conf)
    {
        double? GetDoubleOpt(JsonElement parent, string prop)
        {
            if (parent.TryGetProperty(prop, out var val) && val.ValueKind == JsonValueKind.Number)
                return val.GetDouble();
            return null;
        }

        string FormatMat(JsonElement parent, string matProp, string gaugeProp)
        {
            string mat = parent.TryGetProperty(matProp, out var m) ? m.GetString() ?? "" : "";
            if (string.IsNullOrWhiteSpace(mat)) return "N/A";

            if (parent.TryGetProperty(gaugeProp, out var g) && g.ValueKind == JsonValueKind.Number)
            {
                int gauge = (int)g.GetDouble();
                return $"{gauge} GA {mat}";
            }
            return mat;
        }

        if (conf.TryGetProperty("surfaceSegmentList", out var segList) && segList.ValueKind == JsonValueKind.Array)
        {
            foreach (var seg in segList.EnumerateArray())
            {
                return new CasingSpecModel(
                    WallThicknessTop: GetDoubleOpt(seg, "wallThickness_Top"),
                    WallThicknessBottom: GetDoubleOpt(seg, "wallThickness_Bottom"),
                    WallThicknessLeft: GetDoubleOpt(seg, "wallThickness_Left"),
                    WallThicknessRight: GetDoubleOpt(seg, "wallThickness_Right"),
                    WallThicknessFront: GetDoubleOpt(seg, "wallThickness_Front"),
                    WallThicknessRear: GetDoubleOpt(seg, "wallThickness_Rear"),

                    SkinTop: FormatMat(seg, "skinMaterialType_Top", "skinMaterialGauge_Top"),
                    SkinBottom: FormatMat(seg, "skinMaterialType_Bottom", "skinMaterialGauge_Bottom"),
                    SkinLeft: FormatMat(seg, "skinMaterialType_Left", "skinMaterialGauge_Left"),
                    SkinRight: FormatMat(seg, "skinMaterialType_Right", "skinMaterialGauge_Right"),
                    SkinFront: FormatMat(seg, "skinMaterialType_Front", "skinMaterialGauge_Front"),
                    SkinRear: FormatMat(seg, "skinMaterialType_Rear", "skinMaterialGauge_Rear"),

                    LinerTop: FormatMat(seg, "linerMaterialType_Top", "linerMaterialGauge_Top"),
                    LinerBottom: FormatMat(seg, "linerMaterialType_Bottom", "linerMaterialGauge_Bottom"),
                    LinerLeft: FormatMat(seg, "linerMaterialType_Left", "linerMaterialGauge_Left"),
                    LinerRight: FormatMat(seg, "linerMaterialType_Right", "linerMaterialGauge_Right"),
                    LinerFront: FormatMat(seg, "linerMaterialType_Front", "linerMaterialGauge_Front"),
                    LinerRear: FormatMat(seg, "linerMaterialType_Rear", "linerMaterialGauge_Rear"),

                    FloorMaterialType: seg.TryGetProperty("floorMaterialType", out var fmt) ? fmt.GetString() ?? "" : "",
                    FloorMaterialGauge: seg.TryGetProperty("floorMaterialGauge", out var fmg) && fmg.ValueKind == JsonValueKind.Number ? (int)fmg.GetDouble() : 0,
                    FloorPaintType: seg.TryGetProperty("floorPaintType", out var fpt) ? fpt.GetString() ?? "" : ""
                );
            }
        }

        return new CasingSpecModel();
    }

    private static List<OpeningModel> ExtractOpenings(JsonElement conf)
    {
        var result = new List<OpeningModel>();
        if (!conf.TryGetProperty("surfaceSegmentList", out var segList) || segList.ValueKind != JsonValueKind.Array)
            return result;

        foreach (var seg in segList.EnumerateArray())
        {
            string segType = seg.TryGetProperty("segmentType", out var st) ? st.GetString() ?? "" : "";
            if (seg.TryGetProperty("openingList", out var opList) && opList.ValueKind == JsonValueKind.Array)
            {
                foreach (var op in opList.EnumerateArray())
                {
                    string opType = op.TryGetProperty("openingType", out var ot) ? ot.GetString() ?? "" : "";
                    string shape = op.TryGetProperty("openingShape", out var os) ? os.GetString() ?? "" : "";
                    string side = op.TryGetProperty("unitSide", out var us) ? us.GetString() ?? "" : "";
                    string doorPn = op.TryGetProperty("doorPartNumber", out var dpn) ? dpn.GetString() ?? "" : "";

                    GeometryBox? geomBox = null;
                    if (op.TryGetProperty("geometry", out var geom))
                    {
                        geomBox = ParseGeometryBox(geom);
                    }

                    result.Add(new OpeningModel(
                        SegmentType: segType,
                        OpeningType: opType,
                        OpeningShape: shape,
                        UnitSide: side,
                        DoorPartNumber: doorPn,
                        Geometry: geomBox
                    ));
                }
            }
        }

        return result;
    }

    private static List<BulkheadHolePatternModel> ExtractBulkheadHolePatterns(JsonElement conf)
    {
        var result = new List<BulkheadHolePatternModel>();
        if (!conf.TryGetProperty("surfaceSegmentList", out var segList) || segList.ValueKind != JsonValueKind.Array)
            return result;

        foreach (var seg in segList.EnumerateArray())
        {
            string segType = seg.TryGetProperty("segmentType", out var st) ? st.GetString() ?? "" : "";
            if (seg.TryGetProperty("bulkheadList", out var bhList) && bhList.ValueKind == JsonValueKind.Array)
            {
                foreach (var bh in bhList.EnumerateArray())
                {
                    string bhPart = "";
                    string bhDesc = "";

                    if (bh.TryGetProperty("partInfo", out var pi))
                    {
                        if (pi.TryGetProperty("partNumber", out var pn)) bhPart = pn.GetString() ?? "";
                        if (pi.TryGetProperty("description", out var desc)) bhDesc = desc.GetString() ?? "";
                    }

                    if (bh.TryGetProperty("holePatternList", out var hpList) && hpList.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var hp in hpList.EnumerateArray())
                        {
                            string side = hp.TryGetProperty("unitSide", out var us) ? us.GetString() ?? "" : "";
                            int index = hp.TryGetProperty("index", out var idx) && idx.ValueKind == JsonValueKind.Number ? idx.GetInt32() : 0;
                            double doa = hp.TryGetProperty("doaOffset", out var d) && d.ValueKind == JsonValueKind.Number ? d.GetDouble() : 0.0;
                            double wo = hp.TryGetProperty("widthOffset", out var w) && w.ValueKind == JsonValueKind.Number ? w.GetDouble() : 0.0;
                            double wqty = hp.TryGetProperty("widthQTY", out var wq) && wq.ValueKind == JsonValueKind.Number ? wq.GetDouble() : 0.0;
                            double wsp = hp.TryGetProperty("widthSpacing", out var ws) && ws.ValueKind == JsonValueKind.Number ? ws.GetDouble() : 0.0;
                            double hd = hp.TryGetProperty("holeDiameter", out var h) && h.ValueKind == JsonValueKind.Number ? h.GetDouble() : 0.0;

                            result.Add(new BulkheadHolePatternModel(
                                SegmentType: segType,
                                BulkheadPartNumber: bhPart,
                                BulkheadDescription: bhDesc,
                                UnitSide: side,
                                Index: index,
                                DoaOffset: doa,
                                WidthOffset: wo,
                                WidthQty: wqty,
                                WidthSpacing: wsp,
                                HoleDiameter: hd
                            ));
                        }
                    }
                }
            }
        }

        return result;
    }

    public static List<GeometryBox> ExtractBoxesFromConfig(JsonElement conf)
    {
        var boxes = new List<GeometryBox>();

        void ProcessGeometryList(JsonElement parent, string listPropName)
        {
            if (parent.TryGetProperty(listPropName, out var listElem) && listElem.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in listElem.EnumerateArray())
                {
                    if (item.TryGetProperty("geometry", out var geom))
                    {
                        var box = ParseGeometryBox(geom);
                        if (box != null) boxes.Add(box);
                    }
                    else
                    {
                        var box = ParseGeometryBox(item);
                        if (box != null) boxes.Add(box);
                    }
                }
            }
        }

        string[] parentKeys = { "roof", "wall", "unitBase", "floor", "endPanel", "door" };
        string[] listKeys = { "geometryList", "unitBaseGeometryList", "floorGeometryList" };

        foreach (var parentKey in parentKeys)
        {
            if (conf.TryGetProperty(parentKey, out var parent))
            {
                foreach (var listKey in listKeys)
                {
                    ProcessGeometryList(parent, listKey);
                }
            }
        }

        return boxes;
    }

    private static GeometryBox? ParseGeometryBox(JsonElement elem)
    {
        double GetCoordDouble(string prop)
        {
            if (elem.TryGetProperty(prop, out var val))
            {
                if (val.ValueKind == JsonValueKind.Number) return val.GetDouble();
                if (val.ValueKind == JsonValueKind.String && double.TryParse(val.GetString(), out double d)) return d;
            }
            return 0.0; // Position coordinates default to 0.0 if omitted
        }

        double GetLengthDouble(string prop)
        {
            if (elem.TryGetProperty(prop, out var val))
            {
                if (val.ValueKind == JsonValueKind.Number) return val.GetDouble();
                if (val.ValueKind == JsonValueKind.String && double.TryParse(val.GetString(), out double d)) return d;
            }
            return double.NaN; // Length dimensions are required
        }

        double x = GetCoordDouble("x");
        double y = GetCoordDouble("y");
        double z = GetCoordDouble("z");

        double xl = GetLengthDouble("xLength");
        double yl = GetLengthDouble("yLength");
        double zl = GetLengthDouble("zLength");

        if (double.IsNaN(xl) || double.IsNaN(yl) || double.IsNaN(zl))
            return null;

        if (xl <= 0 || yl <= 0 || zl <= 0) return null;

        return new GeometryBox(x, y, z, xl, yl, zl);
    }
}

