using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using UnitConstructionVerifier.Extraction;
using UnitConstructionVerifier.Models;
using UnitConstructionVerifier.Persistence;

namespace UnitConstructionVerifier.Tests
{
    /// <summary>
    /// Regenerate / Upload changed rules (Docs/batch_regenerate_upload_plan.md section 4.0). Cases follow what the
    /// Hole Pattern Tiger Team test unit showed on 2026-10-23.
    /// </summary>
    [TestFixture]
    public class SurfaceChangeRulesTests
    {
        private const string Surface = @"C:\Unit\391Z010068-0019\391Z010068-0019.IAM";

        private static SurfaceOccurrence Occ(string name, string partType, string status = "NoChange", bool isNew = false,
            string position = "0,0,0", string? part = null, string version = "1") => new SurfaceOccurrence
        {
            Id = name,
            Name = name,
            PartFile = part ?? $@"C:\Unit\391Z010068-0019\{name.Split(':')[0]}.IPT",
            PartType = partType,
            PartStatus = status,
            IsNew = isNew,
            Position = position,
            PartVersion = version,
        };

        private static SurfaceChangeState Wall(params SurfaceOccurrence[] occurrences) =>
            new SurfaceChangeState { SurfaceIamPath = Surface, Occurrences = occurrences.ToList() };

        private static SurfaceChangeState Untouched() => Wall(
            Occ("091Z010068-0015:1", "CHANNEL"),
            Occ("091Z010068-0020:1", "CHANNEL", position: "1,0,0"),
            Occ("091Z010068-0173:1", "SKIN"),
            Occ("091Z010068-0179:1", "LINER"));

        [Test]
        public void Untouched_Surface_Needs_Neither()
        {
            Assert.IsFalse(SurfaceChangeRules.Regenerate(Untouched(), null).Needed);
            Assert.IsFalse(SurfaceChangeRules.Upload(Untouched()).Needed);
        }

        [Test]
        public void Skin_Or_Liner_SQ_Change_Needs_Upload_Only()
        {
            SurfaceChangeState state = Untouched();
            state.Occurrences.Single(o => o.PartType == "SKIN").PartStatus = "Changed_SQ";

            Assert.IsFalse(SurfaceChangeRules.Regenerate(state, null).Needed);
            SurfaceDecision upload = SurfaceChangeRules.Upload(state);
            Assert.IsTrue(upload.Needed);
            CollectionAssert.Contains(upload.Reasons, "1 Changed_SQ part");
        }

        [Test]
        public void Subfloor_SQ_Parts_Count_Once_Per_Part_File()
        {
            // 0091: five SUBFLOOR SQ occurrences over four part files; no channel touched.
            var state = Wall(
                Occ("091Z010068-0018:1", "CHANNEL"),
                Occ("091Z010068-0497:1 [SQ]", "SUBFLOOR", "Changed_SQ", part: "0497.IPT"),
                Occ("091Z010068-0498:1 [SQ]", "SUBFLOOR", "Changed_SQ", part: "0498.IPT"),
                Occ("091Z010068-0498:2 [SQ]", "SUBFLOOR", "Changed_SQ", part: "0498.IPT"),
                Occ("091Z010068-0499:1 [SQ]", "SUBFLOOR", "Changed_SQ", part: "0499.IPT"),
                Occ("091Z010068-0500:1 [SQ]", "SUBFLOOR", "Changed_SQ", part: "0500.IPT"));

            Assert.IsFalse(SurfaceChangeRules.Regenerate(state, null).Needed);
            CollectionAssert.AreEqual(new[] { "4 Changed_SQ parts" }, SurfaceChangeRules.Upload(state).Reasons);
        }

        [Test]
        public void Patterned_Channel_Reusing_An_Unchanged_Part_Needs_Regenerate()
        {
            // 0019 in the spike copy: the patterned occurrence reuses 091Z010068-0020.IPT, still NoChange.
            SurfaceChangeState state = Untouched();
            state.Occurrences.Add(Occ("[0002/B] 091Z010068-0020:2 - NEW", "CHANNEL", isNew: true,
                part: state.Occurrences[1].PartFile, position: "1,0,50"));

            SurfaceDecision regenerate = SurfaceChangeRules.Regenerate(state, null);
            Assert.IsTrue(regenerate.Needed);
            Assert.IsTrue(regenerate.ChannelsCertain);
            CollectionAssert.AreEqual(new[] { "channel added: [0002/B] 091Z010068-0020:2 - NEW" }, regenerate.Reasons);
            CollectionAssert.AreEqual(new[] { "1 occurrence added" }, SurfaceChangeRules.Upload(state).Reasons);
        }

        [TestCase("Changed_SQ")]
        [TestCase("Changed_Parameter")]
        public void Modified_Channel_Needs_Regenerate(string status)
        {
            SurfaceChangeState state = Untouched();
            state.Occurrences[0].PartStatus = status;

            SurfaceDecision regenerate = SurfaceChangeRules.Regenerate(state, null);
            CollectionAssert.AreEqual(new[] { "channel modified: 091Z010068-0015:1" }, regenerate.Reasons);
        }

