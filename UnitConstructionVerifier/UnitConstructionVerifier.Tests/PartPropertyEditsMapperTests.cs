using NUnit.Framework;
using UnitConstructionVerifier.Operations;

namespace UnitConstructionVerifier.Tests
{
    [TestFixture]
    public class PartPropertyEditsMapperTests
    {
        [Test]
        public void Maps_Exterior_Gauge_And_Material()
        {
            var edits = new PartPropertyEdits();

            Assert.IsTrue(PartPropertyEditsMapper.TryApply(edits, "Exterior Gauge & Material", "16 GA STL GALV PPC"));
            Assert.AreEqual("16", edits.MtlGauge);
            Assert.AreEqual("STL GALV PPC", edits.YCMATL);
        }

        [Test]
        public void Maps_Interior_Gauge_And_Material()
        {
            var edits = new PartPropertyEdits();

            Assert.IsTrue(PartPropertyEditsMapper.TryApply(edits, "Interior Gauge & Material", "18 GA STL GALV"));
            Assert.AreEqual("18", edits.MtlGauge);
            Assert.AreEqual("STL GALV", edits.YCMATL);
        }

        [Test]
        public void Maps_Thickness()
        {
            var edits = new PartPropertyEdits();

            Assert.IsTrue(PartPropertyEditsMapper.TryApply(edits, "Thickness", "0.0561"));
            Assert.AreEqual("0.0561", edits.Thickness);
        }

        [Test]
        public void Maps_Base_Structural_Material()
        {
            var edits = new PartPropertyEdits();

            Assert.IsTrue(PartPropertyEditsMapper.TryApply(edits, "Base Structural Material", "STL C CHNL"));
            Assert.AreEqual("STL C CHNL", edits.YCMATL);
        }

        [Test]
        public void Ignores_Unknown_Parameter()
        {
            var edits = new PartPropertyEdits();

            Assert.IsFalse(PartPropertyEditsMapper.TryApply(edits, "Paint Color", "Gray"));
            Assert.IsFalse(PartPropertyEditsMapper.HasAnyValue(edits));
        }
    }
}
