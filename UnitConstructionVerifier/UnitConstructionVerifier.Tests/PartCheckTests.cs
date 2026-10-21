using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using UnitConstructionVerifier.Engine;
using UnitConstructionVerifier.Models;

namespace UnitConstructionVerifier.Tests
{
    /// <summary>Every scanned part gets an outcome; nothing is skipped silently.</summary>
    [TestFixture]
    public class PartCheckTests
    {
        [OneTimeSetUp]
        public void Setup()
        {
            MaterialsConfig.Initialize();
        }

        private static VerificationResult RunWall(params IptProperties[] parts)
        {
            var data = new UnitConstructionData
            {
                WallRows = new List<WallSurfaceRow>
                {
                    new WallSurfaceRow
                    {
                        SourceSurfaceIam      = "W.iam",
                        ExteriorSkinGauge     = "18",
                        ExteriorSkinMaterial  = "STL GALV PPC",
                        InteriorLinerGauge    = "",
                        InteriorLinerMaterial = "",
                    }
                }
            };
            return new VerificationEngine(data, new IptScanResult { Parts = parts.ToList() }).Run();
        }

        private static PartCheck CheckFor(VerificationResult result, string partNumber) =>
            result.Checks.Single(c => c.IptPartNumber == partNumber);

        [Test]
        public void MatchingSkin_Passes()
        {
            var result = RunWall(new IptProperties { PartNumber = "p", OwnerIamPath = "W.iam", ModelNumber = "091-30117-083", MtlGauge = "18", YCMATL = "STL GALV PPC" });

            Assert.AreEqual(PartCheckStatus.Pass, CheckFor(result, "p").Status);
            Assert.IsTrue(result.IsPass);
        }

        [Test]
        public void WrongSkin_IsMismatch()
        {
            var result = RunWall(new IptProperties { PartNumber = "p", OwnerIamPath = "W.iam", ModelNumber = "091-30117-083", MtlGauge = "10", YCMATL = "ALM SHT" });

            Assert.AreEqual(PartCheckStatus.Mismatch, CheckFor(result, "p").Status);
            Assert.AreEqual(1, result.Mismatches.Count);
        }

        [Test]
        public void PartWithoutRule_IsNotInConfig()
        {
            var result = RunWall(new IptProperties { PartNumber = "p", OwnerIamPath = "W.iam", Description = "lifting lug" });

            Assert.AreEqual(PartCheckStatus.NotInConfig, CheckFor(result, "p").Status);
            Assert.IsTrue(result.IsPass);
        }

        [Test]
        public void RuledPartInIamWithoutRow_IsNotChecked()
        {
            var result = RunWall(new IptProperties { PartNumber = "p", OwnerIamPath = "Orphan.iam", ModelNumber = "091-30117-083", MtlGauge = "10", YCMATL = "ALM SHT" });

            var check = CheckFor(result, "p");
            Assert.AreEqual(PartCheckStatus.NotChecked, check.Status);
            StringAssert.Contains("no construction row", check.Reason);
        }

        [Test]
        public void BaseRuleInWallSurface_IsNotChecked_WithWrongSurfaceReason()
        {
            var result = RunWall(new IptProperties { PartNumber = "p", OwnerIamPath = "W.iam", ModelNumber = "091-30117-056", MtlGauge = "10", YCMATL = "ALM SHT" });

            var check = CheckFor(result, "p");
            Assert.AreEqual(PartCheckStatus.NotChecked, check.Status);
            StringAssert.Contains("wall surfaces don't have", check.Reason);
            StringAssert.Contains("FloorGauge", check.Reason);
        }

        [Test]
        public void BlankSide_IsNotChecked_WithBlankReason()
        {
            var result = RunWall(new IptProperties { PartNumber = "p", OwnerIamPath = "W.iam", ModelNumber = "091-30117-082", MtlGauge = "20", YCMATL = "STL GALV" });

            var check = CheckFor(result, "p");
            Assert.AreEqual(PartCheckStatus.NotChecked, check.Status);
            StringAssert.Contains("blank", check.Reason);
        }

        [Test]
        public void DisplayRule_IsDisplayOnly_WithValues()
        {
            var result = RunWall(new IptProperties { PartNumber = "p", OwnerIamPath = "W.iam", ModelNumber = "091-30117-075", MtlGauge = "10", YCMATL = "ALM SHT" });

            var check = CheckFor(result, "p");
            Assert.AreEqual(PartCheckStatus.DisplayOnly, check.Status);
            Assert.IsNotEmpty(check.ActualValue);
            Assert.IsTrue(result.IsPass);
        }
    }
}
