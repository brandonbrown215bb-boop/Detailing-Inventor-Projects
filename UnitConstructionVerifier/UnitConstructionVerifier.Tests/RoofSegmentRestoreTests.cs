using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnitConstructionVerifier.Engine;

namespace UnitConstructionVerifier.Tests
{
    /// <summary>
    /// Shapes from job 20275, skid 1: the vestibule roof 0050 (raised to 2.79" so its slope meets the main roof) lost its
    /// VESTIBULE segment; the main roof and the walls kept theirs.
    /// </summary>
    [TestFixture]
    public class RoofSegmentRestoreTests
    {
        private const string Skid = "69a4eca2-68ac-4c42-9d53-ab3d34e51739";
        private const string OtherSkid = "bf7cd16c-0000-0000-0000-000000000000";

        // z is left out, as YCShell writes a zero.
        private static JObject Vestibule(string skid = Skid, int linerGauge = 18) => JObject.FromObject(new
        {
            id = "ce6762d1-5244-4269-8714-2bbe0e834603",
            skidID = skid,
            segmentType = "VESTIBULE",
            geometry = new { id = "g-vest", x = 130, y = 10, xLength = 140, yLength = 123, zLength = 96 },
            linerMaterialType_Top = "ALM SHT", linerMaterialGauge_Top = linerGauge,
            skinMaterialType_Top = "STL GALV", skinMaterialGauge_Top = 16,
            insulationType = "Foam",
            openingList = new object[]
            {
                new { id = "o1", unitSide = "Bottom", openingType = "Standard" },
            },
        });

        private static JObject Fan(string skid = Skid) => JObject.FromObject(new
        {
            id = "ff-1", skidID = skid, segmentType = "FF",
            geometry = new { id = "g-ff", x = 130, y = 10, z = 96, xLength = 9, yLength = 123, zLength = 134 },
            linerMaterialType_Top = "ALM SHT", linerMaterialGauge_Top = 18,
            skinMaterialType_Top = "STL GALV", skinMaterialGauge_Top = 16,
            insulationType = "Foam",
        });

        // What the ISG Bandage wrote: no skid, fixed materials, a box padded around the roof.
        private static JObject Bandage() => JObject.FromObject(new
        {
            id = "817fae3b-f439-432f-aac6-c4dfa902daf9", segmentType = "IP",
            geometry = new { id = "g-ip", x = 129, y = 111, z = 0, xLength = 142, yLength = 27.79, zLength = 97 },
            skinMaterialType_Top = "STL GALV", skinMaterialGauge_Top = 16,
            linerMaterialType_Top = "STL GALV", linerMaterialGauge_Top = 20,
            insulationType = "Foam",
        });

        private static SurfaceConfigText Roof(string name, double z, double zLength, double yLength, params JObject[] segments) =>
            Roof(name, z, zLength, yLength, nominal: null, segments);

        private static JObject WithPanelThickness(JObject segment, double wallThicknessTop)
        {
            segment["wallThickness_Top"] = wallThicknessTop;
            return segment;
        }

        private static SurfaceConfigText Roof(string name, double z, double zLength, double yLength, double? nominal, params JObject[] segments)
        {
            SurfaceConfigText text = RoofWithoutNominal(name, z, zLength, yLength, segments);
            if (nominal is null) return text;
            var root = JObject.Parse(text.Json);
            root["configuration"]!["nominalSurfaceThickness"] = nominal.Value;
            return new SurfaceConfigText(text.IamPath, root.ToString(Newtonsoft.Json.Formatting.None));
        }

        private static SurfaceConfigText RoofWithoutNominal(string name, double z, double zLength, double yLength, params JObject[] segments)
        {
            var root = new JObject
            {
                ["configuration"] = new JObject
                {
                    ["$type"] = "JCI.DataModel.ISG.Models.Configuration.CConfiguration_Roof, JCI.DataModel.ISG.Models",
                    ["roof"] = new JObject
                    {
                        ["geometryList"] = new JArray(new JObject { ["x"] = 130.0, ["y"] = 131.0, ["z"] = z, ["xLength"] = 140.0, ["yLength"] = yLength, ["zLength"] = zLength }),
                        ["isRoofSloped"] = true,
                    },
                    ["skidID"] = Skid,
                    ["surfaceUnitSide"] = "Top",
                    ["partNumber"] = name,
                    ["outputAssemblyLocation"] = "2026-10-19T18:55:52",
                    ["weight"] = 395.123456789012345m,
                    ["surfaceSegmentList"] = new JArray(segments.Select(s => (JToken)s.DeepClone())),
                    ["insulationList"] = new JArray(),
                },
                ["documentInfo"] = new JObject { ["assemblyNumber"] = name },
            };
            return new SurfaceConfigText($@"C:\Job\{name}\{name}.IAM", root.ToString(Newtonsoft.Json.Formatting.None));
        }

