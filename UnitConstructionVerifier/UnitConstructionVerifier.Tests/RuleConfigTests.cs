using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnitConstructionVerifier.Models;

namespace UnitConstructionVerifier.Tests
{
    [TestFixture]
    public class RuleConfigTests
    {
        [OneTimeSetUp]
        public void Setup()
        {
            MaterialsConfig.Initialize();
        }

        [Test]
        public void ShippedConfig_HasNoRuleProblems()
        {
            Assert.IsEmpty(MaterialsConfig.RuleProblems, string.Join("\n", MaterialsConfig.RuleProblems));
        }

        [Test]
        public void CornerStockNumbers_ResolveToCornerRules()
        {
            Assert.AreEqual("Wall Corner Cap", MaterialsConfig.GetPartClassification("091-30117-072", ""));
            Assert.AreEqual("Wall Corner Liner", MaterialsConfig.GetPartClassification("091-30117-073", ""));
            Assert.AreEqual("Structural Angle", MaterialsConfig.GetPartClassification("091-30117-187", ""));
            Assert.AreEqual("Structural Angle", MaterialsConfig.GetPartClassification("091-30117-189", ""));
        }

        [TestCase("Floor Perimeter Angle", "Perimeter Angle")]
        [TestCase("Sub-Floor Sheet", "Sub-Floor")]
        [TestCase("Sub Floor", "Sub-Floor")]
        [TestCase("SUBFLOOR", "Sub-Floor")]
        [TestCase("Wall Corner Liner", "Wall Corner Liner")]
        [TestCase("Wall Corner Cap", "Wall Corner Cap")]
        [TestCase("Roof Seal-Off Angle", "Roof Accessory")]
        [TestCase("ROOF SUPER ANGLE 52x2", "Roof Accessory")]
        [TestCase("TIERING ANGLE, 42x6x5", "Tiering Angle")]
        [TestCase("Wall Attachment Angle: 12 x 2", "Attachment Angle")]
        [TestCase("ROOF CORNER CAP, VARIABLE 4.3", "Roof Cap")]
        [TestCase("Wall Seal-Off Angle", "Seal-Off Angle")]
        [TestCase("Corner liner", "Liner")]
        [TestCase("bottom floor plate", "Floor Sheet")]
        [TestCase("Lifting Lug", "Unknown")]
        public void Description_MostSpecificKeywordWins(string description, string expected)
        {
            Assert.AreEqual(expected, MaterialsConfig.GetPartClassification("", description));
        }

        [Test]
        public void ValidateRules_FlagsAmbiguities()
        {
            var rules = new List<PartRule>
            {
                new PartRule { Classification = "A", StockNumbers = { "1" }, DescriptionKeywords = { "angle" }, GaugeSource = "fixed:16" },
                new PartRule { Classification = "B", StockNumbers = { "1" }, DescriptionKeywords = { "ANGLE" }, MaterialSource = "fixed:X" },
                new PartRule { Classification = "C", StockNumbers = { "2" } },
                new PartRule { Classification = "D", VerificationMode = "display" },
            };
            var legacy = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["2"] = "Z" };

            var problems = MaterialsConfig.ValidateRules(rules, legacy);

            Assert.AreEqual(4, problems.Count, string.Join("\n", problems));
            StringAssert.Contains("Stock number 1", problems[0]);
            StringAssert.Contains("Keyword 'ANGLE'", problems[1]);
            StringAssert.Contains("maps 2 to 'Z'", problems[2]);
            StringAssert.Contains("'C' is a mismatch rule", problems[3]);
        }
    }
}
