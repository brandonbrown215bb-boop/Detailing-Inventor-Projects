using NUnit.Framework;
using System.Collections.Generic;
using UnitConstructionVerifier.Engine;
using UnitConstructionVerifier.Models;

namespace UnitConstructionVerifier.Tests
{
    [TestFixture]
    public class VerificationEngineTests
    {
        [OneTimeSetUp]
        public void Setup()
        {
            MaterialsConfig.Initialize();
        }

        [Test]
        public void TestActiveCasingWallVerification_EmptyExpectedDoesNotMismatch()
        {
            // If expected gauge/material is empty (e.g. wall thickness is 0 / inactive side),
            // it should NOT generate any mismatches even if actual properties exist.
            var userData = new UnitConstructionData
            {
                RoofRows = new List<RoofSurfaceRow>
                {
                    new RoofSurfaceRow
                    {
                        SourceSurfaceIam = "Roof.iam",
                        ExteriorSkinGauge = "", // Empty expected = inactive side
                        ExteriorSkinMaterial = "",
                        InteriorLinerGauge = "",
                        InteriorLinerMaterial = ""
                    }
                }
            };

            var iptData = new IptScanResult
            {
                Parts = new List<IptProperties>
                {
                    new IptProperties
                    {
                        OwnerIamPath = "Roof.iam",
                        PartNumber = "Skin-Part-1",
                        Description = "roof skin panel",
                        MtlGauge = "18",
                        YCMATL = "STL GALV"
                    }
                }
            };

            var engine = new VerificationEngine(userData, iptData);
            var result = engine.Run();

            Assert.IsTrue(result.IsPass);
            Assert.AreEqual(0, result.Mismatches.Count);
        }

        [Test]
        public void TestActiveCasingWallVerification_MismatchGeneratedWhenExpectedSet()
        {
            var userData = new UnitConstructionData
            {
                RoofRows = new List<RoofSurfaceRow>
                {
                    new RoofSurfaceRow
                    {
                        SourceSurfaceIam = "Roof.iam",
                        ExteriorSkinGauge = "18",
                        ExteriorSkinMaterial = "STL GALV PPC",
                        InteriorLinerGauge = "22",
                        InteriorLinerMaterial = "STL GALV"
                    }
                }
            };

            var iptData = new IptScanResult
            {
                Parts = new List<IptProperties>
                {
                    new IptProperties
                    {
                        OwnerIamPath = "Roof.iam",
                        PartNumber = "Skin-Part-1",
                        Description = "roof skin panel",
                        MtlGauge = "18",
                        YCMATL = "STL GALV" // Expected is STL GALV PPC -> Mismatch!
                    }
                }
            };

            var engine = new VerificationEngine(userData, iptData);
            var result = engine.Run();

            Assert.IsFalse(result.IsPass);
            Assert.AreEqual(1, result.Mismatches.Count);
            Assert.AreEqual("Exterior Gauge & Material", result.Mismatches[0].FieldName);
            Assert.AreEqual("18 GA STL GALV PPC", result.Mismatches[0].ExpectedValue);
            Assert.AreEqual("18 GA STL GALV", result.Mismatches[0].ActualValue);
        }

        [Test]
        public void TestBaseVerification_StructuralVsFormedChannelsAndIgnoreAccessories()
        {
            var userData = new UnitConstructionData
            {
                BaseRows = new List<BaseSurfaceRow>
                {
                    new BaseSurfaceRow
                    {
                        SourceSurfaceIam = "Base.iam",
                        BaseMaterial = "STL C CHNL",
                        FormedChannelGauge = "10",
                        FormedChannelMaterial = "STL HOT ROLL"
                    }
                }
            };

            var iptData = new IptScanResult
            {
                Parts = new List<IptProperties>
                {
                    // 1. Structural C-Channel
                    new IptProperties
                    {
                        OwnerIamPath = "Base.iam",
                        PartNumber = "Struct-Chan-1",
                        Description = "CHN:STRUCT 6IN",
                        YCMATL = "STL C CHNL"
                    },
                    // 2. Formed Channel
                    new IptProperties
                    {
                        OwnerIamPath = "Base.iam",
                        PartNumber = "Formed-Chan-1",
                        Description = "Channel, Formed 10ga",
                        MtlGauge = "10",
                        YCMATL = "STL HOT ROLL"
                    },
                    // 3. Ignore Base Accessories (lifting lug, perimeter angle, filler plate)
                    new IptProperties
                    {
                        OwnerIamPath = "Base.iam",
                        PartNumber = "Accessory-Lug",
                        Description = "LIFTING LUG A36",
                        YCMATL = "A36" // Not verified as main channel -> ignored
                    }
                }
            };

            var engine = new VerificationEngine(userData, iptData);
            var result = engine.Run();

            Assert.IsTrue(result.IsPass);
            Assert.AreEqual(0, result.Mismatches.Count);
        }

