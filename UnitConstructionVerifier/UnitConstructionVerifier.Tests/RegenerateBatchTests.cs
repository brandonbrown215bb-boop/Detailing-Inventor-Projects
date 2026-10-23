using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnitConstructionVerifier.Models;
using UnitConstructionVerifier.Operations;
using UnitConstructionVerifier.UI;

namespace UnitConstructionVerifier.Tests
{
    /// <summary>Regenerate changed: YCShell message handling, outcomes, the batch runner and the review list.</summary>
    [TestFixture]
    public class RegenerateBatchTests
    {
        // Wording and caption YCShell showed on the spike copy, 2026-10-23.
        private static YcShellMessage Completion() =>
            new YcShellMessage("Wall : 391Z010068-0055", "\r\n4 Channel Features have been recreated/replaced\r\n\r\n", new[] { "OK" });

        private static YcShellMessage Question(string text = "Regenerate snaplock holes only?") =>
            new YcShellMessage("Wall : 391Z010068-0074", "Foam hole regeneration will NOT occur\r\n" + text, new[] { "&Yes", "&No" });

        [Test]
        public void Classifies_YCShells_Boxes()
        {
            Assert.AreEqual(YcShellMessageKind.Completion, Completion().Kind);
            Assert.AreEqual(YcShellMessageKind.Question, Question().Kind);
            Assert.AreEqual(YcShellMessageKind.Information,
                new YcShellMessage("Roof : X", "Surface is a sloped/outdoor Roof.\r\nRegenerating Channel Features for sloped/outdoor roofs", new[] { "OK" }).Kind);
            Assert.AreEqual(YcShellMessageKind.Other,
                new YcShellMessage("Cannot Regenerate Holes", "Active Assembly NOT FOUND", new[] { "OK" }).Kind);
        }

        [Test]
        public void Only_Known_OK_Only_Notices_Are_Closed_Automatically()
        {
            Assert.IsTrue(Completion().CanCloseAutomatically);
            Assert.IsTrue(new YcShellMessage("Wall : X", "1 Channel Features have been recreated/replaced", new[] { "&OK" }).CanCloseAutomatically);
            Assert.IsFalse(new YcShellMessage("Wall : X", "1 Channel Features have been recreated/replaced", new[] { "OK", "Cancel" }).CanCloseAutomatically);
            Assert.IsFalse(Question().CanCloseAutomatically, "questions always go to the detailer");
            Assert.IsFalse(new YcShellMessage("Error", "Creation of ISG Generator FAILED.", new[] { "OK" }).CanCloseAutomatically);
        }

        [Test]
        public void Outcome_Follows_What_YCShell_Showed()
        {
            RegenerateOutcome done = RegenerateOutcome.FromMessages("a.iam", new[] { Completion() }, 132);
            Assert.AreEqual(RegenerateOutcomeKind.Done, done.Kind);
            Assert.AreEqual("4 Channel Features have been recreated/replaced", done.Detail);

            RegenerateOutcome withChoices = RegenerateOutcome.FromMessages("a.iam", new[] { Question(), Completion() }, 90);
            Assert.AreEqual(RegenerateOutcomeKind.DoneWithChoices, withChoices.Kind);
            StringAssert.EndsWith("(1 question answered)", withChoices.Detail);

            RegenerateOutcome declined = RegenerateOutcome.FromMessages("a.iam", new[] { Question() }, 10);
            Assert.AreEqual(RegenerateOutcomeKind.NotCompleted, declined.Kind);
            StringAssert.StartsWith("YCShell stopped without regenerating: Foam hole regeneration will NOT occur", declined.Detail);

            Assert.AreEqual(RegenerateOutcomeKind.NotCompleted, RegenerateOutcome.FromMessages("a.iam", Array.Empty<YcShellMessage>(), 1).Kind);
        }

        private sealed class FakeYcShell : ISurfaceRegenerator, IYcShellMessageSource
        {
            public readonly List<string> Regenerated = new List<string>();
            public Func<string, IReadOnlyList<YcShellMessage>> Shows = _ => new[] { Completion() };
            public string? Throws;
            private string _current = string.Empty;

            public void Regenerate(string surfaceIamPath)
            {
                Regenerated.Add(surfaceIamPath);
                if (surfaceIamPath == Throws) throw new InvalidOperationException("YCShell no longer has this surface loaded.");
            }

            public void Begin(string surfaceIamPath) => _current = surfaceIamPath;
            public IReadOnlyList<YcShellMessage> End() => Shows(_current);
        }

