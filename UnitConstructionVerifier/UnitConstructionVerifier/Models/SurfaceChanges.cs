using System;
using System.Collections.Generic;
using System.Linq;

namespace UnitConstructionVerifier.Models
{
    // ─────────────────────────────────────────────────────────────────────────
    // Surface change state: what YCShell has marked since the last upload.
    // See Docs/batch_regenerate_upload_plan.md section 4.0.
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>One top-level occurrence of a surface IAM, with YCShell's markers.</summary>
    public sealed class SurfaceOccurrence
    {
        public string Id          { get; set; } = string.Empty;  // MOM_DATA.OCCURRENCE_ID, or the occurrence name
        public string Name        { get; set; } = string.Empty;  // browser name, e.g. "091Z010068-0020:2"
        public string PartFile    { get; set; } = string.Empty;  // referenced document full path
        public string PartType    { get; set; } = string.Empty;  // MOM_DATA.PART_TYPE on the occurrence: CHANNEL, SKIN, ...
        public bool   IsNew       { get; set; }                  // MOM_DATA.OCCURRENCE_IS_NEW = 1 (added since last upload)
        public string PartStatus  { get; set; } = string.Empty;  // part MOM_DATA.DOCUMENT_STATUS: NoChange, Changed_SQ, New, ...
        public string Position    { get; set; } = string.Empty;  // rounded transformation matrix
        public string PartVersion { get; set; } = string.Empty;  // part file last write time (UTC ticks)
        public bool   PartHardEdited { get; set; }               // IS_HARD_EDITED user property: UCV has written this part

        public bool IsChannel => string.Equals(PartType, "CHANNEL", StringComparison.OrdinalIgnoreCase);

        /// <summary>The part carries a change YCShell's upload will send (anything but NoChange).</summary>
        public bool HasChangedPart => PartStatusRules.IsChange(PartStatus);

        /// <summary>The part was edited (not merely created): Changed_SQ or Changed_Parameter.</summary>
        public bool HasModifiedPart => PartStatusRules.IsModification(PartStatus);
    }

    /// <summary>A surface IAM's occurrences and YCShell's assembly-level change flags.</summary>
    public sealed class SurfaceChangeState
    {
        public string SurfaceIamPath           { get; set; } = string.Empty;
        public bool   HasDeletedComponents     { get; set; }  // MOM_DATA.HAS_DELETED_COMPONENTS = 1
        public bool   HasTransformedComponents { get; set; }  // MOM_DATA.HAS_TRANSFORMED_COMPONENTS = 1
        public List<SurfaceOccurrence> Occurrences { get; set; } = new();
    }

    public static class PartStatusRules
    {
        public const string NoChange = "NoChange";

        public static bool IsChange(string? status) =>
            !string.IsNullOrWhiteSpace(status) &&
            !string.Equals(status!.Trim(), NoChange, StringComparison.OrdinalIgnoreCase);

