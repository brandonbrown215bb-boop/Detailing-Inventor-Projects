using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UnitConstructionVerifier.Engine
{
    /// <summary>One surface's DOCUMENT_CONFIG_JSON as stored on its IAM.</summary>
    public sealed class SurfaceConfigText
    {
        public SurfaceConfigText(string iamPath, string json)
        {
            IamPath = iamPath;
            Json = json;
        }

        public string IamPath { get; }
        public string Json { get; }
    }

    /// <summary>What the review shows of one segment.</summary>
    public sealed class SegmentSummary
    {
        public string Id         { get; set; } = string.Empty;
        public string Type       { get; set; } = string.Empty;
        public string TopSkin    { get; set; } = string.Empty;
        public string TopLiner   { get; set; } = string.Empty;
        public string Insulation { get; set; } = string.Empty;

        public override string ToString() =>
            $"{(Type.Length > 0 ? Type : "(no type)")}: skin {Or(TopSkin)}, liner {Or(TopLiner)}, insulation {Or(Insulation)}";

        private static string Or(string s) => s.Length > 0 ? s : "none";
    }

    /// <summary>What restoring one roof's segments would change.</summary>
    public sealed class RoofRestorePlan
    {
        public string IamPath    { get; set; } = string.Empty;
        public string PartNumber { get; set; } = string.Empty;
        public List<SegmentSummary> Add    { get; } = new List<SegmentSummary>();
        public List<SegmentSummary> Remove { get; } = new List<SegmentSummary>();

        /// <summary>Order openings on the roof (unitSide Top) carried by the restored segments. Not added to the model.</summary>
        public int RoofOpenings { get; set; }

        /// <summary>Why the roof lost its segments, when the geometry shows it.</summary>
        public string Reason { get; set; } = string.Empty;

        /// <summary>Why the roof cannot be restored; empty when it can.</summary>
        public string Problem { get; set; } = string.Empty;

        /// <summary>Set when the nominal thickness is changed so YCShell's foam lookup finds a spec (see HasFoamSpec).</summary>
        public double? NominalFrom { get; set; }
        public double? NominalTo   { get; set; }

        /// <summary>Restored, but something will still come out wrong; shown in the review.</summary>
        public string Warning { get; set; } = string.Empty;

        /// <summary>The roof's config with its segments restored, for YCShell to parse and write.</summary>
        public string RestoredJson { get; set; } = string.Empty;

        public bool CanApply => Problem.Length == 0 && RestoredJson.Length > 0;
    }

    /// <summary>
    /// Puts back the order segments a roof lost when its YLength was raised (the server drops a roof's segments when its
    /// top is above theirs). Segments come verbatim from the other surface configs of the unit, which carry the same
    /// segments by ID. Nothing is invented: a roof with no order segment under it is reported, not filled.
    /// See docs/decisions/0005-ucv-restore-roof-segments.md.
    /// </summary>
    public static class RoofSegmentRestore
    {
        private const double Tolerance = 0.01;   // inches

        public static List<RoofRestorePlan> Plan(IEnumerable<SurfaceConfigText> surfaces)
        {
            var parsed = new List<(SurfaceConfigText Source, JObject Root, JObject Config)>();
            foreach (SurfaceConfigText surface in surfaces)
            {
                JObject? root = TryParse(surface.Json);
                if (root?["configuration"] is JObject config) parsed.Add((surface, root, config));
            }

            // Order segments: the ones with a skid, by ID. A segment's own fields are the same on every surface; its
            // bulkhead list is not (the order lists each surface's own bulkhead records), so it is not compared.
            var orderSegments = new Dictionary<string, JObject>(StringComparer.OrdinalIgnoreCase);
            var differing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (_, _, config) in parsed)
            {
                foreach (JObject segment in Segments(config))
                {
                    string id = Text(segment, "id");
                    if (id.Length == 0 || Text(segment, "skidID").Length == 0) continue;
                    if (!orderSegments.TryGetValue(id, out JObject? first)) orderSegments[id] = segment;
                    else if (!JToken.DeepEquals(OwnFields(first), OwnFields(segment))) differing.Add(id);
                }
            }

            var plans = new List<RoofRestorePlan>();
            foreach (var (source, root, config) in parsed)
            {
                if (config["roof"] is not JObject roof || roof["geometryList"]?.FirstOrDefault() is not JObject geometry) continue;

                string skid = Text(config, "skidID");
                Box roofBox = Box.Of(geometry);
                List<JObject> current = Segments(config).ToList();
                List<JObject> real = current.Where(s => IsOrderSegment(s, skid, orderSegments)).ToList();
                List<JObject> foreign = current.Where(s => !IsOrderSegment(s, skid, orderSegments)).ToList();
                double nominal = Number(config, "nominalSurfaceThickness");
                bool healthy = real.Count > 0 && foreign.Count == 0;
                if (healthy && (nominal <= 0 || HasFoamSpec(nominal))) continue;

                var plan = new RoofRestorePlan { IamPath = source.IamPath, PartNumber = Text(config, "partNumber") };
                plan.Remove.AddRange(foreign.Select(Summarize));
                plans.Add(plan);

                var add = new List<JObject>();
                if (real.Count == 0)
                {
                    if (skid.Length == 0)
                    {
                        plan.Problem = "the roof's config has no skid ID";
                        continue;
                    }

                    List<JObject> under = orderSegments.Values
                        .Where(s => string.Equals(Text(s, "skidID"), skid, StringComparison.OrdinalIgnoreCase) &&
                                    s["geometry"] is JObject g && IsUnder(Box.Of(g), roofBox))
                        .OrderBy(s => Number(s["geometry"] as JObject, "x"))
                        .ToList();

                    List<string> unclear = under.Select(s => Text(s, "id")).Where(differing.Contains).ToList();
                    if (unclear.Count > 0)
                    {
                        plan.Problem = $"the unit's surfaces disagree on segment {string.Join(", ", under.Where(s => unclear.Contains(Text(s, "id"))).Select(s => Text(s, "segmentType")))}";
                        continue;
                    }

                    if (under.Count == 0)
                    {
                        plan.Problem = "no segment of this skid sits under the roof (check the whole unit is open, so its other surfaces are read)";
                        continue;
                    }

                    add = under;
                    plan.Add.AddRange(add.Select(Summarize));
                    plan.RoofOpenings = add.Sum(s => (s["openingList"] as JArray)?.OfType<JObject>()
                        .Count(o => string.Equals(Text(o, "unitSide"), "Top", StringComparison.OrdinalIgnoreCase)) ?? 0);
                }

                List<JObject> kept = real.Concat(add).ToList();
                double segmentTop = kept.Count > 0 ? kept.Max(s => Box.Of(s["geometry"] as JObject).YEnd) : double.NaN;
                if (roofBox.YEnd > segmentTop + Tolerance)
                    plan.Reason = $"roof top {Format(roofBox.YEnd)} is above its segments' top {Format(segmentTop)} (YLength raised)";

                // The raised YLength also raised the nominal thickness, which YCShell's foam lookup only has for 2, 3 and 4.
                // The segments' own top wall thickness is the order's panel thickness for this roof.
                if (nominal > 0 && !HasFoamSpec(nominal))
                {
                    List<double> walls = kept.Select(s => Number(s, "wallThickness_Top")).Where(w => w > 0).Distinct().ToList();
                    if (walls.Count == 1 && HasFoamSpec(walls[0]))
                    {
                        plan.NominalFrom = nominal;
                        plan.NominalTo = walls[0];
                    }
                    else if (healthy)
                    {
                        plan.Problem = $"nominal thickness {Format(nominal)}\" has no foam spec (only 2, 3 or 4\") and its segments give no single panel thickness, so YCShell makes its foam Foamboard";
                        continue;
                    }
                    else
                    {
                        plan.Warning = $"nominal thickness {Format(nominal)}\" has no foam spec and the segments give no single panel thickness, so YCShell will make the foam Foamboard";
                    }
                }

                var restoredRoot = (JObject)root.DeepClone();
                var restoredConfig = (JObject)restoredRoot["configuration"]!;
                // Restored segments go on without the bulkhead records another surface carries: which belong to the roof
                // is not known. Their opening list stays; YCShell reads a roof's openings from surfaceOpeningList only.
                restoredConfig["surfaceSegmentList"] = new JArray(real.Select(s => s.DeepClone()).Concat(add.Select(OwnFields)));
                if (plan.NominalTo is double to) restoredConfig["nominalSurfaceThickness"] = new JValue((decimal)to);
                plan.RestoredJson = restoredRoot.ToString(Formatting.None);
            }

            return plans;
        }

        /// <summary>The review text, one block per roof.</summary>
        public static string Report(IReadOnlyList<RoofRestorePlan> plans)
        {
            if (plans.Count == 0) return "Every roof has its order segments and a panel thickness YCShell has foam for. Nothing to restore.";

            var sb = new StringBuilder();
            foreach (RoofRestorePlan plan in plans)
            {
                sb.AppendLine($"{Name(plan)}{(plan.Reason.Length > 0 ? $"  ({plan.Reason})" : string.Empty)}");
                if (plan.Problem.Length > 0)
                {
                    sb.AppendLine($"   Not restored: {plan.Problem}.");
                }
                else
                {
                    foreach (SegmentSummary s in plan.Add) sb.AppendLine($"   + {s}");
                    foreach (SegmentSummary s in plan.Remove) sb.AppendLine($"   − {s}  (not from the order)");
                    if (plan.NominalTo is double to)
                        sb.AppendLine($"   ~ nominal thickness {Format(plan.NominalFrom ?? 0)}\" → {Format(to)}\" (the segment's panel thickness), so YCShell finds its {Format(to)}\" foam spec instead of making the foam Foamboard");
                    if (plan.Warning.Length > 0) sb.AppendLine($"   ! {plan.Warning}.");
                    if (plan.RoofOpenings > 0)
                        sb.AppendLine($"   ! The order has {plan.RoofOpenings} opening{(plan.RoofOpenings == 1 ? "" : "s")} on this roof. They are not added; add them by hand.");
                }

                sb.AppendLine();
            }

            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Panel thicknesses YCShell's housing insulation calculator (BoundaryEngData_Insulation, YC_HousingInsulation.xlsx
        /// PARTLIST) has foam for. Any other nominal thickness fails its lookup and YCShell converts the foam to Foamboard
        /// (CGenerator.ConvertMalformedInsulationToStub).
        /// </summary>
        private static bool HasFoamSpec(double thickness) => new[] { 2.0, 3.0, 4.0 }.Any(t => Math.Abs(t - thickness) < 0.001);

        private static string Name(RoofRestorePlan plan) =>
            plan.PartNumber.Length > 0 ? plan.PartNumber : Path.GetFileNameWithoutExtension(plan.IamPath);

        private static bool IsOrderSegment(JObject segment, string skid, IReadOnlyDictionary<string, JObject> orderSegments) =>
            orderSegments.ContainsKey(Text(segment, "id")) &&
            string.Equals(Text(segment, "skidID"), skid, StringComparison.OrdinalIgnoreCase);

        /// <summary>The segment overlaps the roof's footprint (X and Z) and reaches up to the roof's underside.</summary>
        private static bool IsUnder(Box segment, Box roof) =>
            Overlap(segment.X, segment.XEnd, roof.X, roof.XEnd) > Tolerance &&
            Overlap(segment.Z, segment.ZEnd, roof.Z, roof.ZEnd) > Tolerance &&
            segment.Y < roof.YEnd && segment.YEnd > roof.Y - Tolerance;

        private static double Overlap(double a0, double a1, double b0, double b1) => Math.Min(a1, b1) - Math.Max(a0, b0);

        /// <summary>A segment without its bulkhead list, for comparing copies.</summary>
        private static JObject OwnFields(JObject segment)
        {
            var copy = (JObject)segment.DeepClone();
            copy.Remove("bulkheadList");
            return copy;
        }

        private static IEnumerable<JObject> Segments(JObject config) =>
            (config["surfaceSegmentList"] as JArray)?.OfType<JObject>() ?? Enumerable.Empty<JObject>();

        private static SegmentSummary Summarize(JObject segment) => new SegmentSummary
        {
            Id         = Text(segment, "id"),
            Type       = Text(segment, "segmentType"),
            TopSkin    = Material(segment, "skinMaterialType_Top", "skinMaterialGauge_Top"),
            TopLiner   = Material(segment, "linerMaterialType_Top", "linerMaterialGauge_Top"),
            Insulation = Text(segment, "insulationType"),
        };

        private static string Material(JObject segment, string type, string gauge)
        {
            string t = Text(segment, type);
            string g = segment[gauge] is JValue { Value: IConvertible } ? Format(Number(segment, gauge)) : Text(segment, gauge);
            return t.Length == 0 ? string.Empty : g.Length == 0 ? t : $"{g} GA {t}";
        }

        private static JObject? TryParse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                // Keep values exactly as written: no date conversion, decimals as text.
                using var reader = new JsonTextReader(new StringReader(json))
                {
                    DateParseHandling = DateParseHandling.None,
                    FloatParseHandling = FloatParseHandling.Decimal,
                };
                return JObject.Load(reader);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static string Text(JObject? o, string key) =>
            o?[key] is JValue v && v.Value is not null ? Convert.ToString(v.Value, CultureInfo.InvariantCulture)?.Trim() ?? string.Empty : string.Empty;

        /// <summary>A number from the JSON; YCShell leaves out zero values, so a missing one is 0.</summary>
        private static double Number(JObject? o, string key) =>
            o?[key] is JValue v && v.Value is IConvertible c ? c.ToDouble(CultureInfo.InvariantCulture) : 0;

        private static string Format(double n) => n.ToString("0.###", CultureInfo.InvariantCulture);

        private readonly struct Box
        {
            private Box(double x, double y, double z, double xEnd, double yEnd, double zEnd)
            {
                X = x; Y = y; Z = z; XEnd = xEnd; YEnd = yEnd; ZEnd = zEnd;
            }

            public double X { get; }
            public double Y { get; }
            public double Z { get; }
            public double XEnd { get; }
            public double YEnd { get; }
            public double ZEnd { get; }

            public static Box Of(JObject? g)
            {
                double x = Number(g, "x"), y = Number(g, "y"), z = Number(g, "z");
                return new Box(x, y, z, x + Number(g, "xLength"), y + Number(g, "yLength"), z + Number(g, "zLength"));
            }
        }
    }
}
