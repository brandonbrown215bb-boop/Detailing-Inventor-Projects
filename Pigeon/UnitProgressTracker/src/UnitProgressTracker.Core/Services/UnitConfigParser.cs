using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using UnitProgressTracker.Core.Models;

namespace UnitProgressTracker.Core.Services;

public class UnitConfigParser
{
    private class RawSegment
    {
        public string TypeCode { get; set; } = string.Empty;
        public string SegmentType { get; set; } = string.Empty;
        public string SegmentId { get; set; } = string.Empty;
        public string TagName { get; set; } = string.Empty;
    }

    public static string NormalizeSegmentCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return string.Empty;
        return Regex.Replace(code, @"[^A-Za-z0-9]", "").ToUpperInvariant();
    }

    public static string NormalizeSegmentGuid(string? guid)
    {
        if (string.IsNullOrWhiteSpace(guid)) return string.Empty;
        return guid.Trim().Trim('{', '}').ToUpperInvariant();
    }

    public static string SegmentPrefixFromBomColumn(string segment)
    {
        string seg = (segment ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(seg) || seg == "<--") return string.Empty;
        string[] parts = seg.Split(new[] { " - " }, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[0].Trim() : seg;
    }

    public static UnitConfigModel ParseUnitConfigXml(string xmlContent, string? sourceFile = null)
    {
        if (string.IsNullOrWhiteSpace(xmlContent))
        {
            throw new ArgumentException("XML content cannot be null or empty.", nameof(xmlContent));
        }

        XDocument doc;
        try
        {
            doc = XDocument.Parse(xmlContent);
        }
        catch (Exception ex)
        {
            throw new InvalidDataException($"Invalid Config.xml — could not parse XML: {ex.Message}", ex);
        }

        var warnings = new List<string>();

        // Find segmentList element (ignoring namespace)
        var segmentListEl = doc.Descendants().FirstOrDefault(e => e.Name.LocalName.Equals("segmentList", StringComparison.OrdinalIgnoreCase));
        var rawSegments = new List<RawSegment>();

        if (segmentListEl != null)
        {
            var typeCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var segEl in segmentListEl.Elements())
            {
                string localName = segEl.Name.LocalName;
                if (!localName.StartsWith("segment_", StringComparison.OrdinalIgnoreCase)) continue;

                string typeCode = localName.Substring("segment_".Length);
                typeCounts[typeCode] = typeCounts.TryGetValue(typeCode, out int cnt) ? cnt + 1 : 1;

                var segTypeEl = segEl.Descendants().FirstOrDefault(e => e.Name.LocalName.Equals("segmentType", StringComparison.OrdinalIgnoreCase));
                var segIdEl = segEl.Descendants().FirstOrDefault(e => e.Name.LocalName.Equals("segmentID", StringComparison.OrdinalIgnoreCase));

                rawSegments.Add(new RawSegment
                {
                    TypeCode = typeCode,
                    SegmentType = segTypeEl?.Value?.Trim() ?? typeCode,
                    SegmentId = NormalizeSegmentGuid(segIdEl?.Value)
                });
            }

            var typeIteration = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var seg in rawSegments)
            {
                if (typeCounts[seg.TypeCode] > 1)
                {
                    typeIteration[seg.TypeCode] = typeIteration.TryGetValue(seg.TypeCode, out int iter) ? iter + 1 : 1;
                    seg.TagName = $"{seg.TypeCode}-{typeIteration[seg.TypeCode]}";
                }
                else
                {
                    seg.TagName = seg.TypeCode;
                }
            }
        }
        else
        {
            warnings.Add("No segmentList found in Config.xml.");
        }

        // Find shippingSkidList element (ignoring namespace)
        var listEl = doc.Descendants().FirstOrDefault(e => e.Name.LocalName.Equals("shippingSkidList", StringComparison.OrdinalIgnoreCase));
        var skids = new List<SkidConfigModel>();

        if (listEl != null && listEl.Elements().Any())
        {
            var byId = new Dictionary<string, RawSegment>(StringComparer.OrdinalIgnoreCase);
            foreach (var seg in rawSegments)
            {
                if (!string.IsNullOrWhiteSpace(seg.SegmentId))
                {
                    byId[seg.SegmentId] = seg;
                }
            }

            int skidIndex = 0;
            foreach (var skidEl in listEl.Elements().Where(e => e.Name.LocalName.Equals("shippingSkid", StringComparison.OrdinalIgnoreCase)))
            {
                skidIndex++;

                var refs = new List<(int Seq, string Id)>();
                foreach (var refEl in skidEl.Descendants().Where(e => e.Name.LocalName.Equals("segmentReference", StringComparison.OrdinalIgnoreCase)))
                {
                    var seqEl = refEl.Descendants().FirstOrDefault(e => e.Name.LocalName.Equals("sequence", StringComparison.OrdinalIgnoreCase));
                    var sidEl = refEl.Descendants().FirstOrDefault(e => e.Name.LocalName.Equals("segmentID", StringComparison.OrdinalIgnoreCase));

                    int seq = int.TryParse(seqEl?.Value?.Trim(), out int sVal) ? sVal : 0;
                    string sid = NormalizeSegmentGuid(sidEl?.Value);
                    if (!string.IsNullOrWhiteSpace(sid))
                    {
                        refs.Add((seq, sid));
                    }
                }

                refs.Sort((a, b) => a.Seq.CompareTo(b.Seq));

                var skidSegments = new List<RawSegment>();
                foreach (var r in refs)
                {
                    if (byId.TryGetValue(r.Id, out var matchedSeg))
                    {
                        skidSegments.Add(matchedSeg);
                    }
                    else
                    {
                        warnings.Add($"Skid {skidIndex}: Segment ID {r.Id} not found in segmentList.");
                    }
                }

                if (skidSegments.Count > 0)
                {
                    var segmentModels = skidSegments.Select((seg, idx) => new SegmentConfigModel
                    {
                        Order = idx + 1,
                        TagName = seg.TagName,
                        TypeCode = seg.TypeCode,
                        SegmentType = seg.SegmentType,
                        Normalized = NormalizeSegmentCode(seg.TagName),
                        FolderPrefix = $"{(idx + 1):D2} {seg.TagName}"
                    }).ToList();

                    skids.Add(new SkidConfigModel
                    {
                        Id = skids.Count + 1,
                        Bracket = string.Join("-", skidSegments.Select(s => s.TagName)),
                        Segments = segmentModels
                    });
                }
            }
        }
        else
        {
            warnings.Add("No shippingSkidList found in Config.xml.");
        }

        if (skids.Count == 0)
        {
            throw new InvalidDataException("Config.xml has no valid shipping skids — cannot map BOM segments.");
        }

        var projIdEl = doc.Descendants().FirstOrDefault(e => e.Name.LocalName.Equals("projectID", StringComparison.OrdinalIgnoreCase));
        string? projectId = projIdEl?.Value?.Trim();

        return new UnitConfigModel
        {
            SourceFile = sourceFile,
            ImportedAt = DateTime.UtcNow.ToString("o"),
            ProjectId = projectId,
            Warnings = warnings,
            Skids = skids
        };
    }

    public static Dictionary<string, List<SegmentConfigModel>> BuildSkidSegmentMap(UnitConfigModel? unitConfig)
    {
        var map = new Dictionary<string, List<SegmentConfigModel>>(StringComparer.OrdinalIgnoreCase);
        if (unitConfig == null || unitConfig.Skids == null) return map;

        foreach (var skid in unitConfig.Skids)
        {
            string skidNum = skid.Id.ToString("D2");
            map[skidNum] = skid.Segments ?? new List<SegmentConfigModel>();
        }

        return map;
    }

    public static string? ResolveSegmentFolderFromConfig(string skidNum, string segment, UnitConfigModel? unitConfig)
    {
        if (unitConfig == null) return null;
        string prefix = SegmentPrefixFromBomColumn(segment);
        if (string.IsNullOrWhiteSpace(prefix)) return null;

        string normalized = NormalizeSegmentCode(prefix);
        if (string.IsNullOrWhiteSpace(normalized)) return null;

        string formattedSkidNum = int.TryParse(skidNum, out int val) ? val.ToString("D2") : skidNum.PadLeft(2, '0');
        var map = BuildSkidSegmentMap(unitConfig);

        if (!map.TryGetValue(formattedSkidNum, out var segments) || segments == null || segments.Count == 0)
        {
            return null;
        }

        // 1. Exact match by normalized tag name
        var match = segments.FirstOrDefault(s => s.Normalized.Equals(normalized, StringComparison.OrdinalIgnoreCase));
        if (match != null) return match.FolderPrefix;

        // 2. Match by type code (e.g. BOM specifies MB, config has MB-1)
        match = segments.FirstOrDefault(s => NormalizeSegmentCode(s.TypeCode).Equals(normalized, StringComparison.OrdinalIgnoreCase));
        if (match != null) return match.FolderPrefix;

        return null;
    }
}
