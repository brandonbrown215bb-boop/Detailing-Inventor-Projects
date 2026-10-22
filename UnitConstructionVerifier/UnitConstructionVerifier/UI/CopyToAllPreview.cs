using System;
using System.Collections.Generic;
using System.Linq;

namespace UnitConstructionVerifier.UI
{
    /// <summary>One expectation field on a surface row, used both to compare and to copy.</summary>
    public sealed class ExpectationField<T>
    {
        public ExpectationField(string label, Func<T, string> get, Action<T, string> set)
        {
            Label = label;
            Get = get;
            Set = set;
        }

        public string Label { get; }
        public Func<T, string> Get { get; }
        public Action<T, string> Set { get; }
    }

    public sealed class FieldChange
    {
        public FieldChange(string label, string from, string to)
        {
            Label = label;
            From = from;
            To = to;
        }

        public string Label { get; }
        public string From { get; }
        public string To { get; }
    }

    public sealed class SurfaceChange<T>
    {
        public SurfaceChange(T row, IReadOnlyList<FieldChange> fields)
        {
            Row = row;
            Fields = fields;
        }

        public T Row { get; }
        public IReadOnlyList<FieldChange> Fields { get; }
    }

    /// <summary>Works out what "Copy to all" would change, so the prompt can say so (or be skipped).</summary>
    public static class CopyToAllPreview
    {
        /// <summary>Targets whose expectations differ from <paramref name="source"/>, with the fields that differ.</summary>
        public static List<SurfaceChange<T>> Diff<T>(T source, IEnumerable<T> targets, IReadOnlyList<ExpectationField<T>> fields)
            where T : class
        {
            var changes = new List<SurfaceChange<T>>();
            foreach (T target in targets)
            {
                if (ReferenceEquals(target, source)) continue;

                var differing = fields
                    .Where(f => !Same(f.Get(source), f.Get(target)))
                    .Select(f => new FieldChange(f.Label, Show(f.Get(target)), Show(f.Get(source))))
                    .ToList();
                if (differing.Count > 0) changes.Add(new SurfaceChange<T>(target, differing));
            }

            return changes;
        }

        public static void Apply<T>(T source, IEnumerable<SurfaceChange<T>> changes, IReadOnlyList<ExpectationField<T>> fields)
        {
            foreach (SurfaceChange<T> change in changes)
            {
                foreach (ExpectationField<T> field in fields)
                {
                    field.Set(change.Row, field.Get(source));
                }
            }
        }

        /// <summary>One line per changed surface, capped so the prompt stays on screen.</summary>
        public static string Describe<T>(IReadOnlyList<SurfaceChange<T>> changes, Func<T, string> name, int maxSurfaces = 12)
        {
            var lines = changes
                .Take(maxSurfaces)
                .Select(c => $"{name(c.Row)}: " + string.Join("; ", c.Fields.Select(f => $"{f.Label} {f.From} → {f.To}")))
                .ToList();
            if (changes.Count > maxSurfaces) lines.Add($"…and {changes.Count - maxSurfaces} more");
            return string.Join("\n", lines);
        }

        private static bool Same(string? a, string? b) =>
            string.Equals((a ?? string.Empty).Trim(), (b ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase);

        private static string Show(string? value) => string.IsNullOrWhiteSpace(value) ? "(blank)" : value!.Trim();
    }
}
