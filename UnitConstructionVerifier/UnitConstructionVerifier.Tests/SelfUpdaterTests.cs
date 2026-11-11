using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using NUnit.Framework;
using UnitConstructionVerifier.Deployment;

namespace UnitConstructionVerifier.Tests
{
    [TestFixture]
    public class SelfUpdaterTests
    {
        private string _root = null!;
        private string _addins = null!;
        private string _zip = null!;
        private Version _installed = null!;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "ucv_selfupdate_" + Guid.NewGuid().ToString("N"));
            _addins = Path.Combine(_root, "Addins");
            Directory.CreateDirectory(_addins);
            _zip = Path.Combine(_root, "UnitConstructionVerifier_Dist.zip");

            // A real PE file, so the installed version is read the same way as on a detailer's PC.
            File.Copy(typeof(SelfUpdater).Assembly.Location, Path.Combine(_addins, "UnitConstructionVerifier.dll"));
            File.WriteAllText(Path.Combine(_addins, "materials_config.json"), "old config");
            _installed = SelfUpdater.InstalledVersion(_addins);
        }

        [TearDown]
        public void TearDown() => Directory.Delete(_root, recursive: true);

        private void MakeZip(string? version, params (string Name, string Text)[] files)
        {
            using ZipArchive zip = ZipFile.Open(_zip, ZipArchiveMode.Create);
            if (version != null) Write(zip, SelfUpdater.VersionEntry, version);
            foreach ((string name, string text) in files) Write(zip, name, text);
        }

        private static void Write(ZipArchive zip, string name, string text)
        {
            using var writer = new StreamWriter(zip.CreateEntry(name).Open());
            writer.Write(text);
        }

        private string Read(string name) => File.ReadAllText(Path.Combine(_addins, name));

        private Version Newer => new Version(_installed.Major, _installed.Minor, _installed.Build + 1, 0);

        [Test]
        public void NewerRelease_ReplacesFilesAndKeepsOldOnesAside()
        {
            MakeZip(Newer.ToString(), ("UnitConstructionVerifier.dll", "new dll"), ("materials_config.json", "new config"),
                    ("install.bat", "not installed"), ("Guide.pdf", "not installed"));

            Version? staged = SelfUpdater.Stage(_zip, _addins);

            Assert.That(staged, Is.EqualTo(Newer));
            Assert.That(Read("UnitConstructionVerifier.dll"), Is.EqualTo("new dll"));
            Assert.That(Read("materials_config.json"), Is.EqualTo("new config"));
            Assert.That(File.Exists(Path.Combine(_addins, "install.bat")), Is.False);
            Assert.That(File.Exists(Path.Combine(_addins, "Guide.pdf")), Is.False);
            Assert.That(Directory.GetFiles(_addins, "*.old_*"), Has.Length.EqualTo(2));
            Assert.That(Directory.GetFiles(_addins, "*.new"), Is.Empty);
        }

        [Test]
        public void SameOrOlderRelease_ChangesNothing()
        {
            MakeZip(_installed.ToString(), ("UnitConstructionVerifier.dll", "same"));

            Assert.That(SelfUpdater.Stage(_zip, _addins), Is.Null);
            Assert.That(Read("materials_config.json"), Is.EqualTo("old config"));
            Assert.That(Directory.GetFiles(_addins).Length, Is.EqualTo(2));
        }

        [TestCase(null)]
        [TestCase("not a version")]
        public void ReleaseWithoutReadableVersion_IsIgnored(string? version)
        {
            MakeZip(version, ("UnitConstructionVerifier.dll", "new dll"));
            Assert.That(SelfUpdater.Stage(_zip, _addins), Is.Null);
        }

        [Test]
        public void ReleaseWithoutTheAddInDll_IsIgnored()
        {
            MakeZip(Newer.ToString(), ("materials_config.json", "new config"));
            Assert.That(SelfUpdater.Stage(_zip, _addins), Is.Null);
            Assert.That(Read("materials_config.json"), Is.EqualTo("old config"));
        }

        [Test]
        public void UnreachableShare_IsIgnored()
            => Assert.That(SelfUpdater.Stage(Path.Combine(_root, "missing.zip"), _addins), Is.Null);

        [Test]
        public void FailedReplace_PutsTheOldFilesBack()
        {
            MakeZip(Newer.ToString(), ("UnitConstructionVerifier.dll", "new dll"), ("materials_config.json", "new config"));
            byte[] oldDll = File.ReadAllBytes(Path.Combine(_addins, "UnitConstructionVerifier.dll"));

            // Held open with no sharing, so it can't be renamed aside: the swap fails after the DLL was replaced.
            using (new FileStream(Path.Combine(_addins, "materials_config.json"), FileMode.Open, FileAccess.Read, FileShare.None))
            {
                Assert.That(() => SelfUpdater.Stage(_zip, _addins), Throws.InstanceOf<IOException>());
            }

            Assert.That(File.ReadAllBytes(Path.Combine(_addins, "UnitConstructionVerifier.dll")), Is.EqualTo(oldDll));
            Assert.That(Read("materials_config.json"), Is.EqualTo("old config"));
            Assert.That(Directory.GetFiles(_addins).Select(Path.GetFileName),
                        Is.EquivalentTo(new[] { "UnitConstructionVerifier.dll", "materials_config.json" }));
        }

        [Test]
        public void RemoveOldCopies_DeletesOnlyOldFiles()
        {
            File.WriteAllText(Path.Combine(_addins, "UnitConstructionVerifier.dll.old_20261101_101010"), "x");
            SelfUpdater.RemoveOldCopies(_addins);
            Assert.That(Directory.GetFiles(_addins).Length, Is.EqualTo(2));
        }
    }
}
