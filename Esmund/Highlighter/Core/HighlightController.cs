using System;
using System.Collections.Generic;
using System.Linq;
using Inventor;

namespace Highlighter.Core
{
    public enum HighlightScopeMode
    {
        All = 0,
        Selective = 1
    }

    /// <summary>
    /// Independent type toggles; one HighlightSet + color per active type.
    /// Optional selective surface scope: pick surfaces → Enter → hide others;
    /// Normal restores visibility and clears highlights.
    /// </summary>
    public sealed class HighlightController
    {
        private readonly Application _app;
        private readonly HashSet<HighlightPartType> _active = new HashSet<HighlightPartType>();
        private readonly Dictionary<HighlightPartType, HighlightRgb> _colors;
        private readonly List<HighlightSet> _heldSets = new List<HighlightSet>();
        private readonly Dictionary<HighlightPartType, HighlightSet> _sets =
            new Dictionary<HighlightPartType, HighlightSet>();
        private readonly List<TransparencyRestore> _transparency = new List<TransparencyRestore>();
        private readonly VisibilitySession _visibility = new VisibilitySession();
        private readonly HashSet<string> _selectedRoots =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private AssemblyDocument _assembly;
        private SurfacePickSession _pick;
        private HighlightScopeMode _scopeMode = HighlightScopeMode.All;
        private bool _selectiveApplied;
        private bool _prehighlightCaptured;
        private bool _savedEnablePrehighlight = true;

        public HighlightController(Application app)
        {
            _app = app ?? throw new ArgumentNullException(nameof(app));
            _colors = HighlightSettingsStore.LoadOrDefaults();
        }

        public bool IsTypeOn(HighlightPartType type) => _active.Contains(type);
        public int ActiveTypeCount => _active.Count;
        public HighlightScopeMode ScopeMode => _scopeMode;
        public bool SelectiveApplied => _selectiveApplied;
        public bool IsPicking => _pick != null && _pick.IsRunning;

        public HighlightRgb GetColor(HighlightPartType type)
        {
            if (_colors.TryGetValue(type, out HighlightRgb rgb) && rgb != null)
            {
                return rgb;
            }

            return HighlightColorPalette.DefaultFor(type);
        }

        public void SetTypeColor(HighlightPartType type, byte r, byte g, byte b)
        {
            string name = HighlightColorPalette.FindName(r, g, b);
            _colors[type] = new HighlightRgb(name, r, g, b);
            SaveColors();
            if (_active.Contains(type))
            {
                Rebuild();
            }
        }

        public string CycleTypeColor(HighlightPartType type)
        {
            HighlightRgb current = GetColor(type);
            HighlightRgb[] opts = HighlightColorPalette.Options;
            int idx = 0;
            for (int i = 0; i < opts.Length; i++)
            {
                if (opts[i].R == current.R && opts[i].G == current.G && opts[i].B == current.B)
                {
                    idx = i;
                    break;
                }
            }

            HighlightRgb next = opts[(idx + 1) % opts.Length];
            SetTypeColor(type, next.R, next.G, next.B);
            return next.Name;
        }

        public void SaveColors()
        {
            HighlightSettingsStore.Save(_colors);
        }

        public void SetScopeMode(HighlightScopeMode mode)
        {
            if (mode == HighlightScopeMode.All)
            {
                CancelPick();
                if (_selectiveApplied)
                {
                    RestoreVisibilityKeepingHighlights();
                    _selectedRoots.Clear();
                    _selectiveApplied = false;
                }

                _scopeMode = HighlightScopeMode.All;
                Rebuild();
                return;
            }

            // Selective: start (or restart) picking.
            if (_selectiveApplied)
            {
                RestoreVisibilityKeepingHighlights();
                _selectedRoots.Clear();
                _selectiveApplied = false;
            }

            _scopeMode = HighlightScopeMode.Selective;
            BeginSelectivePick();
        }

        /// <summary>Restore all visibility, clear highlights, exit selective mode.</summary>
        public void RestoreNormal()
        {
            CancelPick();
            ClearAll();
            AssemblyDocument asm = _app.ActiveDocument as AssemblyDocument ?? _assembly;
            if (asm != null)
            {
                _visibility.Restore(asm);
            }
            else
            {
                // Drop snapshot if assembly closed.
                _visibility.Restore(null);
            }

            _selectedRoots.Clear();
            _selectiveApplied = false;
            _scopeMode = HighlightScopeMode.All;
            SyncPrehighlight();
            try { _app.ActiveView?.Update(); } catch { }
        }

        public string SetType(HighlightPartType type, bool on)
        {
            AssemblyDocument asm = _app.ActiveDocument as AssemblyDocument;
            if (asm == null)
            {
                return string.Empty;
            }

            _assembly = asm;

            if (on)
            {
                _active.Add(type);
            }
            else
            {
                _active.Remove(type);
            }

            Rebuild();
            return string.Empty;
        }

