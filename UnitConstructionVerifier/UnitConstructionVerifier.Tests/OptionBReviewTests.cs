using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnitConstructionVerifier.UI;

namespace UnitConstructionVerifier.Tests
{
    [TestFixture]
    public class WriteReviewTests
    {
        private static StagedEdit Edit(string file, string parameter, string now, string next, string part = "P-1") =>
            new StagedEdit(file, parameter, part, now, next);

        [Test]
        public void Every_Writable_Change_Starts_Ticked_And_Is_Sorted_By_Part()
        {
            var items = WriteReview.BuildItems(new[]
            {
                Edit(@"C:\b.ipt", "Thickness", "2", "4", "B"),
                Edit(@"C:\a.ipt", "Thickness", "2", "3", "A"),
            });

            CollectionAssert.AreEqual(new[] { "A", "B" }, items.Select(i => i.PartNumber));
            Assert.IsTrue(items.All(i => i.IsIncluded && i.CanWrite));
        }

        [Test]
        public void Unmappable_Change_Is_Shown_Unticked_And_Cannot_Be_Ticked()
        {
            var item = WriteReview.BuildItems(new[] { Edit(@"C:\a.ipt", "Hole Pattern", "x", "y") }).Single();

            Assert.IsFalse(item.CanWrite);
            item.IsIncluded = true;
            Assert.IsFalse(item.IsIncluded);
        }

        [Test]
        public void Only_Ticked_Changes_Are_Grouped_For_Writing()
        {
            var items = WriteReview.BuildItems(new[]
            {
                Edit(@"C:\a.ipt", "Thickness", "2", "3"),
                Edit(@"C:\a.ipt", "Base Material", "Steel", "Galv"),
                Edit(@"C:\b.ipt", "Thickness", "2", "4"),
            });
            items.Single(i => i.Edit.FilePath == @"C:\b.ipt").IsIncluded = false;

            Dictionary<string, Operations.PartPropertyEdits> grouped = WriteReview.GroupByFile(items);

            CollectionAssert.AreEquivalent(new[] { @"C:\a.ipt" }, grouped.Keys);
            Assert.AreEqual("3", grouped[@"C:\a.ipt"].Thickness);
            Assert.AreEqual("Galv", grouped[@"C:\a.ipt"].YCMATL, "two changes to one part go out together");
        }

        [Test]
        public void Summary_Says_What_Is_Written_And_What_Stays_Staged()
        {
            var items = WriteReview.BuildItems(new[]
            {
                Edit(@"C:\a.ipt", "Thickness", "2", "3"),
                Edit(@"C:\b.ipt", "Thickness", "2", "4"),
                Edit(@"C:\c.ipt", "Hole Pattern", "x", "y"),
            });
            items[1].IsIncluded = false;

            string summary = WriteReview.Summary(items);

            StringAssert.Contains("1 change to 1 part file will be written", summary);
            StringAssert.Contains("Unticked changes stay staged", summary);
            StringAssert.Contains("1 change cannot be written", summary);
        }

        [Test]
        public void Blank_Current_Value_Reads_As_Blank()
        {
            var item = WriteReview.BuildItems(new[] { Edit(@"C:\a.ipt", "Thickness", "", "3", part: "") }).Single();

            Assert.AreEqual("(blank)", item.Current);
            Assert.AreEqual("a", item.PartNumber, "falls back to the file name when the part number is missing");
        }
    }

    [TestFixture]
    public class CopyToAllPreviewTests
    {
        private sealed class Row
        {
            public string Name = "";
            public string Gauge = "";
            public string Paint = "";
        }

        private static readonly ExpectationField<Row>[] Fields =
        {
            new ExpectationField<Row>("Gauge", r => r.Gauge, (r, v) => r.Gauge = v),
            new ExpectationField<Row>("Paint", r => r.Paint, (r, v) => r.Paint = v),
        };

        [Test]
        public void Lists_Only_Surfaces_And_Fields_That_Differ()
        {
            var source = new Row { Name = "S", Gauge = "18", Paint = "Gray" };
            var same = new Row { Name = "Same", Gauge = "18", Paint = " gray " };
            var other = new Row { Name = "Other", Gauge = "16", Paint = "Gray" };

            var changes = CopyToAllPreview.Diff(source, new[] { source, same, other }, Fields);

            Assert.AreEqual(1, changes.Count);
            Assert.AreSame(other, changes[0].Row);
            Assert.AreEqual("Gauge", changes[0].Fields.Single().Label);
            Assert.AreEqual("Other: Gauge 16 → 18", CopyToAllPreview.Describe(changes, r => r.Name));
        }

        [Test]
        public void Nothing_Differs_Means_No_Changes()
        {
            var source = new Row { Gauge = "18" };
            Assert.IsEmpty(CopyToAllPreview.Diff(source, new[] { source, new Row { Gauge = "18" } }, Fields));
        }

        [Test]
        public void Apply_Copies_Every_Field_To_Changed_Surfaces_Only()
        {
            var source = new Row { Gauge = "18", Paint = "Gray" };
            var same = new Row { Gauge = "18", Paint = "Gray" };
            var other = new Row { Gauge = "16", Paint = "" };
            var changes = CopyToAllPreview.Diff(source, new[] { same, other }, Fields);

            CopyToAllPreview.Apply(source, changes, Fields);

            Assert.AreEqual("18", other.Gauge);
            Assert.AreEqual("Gray", other.Paint);
        }

        [Test]
        public void Long_Lists_Are_Capped()
        {
            var source = new Row { Gauge = "18" };
            var rows = Enumerable.Range(0, 15).Select(i => new Row { Name = "R" + i, Gauge = "16" }).ToList();

            string text = CopyToAllPreview.Describe(CopyToAllPreview.Diff(source, rows, Fields), r => r.Name, maxSurfaces: 12);

            StringAssert.EndsWith("…and 3 more", text);
            Assert.AreEqual(13, text.Split('\n').Length);
        }
    }
}
