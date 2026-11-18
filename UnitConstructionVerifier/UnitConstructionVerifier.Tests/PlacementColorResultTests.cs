using NUnit.Framework;
using UnitConstructionVerifier.Operations;

namespace UnitConstructionVerifier.Tests
{
    [TestFixture]
    public class PlacementColorResultTests
    {
        [Test]
        public void Summary_IsEmptyWhenNothingWasColored()
        {
            var result = new PlacementColorResult();
            result.Problems.Add("0091.IAM: read-only");

            Assert.AreEqual(string.Empty, result.Summary("Pink"));
        }

        [Test]
        public void Summary_CountsPlacementsAndAssemblies()
        {
            var one = new PlacementColorResult { PlacementsColored = 1 };
            one.AssembliesChanged.Add(@"C:\unit\0091.IAM");
            Assert.AreEqual(" Colored 1 placement Pink in 1 assembly file; save the unit to keep them.", one.Summary("Pink"));

            var many = new PlacementColorResult { PlacementsColored = 5 };
            many.AssembliesChanged.Add(@"C:\unit\0091.IAM");
            many.AssembliesChanged.Add(@"C:\unit\0092.IAM");
            Assert.AreEqual(" Colored 5 placements Pink in 2 assembly files; save the unit to keep them.", many.Summary("Pink"));
        }
    }
}