        public void ClearAll()
        {
            RestoreTransparency();
            ClearSets();
            _active.Clear();
            SyncPrehighlight();
            try { _app.ActiveView?.Update(); } catch { }
        }

        public void Dispose()
        {
            SaveColors();
            CancelPick();
            ClearAll();
            AssemblyDocument asm = _app.ActiveDocument as AssemblyDocument ?? _assembly;
            if (asm != null && _visibility.HasSnapshot)
            {
                _visibility.Restore(asm);
            }

            _selectedRoots.Clear();
            _selectiveApplied = false;
            RestorePrehighlight();
        }

        private void BeginSelectivePick()
        {
            CancelPick();
            AssemblyDocument asm = _app.ActiveDocument as AssemblyDocument;
            if (asm == null)
            {
                _scopeMode = HighlightScopeMode.All;
                return;
            }

            _assembly = asm;
            _pick = new SurfacePickSession(_app, OnPickCommitted, OnPickCancelled);
            _pick.Start();
            SyncPrehighlight();
        }

        private void CancelPick()
        {
            if (_pick == null)
            {
                return;
            }

            try { _pick.Stop(cancel: false); } catch { }
            try { _pick.Dispose(); } catch { }
            _pick = null;
            SyncPrehighlight();
        }

        private void OnPickCancelled()
        {
            _pick = null;
            if (!_selectiveApplied)
            {
                _scopeMode = HighlightScopeMode.All;
            }

            SyncPrehighlight();
        }

        private void OnPickCommitted(IReadOnlyList<string> paths)
        {
            _pick = null;
            AssemblyDocument asm = _app.ActiveDocument as AssemblyDocument ?? _assembly;
            if (asm == null)
            {
                _scopeMode = HighlightScopeMode.All;
                return;
            }

            _assembly = asm;
            _selectedRoots.Clear();
            if (paths != null)
            {
                foreach (string p in paths)
                {
                    if (!string.IsNullOrWhiteSpace(p))
                    {
                        _selectedRoots.Add(p);
                    }
                }
            }

            if (_selectedRoots.Count == 0)
            {
                _scopeMode = HighlightScopeMode.All;
                _selectiveApplied = false;
                return;
            }

            if (!_visibility.HasSnapshot)
            {
                _visibility.Begin(asm);
            }

            _visibility.ApplySelectedOnly(asm, _selectedRoots);
            _selectiveApplied = true;
            _scopeMode = HighlightScopeMode.Selective;
            Rebuild();
            try { _app.ActiveView?.Update(); } catch { }
        }

        private void RestoreVisibilityKeepingHighlights()
        {
            AssemblyDocument asm = _app.ActiveDocument as AssemblyDocument ?? _assembly;
            if (asm == null)
            {
                return;
            }

            // Restore without clearing the snapshot bookkeeping used during selective.
            _visibility.Restore(asm);
        }

        private void ClearSets()
        {
            foreach (HighlightSet set in _heldSets)
            {
                try { set.Clear(); } catch { }
                try { set.Delete(); } catch { }
            }

            _heldSets.Clear();
            _sets.Clear();
        }

        private string Rebuild()
        {
            RestoreTransparency();
            ClearSets();

            if (_active.Count == 0)
            {
                try { _app.ActiveView?.Update(); } catch { }
                return string.Empty;
            }

            AssemblyDocument asm = _app.ActiveDocument as AssemblyDocument ?? _assembly;
            if (asm == null)
            {
                return string.Empty;
            }

            _assembly = asm;
            Document host = (Document)(object)asm;

            bool savedUpdating = true;
            try
            {
                savedUpdating = _app.ScreenUpdating;
                _app.ScreenUpdating = false;
            }
            catch
            {
            }

            try
            {
                ComponentOccurrences top = asm.ComponentDefinition.Occurrences;
                var pending = new List<Tuple<HighlightPartType, List<object>, HighlightRgb>>();
                foreach (HighlightPartType type in _active.OrderBy(t => (int)t))
                {
                    List<string> paths = HighlightTypeCatalog.CollectPathNames(asm, type);
                    if (paths.Count == 0)
                    {
                        continue;
                    }

                    var items = new List<object>();
                    var itemSeen = new HashSet<string>(StringComparer.Ordinal);

                    foreach (string path in paths)
                    {
                        if (_selectiveApplied && _selectedRoots.Count > 0
                            && !VisibilitySession.IsUnderAnyRoot(path, _selectedRoots)
                            && !RootContainsPart(path))
                        {
                            continue;
                        }

                        ComponentOccurrence occ = HighlightEngine.ResolvePath(top, path);
                        if (occ == null || occ.Definition?.Document as PartDocument == null)
                        {
                            continue;
                        }

                        // During selective focus, never revive excluded geometry.
                        if (_selectiveApplied)
                        {
                            try
                            {
                                if (!occ.Visible)
                                {
                                    continue;
                                }
                            }
                            catch
                            {
                                continue;
                            }
                        }

                        ApplyTransparency(occ);
                        HighlightEngine.CollectOutlineItems(occ, path, itemSeen, items);
                    }

                    if (items.Count == 0)
                    {
                        continue;
                    }

                    pending.Add(Tuple.Create(type, items, GetColor(type)));
                }

                foreach (Tuple<HighlightPartType, List<object>, HighlightRgb> entry in pending)
                {
                    HighlightSet set = HighlightEngine.CreateHighlightSet(host);
                    if (set == null)
                    {
                        continue;
                    }

                    HighlightEngine.TrySetColor(_app, set, entry.Item3.R, entry.Item3.G, entry.Item3.B);
                    _heldSets.Add(set);
                    _sets[entry.Item1] = set;

                    foreach (object item in entry.Item2)
                    {
                        HighlightEngine.TryAddToSet(set, item);
                    }
                }
            }
            finally
            {
                try { _app.ScreenUpdating = savedUpdating; } catch { }
            }

            try { _app.ActiveView?.Update(); } catch { }

            if (_selectiveApplied && _selectedRoots.Count > 0)
            {
                SuppressWorkFeaturesForSelection(asm);
            }

            SyncPrehighlight();
            return string.Empty;
        }

