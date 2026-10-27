using NUnit.Framework;
using UnitConstructionVerifier.Operations;

namespace UnitConstructionVerifier.Tests
{
    [TestFixture]
    public class CutDepthRuleTests
    {
        // Gauge thicknesses as the materials database codes them, in cm.
        private const double Ga22 = 0.02801 * 2.54;
        private const double Ga18 = 0.04504 * 2.54;
        private const double Ga16 = 0.05601 * 2.54;

        [Test]
        public void SymmetricLibraryCut_MadeThicker_IsLinkedToTwiceThickness()
        {
            // 18 GA skin cut 0.09" both ways from its face; going to 16 GA it would stop short.
            Assert.AreEqual(CutDepthRule.Symmetric,
                CutDepthRule.NewExpression(true, "0.09 in", 0.09 * 2.54, Ga18, Ga16));
        }

        [Test]
        public void SymmetricCutLinkedToThicknessAlone_IsRepaired_EvenWithoutAThicknessChange()
        {
            // SW0004 liners 0441/0442: Thickness both ways from the face reaches 0.014" of 0.028".
            Assert.AreEqual(CutDepthRule.Symmetric,
                CutDepthRule.NewExpression(true, "Thickness", Ga22, Ga22, Ga22));
        }

        [Test]
        public void SymmetricCut_MadeThinner_IsLeftAlone()
        {
            Assert.IsNull(CutDepthRule.NewExpression(true, "0.09 in", 0.09 * 2.54, Ga18, Ga22));
        }

        [Test]
        public void OneSidedCutAtOldThickness_MadeThicker_IsLinkedToThickness()
        {
            Assert.AreEqual(CutDepthRule.OneSided,
                CutDepthRule.NewExpression(false, "0.028 in", Ga22, Ga22, Ga18));
        }

        [Test]
        public void AlreadyLinked_IsLeftAlone()
        {
            Assert.IsNull(CutDepthRule.NewExpression(true, "Thickness*2 ul", 2 * Ga18, Ga18, Ga16));
            Assert.IsNull(CutDepthRule.NewExpression(false, "thickness", Ga18, Ga18, Ga16));
        }

        [Test]
        public void PartialDepthCut_IsLeftAlone()
        {
            // Never went through the old sheet, so it is a deliberate partial cut.
            Assert.IsNull(CutDepthRule.NewExpression(false, "0.01 in", 0.01 * 2.54, Ga22, Ga18));
            Assert.IsNull(CutDepthRule.NewExpression(true, "0.02 in", 0.02 * 2.54, Ga22, Ga18));
        }

        [Test]
        public void DeepCutThatStillGoesThrough_IsLeftAlone()
        {
            Assert.IsNull(CutDepthRule.NewExpression(false, "0.5 in", 0.5 * 2.54, Ga22, Ga16));
        }

        [Test]
        public void LibraryCutWithinTolerance_CountsAsThrough()
        {
            // 0.09" both ways is 0.045" into a 0.04504" PPC skin: through, so nothing to change.
            Assert.IsNull(CutDepthRule.NewExpression(true, "0.09 in", 0.09 * 2.54, Ga18, Ga18));
        }
    }
}
