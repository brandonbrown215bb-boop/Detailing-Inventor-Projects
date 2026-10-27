using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnitConstructionVerifier.Models;
using UnitConstructionVerifier.Persistence;

namespace UnitConstructionVerifier.Tests
{
    /// <summary>
    /// Following surfaces MOM renamed on upload (SW0004 Skid 1, 2026-10-23: 391Z010216-0021 became -0048, and so on).
    /// </summary>
    [TestFixture]
    public class SurfaceRenameTests
    {
        private const string Old21 = @"C:\SW0004\Skid 1\391Z010216-0021\391Z010216-0021.IAM";
        private const string New48 = @"C:\SW0004\Skid 1\391Z010216-0021\391Z010216-0048.IAM";
        private const string Kept25 = @"C:\SW0004\Skid 1\391Z010216-0025\391Z010216-0025.IAM";

        [Test]
        public void Finds_The_New_Surface_The_Unit_References_In_The_Same_Folder()
        {
            var exists = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { New48, Kept25 };
            Dictionary<string, string> renames = SurfaceRenames.Find(new[] { Old21, Kept25 }, exists.Contains, new[] { New48, Kept25 });

            Assert.AreEqual(1, renames.Count);
            Assert.AreEqual(New48, renames[Old21]);
        }

        [Test]
        public void Leaves_A_Surface_Alone_When_The_New_File_Is_Ambiguous_Or_Missing()
        {
            string other = @"C:\SW0004\Skid 1\391Z010216-0021\391Z010216-0049.IAM";
            Assert.IsEmpty(SurfaceRenames.Find(new[] { Old21 }, _ => false, new[] { New48, other }), "two candidates");
            Assert.IsEmpty(SurfaceRenames.Find(new[] { Old21 }, _ => false, Array.Empty<string>()), "not relinked yet");
            Assert.IsEmpty(SurfaceRenames.Find(new[] { Old21, New48 }, p => p == New48, new[] { New48 }), "already a surface row");
        }

        private static UnitConstructionData Unit() => new UnitConstructionData
        {
            WallRows = { new WallSurfaceRow { PartNumber = "391Z010216-0021", SourceSurfaceIam = Old21, Thickness = "2\"" } },
            RoofRows = { new RoofSurfaceRow { PartNumber = "ROOF A", SourceSurfaceIam = Old21 } },
            BaseRows = { new BaseSurfaceRow { PartNumber = "391Z010216-0025", SourceSurfaceIam = Kept25 } },
        };

        [Test]
        public void Rows_Follow_The_Rename_And_Part_Numbers_That_Were_File_Names_Too()
        {
            UnitConstructionData data = Unit();

            int changed = SurfaceRenames.Apply(data, new Dictionary<string, string> { [Old21] = New48 });

            Assert.AreEqual(2, changed);
            Assert.AreEqual(New48, data.WallRows[0].SourceSurfaceIam);
            Assert.AreEqual("391Z010216-0048", data.WallRows[0].PartNumber);
            Assert.AreEqual("ROOF A", data.RoofRows[0].PartNumber, "a part number that was not the file name stays");
            Assert.AreEqual(Kept25, data.BaseRows[0].SourceSurfaceIam);
        }

        [Test]
        public void Saved_Expectations_Stay_With_A_Renamed_Surface()
        {
            string dir = Path.Combine(Path.GetTempPath(), "ucv_rn_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string unit = Path.Combine(dir, "SW GROWTH TEST 4.IAM");
                PersistenceManager.SaveOverrides(unit, Unit());
                var renames = new Dictionary<string, string> { [Old21] = New48 };

                Assert.IsTrue(PersistenceManager.RenameSurfaces(unit, renames));
                Assert.IsFalse(PersistenceManager.RenameSurfaces(unit, renames), "nothing left to rename");

                // A fresh scan after the upload builds the row under the new part number; the saved thickness still applies.
                var live = new UnitConstructionData { WallRows = { new WallSurfaceRow { PartNumber = "391Z010216-0048", SourceSurfaceIam = New48 } } };
                PersistenceManager.LoadOverrides(unit, live);
                Assert.AreEqual("2\"", live.WallRows[0].Thickness);

                ChannelFingerprintStore.Record(unit, new ChannelFingerprint { SurfaceIamPath = Old21, CapturedAtUtc = DateTime.UtcNow });
                ChannelFingerprintStore.RenameSurfaces(unit, renames);
                Assert.IsNull(ChannelFingerprintStore.Find(unit, Old21));
                Assert.AreEqual(New48, ChannelFingerprintStore.Find(unit, New48)?.SurfaceIamPath);
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
        }

        [Test]
        public void On_Open_The_Records_Follow_Renames_From_Their_Own_Paths()
        {
            // The verifier reopened after the upload: its rows are already built under the new numbers, so only the
            // records still know the old surfaces. Real files here, because the follower checks which ones are gone.
            string dir = Path.Combine(Path.GetTempPath(), "ucv_rn_" + Guid.NewGuid().ToString("N"));
            string folder = Path.Combine(dir, "391Z010216-0021");
            Directory.CreateDirectory(folder);
            try
            {
                string unit = Path.Combine(dir, "SW GROWTH TEST 4.IAM");
                string oldIam = Path.Combine(folder, "391Z010216-0021.IAM");
                string newIam = Path.Combine(folder, "391Z010216-0048.IAM");
                File.WriteAllText(newIam, string.Empty);   // MOM's copy exists; the old file was deleted by the upload

                PersistenceManager.SaveOverrides(unit, new UnitConstructionData
                {
                    WallRows = { new WallSurfaceRow { PartNumber = "391Z010216-0021", SourceSurfaceIam = oldIam, Thickness = "2\"" } },
                });
                ChannelFingerprintStore.Record(unit, new ChannelFingerprint { SurfaceIamPath = oldIam, CapturedAtUtc = DateTime.UtcNow });

                Dictionary<string, string> renames = SurfaceRenameFollower.Follow(unit, Array.Empty<string>(), new[] { newIam });

                Assert.AreEqual(newIam, renames[oldIam]);
                var live = new UnitConstructionData { WallRows = { new WallSurfaceRow { PartNumber = "391Z010216-0048", SourceSurfaceIam = newIam } } };
                PersistenceManager.LoadOverrides(unit, live);
                Assert.AreEqual("2\"", live.WallRows[0].Thickness);
                Assert.IsNotNull(ChannelFingerprintStore.Find(unit, newIam));
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }
}