        private static SurfaceConfigText Wall(string name, params JObject[] segments)
        {
            var root = new JObject
            {
                ["configuration"] = new JObject
                {
                    ["$type"] = "JCI.DataModel.ISG.Models.Configuration.CConfiguration_Wall, JCI.DataModel.ISG.Models",
                    ["wall"] = new JObject(),
                    ["skidID"] = Skid,
                    ["surfaceUnitSide"] = "Left",
                    ["partNumber"] = name,
                    ["surfaceSegmentList"] = new JArray(segments.Select(s => (JToken)s.DeepClone())),
                },
            };
            return new SurfaceConfigText($@"C:\Job\{name}\{name}.IAM", root.ToString(Newtonsoft.Json.Formatting.None));
        }

        private static JArray RestoredSegments(RoofRestorePlan plan) =>
            (JArray)JObject.Parse(plan.RestoredJson)["configuration"]!["surfaceSegmentList"]!;

        [Test]
        public void RestoresTheOrderSegmentTheRaisedRoofSitsOn()
        {
            var surfaces = new List<SurfaceConfigText>
            {
                Roof("391Z010296-0050", z: 1, zLength: 95, yLength: 2.79),
                Wall("391Z010296-0131", Vestibule(), Fan()),
            };

            RoofRestorePlan plan = RoofSegmentRestore.Plan(surfaces).Single();

            Assert.IsTrue(plan.CanApply, plan.Problem);
            Assert.AreEqual("VESTIBULE", plan.Add.Single().Type);   // the fan segment starts at Z 96, where the roof ends
            Assert.AreEqual("16 GA STL GALV", plan.Add[0].TopSkin);
            Assert.AreEqual("18 GA ALM SHT", plan.Add[0].TopLiner);
            Assert.AreEqual("Foam", plan.Add[0].Insulation);
            StringAssert.Contains("133.79", plan.Reason);
            StringAssert.Contains("133", plan.Reason);
            Assert.IsTrue(JToken.DeepEquals(Vestibule(), RestoredSegments(plan).Single()), "copied verbatim");
        }

        [Test]
        public void LeavesTheRestOfTheConfigAsWritten()
        {
            SurfaceConfigText roof = Roof("391Z010296-0050", z: 1, zLength: 95, yLength: 2.79);
            RoofRestorePlan plan = RoofSegmentRestore.Plan(new[] { roof, Wall("W", Vestibule()) }).Single();

            var before = JObject.Parse(roof.Json);
            var after = JObject.Parse(plan.RestoredJson);
            ((JObject)before["configuration"]!).Remove("surfaceSegmentList");
            ((JObject)after["configuration"]!).Remove("surfaceSegmentList");
            Assert.IsTrue(JToken.DeepEquals(before, after));
            StringAssert.Contains("\"outputAssemblyLocation\":\"2026-10-19T18:55:52\"", plan.RestoredJson);   // not turned into a date
            StringAssert.Contains("395.123456789012345", plan.RestoredJson);                                   // no rounding
            StringAssert.StartsWith("{\"configuration\":{\"$type\":", plan.RestoredJson);                      // $type stays first
        }

        [Test]
        public void ReplacesASegmentThatIsNotFromTheOrder()
        {
            var surfaces = new[]
            {
                Roof("391Z010296-0139", z: 1, zLength: 95, yLength: 2.79, Bandage()),
                Wall("W", Vestibule()),
            };

            RoofRestorePlan plan = RoofSegmentRestore.Plan(surfaces).Single();

            Assert.AreEqual("IP", plan.Remove.Single().Type);
            Assert.AreEqual("VESTIBULE", plan.Add.Single().Type);
            Assert.AreEqual("VESTIBULE", (string?)RestoredSegments(plan).Single()["segmentType"]);
            StringAssert.Contains("not from the order", RoofSegmentRestore.Report(new[] { plan }));
        }