        [Test]
        public void TestBaseVerification_StructuralAngle()
        {
            var userData = new UnitConstructionData
            {
                BaseRows = new List<BaseSurfaceRow>
                {
                    new BaseSurfaceRow
                    {
                        SourceSurfaceIam = "Base.iam",
                        BaseMaterial = "STL C CHNL"
                    }
                }
            };

            var iptData = new IptScanResult
            {
                Parts = new List<IptProperties>
                {
                    new IptProperties
                    {
                        OwnerIamPath = "Base.iam",
                        PartNumber = "Struct-Angle-1",
                        Description = "ANG:STRUCT 3x3",
                        ModelNumber = "091-30117-187",
                        YCMATL = "STL ANGLE"
                    }
                }
            };

            var engine = new VerificationEngine(userData, iptData);
            var result = engine.Run();

            Assert.IsTrue(result.IsPass);
            Assert.AreEqual(0, result.Mismatches.Count);
        }

        [Test]
        public void TestBaseVerification_StructuralAngle_Aluminum()
        {
            var userData = new UnitConstructionData
            {
                BaseRows = new List<BaseSurfaceRow>
                {
                    new BaseSurfaceRow
                    {
                        SourceSurfaceIam = "Base.iam",
                        BaseMaterial = "ALM C CHNL"
                    }
                }
            };

            var iptData = new IptScanResult
            {
                Parts = new List<IptProperties>
                {
                    new IptProperties
                    {
                        OwnerIamPath = "Base.iam",
                        PartNumber = "Struct-Angle-Alum",
                        Description = "ANG:STRUCT 3x3 AL",
                        ModelNumber = "091-30117-189",
                        YCMATL = "ALM ANGLE"
                    }
                }
            };

            var engine = new VerificationEngine(userData, iptData);
            var result = engine.Run();

            Assert.IsTrue(result.IsPass);
            Assert.AreEqual(0, result.Mismatches.Count);
        }

        [Test]
        public void TestBaseVerification_SubFloorClassification()
        {
            var userData = new UnitConstructionData
            {
                BaseRows = new List<BaseSurfaceRow>
                {
                    new BaseSurfaceRow
                    {
                        SourceSurfaceIam = "Base.iam",
                        SubFloorGauge = "20",
                        SubFloorMaterial = "STL GALV"
                    }
                }
            };

            var iptData = new IptScanResult
            {
                Parts = new List<IptProperties>
                {
                    // Sub-floor sheet classified by model number
                    new IptProperties
                    {
                        OwnerIamPath = "Base.iam",
                        PartNumber = "Subfloor-Part-1",
                        ModelNumber = "091-30117-080",
                        MtlGauge = "20",
                        YCMATL = "STL GALV"
                    },
                    // Sub-floor sheet classified by description
                    new IptProperties
                    {
                        OwnerIamPath = "Base.iam",
                        PartNumber = "Subfloor-Part-2",
                        Description = "SHEET, SUBFLOOR GALV",
                        MtlGauge = "20",
                        YCMATL = "STL GALV"
                    }
                }
            };

            var engine = new VerificationEngine(userData, iptData);
            var result = engine.Run();

            Assert.IsTrue(result.IsPass);
            Assert.AreEqual(0, result.Mismatches.Count);
        }

