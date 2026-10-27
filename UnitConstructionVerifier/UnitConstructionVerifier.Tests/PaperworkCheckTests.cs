using System.Collections.Generic;
using NUnit.Framework;
using UnitConstructionVerifier.Engine;
using UnitConstructionVerifier.Models;

namespace UnitConstructionVerifier.Tests
{
    [TestFixture]
    public class PaperworkCheckTests
    {
        private const string Surface = @"C:\Unit\391Z010216-0030\391Z010216-0061.IAM";

        [OneTimeSetUp]
        public void Setup() => MaterialsConfig.Initialize();

        private static BomRow Row(string seq, string pn, string desc, string sheet = "Liners:2") =>
            new BomRow { Sheet = sheet, Seq = seq, PartNumber = pn, Description = desc };

        private static IptProperties Part(string pn, string gauge, string material, string owner = Surface) =>
            new IptProperties { PartNumber = pn, MtlGauge = gauge, YCMATL = material, OwnerIamPath = owner };

        private static SurfacePaperwork Paperwork(params BomRow[] rows)
        {
            var p = new SurfacePaperwork { SurfaceIamPath = Surface, Source = @"C:\Unit\391Z010216-0030\391Z010216-0061.idw" };
            p.Rows.AddRange(rows);
            return p;
        }

        [TestCase("SQ PART - C:EXT-WALL LINER 36.5L X 70H\nGALVANIZED STEEL, 22 GA", "STL GALV", "22")]
        [TestCase("SQ PART - C:EXT-WALL SKIN 36.5L X 71.25H\r\nGALVANIZED STEEL, PRE-PAINT CHAMPAGNE, 18 GA", "STL GALV PPC", "18")]
        [TestCase("C:INT-FLOOR 62.875L X 24.8H\nALUMINUM, DIAMOND PLATE, 0.125 GA", "ALM DIA", "0.125")]
        public void ReadsThePrintedMaterialLine(string desc, string material, string gauge)
        {
            Assert.IsTrue(PaperworkCheck.TryReadMaterial(desc, out string m, out string g, out _));
            Assert.AreEqual(material, m);
            Assert.AreEqual(gauge, g);
        }

        [TestCase("C:SC 1.5 x 3")]                              // channel: no material printed
        [TestCase("\n\nVOL: 0.35, DENSITY: 2.65")]               // insulation
        [TestCase("SQ PART - X\nUNOBTAINIUM, 22 GA")]            // wording YCShell doesn't print
        public void RowsWithoutAKnownMaterialAreSkipped(string desc)
        {
            Assert.IsFalse(PaperworkCheck.TryReadMaterial(desc, out _, out _, out _));
        }

        [Test]
        public void SW0004_0061_LinersAndSkinsPrintTheConfiguredGauge()
        {
            // Liners written to 20 GA and skins to 16 GA PPC; the surface configuration still says 22 / 18.
            var paperwork = Paperwork(
                Row("M", "091Z010216-0464", "SQ PART - C:EXT-WALL LINER 36.5L X 70H\nGALVANIZED STEEL, 22 GA"),
                Row("O", "091Z010216-0466", "SQ PART - C:EXT-WALL SKIN 36.5L X 71.25H\nGALVANIZED STEEL, PRE-PAINT CHAMPAGNE, 18 GA", "Skins:3"),
                Row("A", "091Z010216-0211", "C:SC 1.5 x 3", "Channels:1"));
            var parts = new List<IptProperties>
            {
                Part("091Z010216-0464", "20", "STL GALV"),
                Part("091Z010216-0466", "16", "STL GALV PPC"),
                Part("091Z010216-0211", "16", "STL GALV"),
            };

            var findings = PaperworkCheck.Compare(paperwork, parts);

            Assert.AreEqual(2, findings.Count);
            Assert.AreEqual("22 GA STL GALV", findings[0].IdwValue);
            Assert.AreEqual("20 GA STL GALV", findings[0].PartValue);
            Assert.AreEqual("18 GA STL GALV PPC", findings[1].IdwValue);
            Assert.AreEqual("16 GA STL GALV PPC", findings[1].PartValue);
        }

        [Test]
        public void MatchingRow_IsNotReported()
        {
            var paperwork = Paperwork(Row("M", "091Z010216-0464", "LINER\nGALVANIZED STEEL, 22 GA"));
            Assert.IsEmpty(PaperworkCheck.Compare(paperwork, new[] { Part("091Z010216-0464", "22", "STL GALV") }));
        }

        [Test]
        public void DecimalGaugeOnThePart_IsMappedBeforeComparing()
        {
            // INPUT_PARAMETER_Mtl_Gauge falls back to the coded thickness when the gauge is missing.
            var paperwork = Paperwork(Row("M", "091Z010216-0464", "LINER\nGALVANIZED STEEL, 20 GA"));
            Assert.IsEmpty(PaperworkCheck.Compare(paperwork, new[] { Part("091Z010216-0464", "0.03401", "STL GALV") }));
        }

        [Test]
        public void MaterialDifference_IsReported()
        {
            var paperwork = Paperwork(Row("O", "091Z010216-0466", "SKIN\nGALVANIZED STEEL, 18 GA", "Skins:3"));
            var findings = PaperworkCheck.Compare(paperwork, new[] { Part("091Z010216-0466", "18", "STL GALV PPC") });
            Assert.AreEqual(1, findings.Count);
            Assert.AreEqual("18 GA STL GALV PPC", findings[0].PartValue);
        }

        [Test]
        public void PartNotInTheUnit_IsReported()
        {
            var paperwork = Paperwork(Row("M", "091Z010216-0999", "LINER\nGALVANIZED STEEL, 22 GA"));
            var findings = PaperworkCheck.Compare(paperwork, new IptProperties[0]);
            Assert.AreEqual("part not found in the unit", findings[0].Note);
        }

        [Test]
        public void Report_ListsEachSurfaceAndEachMismatch()
        {
            var paperwork = Paperwork(Row("M", "091Z010216-0464", "LINER\nGALVANIZED STEEL, 22 GA"));
            var findings = PaperworkCheck.Compare(paperwork, new[] { Part("091Z010216-0464", "20", "STL GALV") });
            var missing = new SurfacePaperwork { SurfaceIamPath = @"C:\Unit\391Z010216-0021\391Z010216-0056.IAM", Problem = "no IDW next to the IAM or in its ZIP" };

            string report = PaperworkCheck.Report(new[] { (paperwork, findings), (missing, new List<PaperworkFinding>()) });

            StringAssert.Contains("1 mismatched rows in 1 checked, 2 surfaces", report);
            StringAssert.Contains("Liners:2 M 091Z010216-0464: IDW 22 GA STL GALV, part 20 GA STL GALV", report);
            StringAssert.Contains("391Z010216-0056: no IDW next to the IAM or in its ZIP", report);
        }
    }
}
