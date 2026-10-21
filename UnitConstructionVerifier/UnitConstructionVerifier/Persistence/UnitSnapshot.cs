using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnitConstructionVerifier.Engine;
using UnitConstructionVerifier.Extraction;
using UnitConstructionVerifier.Models;

namespace UnitConstructionVerifier.Persistence
{
    /// <summary>
    /// Everything ConstructionDataBuilder and VerificationEngine consume for one unit, captured
    /// from a live Inventor scan so it can be replayed in tests without Inventor.
    /// </summary>
    public sealed class UnitSnapshot
    {
        public const int CurrentSchemaVersion = 1;

        public int SchemaVersion { get; set; } = CurrentSchemaVersion;
        public string SourceIamPath { get; set; } = string.Empty;
        public DateTime CapturedAtUtc { get; set; }
        public string AddinVersion { get; set; } = string.Empty;

        public List<ConfigData> Configs { get; set; } = new();
        public EngineeringData? Engineering { get; set; }
        public IptScanResult Ipt { get; set; } = new();
    }

    /// <summary>The builder and engine output for a snapshot; compared against an approved baseline.</summary>
    public sealed class UnitSnapshotOutcome
    {
        public UnitConstructionData Construction { get; set; } = new();
        public List<PartCheck> Checks { get; set; } = new();

        public static UnitSnapshotOutcome From(UnitSnapshot snapshot)
        {
            var data = new ConstructionDataBuilder(snapshot.Configs, snapshot.Engineering, snapshot.Ipt).Build();
            var result = new VerificationEngine(data, snapshot.Ipt).Run();
            return new UnitSnapshotOutcome { Construction = data, Checks = result.Checks };
        }
    }

    public static class SnapshotSerializer
    {
        public const string FileSuffix = ".unit_snapshot.json";

        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Include,
        };

        public static string GetDefaultPath(string iamPath)
        {
            string dir = Path.GetDirectoryName(iamPath) ?? string.Empty;
            return Path.Combine(dir, Path.GetFileNameWithoutExtension(iamPath) + FileSuffix);
        }

        public static string Serialize(object value) => JsonConvert.SerializeObject(value, Settings);

        public static void Save(string path, UnitSnapshot snapshot)
        {
            File.WriteAllText(path, Serialize(snapshot));
        }

        public static UnitSnapshot Load(string path)
        {
            var snapshot = JsonConvert.DeserializeObject<UnitSnapshot>(File.ReadAllText(path), Settings)
                ?? throw new InvalidDataException($"Snapshot is empty: {path}");
            if (snapshot.SchemaVersion > UnitSnapshot.CurrentSchemaVersion)
                throw new InvalidDataException(
                    $"Snapshot schema v{snapshot.SchemaVersion} is newer than supported v{UnitSnapshot.CurrentSchemaVersion}: {path}");
            return snapshot;
        }
    }
}