        [Test]
        public void Moved_Or_Deleted_Flag_Alone_Is_Ticked_But_Uncertain()
        {
            SurfaceChangeState state = Untouched();
            state.HasTransformedComponents = true;

            SurfaceDecision regenerate = SurfaceChangeRules.Regenerate(state, null);
            Assert.IsTrue(regenerate.Needed);
            Assert.IsFalse(regenerate.ChannelsCertain);
            CollectionAssert.AreEqual(new[] { "components moved" }, SurfaceChangeRules.Upload(state).Reasons);
        }

        [Test]
        public void After_UCV_Regenerates_The_Same_Channels_Do_Not_Need_It_Again()
        {
            // Regenerate leaves its replacement channels marked new with New parts until upload.
            var state = Wall(
                Occ("091Z010068-0015:1", "CHANNEL"),
                Occ("091Z010068-0505:1", "CHANNEL", "New", isNew: true),
                Occ("091Z010068-0506:1", "CHANNEL", "New", isNew: true),
                Occ("091Z010068-0173:1", "SKIN"));
            state.HasDeletedComponents = true;
            ChannelFingerprint afterRegenerate = ChannelFingerprint.From(state, new DateTime(2026, 10, 23, 12, 0, 0, DateTimeKind.Utc));

            Assert.IsTrue(SurfaceChangeRules.Regenerate(state, null).Needed, "without a record the markers decide");
            Assert.IsFalse(SurfaceChangeRules.Regenerate(state, afterRegenerate).Needed);
            Assert.IsTrue(SurfaceChangeRules.Upload(state).Needed, "still waiting to be uploaded");
        }

        [Test]
        public void Fingerprint_Ignores_Skin_Moves_But_Names_Channel_Changes()
        {
            SurfaceChangeState before = Untouched();
            ChannelFingerprint record = ChannelFingerprint.From(before, DateTime.UtcNow);

            SurfaceChangeState skinMoved = Untouched();
            skinMoved.Occurrences.Single(o => o.PartType == "SKIN").Position = "9,9,9";
            skinMoved.HasTransformedComponents = true;
            Assert.IsFalse(SurfaceChangeRules.Regenerate(skinMoved, record).Needed);

            SurfaceChangeState changed = Untouched();
            changed.Occurrences[0].Position = "0,0,12.5";                     // moved
            changed.Occurrences[1].PartVersion = "2";                         // part saved again (edited)
            changed.Occurrences.Add(Occ("091Z010068-0020:2", "CHANNEL"));     // added
            CollectionAssert.AreEquivalent(
                new[] { "channel moved: 091Z010068-0015:1", "channel changed: 091Z010068-0020:1", "channel added: 091Z010068-0020:2" },
                SurfaceChangeRules.Regenerate(changed, record).Reasons);

            SurfaceChangeState removed = Untouched();
            removed.Occurrences.RemoveAt(0);
            CollectionAssert.AreEqual(new[] { "channel removed: 091Z010068-0015:1" }, SurfaceChangeRules.Regenerate(removed, record).Reasons);
        }

        [Test]
        public void Positions_Round_And_Drop_Negative_Zero()
        {
            double[] cells = { 1, -0.0000001, 0, 12.34567, 0, 1, 0, -3.0004, 0, 0, 1, 0, 0, 0, 0, 1 };
            Assert.AreEqual("1,0,0,12.346,0,1,0,-3,0,0,1,0,0,0,0,1", SurfaceChangeReader.FormatPosition(cells));
        }

        [Test]
        public void Fingerprint_Store_Keeps_One_Record_Per_Surface()
        {
            string dir = Path.Combine(Path.GetTempPath(), "ucv_fp_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string unit = Path.Combine(dir, "FINAL.IAM");
                ChannelFingerprint first = ChannelFingerprint.From(Untouched(), DateTime.UtcNow);
                ChannelFingerprintStore.Record(unit, first);
                var other = new ChannelFingerprint { SurfaceIamPath = @"C:\Unit\0055\0055.IAM", CapturedAtUtc = DateTime.UtcNow };
                ChannelFingerprintStore.Record(unit, other);
                ChannelFingerprintStore.Record(unit, ChannelFingerprint.From(Untouched(), DateTime.UtcNow));   // replaces the first

                Assert.AreEqual(2, ChannelFingerprintStore.Load(unit).Count);
                ChannelFingerprint? found = ChannelFingerprintStore.Find(unit, Surface.ToLowerInvariant());
                Assert.IsNotNull(found);
                Assert.AreEqual(2, found!.Channels.Count);

                File.WriteAllText(ChannelFingerprintStore.GetSidecarPath(unit), "{ not json");
                Assert.AreEqual(0, ChannelFingerprintStore.Load(unit).Count, "a damaged record falls back to YCShell's markers");
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
        }

        [Test]
        public void Older_Snapshots_Without_Surfaces_Still_Load()
        {
            var result = JsonConvert.DeserializeObject<IptScanResult>("{ \"Parts\": [] }")!;
            Assert.IsNotNull(result.Surfaces);
            Assert.IsEmpty(result.Surfaces);
        }
    }
}
