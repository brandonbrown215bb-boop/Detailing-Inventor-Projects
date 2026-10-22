using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using UnitConstructionVerifier.Engine;
using UnitConstructionVerifier.Extraction;
using UnitConstructionVerifier.Models;

namespace UnitConstructionVerifier.Tests
{
    /// <summary>
    /// Expectation rules confirmed against real ISG units (ucv-scan of C:\Users\jbrow263\ISG, 2026-10-21).
    /// </summary>
    [TestFixture]
    public class RealUnitRuleTests
    {
        [OneTimeSetUp]
        public void Setup()
        {
            MaterialsConfig.Initialize();
        }

        /// <summary>A segment like SW0004: unpainted 16 GA top, 18 GA PPC sides, 22 GA liners.</summary>
        private static SurfaceSegment TypicalSegment() => new SurfaceSegment
        {
            SegmentType = "XA",
            SkinMaterialType_Top = "STL GALV",       SkinMaterialGauge_Top = 16,
            SkinMaterialType_Left = "STL GALV PPC",  SkinMaterialGauge_Left = 18,
            SkinMaterialType_Right = "STL GALV PPC", SkinMaterialGauge_Right = 18,
            SkinMaterialType_Front = "STL GALV PPC", SkinMaterialGauge_Front = 18,
            SkinMaterialType_Rear = "STL GALV PPC",  SkinMaterialGauge_Rear = 18,
            LinerMaterialType_Top = "STL GALV",      LinerMaterialGauge_Top = 22,
            LinerMaterialType_Left = "STL GALV",     LinerMaterialGauge_Left = 22,
            LinerMaterialType_Right = "STL GALV",    LinerMaterialGauge_Right = 22,
            LinerMaterialType_Front = "STL GALV",    LinerMaterialGauge_Front = 22,
            LinerMaterialType_Rear = "STL GALV",     LinerMaterialGauge_Rear = 22,
            WallThickness_Top = 2, WallThickness_Left = 2, WallThickness_Right = 2, WallThickness_Front = 2, WallThickness_Rear = 2,
        };

        private static ConfigData Surface(string type, string iam, string side, double thickness = 2) => new ConfigData
        {
            SurfaceType = type,
            SourceIamPath = iam,
            SurfaceId = iam + "-id",
            NominalSurfaceThickness = thickness,
            UnitSurfaceList = new List<UnitSurface> { new UnitSurface { Id = iam + "-id", UnitSide = side } },
            SurfaceSegmentList = new List<SurfaceSegment> { TypicalSegment() },
        };

        private static VerificationResult Run(List<ConfigData> configs, EngineeringData? eng, params IptProperties[] parts)
        {
            var scan = new IptScanResult { Parts = parts.ToList() };
            var data = new ConstructionDataBuilder(configs, eng, scan).Build();
            return new VerificationEngine(data, scan).Run();
        }

        private static IptProperties Part(string iam, string model, string gauge, string material, string description = "") =>
            new IptProperties { PartNumber = model + "@" + iam, OwnerIamPath = iam, ModelNumber = model, MtlGauge = gauge, YCMATL = material, Description = description };

        [Test]
        public void Roof_UsesTopSide_NotMajorityOfSides()
        {
            var roof = new ConstructionDataBuilder(new List<ConfigData> { Surface("Roof", "R.iam", "Top") }, null, new IptScanResult()).Build().RoofRows.Single();

            Assert.AreEqual("16", roof.ExteriorSkinGauge);
            Assert.AreEqual("STL GALV", roof.ExteriorSkinMaterial);
            Assert.AreEqual("22", roof.InteriorLinerGauge);
        }

        [Test]
        public void Wall_UsesItsOwnSide()
        {
            var config = Surface("Wall", "W.iam", "Left");
            config.SurfaceSegmentList[0].SkinMaterialType_Left = "STL GALV";
            config.SurfaceSegmentList[0].SkinMaterialGauge_Left = 20;

            var wall = new ConstructionDataBuilder(new List<ConfigData> { config }, null, new IptScanResult()).Build().WallRows.Single();

            Assert.AreEqual("20", wall.ExteriorSkinGauge);
            Assert.AreEqual("STL GALV", wall.ExteriorSkinMaterial);
        }

        [Test]
        public void RoofParts_FollowRealConventions()
        {
            var result = Run(new List<ConfigData> { Surface("Roof", "R.iam", "Top") }, null,
                Part("R.iam", "091-30117-083", "16", "STL GALV", "C:EXT-ROOF SKIN"),
                Part("R.iam", "091-30117-086", "16", "STL GALV2", "C:SC 1.5 x 2"),
                Part("R.iam", "091-30117-070", "16", "STL GALV", "C:SC 1.5 x 2"),
                Part("R.iam", "091-30117-001", "16", "STL GALV PPC", "ROOF CORNER CAP, VARIABLE 4.3"),
                Part("R.iam", "091-30117-022", "16", "STL GALV", "ROOF SUPER ANGLE 52x2"),
                Part("R.iam", "091-30117-057", "14", "STL GALV", "TIERING ANGLE, 42x6x5"));

            Assert.IsEmpty(result.Mismatches, string.Join("\n", result.Mismatches.Select(m => $"{m.IptPartNumber}: {m.ExpectedValue} vs {m.ActualValue}")));
            Assert.AreEqual(6, result.CountOf(PartCheckStatus.Pass));
        }

