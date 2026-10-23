using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnitConstructionVerifier.Models;

namespace UnitConstructionVerifier.Operations
{
    /// <summary>Runs Regenerate Channel Features on one surface and returns when it is done.</summary>
    public interface ISurfaceRegenerator
    {
        void Regenerate(string surfaceIamPath);
    }

    /// <summary>Collects the message boxes YCShell shows while one surface runs.</summary>
    public interface IYcShellMessageSource
    {
        void Begin(string surfaceIamPath);
        IReadOnlyList<YcShellMessage> End();
    }

    /// <summary>
    /// Regenerate changed: one surface after another, never in parallel. Stop takes effect between surfaces. YCShell
    /// saves each surface as it goes, so a stopped batch keeps the surfaces already done.
    /// </summary>
    public sealed class RegenerateBatchRunner
    {
        private readonly ISurfaceRegenerator _regenerator;
        private readonly IYcShellMessageSource _messages;
        private readonly Func<string, string?> _skipReason;
        private readonly Action<string> _onSucceeded;

        /// <param name="skipReason">A reason not to run a surface (for example suppressed parts), or null.</param>
        /// <param name="onSucceeded">Runs after a surface completed, e.g. to record its channel fingerprint.</param>
        public RegenerateBatchRunner(ISurfaceRegenerator regenerator, IYcShellMessageSource messages,
            Func<string, string?> skipReason, Action<string> onSucceeded)
        {
            _regenerator = regenerator;
            _messages = messages;
            _skipReason = skipReason;
            _onSucceeded = onSucceeded;
        }

        public IReadOnlyList<RegenerateOutcome> Run(IReadOnlyList<string> surfaces, Func<bool> stopRequested,
            Action<int, int, string>? progress = null)
        {
            var outcomes = new List<RegenerateOutcome>();
            for (int i = 0; i < surfaces.Count; i++)
            {
                string surface = surfaces[i];
                if (stopRequested())
                {
                    outcomes.AddRange(surfaces.Skip(i).Select(s => new RegenerateOutcome
                    {
                        SurfaceIamPath = s, Kind = RegenerateOutcomeKind.NotRun, Detail = "stopped before this surface",
                    }));
                    break;
                }

                progress?.Invoke(i + 1, surfaces.Count, surface);
                outcomes.Add(RunOne(surface));
            }

            return outcomes;
        }

        private RegenerateOutcome RunOne(string surface)
        {
            string? skip = _skipReason(surface);
            if (skip is not null)
            {
                return new RegenerateOutcome { SurfaceIamPath = surface, Kind = RegenerateOutcomeKind.Skipped, Detail = skip };
            }

            var watch = Stopwatch.StartNew();
            _messages.Begin(surface);
            Exception? failure = null;
            try
            {
                _regenerator.Regenerate(surface);
            }
            catch (Exception ex)
            {
                failure = ex;
            }

            IReadOnlyList<YcShellMessage> seen = _messages.End();
            double seconds = watch.Elapsed.TotalSeconds;

            if (failure is not null)
            {
                DebugLogger.Log(failure, $"Regenerate changed: {surface}");
                return new RegenerateOutcome
                {
                    SurfaceIamPath = surface, Kind = RegenerateOutcomeKind.Failed, Seconds = seconds, Messages = seen.ToList(),
                    Detail = failure.Message,
                };
            }

            RegenerateOutcome outcome = RegenerateOutcome.FromMessages(surface, seen, seconds);
            if (outcome.Succeeded)
            {
                try
                {
                    _onSucceeded(surface);
                }
                catch (Exception ex)
                {
                    DebugLogger.Log(ex, $"Regenerate changed: after {surface}");
                    outcome.Detail += $" (channel record not saved: {ex.Message})";
                }
            }

            return outcome;
        }
    }

    /// <summary><see cref="ISurfaceRegenerator"/> over YCShell, matching surfaces by YCShell's own path rule.</summary>
    public sealed class YcShellSurfaceRegenerator : ISurfaceRegenerator
    {
        private readonly YcShellBridge _bridge;

        public YcShellSurfaceRegenerator(YcShellBridge bridge) => _bridge = bridge;

        public void Regenerate(string surfaceIamPath)
        {
            YcSurface surface = _bridge.GetOpenSurfaces().FirstOrDefault(s => s.Matches(surfaceIamPath))
                ?? throw new InvalidOperationException("YCShell no longer has this surface loaded.");
            _bridge.Regenerate(surface);
        }
    }
}
