using System.Threading;
using System.Windows.Controls;
using System.Windows.Documents;
using NUnit.Framework;
using UnitConstructionVerifier.UI;

namespace UnitConstructionVerifier.Tests
{
    [TestFixture, Apartment(ApartmentState.STA)]
    public class TreeSearchTests
    {
        [Test]
        public void Click_On_Text_Run_Finds_Ancestor_Without_Throwing()
        {
            // Clicking a part's description reports a Run as the source; VisualTreeHelper.GetParent threw on it,
            // and with no handler that exception closed Inventor.
            var run = new Run("ROOF SKIN");
            var text = new TextBlock();
            text.Inlines.Add(run);
            var border = new Border { Child = text };

            Assert.AreSame(border, TreeSearch.FindAncestor<Border>(run));
            Assert.IsNull(TreeSearch.FindAncestor<Button>(run));
        }

        [Test]
        public void Visual_Ancestor_Is_Found()
        {
            var text = new TextBlock();
            var inner = new Border { Child = text };
            var outer = new Grid();
            outer.Children.Add(inner);

            Assert.AreSame(outer, TreeSearch.FindAncestor<Grid>(text));
            Assert.AreSame(text, TreeSearch.FindAncestor<TextBlock>(text));
        }
    }
}