        [Test]
        public void StandardChannels_UseThickness_EvenWhenConfigSaysPlain()
        {
            // Hole Pattern Tiger Team FINAL: config says Outdoor / plain STL GALV, but the roofs use the standard
            // indoor channel parts. Standard parts are always STL GALV + thickness, so the plain ones are flagged.
            var config = Surface("Roof", "R.iam", "Top", thickness: 4);
            config.FormedChannelMaterialType = "STL GALV";
            config.FormedChannelMaterialGauge = 16;

            var result = Run(new List<ConfigData> { config }, null,
                Part("R.iam", "091-30117-086", "16", "STL GALV", "C:SC 1.5 x 4"),
                Part("R.iam", "091-30117-085", "16", "STL GALV4", "C:SC 1.5 x 4"),
                Part("R.iam", "091-30117-069", "16", "STL GALV", "C:SC 1.5 x 4.3437"));

            Assert.AreEqual(PartCheckStatus.Mismatch, result.Checks[0].Status);
            Assert.AreEqual(PartCheckStatus.Pass, result.Checks[1].Status);
            Assert.AreEqual(PartCheckStatus.Pass, result.Checks[2].Status, "sloped roof channels are plain STL GALV");
        }

        [Test]
        public void RoofCap_InAluminumEmbossed_Is20Gauge()
        {
            var config = Surface("Roof", "R.iam", "Top");
            var seg = config.SurfaceSegmentList[0];
            seg.SkinMaterialType_Left = seg.SkinMaterialType_Right = seg.SkinMaterialType_Front = seg.SkinMaterialType_Rear = "ALM EMB";

            var result = Run(new List<ConfigData> { config }, null, Part("R.iam", "091-30117-058", "20", "ALM EMB", "ROOF CAP, VARIABLE 92"));

            Assert.AreEqual(PartCheckStatus.Pass, result.Checks.Single().Status, result.Checks.Single().ExpectedValue);
        }

        [Test]
        public void AttachmentAngle_Is16Gauge_InLinerMaterial()
        {
            var result = Run(new List<ConfigData> { Surface("Wall", "W.iam", "Front") }, null,
                Part("W.iam", "091-30117-049", "16", "STL GALV", "Wall Attachment Angle: 12 x 2"),
                Part("W.iam", "091-30119-006", "18", "STL GALV PPC", "Wall Seal-Off Angle: 12 x 2"));

            Assert.AreEqual(2, result.CountOf(PartCheckStatus.Pass));
        }

        private static ConfigData Base(string floor, bool upturnedLip, string iam = "B.iam")
        {
            var config = new ConfigData
            {
                SurfaceType = "UnitBase",
                SourceIamPath = iam,
                StructuralMaterialType = "Steel",
                DefaultFloorMaterialType = floor,
                UnitBase = new UnitBaseConfig
                {
                    UnitBaseGeometryList = new List<UnitBaseGeometry>
                    {
                        new UnitBaseGeometry { HasSubFloor = true, SubFloorMaterialType = "ALM SHT", SubFloorMaterialGauge = 18, UpturnedLipHeight = upturnedLip ? 2.0 : (double?)null }
                    }
                },
                SurfaceSegmentList = new List<SurfaceSegment> { new SurfaceSegment { SegmentType = "CC" } },
            };
            return config;
        }

        [Test]
        public void SubFloor_ComesFromUnitBaseGeometry()
        {
            var row = new ConstructionDataBuilder(new List<ConfigData> { Base("ALM DIA", false) }, null, new IptScanResult()).Build().BaseRows.Single();

            Assert.AreEqual("18", row.SubFloorGauge);
            Assert.AreEqual("ALM SHT", row.SubFloorMaterial);
        }

        [Test]
        public void PerimeterAngle_NonUtl_UsesEngineeringStandard()
        {
            var eng = new EngineeringData();
            eng.Constants["UnitBase_PerimeterAngle_MaterialGauge"] = "16";
            eng.Constants["UnitBase_PerimeterAngle_MaterialType"] = "STL GALV";

            var row = new ConstructionDataBuilder(new List<ConfigData> { Base("ALM DIA", false) }, eng, new IptScanResult()).Build().BaseRows.Single();

            Assert.AreEqual("16", row.PerimeterAngleGauge);
            Assert.AreEqual("STL GALV", row.PerimeterAngleMaterial);
        }

