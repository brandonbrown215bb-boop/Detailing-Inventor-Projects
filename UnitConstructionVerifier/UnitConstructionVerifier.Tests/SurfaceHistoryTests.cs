using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnitConstructionVerifier.Models;
using UnitConstructionVerifier.Persistence;

namespace UnitConstructionVerifier.Tests
{
    /// <summary>Surface number history (product owner, 2026-10-23: "0021 -> 0048 -> 0049 -> 0122").</summary>
    [TestFixture]
    public class SurfaceHistoryTests
    {
        private static string Iam(string n) => $@"C:\SW0004\Skid 1\391Z010216-0021\391Z010216-{n}.IAM";

        private static readonly DateTime T0 = new DateTime(2026, 10, 23, 17, 13, 0, DateTimeKind.Utc);

        private static Dictionary<string, string> Rename(string from, string to) => new Dictionary<string, string> { [Iam(from)] = Iam(to) };

        [Test]
        public void A_Surface_Renamed_Over_Several_Uploads_Keeps_One_Chain()
        {
            var all = new List<SurfaceHistory>();
            SurfaceHistoryRules.AddRenames(all, Rename("0021", "0048"), _ => T0, T0);
            SurfaceHistoryRules.AddRenames(all, Rename("0048", "0049"), _ => T0.AddDays(2), T0.AddDays(2));
            SurfaceHistoryRules.AddRenames(all, Rename("0049", "0122"), _ => T0.AddDays(9), T0.AddDays(9));

            SurfaceHistory history = all.Single();
            Assert.AreEqual("391Z010216-0021 → 0048 → 0049 → 0122", history.NumberChain());
            Assert.AreEqual(Iam("0122"), history.Current!.IamPath);
            CollectionAssert.AreEqual(
                new[] { SurfaceHistory.FirstSeen, SurfaceHistory.Renamed, SurfaceHistory.Renamed, SurfaceHistory.Renamed },
                history.Entries.Select(e => e.Event));
        }

        [Test]
        public void Uploads_Are_Noted_Once_And_Not_Twice_For_A_Rename_From_The_Same_Upload()
        {
            var all = new List<SurfaceHistory>();
            SurfaceHistoryRules.AddRenames(all, Rename("0021", "0048"), _ => T0, T0);
            SurfaceHistoryRules.AddUploads(all, new[] { Iam("0048"), Iam("0025") }, T0.AddMinutes(1));

            Assert.AreEqual(2, all.Count);
            Assert.AreEqual(SurfaceHistory.Renamed, all[0].Current!.Event, "the rename is the upload");
            Assert.AreEqual(SurfaceHistory.Uploaded, all[1].Current!.Event);

            SurfaceHistoryRules.AddUploads(all, new[] { Iam("0048") }, T0.AddDays(1));
            Assert.AreEqual(SurfaceHistory.Uploaded, all[0].Current!.Event, "a later upload without a new number is noted");
            Assert.AreEqual("391Z010216-0021 → 0048", all[0].NumberChain(), "the chain shows numbers once");
        }

        [Test]
        public void Report_Lists_Renamed_Surfaces_First_With_Dates()
        {
            var all = new List<SurfaceHistory>();
            SurfaceHistoryRules.AddUploads(all, new[] { Iam("0025") }, T0);
            SurfaceHistoryRules.AddRenames(all, Rename("0021", "0048"), _ => T0, T0);

            string report = SurfaceHistoryRules.Report(all);

            StringAssert.StartsWith("391Z010216-0021 → 0048", report);
            StringAssert.Contains($"{T0.ToLocalTime():yyyy-MM-dd HH:mm}  391Z010216-0048    renamed by MOM", report);
            Assert.Less(report.IndexOf("0048", StringComparison.Ordinal), report.IndexOf("391Z010216-0025", StringComparison.Ordinal));
            StringAssert.StartsWith("No surface history yet", SurfaceHistoryRules.Report(new List<SurfaceHistory>()));
        }

        [Test]
        public void History_Survives_Save_And_An_Unreadable_File_Is_Kept_Aside()
        {
            string dir = Path.Combine(Path.GetTempPath(), "ucv_hist_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string unit = Path.Combine(dir, "FINAL.IAM");
                SurfaceHistoryStore.RecordRenames(unit, Rename("0021", "0048"));
                SurfaceHistoryStore.RecordUploads(unit, new[] { Iam("0025") });
                Assert.AreEqual(2, SurfaceHistoryStore.Load(unit).Count);

                File.WriteAllText(SurfaceHistoryStore.GetSidecarPath(unit), "{ broken");
                Assert.IsEmpty(SurfaceHistoryStore.Load(unit));
                Assert.IsTrue(File.Exists(SurfaceHistoryStore.GetSidecarPath(unit) + ".unreadable"), "the damaged history is kept, not lost");
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }
}
