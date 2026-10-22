using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using NUnit.Framework;
using UnitConstructionVerifier.Models;
using UnitConstructionVerifier.UI;

namespace UnitConstructionVerifier.Tests
{
    /// <summary>Status summaries, layout rules and XAML structure behind the Option A window.</summary>
    [TestFixture]
    public class VerifierWindowLayoutTests
    {
        // ── Unit status (header pill, tab badges, surface dots, banner) ──

        private static VerificationResult Result(params (string Surface, PartCheckStatus Status, bool Modified)[] checks)
        {
            var result = new VerificationResult();
            foreach (var c in checks)
                result.Checks.Add(new PartCheck { SurfaceIamPath = c.Surface, Status = c.Status, IsModified = c.Modified });
            return result;
        }

        [Test]
        public void Status_Counts_Per_Surface_Ignoring_Path_Case()
        {
            var status = UnitStatus.From(Result(
                ("R1.iam", PartCheckStatus.Pass, false),
                ("R1.iam", PartCheckStatus.Mismatch, false),
                ("r1.IAM", PartCheckStatus.Mismatch, true),
                ("W1.iam", PartCheckStatus.NotChecked, false),
                ("W1.iam", PartCheckStatus.Pass, false)));

            Assert.AreEqual(2, status.For("R1.iam").Mismatches);
            Assert.IsTrue(status.For("R1.iam").IsFail);
            Assert.IsTrue(status.For("W1.iam").IsPass);
            Assert.AreEqual(1, status.For("W1.iam").NotChecked);
            Assert.AreEqual(1, status.SurfacesWithMismatches);
            Assert.AreEqual(4, status.Checked);
            Assert.AreEqual(1, status.Modified);
        }

        [Test]
        public void Scanned_Surface_Without_Parts_Is_Neither_Pass_Nor_Fail()
        {
            var surface = UnitStatus.From(Result(("R1.iam", PartCheckStatus.Pass, false))).For("Other.iam");

            Assert.IsFalse(surface.IsPass);
            Assert.IsFalse(surface.IsFail);
            Assert.IsTrue(surface.Scanned);
        }

        [Test]
        public void Unscanned_Unit_Reports_Unknown()
        {
            Assert.IsFalse(UnitStatus.Empty.For("R1.iam").Scanned);
            Assert.AreEqual("Not scanned", UnitStatus.Empty.HeaderText());
        }

        [Test]
        public void Header_And_Banner_Text()
        {
            var failing = UnitStatus.From(Result(
                ("R1.iam", PartCheckStatus.Mismatch, false),
                ("W1.iam", PartCheckStatus.Mismatch, false),
                ("W1.iam", PartCheckStatus.NotChecked, false),
                ("W1.iam", PartCheckStatus.Pass, true)));

            Assert.AreEqual("✗ 2 mismatches on 2 surfaces", failing.HeaderText());
            Assert.AreEqual("Re-checked 3 parts · 2 mismatches on 2 surfaces · 1 not checked · 1 modified after generation.", failing.BannerText());

            var passing = UnitStatus.From(Result(("R1.iam", PartCheckStatus.Pass, false)));
            Assert.AreEqual("✓ All 1 checked parts match", passing.HeaderText());
            Assert.AreEqual("Re-checked 1 part · no mismatches.", passing.BannerText());
        }

        [Test]
        public void Tab_Badge_Sums_Its_Surfaces()
        {
            var status = UnitStatus.From(Result(
                ("R1.iam", PartCheckStatus.Mismatch, false),
                ("R2.iam", PartCheckStatus.Mismatch, false),
                ("W1.iam", PartCheckStatus.Mismatch, false)));

            Assert.AreEqual(2, status.MismatchesIn(new[] { "R1.iam", "R2.iam", "R3.iam" }));
        }

        // ── Companion window placement (wireframe preview) ──

        [Test]
        public void Preview_Opens_Right_Of_Owner_When_It_Fits()
        {
            Assert.AreEqual(1112, WindowLayout.CompanionLeft(ownerLeft: 0, ownerWidth: 1100, width: 520, screenLeft: 0, screenWidth: 1920));
        }

