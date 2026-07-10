using NUnit.Framework;
using System;
using UnitConstructionVerifier.Models;

namespace UnitConstructionVerifier.Tests
{
    [TestFixture]
    public class MaterialsConfigTests
    {
        [OneTimeSetUp]
        public void Setup()
        {
            // Initialize config paths
            MaterialsConfig.Initialize();
        }

        [Test]
        public void TestMapMaterial_StandardMappings()
        {
            Assert.AreEqual("STL GALV", MaterialsConfig.MapMaterial("STEEL, GALVANIZED"));
            Assert.AreEqual("STL GALV", MaterialsConfig.MapMaterial("Steel, Galvanized"));
            Assert.AreEqual("ALM SHT", MaterialsConfig.MapMaterial("ALUMINUM"));
            Assert.AreEqual("SST304", MaterialsConfig.MapMaterial("STAINLESS STEEL"));
        }

        [Test]
        public void TestMapMaterial_CustomOrPassThrough()
        {
            Assert.AreEqual("STL GALV PPC", MaterialsConfig.MapMaterial("STL GALV PPC"));
            Assert.AreEqual("MY_CUSTOM_MAT", MaterialsConfig.MapMaterial("MY_CUSTOM_MAT"));
            Assert.AreEqual("", MaterialsConfig.MapMaterial(null!));
            Assert.AreEqual("", MaterialsConfig.MapMaterial("   "));
        }

        [Test]
        public void TestMapGauge_Normalizations()
        {
            // Handles standard gauge mapping lookup
            Assert.AreEqual("18", MaterialsConfig.MapGauge("0.0478"));
            Assert.AreEqual("16", MaterialsConfig.MapGauge("0.0598"));
            Assert.AreEqual("12", MaterialsConfig.MapGauge("0.1046"));
            
            // Handles floats e.g. "16.00000" -> "16"
            Assert.AreEqual("16", MaterialsConfig.MapGauge("16.00000"));
            Assert.AreEqual("18", MaterialsConfig.MapGauge("18"));
            Assert.AreEqual("22", MaterialsConfig.MapGauge("22.0"));
        }

        [Test]
        public void TestMapGauge_PreciseThicknessLookup()
        {
            // JCI specific precise decimal thicknesses
            // e.g. 0.05604 (16 GA STL GALV PPC) vs 0.05601 (16 GA STL GALV)
            Assert.AreEqual("16", MaterialsConfig.MapGauge("0.05604"));
            Assert.AreEqual("16", MaterialsConfig.MapGauge("0.05601"));
            Assert.AreEqual("18", MaterialsConfig.MapGauge("0.04803"));

            // Nominal decimal thicknesses should fallback to closest default JCI thickness
            Assert.AreEqual("18", MaterialsConfig.MapGauge("0.045"));
            Assert.AreEqual("20", MaterialsConfig.MapGauge("0.034"));
            Assert.AreEqual("24", MaterialsConfig.MapGauge("0.022"));
            Assert.AreEqual("22", MaterialsConfig.MapGauge("0.028"));
        }

