using System.Text;

namespace UnitConstructionVerifier.UI
{
    /// <summary>
    /// Text for the prompt shown when the verifier closes with unsaved expectation changes or staged part edits.
    /// </summary>
    public static class UnsavedWorkPrompt
    {
        /// <summary>Returns null when there is nothing to lose.</summary>
        public static string? BuildMessage(bool overridesDirty, int stagedEditCount)
        {
            if (!overridesDirty && stagedEditCount <= 0)
            {
                return null;
            }

            var sb = new StringBuilder();
            if (overridesDirty)
            {
                sb.AppendLine("Expectation changes on this unit have not been saved.");
            }

            if (stagedEditCount > 0)
            {
                sb.AppendLine(stagedEditCount == 1
                    ? "1 staged part edit has not been written to its IPT and will be discarded."
                    : $"{stagedEditCount} staged part edits have not been written to their IPTs and will be discarded.");
            }

            sb.AppendLine();
            sb.Append(overridesDirty
                ? "Yes: save expectation changes and close.\nNo: close without saving.\nCancel: keep the verifier open."
                : "OK: close and discard.\nCancel: keep the verifier open.");
            return sb.ToString();
        }
    }
}