        public static bool IsModification(string? status) =>
            string.Equals(status?.Trim(), "Changed_SQ", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(status?.Trim(), "Changed_Parameter", StringComparison.OrdinalIgnoreCase);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Part write record: what UCV's gauge / material write-back did to a part.
    // UCV stamps Changed_SQ just as YCShell does for a reshape, so this record is
    // how a material-only edit is told apart (product owner, 2026-10-23: gauge or
    // material edits alone do not need Regenerate).
    // ─────────────────────────────────────────────────────────────────────────

    public sealed class PartWriteRecord
    {
        public string PartFile       { get; set; } = string.Empty;
        public string PreviousStatus { get; set; } = string.Empty;  // DOCUMENT_STATUS before UCV first wrote it
        public string VersionBefore  { get; set; } = string.Empty;  // file version before UCV first wrote it
        public string VersionAfter   { get; set; } = string.Empty;  // file version right after UCV's last save
        public DateTime WrittenAtUtc { get; set; }

        /// <summary>
        /// Folds a later UCV write into this one when nothing else touched the file in between, so a part written
        /// twice by UCV still remembers its state before UCV.
        /// </summary>
        public PartWriteRecord FollowedBy(PartWriteRecord later) =>
            string.Equals(VersionAfter, later.VersionBefore, StringComparison.Ordinal)
                ? new PartWriteRecord { PartFile = later.PartFile, PreviousStatus = PreviousStatus, VersionBefore = VersionBefore, VersionAfter = later.VersionAfter, WrittenAtUtc = later.WrittenAtUtc }
                : later;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Channel fingerprint: UCV's own record of a surface's channels after it
    // regenerated them. Catches channel moves, which YCShell only flags per surface.
    // ─────────────────────────────────────────────────────────────────────────

    public sealed class ChannelEntry
    {
        public string Id          { get; set; } = string.Empty;
        public string Name        { get; set; } = string.Empty;
        public string PartFile    { get; set; } = string.Empty;
        public string Position    { get; set; } = string.Empty;
        public string PartVersion { get; set; } = string.Empty;
    }

    public sealed class ChannelFingerprint
    {
        public string SurfaceIamPath { get; set; } = string.Empty;
        public DateTime CapturedAtUtc { get; set; }
        public List<ChannelEntry> Channels { get; set; } = new();

        public static ChannelFingerprint From(SurfaceChangeState state, DateTime capturedAtUtc) => new ChannelFingerprint
        {
            SurfaceIamPath = state.SurfaceIamPath,
            CapturedAtUtc = capturedAtUtc,
            Channels = state.Occurrences
                .Where(o => o.IsChannel)
                .Select(o => new ChannelEntry { Id = o.Id, Name = o.Name, PartFile = o.PartFile, Position = o.Position, PartVersion = o.PartVersion })
                .OrderBy(c => c.Id, StringComparer.OrdinalIgnoreCase)
                .ToList(),
        };

        /// <summary>
        /// What changed in the channels between this (earlier) fingerprint and <paramref name="current"/>. A part saved
        /// again in place is skipped when <paramref name="isMaterialOnlyEdit"/> says UCV's write-back did it.
        /// </summary>
        public List<string> DifferencesTo(ChannelFingerprint current, Func<ChannelEntry, ChannelEntry, bool>? isMaterialOnlyEdit = null)
        {
            var differences = new List<string>();
            var before = Channels.GroupBy(c => c.Id, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            var after = current.Channels.GroupBy(c => c.Id, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            foreach (ChannelEntry now in current.Channels)
            {
                if (!before.TryGetValue(now.Id, out ChannelEntry? then))
                {
                    differences.Add($"channel added: {now.Name}");
                }
                else if (!string.Equals(then.Position, now.Position, StringComparison.Ordinal))
                {
                    differences.Add($"channel moved: {now.Name}");
                }
                else if (!string.Equals(then.PartFile, now.PartFile, StringComparison.OrdinalIgnoreCase))
                {
                    differences.Add($"channel changed: {now.Name}");
                }
                else if (!string.Equals(then.PartVersion, now.PartVersion, StringComparison.Ordinal) &&
                         isMaterialOnlyEdit?.Invoke(then, now) != true)
                {
                    differences.Add($"channel changed: {now.Name}");
                }
            }

            foreach (ChannelEntry then in Channels.Where(c => !after.ContainsKey(c.Id)))
            {
                differences.Add($"channel removed: {then.Name}");
            }

            return differences;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // The two rules
    // ─────────────────────────────────────────────────────────────────────────

    public sealed class SurfaceDecision
    {
        public SurfaceDecision(bool needed, IReadOnlyList<string> reasons, bool channelsCertain = true)
        {
            Needed = needed;
            Reasons = reasons;
            ChannelsCertain = channelsCertain;
        }

        public bool Needed { get; }
        public IReadOnlyList<string> Reasons { get; }

        /// <summary>
        /// False when Regenerate is needed only because YCShell flagged moved or deleted components on the surface,
        /// which may not be channels. Such surfaces are still ticked by default (product owner, 2026-10-23).
        /// </summary>
        public bool ChannelsCertain { get; }

        public override string ToString() => Needed ? string.Join("; ", Reasons) : (Reasons.FirstOrDefault() ?? "no change");
    }

    public static class SurfaceChangeRules
    {
        /// <summary>
        /// Upload is needed for any change YCShell's upload counts (part status, deleted or moved components), plus
        /// occurrences added since the last upload.
        /// </summary>
        public static SurfaceDecision Upload(SurfaceChangeState state)
        {
            var reasons = new List<string>();

            int added = state.Occurrences.Count(o => o.IsNew);
            if (added > 0) reasons.Add(Plural(added, "occurrence added", "occurrences added"));
            if (state.HasDeletedComponents) reasons.Add("components deleted");
            if (state.HasTransformedComponents) reasons.Add("components moved");

            foreach (var group in ChangedParts(state).GroupBy(o => o.PartStatus.Trim(), StringComparer.OrdinalIgnoreCase).OrderBy(g => g.Key))
            {
                reasons.Add($"{group.Count()} {group.Key} part{(group.Count() == 1 ? "" : "s")}");
            }

            return reasons.Count > 0 ? new SurfaceDecision(true, reasons) : new SurfaceDecision(false, new[] { "no change since last upload" });
        }

        /// <summary>
        /// Regenerate is needed only when channels were added, moved or reshaped; a gauge or material edit alone does
        /// not need it. With a fingerprint from UCV's last regenerate of this surface, the fingerprint decides. Without
        /// one, YCShell's markers decide. <paramref name="partWrites"/> (UCV's write-back records, by part file) tell a
        /// material-only edit from a reshape, since both leave the part Changed_SQ.
        /// </summary>
        public static SurfaceDecision Regenerate(SurfaceChangeState state, ChannelFingerprint? lastRegenerate,
            IReadOnlyDictionary<string, PartWriteRecord>? partWrites = null)
        {
            if (lastRegenerate is not null)
            {
                List<string> differences = lastRegenerate.DifferencesTo(
                    ChannelFingerprint.From(state, DateTime.UtcNow),
                    (then, now) => Write(partWrites, now.PartFile) is PartWriteRecord write &&
                                   write.VersionAfter == now.PartVersion && write.VersionBefore == then.PartVersion);
                return differences.Count > 0
                    ? new SurfaceDecision(true, differences)
                    : new SurfaceDecision(false, new[] { $"channels unchanged since UCV regenerated them ({lastRegenerate.CapturedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm})" });
            }

            var certain = new List<string>();
            var uncertain = new List<string>();
            foreach (SurfaceOccurrence o in state.Occurrences.Where(o => o.IsChannel && o.IsNew)) certain.Add($"channel added: {o.Name}");

            foreach (SurfaceOccurrence o in state.Occurrences.Where(o => o.IsChannel && !o.IsNew && o.HasModifiedPart))
            {
                PartWriteRecord? write = Write(partWrites, o.PartFile);
                if (write is not null)
                {
                    // UCV wrote it. A material-only edit: unchanged before UCV, untouched since.
                    bool materialOnly = write.VersionAfter == o.PartVersion && !PartStatusRules.IsChange(write.PreviousStatus);
                    if (!materialOnly) certain.Add($"channel reshaped: {o.Name}");
                }
                else if (o.PartHardEdited)
                {
                    // Written by a UCV build that kept no record: a gauge / material edit, or a reshape, can't tell.
                    uncertain.Add($"channel edited (gauge/material or reshape, can't tell): {o.Name}");
                }
                else
                {
                    certain.Add($"channel reshaped: {o.Name}");
                }
            }

            if (state.HasDeletedComponents || state.HasTransformedComponents)
            {
                uncertain.Add("components moved or deleted (may not be channels)");
            }

            if (certain.Count > 0) return new SurfaceDecision(true, certain.Concat(uncertain).ToList());
            if (uncertain.Count > 0) return new SurfaceDecision(true, uncertain, channelsCertain: false);
            return new SurfaceDecision(false, new[] { "no channel changes" });
        }

        private static PartWriteRecord? Write(IReadOnlyDictionary<string, PartWriteRecord>? partWrites, string partFile) =>
            partWrites is not null && partWrites.TryGetValue(partFile.Trim(), out PartWriteRecord? write) ? write : null;

        /// <summary>Distinct parts with a change status, one entry per part file.</summary>
        private static IEnumerable<SurfaceOccurrence> ChangedParts(SurfaceChangeState state) =>
            state.Occurrences
                .Where(o => o.HasChangedPart)
                .GroupBy(o => o.PartFile, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First());

        private static string Plural(int n, string one, string many) => $"{n} {(n == 1 ? one : many)}";
    }
}