        [Test]
        public void TestRoofTrim_MiscTrimPrecedence()
        {
            var userData = new UnitConstructionData
            {
                RoofRows = new List<RoofSurfaceRow>
                {
                    new RoofSurfaceRow
                    {
                        SourceSurfaceIam = "Roof.iam",
                        TrimSkinGauge = "16",
                        TrimSkinMaterial = "STL GALV PPC"
                    }
                }
            };

            var iptData = new IptScanResult
            {
                Parts = new List<IptProperties>
                {
                    // A custom roof cap part number 091-30119-007
                    // This is classified as Misc Trim, so it should NOT be checked against the standard Trim settings,
                    // and therefore should NOT produce a mismatch.
                    new IptProperties
                    {
                        OwnerIamPath = "Roof.iam",
                        PartNumber = "Custom-Roof-Trim",
                        ModelNumber = "091-30119-007",
                        Description = "roof corner cap - custom",
                        MtlGauge = "18",
                        YCMATL = "STL GALV" 
                    }
                }
            };

            var engine = new VerificationEngine(userData, iptData);
            var result = engine.Run();

            // Passes because Misc Trim is ignored/exempt from standard Trim checks
            Assert.IsTrue(result.IsPass);
            Assert.AreEqual(0, result.Mismatches.Count);
        }

        [Test]
        public void TestBaseVerification_PerimeterAngle()
        {
            var userData = new UnitConstructionData
            {
                BaseRows = new List<BaseSurfaceRow>
                {
                    new BaseSurfaceRow
                    {
                        SourceSurfaceIam = "Base.iam",
                        PerimeterAngleGauge = "12",
                        PerimeterAngleMaterial = "STL GALV"
                    }
                }
            };

            var iptData = new IptScanResult
            {
                Parts = new List<IptProperties>
                {
                    new IptProperties
                    {
                        OwnerIamPath = "Base.iam",
                        PartNumber = "Angle-Part-1",
                        Description = "Perimeter Angle 12ga",
                        MtlGauge = "12",
                        YCMATL = "STL GALV"
                    },
                    new IptProperties
                    {
                        OwnerIamPath = "Base.iam",
                        PartNumber = "Angle-Part-2",
                        Description = "Perimeter Angle 14ga",
                        MtlGauge = "14",
                        YCMATL = "STL GALV"
                    }
                }
            };

            var engine = new VerificationEngine(userData, iptData);
            var result = engine.Run();

            Assert.IsFalse(result.IsPass);
            Assert.AreEqual(1, result.Mismatches.Count);
            Assert.AreEqual("Angle-Part-2", result.Mismatches[0].IptPartNumber);
        }

        [Test]
        public void TestWallChannelVerification_NonAutomatingParts()
        {
            var userData = new UnitConstructionData
            {
                WallRows = new List<WallSurfaceRow>
                {
                    new WallSurfaceRow
                    {
                        SourceSurfaceIam = "Wall.iam",
                        ChannelSkinGauge = "16",
                        ChannelSkinMaterial = "STL GALV3"
                    }
                }
            };

            var iptData = new IptScanResult
            {
                Parts = new List<IptProperties>
                {
                    // Non-automating channel: expects STL GALV (STL GALV3 stripped of '3')
                    new IptProperties
                    {
                        OwnerIamPath = "Wall.iam",
                        PartNumber = "NonAuto-Channel-1",
                        ModelNumber = "091-30117-078",
                        Description = "Coil Panel Nesting Channel",
                        MtlGauge = "16",
                        YCMATL = "STL GALV"
                    },
                    // Automating channel: expects STL GALV3 (no stripping). This should trigger a mismatch if it is only STL GALV.
                    new IptProperties
                    {
                        OwnerIamPath = "Wall.iam",
                        PartNumber = "Auto-Channel-2",
                        ModelNumber = "091-30117-064",
                        Description = "Horizontal Channel",
                        MtlGauge = "16",
                        YCMATL = "STL GALV"
                    }
                }
            };

            var engine = new VerificationEngine(userData, iptData);
            var result = engine.Run();

            // Should have exactly 1 mismatch for the automating channel (expected: "16 GA STL GALV3", actual: "16 GA STL GALV")
            Assert.IsFalse(result.IsPass);
            Assert.AreEqual(1, result.Mismatches.Count);
            Assert.AreEqual("Auto-Channel-2", result.Mismatches[0].IptPartNumber);
            Assert.AreEqual("16 GA STL GALV3", result.Mismatches[0].ExpectedValue);
            Assert.AreEqual("16 GA STL GALV", result.Mismatches[0].ActualValue);
        }
        [Test]
        public void TestWallVerification_SealOffAngle_MatchesLinerMaterial()
        {
            // Seal-Off Angle with correct gauge (16 GA) and material matching the liner → no mismatch.
            var userData = new UnitConstructionData
            {
                WallRows = new List<WallSurfaceRow>
                {
                    new WallSurfaceRow
                    {
                        SourceSurfaceIam  = "Wall.iam",
                        InteriorLinerGauge    = "22",
                        InteriorLinerMaterial = "STL GALV"
                    }
                }
            };

            var iptData = new IptScanResult
            {
                Parts = new List<IptProperties>
                {
                    new IptProperties
                    {
                        OwnerIamPath = "Wall.iam",
                        PartNumber   = "SealOff-Pass",
                        Description  = "Wall Seal-Off Angle",
                        MtlGauge     = "16",
                        YCMATL       = "STL GALV"
                    }
                }
            };

            var engine = new VerificationEngine(userData, iptData);
            var result = engine.Run();

            Assert.IsTrue(result.IsPass);
            Assert.AreEqual(0, result.Mismatches.Count);
        }