        /// <summary>
        /// Mouse-over prehighlight greens out inspection of highlighted edges.
        /// Keep it off while types are active; leave it on during Selective pick.
        /// </summary>
        private void SyncPrehighlight()
        {
            bool wantOff = _active.Count > 0 && !IsPicking;
            if (wantOff)
            {
                CaptureAndDisablePrehighlight();
            }
            else
            {
                RestorePrehighlight();
            }
        }

        private void CaptureAndDisablePrehighlight()
        {
            try
            {
                if (!_prehighlightCaptured)
                {
                    _savedEnablePrehighlight = _app.ColorSchemes.EnablePrehighlight;
                    _prehighlightCaptured = true;
                }

                _app.ColorSchemes.EnablePrehighlight = false;
            }
            catch
            {
            }
        }

        private void RestorePrehighlight()
        {
            if (!_prehighlightCaptured)
            {
                return;
            }

            try { _app.ColorSchemes.EnablePrehighlight = _savedEnablePrehighlight; } catch { }
            _prehighlightCaptured = false;
        }

        private void SuppressWorkFeaturesForSelection(AssemblyDocument asm)
        {
            if (asm == null)
            {
                return;
            }

            WorkFeatureHide.SuppressDocument((Document)(object)asm);
            ComponentOccurrences top = asm.ComponentDefinition.Occurrences;
            foreach (string path in _selectedRoots)
            {
                ComponentOccurrence root = HighlightEngine.ResolvePath(top, path);
                if (root == null)
                {
                    continue;
                }

                WorkFeatureHide.HideUnderOccurrence(root);
                try
                {
                    Document doc = null;
                    try { doc = root.ReferencedDocumentDescriptor.ReferencedDocument as Document; } catch { }
                    WorkFeatureHide.SuppressDocument(doc);
                }
                catch
                {
                }
            }
        }

        /// <summary>
        /// Part path may be a short leaf name while selected root is a longer assembly path —
        /// resolve occurrence and check ancestry against selected roots.
        /// </summary>
        private bool RootContainsPart(string partPath)
        {
            AssemblyDocument asm = _app.ActiveDocument as AssemblyDocument ?? _assembly;
            if (asm == null)
            {
                return false;
            }

            ComponentOccurrence occ = HighlightEngine.ResolvePath(asm.ComponentDefinition.Occurrences, partPath);
            if (occ == null)
            {
                return false;
            }

            ComponentOccurrence cur = occ;
            int guard = 0;
            while (cur != null && guard++ < 64)
            {
                string name;
                try { name = cur.Name; }
                catch { break; }
                if (_selectedRoots.Contains(name))
                {
                    return true;
                }

                try { cur = cur.ParentOccurrence; }
                catch { break; }
            }

            return false;
        }

        private bool ApplyTransparency(ComponentOccurrence occ)
        {
            try
            {
                bool wasTransparent = false;
                try { wasTransparent = occ.Transparent; } catch { }

                double wasOpacity = 1.0;
                try { wasOpacity = occ.OverrideOpacity; } catch { }

                _transparency.Add(new TransparencyRestore
                {
                    Occurrence = occ,
                    WasTransparent = wasTransparent,
                    WasOverrideOpacity = wasOpacity
                });

                // Transparent alone is only a ghost; Opacity 0 hides the solid
                // entirely while Visible stays true so edge HighlightSets remain.
                occ.Transparent = true;
                try { occ.OverrideOpacity = 0.0; } catch { }
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void RestoreTransparency()
        {
            foreach (TransparencyRestore entry in _transparency)
            {
                if (entry?.Occurrence == null)
                {
                    continue;
                }

                try { entry.Occurrence.OverrideOpacity = entry.WasOverrideOpacity; } catch { }
                try { entry.Occurrence.Transparent = entry.WasTransparent; } catch { }
            }

            _transparency.Clear();
        }
    }
}
