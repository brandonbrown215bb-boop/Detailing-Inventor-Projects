using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using UnitConstructionVerifier.Extraction;
using UnitConstructionVerifier.Models;

namespace UnitConstructionVerifier.Tests
{
    [TestFixture]
    public class ConstructionDataBuilderTests
    {
        [OneTimeSetUp]
        public void Setup()
        {
            MaterialsConfig.Initialize();
        }

        [Test]
        public void TestSegmentSorting_AirflowDirectionDescendingX()
        {
            // Create three mock ConfigData elements representing segments at different X positions
            // They should be sorted physically from intake to discharge (X descending)
            var configs = new List<ConfigData>
            {
                new ConfigData
                {
                    SurfaceType = "ROOF",
                    SourceIamPath = "Seg_Middle.iam",
                    Roof = new SurfaceGeometryContainer
                    {
                        GeometryList = new List<GeometryItem> { new GeometryItem { X = 50.0 } }
                    }
                },
                new ConfigData
                {
                    SurfaceType = "ROOF",
                    SourceIamPath = "Seg_Intake.iam",
                    Roof = new SurfaceGeometryContainer
                    {
                        GeometryList = new List<GeometryItem> { new GeometryItem { X = 100.0 } }
                    }
                },
                new ConfigData
                {
                    SurfaceType = "ROOF",
                    SourceIamPath = "Seg_Discharge.iam",
                    Roof = new SurfaceGeometryContainer
                    {
                        GeometryList = new List<GeometryItem> { new GeometryItem { X = 10.0 } }
                    }
                }
            };

            var builder = new ConstructionDataBuilder(configs, null, new IptScanResult());
            var result = builder.Build();

            Assert.AreEqual(3, result.RoofRows.Count);
            // Verify ordering by X descending: Seg_Intake (100.0) -> Seg_Middle (50.0) -> Seg_Discharge (10.0)
            Assert.AreEqual("Seg_Intake", result.RoofRows[0].PartNumber);
            Assert.AreEqual("Seg_Middle", result.RoofRows[1].PartNumber);
            Assert.AreEqual("Seg_Discharge", result.RoofRows[2].PartNumber);
        }

        [Test]
        public void TestCasingThicknessFallbacks()
        {
            // Test 1: Config with nominal surface thickness specified
            var config1 = new ConfigData
            {
                SurfaceType = "ROOF",
                SourceIamPath = "Config1.iam",
                NominalSurfaceThickness = 3.0,
                Roof = new SurfaceGeometryContainer
                {
                    GeometryList = new List<GeometryItem> { new GeometryItem { X = 10.0 } }
                }
            };

            // Test 2: Config with segment specific thicknesses
            var segment = new SurfaceSegment
            {
                SegmentType = "MB",
                SegmentTypeSuffix = "1",
                WallThickness_Top = 2.0,
                SkinMaterialType_Top = "Steel, Galvanized",
                SkinMaterialGauge_Top = 18.0
            };
            var config2 = new ConfigData
            {
                SurfaceType = "ROOF",
                SourceIamPath = "Config2.iam",
                NominalSurfaceThickness = 0.0,
                SurfaceSegmentList = new List<SurfaceSegment> { segment },
                Roof = new SurfaceGeometryContainer
                {
                    GeometryList = new List<GeometryItem> { new GeometryItem { X = 20.0 } }
                }
            };

            var configs = new List<ConfigData> { config1, config2 };
            var builder = new ConstructionDataBuilder(configs, null, new IptScanResult());
            var result = builder.Build();

            // Config2 (X = 20) should be first, Config1 (X = 10) second
            Assert.AreEqual(2, result.RoofRows.Count);

            var rowConfig2 = result.RoofRows[0];
            Assert.AreEqual("Config2", rowConfig2.PartNumber);
            Assert.AreEqual("2\"", rowConfig2.Thickness);

            var rowConfig1 = result.RoofRows[1];
            Assert.AreEqual("Config1", rowConfig1.PartNumber);
            Assert.AreEqual("3\"", rowConfig1.Thickness);
        }