        [Test]
        public void LeavesARoofThatHasItsSegmentsAlone()
        {
            var surfaces = new[]
            {
                Roof("391Z010296-0130", z: 97, zLength: 132, yLength: 2, Fan()),
                Wall("W", Vestibule(), Fan()),
            };

            Assert.IsEmpty(RoofSegmentRestore.Plan(surfaces));
        }

        [Test]
        public void RemovesOnlyTheForeignSegmentWhenTheOrderOnesAreThere()
        {
            var surfaces = new[]
            {
                Roof("R", z: 97, zLength: 132, yLength: 2, Fan(), Bandage()),
                Wall("W", Fan()),
            };

            RoofRestorePlan plan = RoofSegmentRestore.Plan(surfaces).Single();

            Assert.IsEmpty(plan.Add);
            Assert.AreEqual("IP", plan.Remove.Single().Type);
            Assert.AreEqual("FF", (string?)RestoredSegments(plan).Single()["segmentType"]);
        }

        [Test]
        public void ReportsARoofWithNothingUnderIt()
        {
            // Only the roof is open, so the unit's segments cannot be read.
            RoofRestorePlan plan = RoofSegmentRestore.Plan(new[] { Roof("391Z010296-0050", z: 1, zLength: 95, yLength: 2.79) }).Single();

            Assert.IsFalse(plan.CanApply);
            StringAssert.Contains("no segment of this skid", plan.Problem);
        }

        [Test]
        public void IgnoresSegmentsOfOtherSkids()
        {
            var surfaces = new[]
            {
                Roof("391Z010296-0050", z: 1, zLength: 95, yLength: 2.79),
                Wall("W", Vestibule(OtherSkid)),
            };

            Assert.IsFalse(RoofSegmentRestore.Plan(surfaces).Single().CanApply);
        }

        [Test]
        public void TakesTheSegmentWithoutAnotherSurfacesBulkheads()
        {
            // Each surface's copy lists its own bulkhead records (job 20275: same segment, different bulkheads).
            JObject leftWallCopy = Vestibule();
            leftWallCopy["bulkheadList"] = JArray.FromObject(new[] { new { id = "b-left", partInfo = new { partNumber = "391-60237-006" } } });
            JObject baseCopy = Vestibule();
            baseCopy["bulkheadList"] = JArray.FromObject(new[] { new { id = "b-base", partInfo = new { partNumber = "391-60236-997" } } });
            var surfaces = new[] { Roof("391Z010296-0050", z: 1, zLength: 95, yLength: 2.79), Wall("W", leftWallCopy), Wall("B", baseCopy) };

            RoofRestorePlan plan = RoofSegmentRestore.Plan(surfaces).Single();

            Assert.IsTrue(plan.CanApply, plan.Problem);
            JObject restored = (JObject)RestoredSegments(plan).Single();
            Assert.IsNull(restored["bulkheadList"]);
            Assert.IsTrue(JToken.DeepEquals(Vestibule(), restored), "own fields and opening list copied verbatim");
        }

        [Test]
        public void RefusesWhenSurfacesDisagreeOnTheSegment()
        {
            var surfaces = new[]
            {
                Roof("391Z010296-0050", z: 1, zLength: 95, yLength: 2.79),
                Wall("W1", Vestibule()),
                Wall("W2", Vestibule(linerGauge: 20)),
            };

            RoofRestorePlan plan = RoofSegmentRestore.Plan(surfaces).Single();

            Assert.IsFalse(plan.CanApply);
            StringAssert.Contains("disagree", plan.Problem);
        }

        [Test]
        public void CountsTheOrdersRoofOpeningsButDoesNotAddThem()
        {
            JObject vestibule = Vestibule();
            ((JArray)vestibule["openingList"]!).Add(JObject.FromObject(new { id = "o2", unitSide = "Top", openingType = "Standard" }));
            var surfaces = new[] { Roof("391Z010296-0050", z: 1, zLength: 95, yLength: 2.79), Wall("W", vestibule) };

            RoofRestorePlan plan = RoofSegmentRestore.Plan(surfaces).Single();

            Assert.AreEqual(1, plan.RoofOpenings);
            Assert.IsNull(JObject.Parse(plan.RestoredJson)["configuration"]!["surfaceOpeningList"]);
            StringAssert.Contains("add them by hand", RoofSegmentRestore.Report(new[] { plan }));
        }

