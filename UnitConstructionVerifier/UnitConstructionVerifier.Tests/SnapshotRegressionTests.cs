using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using UnitConstructionVerifier.Models;
using UnitConstructionVerifier.Persistence;

namespace UnitConstructionVerifier.Tests
{
    /// <summary>
    /// Replays real-unit snapshots (Snapshots/*.unit_snapshot.json, saved from the verifier's
    /// "Save Snapshot" button) through the builder and engine, and compares the outcome with the
    /// approved baseline next to it (*.approved.json). See Snapshots/README.md.
    /// </summary>
    [TestFixture]
    public class SnapshotRegressionTests
    {
        private const string ApprovedSuffix = ".approved.json";
        private const string ReceivedSuffix = ".received.json";

        [OneTimeSetUp]
        public void Setup()
        {
            MaterialsConfig.Initialize();
        }

        private static string SnapshotDirectory([CallerFilePath] string thisFile = "")
            => Path.Combine(Path.GetDirectoryName(thisFile)!, "Snapshots");

        private static IEnumerable<TestCaseData> Snapshots()
        {
            string dir = SnapshotDirectory();
            var files = Directory.Exists(dir)
                ? Directory.GetFiles(dir, "*" + SnapshotSerializer.FileSuffix).OrderBy(f => f).ToList()
                : new List<string>();

            if (files.Count == 0)
            {
                yield return new TestCaseData("(none)").SetName("Snapshot_(none captured)")
                    .Ignore("No real-unit snapshots captured yet; see Snapshots/README.md.");
                yield break;
            }

            foreach (string file in files)
            {
                string name = Path.GetFileName(file).Substring(0, Path.GetFileName(file).Length - SnapshotSerializer.FileSuffix.Length);
                yield return new TestCaseData(file).SetName($"Snapshot_{name}");
            }
        }

        [TestCaseSource(nameof(Snapshots))]
        public void Snapshot_MatchesApprovedOutcome(string snapshotPath)
        {
            string stem = snapshotPath.Substring(0, snapshotPath.Length - SnapshotSerializer.FileSuffix.Length);
            string approvedPath = stem + ApprovedSuffix;
            string receivedPath = stem + ReceivedSuffix;

            var snapshot = SnapshotSerializer.Load(snapshotPath);
            string received = Normalize(SnapshotSerializer.Serialize(UnitSnapshotOutcome.From(snapshot)));

            if (!File.Exists(approvedPath))
            {
                File.WriteAllText(receivedPath, received);
                Assert.Inconclusive(
                    $"No approved baseline yet. Review {Path.GetFileName(receivedPath)} and rename it to " +
                    $"{Path.GetFileName(approvedPath)} if the unit is known-good.");
            }

            string approved = Normalize(File.ReadAllText(approvedPath));
            if (approved == received)
            {
                if (File.Exists(receivedPath)) File.Delete(receivedPath);
                return;
            }

            File.WriteAllText(receivedPath, received);
            Assert.Fail(
                $"Outcome changed for {Path.GetFileName(snapshotPath)}.\n{FirstDifference(approved, received)}\n" +
                $"Diff {Path.GetFileName(approvedPath)} against {Path.GetFileName(receivedPath)}. If the change is " +
                "intended, replace the approved file with the received one.");
        }

        [Test]
        public void Serializer_RoundTripsBuilderInputs()
        {
            var snapshot = new UnitSnapshot
            {
                SourceIamPath = @"C:\Units\Unit.iam",
                CapturedAtUtc = new DateTime(2026, 10, 21, 12, 0, 0, DateTimeKind.Utc),
                AddinVersion = "1.0.20.0",
                Configs = new List<ConfigData>
                {
                    new ConfigData
                    {
                        SurfaceType = "YC SURF WALL",
                        SkidNumber = 2,
                        SurfaceSegmentList = new List<SurfaceSegment>
                        {
                            new SurfaceSegment { SegmentType = "FF", SegmentTypeSuffix = 1L, SkinMaterialGauge_Left = 18 }
                        }
                    }
                },
                Engineering = new EngineeringData { CurbSupportAngleHeight = 4.5 },
                Ipt = new IptScanResult
                {
                    Parts = new List<IptProperties>
                    {
                        new IptProperties { PartNumber = "091Z009287-0001", YCMATL = "STL GALV", Thickness = "0.0561", IsHardEdited = true }
                    }
                }
            };

            string path = Path.Combine(Path.GetTempPath(), $"ucv_{Guid.NewGuid():N}{SnapshotSerializer.FileSuffix}");
            try
            {
                SnapshotSerializer.Save(path, snapshot);
                var loaded = SnapshotSerializer.Load(path);

                Assert.AreEqual(SnapshotSerializer.Serialize(snapshot), SnapshotSerializer.Serialize(loaded));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Test]
        public void Serializer_RejectsNewerSchema()
        {
            string path = Path.Combine(Path.GetTempPath(), $"ucv_{Guid.NewGuid():N}{SnapshotSerializer.FileSuffix}");
            try
            {
                File.WriteAllText(path, "{ \"SchemaVersion\": 99 }");
                Assert.Throws<InvalidDataException>(() => SnapshotSerializer.Load(path));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Test]
        public void DefaultPath_SitsNextToIam()
        {
            Assert.AreEqual(@"C:\Units\6E-1234.unit_snapshot.json", SnapshotSerializer.GetDefaultPath(@"C:\Units\6E-1234.iam"));
        }

        private static string Normalize(string json) => json.Replace("\r\n", "\n").Trim();

        private static string FirstDifference(string approved, string received)
        {
            string[] a = approved.Split('\n');
            string[] r = received.Split('\n');
            for (int i = 0; i < Math.Max(a.Length, r.Length); i++)
            {
                string left = i < a.Length ? a[i] : "<end>";
                string right = i < r.Length ? r[i] : "<end>";
                if (left != right)
                    return $"First difference at line {i + 1}:\n  approved: {left.Trim()}\n  received: {right.Trim()}";
            }
            return "Files differ only in whitespace.";
        }
    }
}
