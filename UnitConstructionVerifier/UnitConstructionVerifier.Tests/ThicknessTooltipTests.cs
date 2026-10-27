using NUnit.Framework;
using UnitConstructionVerifier.Models;
using UnitConstructionVerifier.Operations;

namespace UnitConstructionVerifier.Tests
{
    [TestFixture]
    public class ThicknessTooltipTests
    {
        [OneTimeSetUp]
        public void Setup() => MaterialsConfig.Initialize();

        private static IptProperties Part(string gauge, string material, string thickness) =>
            new IptProperties { MtlGauge = gauge, YCMATL = material, Thickness = thickness };

        [Test]
        public void MatchedSheetMetal_ShowsGaugeMaterialAndThicknessOnly()
        {
            string text = ThicknessTooltip.Build(Part("16", "STL GALV", "0.05601"), 0.05601);
            Assert.AreEqual("16 GA STL GALV\n0.056\"", text);
        }

        [Test]
        public void MaterialVariant_ComesFromTheMaterialNotTheThickness()
        {
            string text = ThicknessTooltip.Build(Part("16", "STL GALV PPC", "0.05604"), 0.05604);
            Assert.AreEqual("16 GA STL GALV PPC\n0.056\"", text);
        }

        [Test]
        public void MeasuredWithinTolerance_IsNotShown()
        {
            string text = ThicknessTooltip.Build(Part("16", "STL GALV", "0.05601"), 0.0561);
            Assert.AreEqual("16 GA STL GALV\n0.056\"", text);
        }

        [Test]
        public void MeasuredOffTheRecordedThickness_AddsMeasuredLine()
        {
            string text = ThicknessTooltip.Build(Part("16", "STL GALV", "0.05601"), 0.0598);
            Assert.AreEqual("16 GA STL GALV\n0.056\"\nMeasured 0.0598\"", text);
        }

        [Test]
        public void UnmatchedGauge_ShowsMaterialAloneAndSaysSo()
        {
            string text = ThicknessTooltip.Build(Part("0.0612", "STL GALV", "0.0612"), 0.0612);
            Assert.AreEqual("STL GALV\n0.061\"\nNo gauge match", text);
        }

        [Test]
        public void SolidPart_UsesRecordedThicknessNotItsSize()
        {
            // No measured value for a non-sheet-metal part: its bounding box is not its thickness.
            string text = ThicknessTooltip.Build(Part("10", "STL HOT ROLL", "0.12701"), null);
            Assert.AreEqual("10 GA STL HOT ROLL\n0.127\"", text);
        }

        [Test]
        public void SolidPartWithNothingRecorded_SaysSoInsteadOfGuessing()
        {
            string text = ThicknessTooltip.Build(Part("", "STL HOT ROLL", ""), null);
            Assert.AreEqual("STL HOT ROLL\nNo thickness recorded", text);
        }

        [Test]
        public void NothingRecorded_SaysSo()
        {
            string text = ThicknessTooltip.Build(Part("", "", ""), null);
            Assert.AreEqual("No gauge or material recorded\nNo thickness recorded", text);
        }

        [Test]
        public void TooltipHasNoStyleOrTypeLines()
        {
            var part = Part("18", "STL GALV", "0.04501");
            part.MaterialStyle = "STL GALV";
            StringAssert.DoesNotContain("Type:", ThicknessTooltip.Build(part, 0.04501));
            StringAssert.DoesNotContain("Style:", ThicknessTooltip.Build(part, 0.04501));
        }
    }
}