        [Test]
        public void Preview_Opens_Left_When_Right_Side_Is_Off_Screen()
        {
            // Centred 1150 px window on a 1920 px screen: the old placement put 150 px of the preview off-screen.
            double left = WindowLayout.CompanionLeft(ownerLeft: 385, ownerWidth: 1150, width: 520, screenLeft: 0, screenWidth: 1920);

            Assert.That(left + 520, Is.LessThanOrEqualTo(1920));
            Assert.That(left, Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void Preview_Clamps_To_Screen_When_Neither_Side_Fits()
        {
            Assert.AreEqual(846, WindowLayout.CompanionLeft(ownerLeft: 100, ownerWidth: 1200, width: 520, screenLeft: 0, screenWidth: 1366));
            Assert.AreEqual(248, WindowLayout.CompanionTop(ownerTop: 600, height: 520, screenTop: 0, screenHeight: 768));
        }

        // ── Gauge dropdown order ──

        [Test]
        public void Gauges_Sort_Numerically_With_Plate_And_Placeholders_Last()
        {
            var ordered = WindowLayout.OrderGauges(new[] { "0", "0.063", "0.125", "000", "1", "10", "16", "3", "22", "0.25", "0000000", "18" });

            CollectionAssert.AreEqual(
                new[] { "1", "3", "10", "16", "18", "22", "0.063", "0.125", "0.25", "0", "000", "0000000" },
                ordered);
        }

        [Test]
        public void Config_Gauges_Keep_Every_Entry()
        {
            MaterialsConfig.Initialize();

            CollectionAssert.AreEquivalent(MaterialsConfig.Gauges, WindowLayout.OrderGauges(MaterialsConfig.Gauges));
        }

        // ── Grid rows ──

        [Test]
        public void Not_Checked_Reason_Shows_After_Description()
        {
            var row = new IptVerificationRow { Description = "ROOF TRIM", CheckStatus = PartCheckStatus.NotChecked, Reason = "No expected trim value" };

            StringAssert.Contains("No expected trim value", row.ReasonSuffix);
            StringAssert.Contains("No expected trim value", row.StatusTooltip);
        }

        [Test]
        public void Staged_Row_Tooltip_Shows_Old_And_New_Value()
        {
            var row = new IptVerificationRow { Actual = "20 GA STL GALV", CheckStatus = PartCheckStatus.Mismatch };
            row.NewValue = "22 GA STL GALV";

            Assert.AreEqual("● Staged", row.StatusText);
            StringAssert.Contains("20 GA STL GALV → 22 GA STL GALV", row.StatusTooltip);
        }

        // ── XAML structure ──

        [Test]
        public void Part_Rows_Carry_Their_Own_Context_Menu()
        {
            // A context menu on the DataGrid itself never sees the clicked row, so Open in Inventor did nothing.
            var grids = WindowXaml().Descendants(Wpf + "DataGrid").ToList();

            Assert.That(grids.All(g => g.Attribute("ContextMenu") == null));
            Assert.That(grids.All(g => (string?)g.Attribute("RowStyle") == "{StaticResource PartRowStyle}"));
        }

        [Test]
        public void Grids_Hide_Row_Headers()
        {
            var gridStyle = WindowXaml().Descendants(Wpf + "Style").Single(s => (string?)s.Attribute("TargetType") == "DataGrid");

            Assert.That(gridStyle.Elements(Wpf + "Setter").Any(s =>
                (string?)s.Attribute("Property") == "HeadersVisibility" && (string?)s.Attribute("Value") == "Column"));
        }

        [Test]
        public void Highlight_Is_One_Labelled_Choice()
        {
            var xaml = WindowXaml();
            var modes = xaml.Descendants(Wpf + "RadioButton").Where(r => (string?)r.Attribute("GroupName") == "HighlightMode")
                .Select(r => (string?)r.Attribute("Content")).ToList();

            CollectionAssert.AreEqual(new[] { "Box", "X-Ray", "Off" }, modes);
        }

        [Test]
        public void Window_Xaml_Avoids_System_Keys_That_Collide_In_Inventor()
        {
            // Inventor runs WPF with AreInactiveSelectionHighlightBrushKeysSupported = false, where these keys
            // equal ControlBrushKey / ControlTextBrushKey; defining both threw "Item has already been added".
            string path = Path.Combine(Path.GetDirectoryName(ThisFile())!, "..", "UnitConstructionVerifier", "UI", "VerifierWindow.xaml");
            string xaml = File.ReadAllText(path);

            StringAssert.DoesNotContain("{x:Static SystemColors.InactiveSelectionHighlightBrushKey}", xaml);
            StringAssert.DoesNotContain("{x:Static SystemColors.InactiveSelectionHighlightTextBrushKey}", xaml);
        }

        private static string ThisFile([CallerFilePath] string thisFile = "") => thisFile;

        private static readonly XNamespace Wpf ="http://schemas.microsoft.com/winfx/2006/xaml/presentation";

        private static XDocument WindowXaml([CallerFilePath] string thisFile = "")
        {
            string path = Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "UnitConstructionVerifier", "UI", "VerifierWindow.xaml");
            return XDocument.Load(path);
        }
    }
}
