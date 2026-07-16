using System;
using System.Collections.Generic;
using Inventor;

namespace Highlighter.Core
{
    /// <summary>
    /// Multi-pick surface roots; Enter commits the selection. Quiet — no prompts.
    /// </summary>
    internal sealed class SurfacePickSession : IDisposable
    {
        private readonly Application _app;
        private readonly Action<IReadOnlyList<string>> _onCommitted;
        private readonly Action _onCancelled;
        private InteractionEvents _interaction;
        private SelectEvents _select;
        private KeyboardEvents _keyboard;
        private bool _running;

        public SurfacePickSession(Application app, Action<IReadOnlyList<string>> onCommitted, Action onCancelled)
        {
            _app = app ?? throw new ArgumentNullException(nameof(app));
            _onCommitted = onCommitted;
            _onCancelled = onCancelled;
        }

        public bool IsRunning => _running;

        public void Start()
        {
            Stop(cancel: false);
            try
            {
                AssemblyDocument asm = _app.ActiveDocument as AssemblyDocument;
                if (asm == null)
                {
                    return;
                }

                try { asm.SelectSet.Clear(); } catch { }
                try { _app.CommandManager.StopActiveCommand(); } catch { }

                _interaction = _app.CommandManager.CreateInteractionEvents();
                _select = _interaction.SelectEvents;
                _keyboard = _interaction.KeyboardEvents;

                _select.ClearSelectionFilter();
                _select.AddSelectionFilter(SelectionFilterEnum.kAssemblyOccurrenceFilter);
                _select.AddSelectionFilter(SelectionFilterEnum.kAssemblyLeafOccurrenceFilter);
                try { _select.SingleSelectEnabled = false; } catch { }
                try { _select.Enabled = true; } catch { }

                ((SelectEventsSink_Event)_select).OnSelect += OnSelect;
                ((KeyboardEventsSink_Event)_keyboard).OnKeyPress += OnKeyPress;
                ((InteractionEventsSink_Event)_interaction).OnTerminate += OnTerminate;

                try { _interaction.StatusBarText = string.Empty; } catch { }
                _interaction.Start();
                _running = true;
            }
            catch
            {
                Stop(cancel: true);
            }
        }

        public void Stop(bool cancel)
        {
            if (!_running && _interaction == null)
            {
                return;
            }

            _running = false;
            try
            {
                if (_select != null)
                {
                    ((SelectEventsSink_Event)_select).OnSelect -= OnSelect;
                }
            }
            catch { }

            try
            {
                if (_keyboard != null)
                {
                    ((KeyboardEventsSink_Event)_keyboard).OnKeyPress -= OnKeyPress;
                }
            }
            catch { }

            try
            {
                if (_interaction != null)
                {
                    ((InteractionEventsSink_Event)_interaction).OnTerminate -= OnTerminate;
                    try { _interaction.Stop(); } catch { }
                }
            }
            catch { }

            _select = null;
            _keyboard = null;
            _interaction = null;

            if (cancel)
            {
                try { _onCancelled?.Invoke(); } catch { }
            }
        }

        private void OnSelect(
            ObjectsEnumerator justSelectedEntities,
            SelectionDeviceEnum selectionDevice,
            Point modelPosition,
            Point2d viewPosition,
            View view)
        {
            // SelectEvents owns multi-select accumulation; surface roots are resolved on Enter.
        }

        private void OnKeyPress(int keyASCII)
        {
            // Enter / Return
            if (keyASCII != 13)
            {
                return;
            }

            Commit();
        }

        private void OnTerminate()
        {
            if (_running)
            {
                _running = false;
                try { _onCancelled?.Invoke(); } catch { }
            }
        }

        private void Commit()
        {
            var paths = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                ObjectsEnumerator selected = _select?.SelectedEntities;
                if (selected != null)
                {
                    for (int i = 1; i <= selected.Count; i++)
                    {
                        ComponentOccurrence occ = selected[i] as ComponentOccurrence;
                        if (occ == null)
                        {
                            continue;
                        }

                        ComponentOccurrence root = ResolveSurfaceRoot(occ) ?? occ;
                        string path;
                        try { path = root.Name; }
                        catch { continue; }
                        if (!string.IsNullOrWhiteSpace(path) && seen.Add(path))
                        {
                            paths.Add(path);
                        }
                    }
                }
            }
            catch
            {
            }

            Stop(cancel: false);
            try { _onCommitted?.Invoke(paths); } catch { }
        }

        internal static ComponentOccurrence ResolveSurfaceRoot(ComponentOccurrence picked)
        {
            if (picked == null)
            {
                return null;
            }

            ComponentOccurrence found = null;
            ComponentOccurrence cur = picked;
            int guard = 0;
            while (cur != null && guard++ < 64)
            {
                if (IsYcSurf(cur))
                {
                    found = cur;
                }

                try { cur = cur.ParentOccurrence; }
                catch { break; }
            }

            return found ?? picked;
        }

        private static bool IsYcSurf(ComponentOccurrence occ)
        {
            try
            {
                Document doc = null;
                try { doc = occ.ReferencedDocumentDescriptor.ReferencedDocument as Document; } catch { }
                if (doc == null)
                {
                    try { doc = occ.Definition?.Document as Document; } catch { }
                }

                if (doc == null)
                {
                    return false;
                }

                object v = doc.PropertySets["Design Tracking Properties"]["Description"].Value;
                string desc = v == null ? string.Empty : Convert.ToString(v);
                return !string.IsNullOrEmpty(desc)
                    && desc.IndexOf("YC SURF", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch
            {
                return false;
            }
        }

        public void Dispose()
        {
            Stop(cancel: false);
        }
    }
}
