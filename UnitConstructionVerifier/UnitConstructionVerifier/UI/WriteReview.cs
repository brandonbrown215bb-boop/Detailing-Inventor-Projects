using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using UnitConstructionVerifier.Operations;

namespace UnitConstructionVerifier.UI
{
    /// <summary>One staged part change: what the part holds now and what will be written.</summary>
    public sealed class StagedEdit
    {
        public StagedEdit(string filePath, string parameter, string partNumber, string current, string newValue)
        {
            FilePath = filePath;
            Parameter = parameter;
            PartNumber = partNumber;
            Current = current;
            NewValue = newValue;
        }

        public string FilePath   { get; }
        public string Parameter  { get; }
        public string PartNumber { get; }
        public string Current    { get; }
        public string NewValue   { get; }
        public string Key => KeyFor(FilePath, Parameter);

        public static string KeyFor(string filePath, string parameter) => $"{filePath}|{parameter}";
    }

    /// <summary>A row in the write review: one staged change the user can untick to leave it staged.</summary>
    public sealed class WriteReviewItem : INotifyPropertyChanged
    {
        public WriteReviewItem(StagedEdit edit, bool canWrite)
        {
            Edit = edit;
            CanWrite = canWrite;
            _isIncluded = canWrite;
        }

        public StagedEdit Edit { get; }
        /// <summary>False when the value cannot be mapped to a part property; such rows are shown but never written.</summary>
        public bool CanWrite { get; }
        public string PartNumber => string.IsNullOrWhiteSpace(Edit.PartNumber) ? Path.GetFileNameWithoutExtension(Edit.FilePath) : Edit.PartNumber;
        public string Parameter => Edit.Parameter;
        public string Current => string.IsNullOrWhiteSpace(Edit.Current) ? "(blank)" : Edit.Current;
        public string NewValue => Edit.NewValue;
        public string FileTooltip => Edit.FilePath;
        public string NewValueTooltip => CanWrite ? "Will be written to the part." : "Cannot be written to a part property; skipped.";

        private bool _isIncluded;
        public bool IsIncluded
        {
            get => _isIncluded;
            set
            {
                bool next = value && CanWrite;
                if (_isIncluded == next) return;
                _isIncluded = next;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsIncluded)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    /// <summary>Builds the write review list and turns the ticked rows into per-file property edits.</summary>
    public static class WriteReview
    {
        public static List<WriteReviewItem> BuildItems(IEnumerable<StagedEdit> edits) =>
            edits
                .OrderBy(e => e.PartNumber, StringComparer.OrdinalIgnoreCase)
                .ThenBy(e => e.Parameter, StringComparer.OrdinalIgnoreCase)
                .Select(e => new WriteReviewItem(e, PartPropertyEditsMapper.TryApply(new PartPropertyEdits(), e.Parameter, e.NewValue)))
                .ToList();

        /// <summary>Ticked, writable rows grouped by part file. Files left with nothing to write are dropped.</summary>
        public static Dictionary<string, PartPropertyEdits> GroupByFile(IEnumerable<WriteReviewItem> items)
        {
            var grouped = new Dictionary<string, PartPropertyEdits>(StringComparer.OrdinalIgnoreCase);
            foreach (WriteReviewItem item in items.Where(i => i.IsIncluded && i.CanWrite))
            {
                if (!grouped.TryGetValue(item.Edit.FilePath, out PartPropertyEdits edits))
                {
                    edits = new PartPropertyEdits();
                    grouped[item.Edit.FilePath] = edits;
                }

                PartPropertyEditsMapper.TryApply(edits, item.Edit.Parameter, item.Edit.NewValue);
            }

            foreach (string empty in grouped.Where(kvp => !PartPropertyEditsMapper.HasAnyValue(kvp.Value)).Select(kvp => kvp.Key).ToList())
            {
                grouped.Remove(empty);
            }

            return grouped;
        }

        public static string Summary(IReadOnlyCollection<WriteReviewItem> items)
        {
            int ticked = items.Count(i => i.IsIncluded);
            int files = items.Where(i => i.IsIncluded).Select(i => i.Edit.FilePath).Distinct(StringComparer.OrdinalIgnoreCase).Count();
            int skipped = items.Count(i => !i.CanWrite);

            string text = ticked == 0
                ? "Nothing is ticked."
                : $"{Plural(ticked, "change")} to {Plural(files, "part file")} will be written in Inventor.";
            if (ticked < items.Count - skipped) text += " Unticked changes stay staged.";
            if (skipped > 0) text += $" {Plural(skipped, "change")} cannot be written to part properties and {(skipped == 1 ? "is" : "are")} skipped.";
            return text;
        }

        internal static string Plural(int count, string noun) => count == 1 ? $"1 {noun}" : $"{count} {noun}s";
    }
}