        // YCShell's insulation lookup has foam only for 2, 3 and 4" panels; 2.79 made job 20275's 0050 come out Foamboard.
        [Test]
        public void SetsTheRaisedNominalThicknessBackToTheSegmentsPanelThickness()
        {
            var surfaces = new[]
            {
                Roof("391Z010296-0050", z: 1, zLength: 95, yLength: 2.79, nominal: 2.79),
                Wall("W", WithPanelThickness(Vestibule(), 2)),
            };

            RoofRestorePlan plan = RoofSegmentRestore.Plan(surfaces).Single();

            Assert.IsTrue(plan.CanApply, plan.Problem);
            Assert.AreEqual(2.79, plan.NominalFrom);
            Assert.AreEqual(2, plan.NominalTo);
            Assert.AreEqual(2m, (decimal)JObject.Parse(plan.RestoredJson)["configuration"]!["nominalSurfaceThickness"]!);
            Assert.AreEqual(2.79, (double)JObject.Parse(plan.RestoredJson)["configuration"]!["roof"]!["geometryList"]![0]!["yLength"]!, "geometry stays raised");
            StringAssert.Contains("nominal thickness 2.79\" → 2\"", RoofSegmentRestore.Report(new[] { plan }));
        }

        [Test]
        public void FixesTheNominalThicknessOnARoofThatAlreadyHasItsSegment()
        {
            // 0050 after the first v1.0.38 restore: segment back, foam came out Foamboard.
            JObject vestibule = WithPanelThickness(Vestibule(), 2);
            var surfaces = new[] { Roof("391Z010296-0050", z: 1, zLength: 95, yLength: 2.79, nominal: 2.79, vestibule), Wall("W", vestibule) };

            RoofRestorePlan plan = RoofSegmentRestore.Plan(surfaces).Single();

            Assert.IsTrue(plan.CanApply, plan.Problem);
            Assert.IsEmpty(plan.Add);
            Assert.IsEmpty(plan.Remove);
            Assert.AreEqual(2, plan.NominalTo);
        }

        [TestCase(2.0)]
        [TestCase(3.0)]
        [TestCase(4.0)]
        public void LeavesAStandardThicknessAlone(double nominal)
        {
            JObject fan = WithPanelThickness(Fan(), 2);
            var surfaces = new[] { Roof("391Z010296-0130", z: 97, zLength: 132, yLength: 2, nominal: nominal, fan), Wall("W", fan) };

            Assert.IsEmpty(RoofSegmentRestore.Plan(surfaces));
        }

        [Test]
        public void ReportsAThicknessItCannotFixOnAHealthyRoof()
        {
            JObject vestibule = Vestibule();   // no wallThickness_Top
            var surfaces = new[] { Roof("R", z: 1, zLength: 95, yLength: 2.79, nominal: 2.79, vestibule), Wall("W", vestibule) };

            RoofRestorePlan plan = RoofSegmentRestore.Plan(surfaces).Single();

            Assert.IsFalse(plan.CanApply);
            StringAssert.Contains("Foamboard", plan.Problem);
        }

        [Test]
        public void StillRestoresTheSegmentWhenTheThicknessCannotBeFixed()
        {
            var surfaces = new[] { Roof("R", z: 1, zLength: 95, yLength: 2.79, nominal: 2.79), Wall("W", Vestibule()) };

            RoofRestorePlan plan = RoofSegmentRestore.Plan(surfaces).Single();

            Assert.IsTrue(plan.CanApply, plan.Problem);
            Assert.IsNull(plan.NominalTo);
            StringAssert.Contains("Foamboard", plan.Warning);
        }

        [Test]
        public void BacksUpTheOriginalConfigBesideTheSurface()
        {
            string folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "391Z010296-0050");
            string iam = System.IO.Path.Combine(folder, "391Z010296-0050.IAM");

            string path = Persistence.ConfigBackup.Save(iam, "{\"configuration\":{}}", new System.DateTime(2026, 11, 11, 15, 4, 5));

            Assert.AreEqual(System.IO.Path.Combine(folder, ".ucv_config_backups", "391Z010296-0050_20261111-150405.json"), path);
            Assert.AreEqual("{\"configuration\":{}}", System.IO.File.ReadAllText(path));
        }

        [Test]
        public void SkipsSurfacesThatAreNotRoofsOrNotJson()
        {
            var surfaces = new[] { Wall("W", Vestibule()), new SurfaceConfigText(@"C:\Job\bad.IAM", "not json") };

            Assert.IsEmpty(RoofSegmentRestore.Plan(surfaces));
            StringAssert.Contains("Nothing to restore", RoofSegmentRestore.Report(new List<RoofRestorePlan>()));
        }
    }
}
