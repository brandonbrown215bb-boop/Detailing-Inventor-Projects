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

            // Invalid thickness
            resolved = MaterialsConfig.ResolveFromThickness("invalid", out _, out _);
            Assert.IsFalse(resolved);
        }
    }
}