        [Test]
        public void TestWallChannelMaterialMapping()
        {
            var config2In = new ConfigData
            {
                SurfaceType = "WALL",
                SourceIamPath = "Wall_2in.iam",
                NominalSurfaceThickness = 2.0
            };
            var config3In = new ConfigData
            {
                SurfaceType = "WALL",
                SourceIamPath = "Wall_3in.iam",
                NominalSurfaceThickness = 3.0
            };
            var config4In = new ConfigData
            {
                SurfaceType = "WALL",
                SourceIamPath = "Wall_4in.iam",
                NominalSurfaceThickness = 4.0
            };
            var configUnk = new ConfigData
            {
                SurfaceType = "WALL",
                SourceIamPath = "Wall_Unk.iam",
                NominalSurfaceThickness = 5.0
            };
            var configEmpty = new ConfigData
            {
                SurfaceType = "WALL",
                SourceIamPath = "Wall_Empty.iam",
                NominalSurfaceThickness = 0.0
            };

            var configs = new List<ConfigData> { config2In, config3In, config4In, configUnk, configEmpty };
            var builder = new ConstructionDataBuilder(configs, null, new IptScanResult());
            var result = builder.Build();

            Assert.AreEqual(5, result.WallRows.Count);

            var wall2 = result.WallRows.First(w => w.PartNumber == "Wall_2in");
            Assert.AreEqual("16", wall2.ChannelSkinGauge);
            Assert.AreEqual("STL GALV2", wall2.ChannelSkinMaterial);

            var wall3 = result.WallRows.First(w => w.PartNumber == "Wall_3in");
            Assert.AreEqual("16", wall3.ChannelSkinGauge);
            Assert.AreEqual("STL GALV3", wall3.ChannelSkinMaterial);

            var wall4 = result.WallRows.First(w => w.PartNumber == "Wall_4in");
            Assert.AreEqual("16", wall4.ChannelSkinGauge);
            Assert.AreEqual("STL GALV4", wall4.ChannelSkinMaterial);

            var wallUnk = result.WallRows.First(w => w.PartNumber == "Wall_Unk");
            Assert.AreEqual("16", wallUnk.ChannelSkinGauge);
            Assert.AreEqual("STL GALV?", wallUnk.ChannelSkinMaterial);

            var wallEmpty = result.WallRows.First(w => w.PartNumber == "Wall_Empty");
            Assert.AreEqual("16", wallEmpty.ChannelSkinGauge);
            Assert.AreEqual("STL GALV?", wallEmpty.ChannelSkinMaterial);
        }

        [Test]
        public void TestBaseDropdownsExtraction()
        {
            var config = new ConfigData
            {
                SurfaceType = "BASE",
                SourceIamPath = "Base_Test.iam",
                DefaultFloorMaterialGauge = 16,
                DefaultFloorMaterialType = "STEEL, GALVANIZED"
            };

            var ipt1 = new IptProperties
            {
                OwnerIamPath = "Base_Test.iam",
                PartNumber = "Subfloor-Part",
                ModelNumber = "091-30117-080",
                MtlGauge = "20",
                YCMATL = "STL GALV"
            };
            var ipt2 = new IptProperties
            {
                OwnerIamPath = "Base_Test.iam",
                PartNumber = "Formed-Part",
                Description = "Channel, Formed 10ga",
                MtlGauge = "10",
                YCMATL = "STL HOT ROLL"
            };
            var ipt3 = new IptProperties
            {
                OwnerIamPath = "Base_Test.iam",
                PartNumber = "Angle-Part",
                Description = "Perimeter Angle 12ga",
                MtlGauge = "12",
                YCMATL = "STL GALV"
            };

            var scan = new IptScanResult { Parts = new List<IptProperties> { ipt1, ipt2, ipt3 } };
            var builder = new ConstructionDataBuilder(new List<ConfigData> { config }, null, scan);
            var result = builder.Build();

            Assert.AreEqual(1, result.BaseRows.Count);
            var row = result.BaseRows[0];

            Assert.AreEqual("16", row.FloorGauge);
            Assert.AreEqual("STL GALV", row.FloorMaterial);
            Assert.AreEqual("20", row.SubFloorGauge);
            Assert.AreEqual("STL GALV", row.SubFloorMaterial);
            Assert.AreEqual("10", row.FormedChannelGauge);
            Assert.AreEqual("STL HOT ROLL", row.FormedChannelMaterial);
            Assert.AreEqual("12", row.PerimeterAngleGauge);
            Assert.AreEqual("STL GALV", row.PerimeterAngleMaterial);
        }

        [Test]
        public void TestSharedWall_SkinInheritsLinerProperties()
        {
            var config = new ConfigData
            {
                SurfaceType = "WALL",
                SourceIamPath = "SharedWall.iam",
                SurfaceId = "shared-wall-id",
                UnitSurfaceList = new List<UnitSurface>
                {
                    new UnitSurface
                    {
                        Id = "shared-wall-id",
                        IsInteriorWall = true
                    }
                },
                SurfaceSegmentList = new List<SurfaceSegment>
                {
                    new SurfaceSegment
                    {
                        SegmentType = "MB",
                        SegmentTypeSuffix = "1",
                        WallThickness_Left = 3.0,
                        SkinMaterialGauge_Left = 20.0,
                        SkinMaterialType_Left = "Steel, Galvanized",
                        LinerMaterialGauge_Left = 18.0,
                        LinerMaterialType_Left = "Aluminum 6061"
                    }
                },
                Wall = new SurfaceGeometryContainer
                {
                    GeometryList = new List<GeometryItem> { new GeometryItem { X = 100.0 } }
                }
            };

            var builder = new ConstructionDataBuilder(new List<ConfigData> { config }, null, new IptScanResult());
            var result = builder.Build();

            Assert.AreEqual(1, result.WallRows.Count);
            var row = result.WallRows[0];
            Assert.IsTrue(row.IsSharedWall);
            Assert.AreEqual("18", row.ExteriorSkinGauge);
            Assert.AreEqual("Aluminum 6061", row.ExteriorSkinMaterial);
            Assert.AreEqual("18", row.InteriorLinerGauge);
            Assert.AreEqual("Aluminum 6061", row.InteriorLinerMaterial);
        }
    }
}
