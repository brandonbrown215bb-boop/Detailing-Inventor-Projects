using System;
using System.Collections.Generic;
using System.Linq;

namespace UnitConstructionVerifier.Models
{
    // ─────────────────────────────────────────────────────────────────────────
    // Regenerate changed: YCShell's message boxes, per-surface outcomes and the
    // batch summary. See Docs/batch_regenerate_upload_plan.md sections 4.2 - 4.4.
    // ─────────────────────────────────────────────────────────────────────────

    public enum YcShellMessageKind
    {
        /// <summary>"N Channel Features have been recreated/replaced": the surface finished.</summary>
        Completion,

        /// <summary>An OK-only notice UCV may close, such as the sloped / outdoor roof notice.</summary>
        Information,

        /// <summary>A Yes/No decision. Never answered by UCV.</summary>
        Question,

        /// <summary>Anything else (errors, unknown wording). Left for the detailer.</summary>
        Other,
    }

    public sealed class YcShellMessage
    {
        public YcShellMessage(string caption, string text, IReadOnlyList<string> buttons)
        {
            Caption = caption;
            Text = text;
            Buttons = buttons;
            Kind = Classify(text, buttons);
        }

        public string Caption { get; }
        public string Text { get; }
        public IReadOnlyList<string> Buttons { get; }
        public YcShellMessageKind Kind { get; }

        /// <summary>Set by the watcher when it closed the box itself.</summary>
        public bool ClosedByUcv { get; set; }

        /// <summary>UCV only closes boxes whose wording it knows and that offer nothing but OK.</summary>
        public bool CanCloseAutomatically =>
            (Kind == YcShellMessageKind.Completion || Kind == YcShellMessageKind.Information) &&
            Buttons.Count == 1 && string.Equals(Buttons[0].Replace("&", "").Trim(), "OK", StringComparison.OrdinalIgnoreCase);

        public string OneLine => string.Join(" ", Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim()));

        public static YcShellMessageKind Classify(string text, IReadOnlyList<string> buttons)
        {
            if (buttons.Any(b => string.Equals(b.Replace("&", "").Trim(), "Yes", StringComparison.OrdinalIgnoreCase)))
                return YcShellMessageKind.Question;
            if (text.IndexOf("Channel Features have been recreated/replaced", StringComparison.OrdinalIgnoreCase) >= 0)
                return YcShellMessageKind.Completion;
            if (text.IndexOf("Surface is a sloped/outdoor Roof", StringComparison.OrdinalIgnoreCase) >= 0)
                return YcShellMessageKind.Information;
            return YcShellMessageKind.Other;
        }
    }

    public enum RegenerateOutcomeKind
    {
        Done,
        DoneWithChoices,
        NotCompleted,
        Skipped,
        Failed,
        NotRun,
    }

    public sealed class RegenerateOutcome
    {
        public string SurfaceIamPath { get; set; } = string.Empty;
        public RegenerateOutcomeKind Kind { get; set; }
        public string Detail { get; set; } = string.Empty;
        public double Seconds { get; set; }
        public List<YcShellMessage> Messages { get; set; } = new();

        public string SurfaceName => System.IO.Path.GetFileNameWithoutExtension(SurfaceIamPath);

        public bool Succeeded => Kind == RegenerateOutcomeKind.Done || Kind == RegenerateOutcomeKind.DoneWithChoices;

        /// <summary>How a surface ended, from what YCShell showed while it ran.</summary>
        public static RegenerateOutcome FromMessages(string surfaceIamPath, IReadOnlyList<YcShellMessage> messages, double seconds)
        {
            var outcome = new RegenerateOutcome { SurfaceIamPath = surfaceIamPath, Seconds = seconds, Messages = messages.ToList() };
            List<YcShellMessage> questions = messages.Where(m => m.Kind == YcShellMessageKind.Question).ToList();
            YcShellMessage? completion = messages.LastOrDefault(m => m.Kind == YcShellMessageKind.Completion);

            if (completion is not null)
            {
                outcome.Kind = questions.Count > 0 ? RegenerateOutcomeKind.DoneWithChoices : RegenerateOutcomeKind.Done;
                outcome.Detail = completion.OneLine;
            }
            else
            {
                outcome.Kind = RegenerateOutcomeKind.NotCompleted;
                YcShellMessage? last = messages.LastOrDefault(m => m.Kind != YcShellMessageKind.Information);
                outcome.Detail = last is not null
                    ? $"YCShell stopped without regenerating: {last.OneLine}"
                    : "YCShell finished without its completion message.";
            }

            if (questions.Count > 0)
            {
                outcome.Detail += $" ({questions.Count} question{(questions.Count == 1 ? "" : "s")} answered)";
            }

            return outcome;
        }
    }

    public static class RegenerateBatchSummary
    {
        private static readonly (RegenerateOutcomeKind Kind, string Heading)[] Groups =
        {
            (RegenerateOutcomeKind.Failed, "Failed"),
            (RegenerateOutcomeKind.NotCompleted, "Not regenerated by YCShell"),
            (RegenerateOutcomeKind.Skipped, "Skipped"),
            (RegenerateOutcomeKind.DoneWithChoices, "Done, with your answers"),
            (RegenerateOutcomeKind.Done, "Done"),
            (RegenerateOutcomeKind.NotRun, "Not run (stopped)"),
        };

        public static string Headline(IReadOnlyList<RegenerateOutcome> outcomes)
        {
            int done = outcomes.Count(o => o.Succeeded);
            int attention = outcomes.Count(o => o.Kind == RegenerateOutcomeKind.Failed || o.Kind == RegenerateOutcomeKind.NotCompleted || o.Kind == RegenerateOutcomeKind.Skipped);
            int notRun = outcomes.Count(o => o.Kind == RegenerateOutcomeKind.NotRun);
            string text = $"{done} of {outcomes.Count} surface{(outcomes.Count == 1 ? "" : "s")} regenerated";
            if (attention > 0) text += $", {attention} need{(attention == 1 ? "s" : "")} a look";
            if (notRun > 0) text += $", {notRun} not run";
            return text + ".";
        }

        public static string Text(IReadOnlyList<RegenerateOutcome> outcomes)
        {
            var lines = new List<string> { Headline(outcomes), string.Empty };
            foreach ((RegenerateOutcomeKind kind, string heading) in Groups)
            {
                List<RegenerateOutcome> group = outcomes.Where(o => o.Kind == kind).ToList();
                if (group.Count == 0) continue;
                lines.Add($"{heading} ({group.Count})");
                foreach (RegenerateOutcome o in group)
                {
                    string time = o.Seconds > 0 ? $" [{o.Seconds:F0} s]" : string.Empty;
                    lines.Add($"   {o.SurfaceName}{time}  {o.Detail}");
                }
                lines.Add(string.Empty);
            }

            return string.Join(Environment.NewLine, lines).TrimEnd();
        }
    }
}
