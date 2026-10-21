using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using NUnit.Framework;
using UnitConstructionVerifier.Operations;
using UnitConstructionVerifier.UI;

namespace UnitConstructionVerifier.Tests
{
    /// <summary>
    /// Guards against the verifier window losing or silently dropping data (UI review D1-D4, I3).
    /// </summary>
    [TestFixture]
    public class VerifierWindowSafetyTests
    {
        private static readonly IReadOnlyList<string> Gauges = new[] { "16", "18", "20", "22" };

        // ── D1: dropdowns keep values that are not standard options ──

        [Test]
        public void Stored_Value_Missing_From_List_Is_Added_And_Selected()
        {
            var options = ExpectationChoices.WithStoredValue(Gauges, "16.5", out string? selected);

            Assert.AreEqual("16.5", selected);
            CollectionAssert.AreEqual(new[] { "16.5", "16", "18", "20", "22" }, options);
            CollectionAssert.AreEqual(new[] { "16", "18", "20", "22" }, Gauges, "standard list must not change");
        }

        [Test]
        public void Stored_Value_Matching_Ignoring_Case_And_Spaces_Selects_Standard_Option()
        {
            var materials = new[] { "STL GALV", "ALM SHT" };

            var options = ExpectationChoices.WithStoredValue(materials, " stl galv ", out string? selected);

            Assert.AreEqual("STL GALV", selected);
            Assert.AreSame(materials, options);
        }

        [Test]
        public void Blank_Stored_Value_Selects_Nothing()
        {
            var options = ExpectationChoices.WithStoredValue(Gauges, "  ", out string? selected);

            Assert.IsNull(selected);
            Assert.AreSame(Gauges, options);
        }

        [TestCase("14\"")]
        [TestCase("STL TUBE")]
        public void Nonstandard_Base_Values_Are_Kept(string stored)
        {
            var standard = stored.EndsWith("\"") ? ExpectationChoices.BaseHeights : ExpectationChoices.BaseMaterials;

            var options = ExpectationChoices.WithStoredValue(standard, stored, out string? selected);

            Assert.AreEqual(stored, selected);
            CollectionAssert.Contains(options, stored);
        }

        // ── D2: thermal break shows blank instead of "No" ──

        [TestCase(null)]
        [TestCase("")]
        public void Blank_Thermal_Break_Does_Not_Show_No(string? stored)
        {
            ExpectationChoices.WithStoredValue(ExpectationChoices.YesNo, stored, out string? selected);

            Assert.IsNull(selected);
        }

        [TestCase("yes", "Yes")]
        [TestCase("NO", "No")]
        public void Thermal_Break_Matches_Ignoring_Case(string stored, string expected)
        {
            ExpectationChoices.WithStoredValue(ExpectationChoices.YesNo, stored, out string? selected);

            Assert.AreEqual(expected, selected);
        }

        // ── D3: closing with unsaved work prompts ──

        [Test]
        public void No_Prompt_When_Nothing_Is_Unsaved()
        {
            Assert.IsNull(UnsavedWorkPrompt.BuildMessage(false, 0));
        }

        [Test]
        public void Prompt_Offers_Save_For_Unsaved_Expectations()
        {
            string? message = UnsavedWorkPrompt.BuildMessage(true, 0);

            Assert.IsNotNull(message);
            StringAssert.Contains("Expectation changes", message);
            StringAssert.Contains("Yes: save", message);
        }

        [Test]
        public void Prompt_Counts_Staged_Part_Edits()
        {
            string? message = UnsavedWorkPrompt.BuildMessage(false, 3);

            Assert.IsNotNull(message);
            StringAssert.Contains("3 staged part edits", message);
            StringAssert.Contains("OK: close and discard", message);
        }

        // ── D4: only writable parameters can be staged ──

        [TestCase("Thickness")]
        [TestCase("Exterior Gauge & Material")]
        [TestCase("Formed Channel Material")]
        [TestCase("Base Structural Material")]
        [TestCase("Base Material")]
        [TestCase("Paint Color")]
        [TestCase("")]
        public void CanWrite_Agrees_With_TryApply(string parameter)
        {
            bool applied = PartPropertyEditsMapper.TryApply(new PartPropertyEdits(), parameter, "16 GA STL GALV");

            Assert.AreEqual(applied, PartPropertyEditsMapper.CanWrite(parameter));
        }

        // ── I3: part values are read-only outside Edit Mode ──

        [Test]
        public void Actual_Columns_Start_Read_Only()
        {
            var columns = WindowXaml().Descendants(Wpf + "DataGridTextColumn")
                .Where(c => (string?)c.Attribute("Header") == "Actual Part Spec")
                .ToList();

            Assert.AreEqual(3, columns.Count);
            Assert.That(columns.All(c => (string?)c.Attribute("IsReadOnly") == "True"));
        }

        [Test]
        public void Every_Parts_Grid_Guards_Edits()
        {
            var grids = WindowXaml().Descendants(Wpf + "DataGrid").ToList();

            Assert.AreEqual(3, grids.Count);
            Assert.That(grids.All(g => (string?)g.Attribute("BeginningEdit") == "OnPartsGridBeginningEdit"));
        }

        [Test]
        public void Expectation_Dropdowns_Have_No_Inline_Items()
        {
            // Inline ComboBoxItems bypass ExpectationChoices and bring back D1/D2.
            Assert.IsEmpty(WindowXaml().Descendants(Wpf + "ComboBoxItem"));
        }

        private static readonly XNamespace Wpf = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

        private static XDocument WindowXaml([CallerFilePath] string thisFile = "")
        {
            string path = Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "UnitConstructionVerifier", "UI", "VerifierWindow.xaml");
            return XDocument.Load(path);
        }
    }
}