        [Test]
        public void Runs_Surfaces_In_Order_And_Records_Only_Finished_Ones()
        {
            var yc = new FakeYcShell
            {
                Throws = "0009.iam",
                Shows = s => s == "0006.iam" ? new[] { Question() } : new[] { Completion() },
            };
            var recorded = new List<string>();
            var runner = new RegenerateBatchRunner(yc, yc, s => s == "0019.iam" ? "has suppressed parts" : null, recorded.Add);

            var progress = new List<string>();
            IReadOnlyList<RegenerateOutcome> outcomes = runner.Run(
                new[] { "0003.iam", "0006.iam", "0009.iam", "0019.iam", "0055.iam" }, () => false, (n, total, s) => progress.Add($"{n}/{total} {s}"));

            CollectionAssert.AreEqual(new[] { "0003.iam", "0006.iam", "0009.iam", "0055.iam" }, yc.Regenerated, "skipped surfaces are not run");
            CollectionAssert.AreEqual(
                new[] { RegenerateOutcomeKind.Done, RegenerateOutcomeKind.NotCompleted, RegenerateOutcomeKind.Failed, RegenerateOutcomeKind.Skipped, RegenerateOutcomeKind.Done },
                outcomes.Select(o => o.Kind));
            CollectionAssert.AreEqual(new[] { "0003.iam", "0055.iam" }, recorded);
            Assert.AreEqual("1/5 0003.iam", progress[0]);
        }

        [Test]
        public void Stop_Takes_Effect_Between_Surfaces()
        {
            var yc = new FakeYcShell();
            var runner = new RegenerateBatchRunner(yc, yc, _ => null, _ => { });

            IReadOnlyList<RegenerateOutcome> outcomes = runner.Run(new[] { "a.iam", "b.iam", "c.iam" }, () => yc.Regenerated.Count >= 1);

            CollectionAssert.AreEqual(new[] { "a.iam" }, yc.Regenerated);
            CollectionAssert.AreEqual(new[] { RegenerateOutcomeKind.Done, RegenerateOutcomeKind.NotRun, RegenerateOutcomeKind.NotRun }, outcomes.Select(o => o.Kind));
            Assert.AreEqual("1 of 3 surfaces regenerated, 2 not run.", RegenerateBatchSummary.Headline(outcomes));
        }

        [Test]
        public void A_Failed_Channel_Record_Does_Not_Undo_A_Finished_Surface()
        {
            var yc = new FakeYcShell();
            var runner = new RegenerateBatchRunner(yc, yc, _ => null, _ => throw new UnauthorizedAccessException("read-only folder"));

            RegenerateOutcome outcome = runner.Run(new[] { "a.iam" }, () => false).Single();

            Assert.AreEqual(RegenerateOutcomeKind.Done, outcome.Kind);
            StringAssert.Contains("channel record not saved: read-only folder", outcome.Detail);
        }

        [Test]
        public void Summary_Lists_Problems_First()
        {
            var outcomes = new[]
            {
                new RegenerateOutcome { SurfaceIamPath = @"C:\U\0003\391Z010068-0003.IAM", Kind = RegenerateOutcomeKind.Done, Seconds = 60, Detail = "2 Channel Features have been recreated/replaced" },
                new RegenerateOutcome { SurfaceIamPath = @"C:\U\0009\391Z010068-0009.IAM", Kind = RegenerateOutcomeKind.Failed, Detail = "boom" },
            };

            string text = RegenerateBatchSummary.Text(outcomes);
            StringAssert.StartsWith("1 of 2 surfaces regenerated, 1 needs a look.", text);
            Assert.Less(text.IndexOf("Failed (1)", StringComparison.Ordinal), text.IndexOf("Done (1)", StringComparison.Ordinal));
            StringAssert.Contains("391Z010068-0003 [60 s]", text);
        }

        private static SurfaceChangeState Surface(string path, bool patternedChannel = false) => new SurfaceChangeState
        {
            SurfaceIamPath = path,
            Occurrences =
            {
                new SurfaceOccurrence { Id = "c1", Name = "c:1", PartFile = path + ".c1.ipt", PartType = "CHANNEL", PartStatus = "NoChange" },
                new SurfaceOccurrence { Id = "c2", Name = "c:2 - NEW", PartFile = path + ".c1.ipt", PartType = "CHANNEL", PartStatus = "NoChange", IsNew = patternedChannel },
            },
        };