        [Test]
        public void TestResolveFromThickness()
        {
            // 0.05604 -> 16 GA STL GALV PPC
            bool resolved = MaterialsConfig.ResolveFromThickness("0.05604", out string gauge, out string material);
            Assert.IsTrue(resolved);
            Assert.AreEqual("16", gauge);
            Assert.AreEqual("STL GALV PPC", material);

            // 0.05601 -> 16 GA STL GALV
            resolved = MaterialsConfig.ResolveFromThickness("0.05601", out string gauge2, out string material2);
            Assert.IsTrue(resolved);
            Assert.AreEqual("16", gauge2);
            Assert.AreEqual("STL GALV", material2);

            // 0.12701 -> 10 GA STL HOT ROLL
            resolved = MaterialsConfig.ResolveFromThickness("0.12701", out string gauge3, out string material3);
            Assert.IsTrue(resolved);
            Assert.AreEqual("10", gauge3);
            Assert.AreEqual("STL HOT ROLL", material3);

            // Nominal decimal thicknesses fallback matching
            resolved = MaterialsConfig.ResolveFromThickness("0.045", out string gaugeNom, out string materialNom);
            Assert.IsTrue(resolved);
            Assert.AreEqual("18", gaugeNom);
            Assert.AreEqual("STL GALV", materialNom);

            resolved = MaterialsConfig.ResolveFromThickness("0.034", out string gaugeNom2, out string materialNom2);
            Assert.IsTrue(resolved);
            Assert.AreEqual("20", gaugeNom2);
            Assert.AreEqual("STL GALV", materialNom2);

            // Invalid thickness
            resolved = MaterialsConfig.ResolveFromThickness("invalid", out _, out _);
            Assert.IsFalse(resolved);
        }

        [Test]
        public void TestGetPartClassification()
        {
            // Stock number classification matches
            Assert.AreEqual("Liner", MaterialsConfig.GetPartClassification("091-30117-082", "random desc"));
            Assert.AreEqual("Skin", MaterialsConfig.GetPartClassification("091-30117-083", ""));
            Assert.AreEqual("Trim", MaterialsConfig.GetPartClassification("091-30117-074", "other desc"));
            Assert.AreEqual("Structural Angle", MaterialsConfig.GetPartClassification("091-30117-187", "Structural steel angle"));
            Assert.AreEqual("Misc Trim", MaterialsConfig.GetPartClassification("091-30117-076", "Split Cover"));
            Assert.AreEqual("Sub-Floor", MaterialsConfig.GetPartClassification("091-30117-080", "Attachment Angle"));
            Assert.AreEqual("Split Cover", MaterialsConfig.GetPartClassification("091-30117-075", ""));
            Assert.AreEqual("Formed Channel", MaterialsConfig.GetPartClassification("091-30117-051", "Formed Channel"));

            // Legacy Description Fallbacks when stock number is unknown
            Assert.AreEqual("Liner", MaterialsConfig.GetPartClassification("UNKNOWN", "Corner liner"));
            Assert.AreEqual("Trim", MaterialsConfig.GetPartClassification("", "roof corner cap"));
            Assert.AreEqual("Channel", MaterialsConfig.GetPartClassification(null, "C:SC-100 Channel"));
            Assert.AreEqual("Skin", MaterialsConfig.GetPartClassification("123-456", "outer panel"));
            Assert.AreEqual("Floor Sheet", MaterialsConfig.GetPartClassification("ABC-XYZ", "bottom floor plate"));
            Assert.AreEqual("Structural Channel", MaterialsConfig.GetPartClassification("ACCESSORY", "CHN:STRUCT channel"));
            Assert.AreEqual("Perimeter Angle", MaterialsConfig.GetPartClassification("ANGLE", "low side perimeter angle"));
            Assert.AreEqual("Unknown", MaterialsConfig.GetPartClassification("UNKNOWN", "random bracket"));
        }

        [Test]
        public void TestGetPartClassification_SealOffAngle()
        {
            // Description keyword 'seal-off angle' (case-insensitive) should classify as the new rule-based type.
            Assert.AreEqual("Seal-Off Angle", MaterialsConfig.GetPartClassification("",        "Wall Seal-Off Angle"));
            Assert.AreEqual("Seal-Off Angle", MaterialsConfig.GetPartClassification("UNKNOWN", "wall seal-off angle"));
            Assert.AreEqual("Seal-Off Angle", MaterialsConfig.GetPartClassification(null,      "WALL SEAL-OFF ANGLE 16GA"));

            // Roof seal-off angle still routes to Misc Trim via the existing legacy fallback.
            Assert.AreEqual("Misc Trim",      MaterialsConfig.GetPartClassification("",        "Roof Seal-Off Angle"));
        }
    }
}