        [TestCase("ALM DIA", "ALM SHT")]
        [TestCase("SST304D", "SST304D")]
        public void PerimeterAngle_Utl_Is10GaugeInFloorAlloy(string floor, string expectedMaterial)
        {
            var row = new ConstructionDataBuilder(new List<ConfigData> { Base(floor, true) }, null, new IptScanResult()).Build().BaseRows.Single();

            Assert.AreEqual("10", row.PerimeterAngleGauge);
            Assert.AreEqual(expectedMaterial, row.PerimeterAngleMaterial);
        }

        [Test]
        public void PerimeterAngle_UtlOnGalvanizedFloor_Is12GaugeStlGalv()
        {
            var row = new ConstructionDataBuilder(new List<ConfigData> { Base("STL GALV", true) }, null, new IptScanResult()).Build().BaseRows.Single();

            Assert.AreEqual("12", row.PerimeterAngleGauge);
            Assert.AreEqual("STL GALV", row.PerimeterAngleMaterial);
        }

        [Test]
        public void RoofWithoutSegments_BorrowsUnitWallExterior()
        {
            var roof = new ConfigData { SurfaceType = "Roof", SourceIamPath = "R.iam" };
            var wall = Surface("Wall", "W.iam", "Front");

            var row = new ConstructionDataBuilder(new List<ConfigData> { roof, wall }, null, new IptScanResult()).Build().RoofRows.Single();

            Assert.AreEqual("16", row.TrimSkinGauge);
            Assert.AreEqual("STL GALV PPC", row.TrimSkinMaterial);
        }

        [Test]
        public void UpturnedLip_IsPerBase()
        {
            // A lip on one skid's base must not change another base's perimeter angles.
            var withLip = Base("ALM DIA", true, "B1.iam");
            var withoutLip = Base("ALM DIA", false, "B2.iam");

            var rows = new ConstructionDataBuilder(new List<ConfigData> { withLip, withoutLip }, null, new IptScanResult()).Build().BaseRows;

            Assert.AreEqual("10", rows.Single(r => r.SourceSurfaceIam == "B1.iam").PerimeterAngleGauge);
            Assert.AreEqual("16", rows.Single(r => r.SourceSurfaceIam == "B2.iam").PerimeterAngleGauge);
        }

        [Test]
        public void UpturnedLip_FromUnitBaseListForThisSkid()
        {
            var config = Base("ALM DIA", false);
            config.SkidId = "skid-1";
            config.UnitBaseList = new List<UnitBase>
            {
                new UnitBase { SkidId = "skid-2", UpturnedLipHeight = 2 },
            };
            Assert.IsFalse(config.HasUpturnedLip(), "another skid's lip does not count");

            config.UnitBaseList.Add(new UnitBase { SkidId = "skid-1", UpturnedLipHeight = 2 });
            Assert.IsTrue(config.HasUpturnedLip());
        }

        [TestCase("0.125", "ALM DIA", "0.125")]
        [TestCase("0.125", "SST304D", "0.125")]
        [TestCase("0.125", "ALM SHT", "10")]
        [TestCase("0.056", "STL GALV", "16")]
        public void PlateFloors_KeepThicknessLabel(string raw, string material, string expected)
        {
            Assert.AreEqual(expected, MaterialsConfig.MapGauge(raw, material));
        }

        [TestCase("Steel", "ANGLE, 17.319 x 1.5 x 1.5", "10", "STL HOT ROLL", PartCheckStatus.Pass)]
        [TestCase("Steel", "ANGLE, 22.198 x 2 x 2", "10", "STL HOT ROLL", PartCheckStatus.Pass)]
        [TestCase("Aluminum", "ANGLE, 17.319 x 1.5 x 1.5", "10", "ALM SHT", PartCheckStatus.Pass)]
        [TestCase("Steel", "ANGLE, 17.319 x 1.5 x 1.5", "16", "STL GALV", PartCheckStatus.Mismatch)]
        [TestCase("Steel", "ANGLE, 36.98 x 2.5 x 1", "14", "ALM SHT", PartCheckStatus.Pass)]
        [TestCase("Steel", "ANGLE, 17.319 x 1.75 x 1.75", "10", "STL HOT ROLL", PartCheckStatus.NotInConfig)]
        public void BaseAngles_ClassifiedByProfile(string structural, string description, string gauge, string material, PartCheckStatus expected)
        {
            var config = Base("ALM DIA", false);
            config.StructuralMaterialType = structural;

            var result = Run(new List<ConfigData> { config }, null, Part("B.iam", "091-30117-079", gauge, material, description));

            Assert.AreEqual(expected, result.Checks.Single().Status, result.Checks.Single().ExpectedValue);
        }

        [TestCase("SQ PART - SKIN", "", false, true)]
        [TestCase("C:EXT-WALL SKIN", "SQPart", false, true)]
        [TestCase("C:EXT-WALL SKIN", "", true, true)]
        [TestCase("C:EXT-WALL SKIN", "DerivedFromLibraryPart", false, false)]
        public void ModifiedFlag(string description, string partSource, bool hardEdited, bool expected)
        {
            var part = new IptProperties { Description = description, PartSource = partSource, IsHardEdited = hardEdited };

            Assert.AreEqual(expected, part.IsModified);
        }
    }
}