        [Test]
        public void TestWallVerification_SealOffAngle_WrongGaugeMismatch()
        {
            // Seal-Off Angle with wrong gauge (18 instead of fixed 16) → one mismatch.
            var userData = new UnitConstructionData
            {
                WallRows = new List<WallSurfaceRow>
                {
                    new WallSurfaceRow
                    {
                        SourceSurfaceIam      = "Wall.iam",
                        InteriorLinerGauge    = "22",
                        InteriorLinerMaterial = "STL GALV"
                    }
                }
            };

            var iptData = new IptScanResult
            {
                Parts = new List<IptProperties>
                {
                    new IptProperties
                    {
                        OwnerIamPath = "Wall.iam",
                        PartNumber   = "SealOff-Fail",
                        Description  = "Wall Seal-Off Angle",
                        MtlGauge     = "18",          // Wrong — rule expects fixed:16
                        YCMATL       = "STL GALV"
                    }
                }
            };

            var engine = new VerificationEngine(userData, iptData);
            var result = engine.Run();

            Assert.IsFalse(result.IsPass);
            Assert.AreEqual(1, result.Mismatches.Count);
            Assert.AreEqual("Seal-Off Angle Gauge & Material", result.Mismatches[0].FieldName);
            Assert.AreEqual("16 GA STL GALV", result.Mismatches[0].ExpectedValue);
            Assert.AreEqual("18 GA STL GALV", result.Mismatches[0].ActualValue);
        }

        [Test]
        public void TestConditionalResolve_WallCornerCap_Steel()
        {
            var userData = new UnitConstructionData
            {
                WallRows = new List<WallSurfaceRow>
                {
                    new WallSurfaceRow
                    {
                        SourceSurfaceIam      = "Wall.iam",
                        ExteriorSkinMaterial  = "STL GALV PPC"
                    }
                }
            };

            var iptData = new IptScanResult
            {
                Parts = new List<IptProperties>
                {
                    new IptProperties
                    {
                        OwnerIamPath = "Wall.iam",
                        PartNumber   = "CornerCap-Steel",
                        Description  = "Wall Corner Cap",
                        ModelNumber  = "091-30117-072",
                        MtlGauge     = "16",
                        YCMATL       = "STL GALV PPC"
                    }
                }
            };

            var engine = new VerificationEngine(userData, iptData);
            var result = engine.Run();

            Assert.IsTrue(result.IsPass);
            Assert.AreEqual(0, result.Mismatches.Count);
        }