        [Test]
        public void Upload_Review_Ticks_Any_Change_And_Warns_When_Channels_Still_Need_Regenerate()
        {
            SurfaceChangeState skinOnly = Surface(@"C:\U\0003\0003.IAM");
            skinOnly.Occurrences.Add(new SurfaceOccurrence { Id = "s", Name = "skin:1", PartFile = "skin.ipt", PartType = "SKIN", PartStatus = "Changed_SQ" });
            var surfaces = new[]
            {
                skinOnly,
                Surface(@"C:\U\0019\0019.IAM", patternedChannel: true),
                Surface(@"C:\U\0010\0010.IAM"),
            };
            var none = new Dictionary<string, ChannelFingerprint>();
            var noWrites = new Dictionary<string, PartWriteRecord>();

            List<SurfaceReviewItem> items = UploadReview.BuildItems(surfaces, none, noWrites, _ => true);

            CollectionAssert.AreEqual(new[] { "0003", "0019", "0010" }, items.Select(i => i.SurfaceName));
            Assert.IsTrue(items[0].IsIncluded);
            Assert.IsFalse(items[0].NeedsAttention, "a skin change needs no Regenerate");
            Assert.IsTrue(items[1].IsIncluded);
            Assert.IsTrue(items[1].NeedsAttention);
            StringAssert.StartsWith("still needs Regenerate  ·  1 occurrence added", items[1].ReasonText);
            Assert.IsFalse(items[2].IsIncluded);
            Assert.AreEqual(2, UploadReview.CountNeeded(surfaces));
            Assert.AreEqual(
                "2 of 3 surfaces have changes since their last upload; 2 ticked. 1 ticked surface still has channel changes: run Regenerate changed first. " +
                "FINAL.IAM goes along, so a surface MOM renames is replaced in the unit.",
                UploadReview.Summary(items, "FINAL.IAM"));
        }

        [Test]
        public void Bases_Are_Left_Out_Of_Regenerate_But_Still_Uploaded()
        {
            // YCShell: "Regenerating Features is only supported for Wall and Indoor Roof surfaces".
            var surfaces = new[]
            {
                Surface(@"C:\U\0019\0019.IAM", patternedChannel: true),
                Surface(@"C:\U\0025\391Z010216-0025.IAM", patternedChannel: true),   // a base
            };
            var none = new Dictionary<string, ChannelFingerprint>();
            var noWrites = new Dictionary<string, PartWriteRecord>();
            Func<string, bool> notBase = path => !path.EndsWith("391Z010216-0025.IAM");

            CollectionAssert.AreEqual(new[] { "0019" }, RegenerateReview.BuildItems(surfaces, none, noWrites, null, notBase).Select(i => i.SurfaceName));
            Assert.AreEqual(1, RegenerateReview.CountNeeded(surfaces, none, noWrites, notBase));

            List<SurfaceReviewItem> upload = UploadReview.BuildItems(surfaces, none, noWrites, null, notBase);
            SurfaceReviewItem baseItem = upload.Single(i => i.SurfaceName == "391Z010216-0025");
            Assert.IsTrue(baseItem.IsIncluded);
            Assert.AreEqual(string.Empty, baseItem.Warning, "no 'still needs Regenerate' on a base");
        }

        [Test]
        public void Upload_Result_Says_What_Happened_To_Each_Surface()
        {
            var sent = new[] { @"C:\U\0003\0003.IAM", @"C:\U\0068\0068.IAM", @"C:\U\0074\0074.IAM" };
            string result = UploadReview.Result(
                sent,
                path => !path.EndsWith("0068.IAM"),                                                                  // renamed by MOM
                path => path.EndsWith("0074.IAM") ? new SurfaceDecision(true, new[] { "2 Changed_SQ parts" }) : new SurfaceDecision(false, new[] { "no change since last upload" }));

            StringAssert.StartsWith("2 of 3 surfaces uploaded.", result);
            StringAssert.Contains("Still changed (1)", result);
            StringAssert.Contains("   0074: 2 Changed_SQ parts", result);
            StringAssert.Contains("Renamed by MOM (1)", result);
            StringAssert.Contains("   0003", result);
        }

        [Test]
        public void Review_Ticks_Surfaces_With_Channel_Changes_That_YCShell_Can_Run()
        {
            var surfaces = new[]
            {
                Surface(@"C:\U\0003\0003.IAM"),
                Surface(@"C:\U\0019\0019.IAM", patternedChannel: true),
                Surface(@"C:\U\0068\0091.IAM", patternedChannel: true),
                Surface(@"c:\u\0019\0019.iam", patternedChannel: true),     // same surface reached twice
            };
            var none = new Dictionary<string, ChannelFingerprint>();
            var noWrites = new Dictionary<string, PartWriteRecord>();

            List<SurfaceReviewItem> items = RegenerateReview.BuildItems(surfaces, none, noWrites, path => !path.EndsWith("0091.IAM"));

            CollectionAssert.AreEqual(new[] { "0019", "0091", "0003" }, items.Select(i => i.SurfaceName));
            Assert.IsTrue(items[0].IsIncluded);
            Assert.IsFalse(items[1].IsIncluded, "not loaded by YCShell");
            items[1].IsIncluded = true;
            Assert.IsFalse(items[1].IsIncluded, "can't be ticked");
            Assert.IsFalse(items[2].IsIncluded);
            Assert.AreEqual(2, RegenerateReview.CountNeeded(surfaces, none, noWrites));
            StringAssert.StartsWith("2 of 3 surfaces have channel changes; 1 ticked.", RegenerateReview.Summary(items));
        }
    }
}