        [Test]
        public void TestConditionalResolve_WallCornerLiner_Aluminum()
        {
            var userData = new UnitConstructionData
            {
                WallRows = new List<WallSurfaceRow>
                {
                    new WallSurfaceRow
                    {
                        SourceSurfaceIam      = "Wall.iam",
                        InteriorLinerMaterial = "ALM SHT"
                    }
                }
            };

            var iptData = new IptScanResult
            {
                Parts = new List<IptProperties>
                {
                    new IptProperties
                    {
                        OwnerIamPath = "Wall.iam",
                        PartNumber   = "CornerLiner-Alum",
                        Description  = "Wall Corner Liner",
                        ModelNumber  = "091-30117-073",
                        MtlGauge     = "14",
                        YCMATL       = "ALM SHT"
                    }
                }
            };

            var engine = new VerificationEngine(userData, iptData);
            var result = engine.Run();

            Assert.IsTrue(result.IsPass);
            Assert.AreEqual(0, result.Mismatches.Count);
        }

        [Test]
        public void TestConditionalResolve_WallCornerCap_Aluminum()
        {
            var userData = new UnitConstructionData
            {
                WallRows = new List<WallSurfaceRow>
                {
                    new WallSurfaceRow
                    {
                        SourceSurfaceIam      = "Wall.iam",
                        ExteriorSkinMaterial  = "ALM SHT"
                    }
                }
            };

            var iptData = new IptScanResult
            {
                Parts = new List<IptProperties>
                {
                    new IptProperties
                    {
                        OwnerIamPath = "Wall.iam",
                        PartNumber   = "CornerCap-Alum",
                        Description  = "Wall Corner Cap",
                        ModelNumber  = "091-30117-072",
                        MtlGauge     = "14",
                        YCMATL       = "ALM SHT"
                    }
                }
            };

            var engine = new VerificationEngine(userData, iptData);
            var result = engine.Run();

            Assert.IsTrue(result.IsPass);
            Assert.AreEqual(0, result.Mismatches.Count);
        }

        [Test]
        public void TestConditionalResolve_WallCornerCap_AluminumEmbossed()
        {
            var userData = new UnitConstructionData
            {
                WallRows = new List<WallSurfaceRow>
                {
                    new WallSurfaceRow
                    {
                        SourceSurfaceIam      = "Wall.iam",
                        ExteriorSkinMaterial  = "ALM EMB"
                    }
                }
            };

            var iptData = new IptScanResult
            {
                Parts = new List<IptProperties>
                {
                    new IptProperties
                    {
                        OwnerIamPath = "Wall.iam",
                        PartNumber   = "CornerCap-AlumEmb",
                        Description  = "Wall Corner Cap",
                        ModelNumber  = "091-30117-072",
                        MtlGauge     = "20",
                        YCMATL       = "ALM EMB"
                    }
                }
            };

            var engine = new VerificationEngine(userData, iptData);
            var result = engine.Run();

            Assert.IsTrue(result.IsPass);
            Assert.AreEqual(0, result.Mismatches.Count);
        }

        [Test]
        public void TestSharedWall_SkinPartVerifiesAgainstLinerProperties()
        {
            var userData = new UnitConstructionData
            {
                WallRows = new List<WallSurfaceRow>
                {
                    new WallSurfaceRow
                    {
                        SourceSurfaceIam      = "Wall.iam",
                        IsSharedWall          = true,
                        ExteriorSkinGauge     = "20",
                        ExteriorSkinMaterial  = "STL GALV",
                        InteriorLinerGauge    = "18",
                        InteriorLinerMaterial = "ALM SHT"
                    }
                }
            };

            var iptData = new IptScanResult
            {
                Parts = new List<IptProperties>
                {
                    new IptProperties
                    {
                        OwnerIamPath = "Wall.iam",
                        PartNumber   = "Skin-Part-As-Liner",
                        Description  = "casing skin panel",
                        ModelNumber  = "091-30117-083", // classified as Skin
                        MtlGauge     = "18", // should verify against liner gauge
                        YCMATL       = "ALM SHT" // should verify against liner material
                    }
                }
            };

            var engine = new VerificationEngine(userData, iptData);
            var result = engine.Run();

            Assert.IsTrue(result.IsPass);
            Assert.AreEqual(0, result.Mismatches.Count);
        }
    }
}
