using System;
using System.Collections.Generic;
using System.Linq;
using Inventor;

namespace Highlighter.Core
{
    /// <summary>
    /// Independent type toggles; one HighlightSet + color per active type.
    /// </summary>
    public sealed class HighlightController
    {
        private readonly Application _app;
        private readonly HashSet<HighlightPartType> _active = new HashSet<HighlightPartType>();
        private readonly Dictionary<HighlightPartType, HighlightRgb> _colors;
        // Strong list so Inventor does not release earlier sets while we build later ones.
        private readonly List<HighlightSet> _heldSets = new List<HighlightSet>();
        private readonly Dictionary<HighlightPartType, HighlightSet> _sets =
            new Dictionary<HighlightPartType, HighlightSet>();
        private readonly List<TransparencyRestore> _transparency = new List<TransparencyRestore>();
        private AssemblyDocument _assembly;

        public HighlightController(Application app)
        {
            _app = app ?? throw new ArgumentNullException(nameof(app));
            _colors = HighlightSettingsStore.LoadOrDefaults();
        }

        public bool IsTypeOn(HighlightPartType type) => _active.Contains(type);
        public int ActiveTypeCount => _active.Count;

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

        /// <summary>Cycle to the next palette color for this type.</summary>
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

        public string SetType(HighlightPartType type, bool on)
        {
            AssemblyDocument asm = _app.ActiveDocument as AssemblyDocument;
            if (asm == null)
            {
                return "Open an assembly first.";
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

            return Rebuild();
        }

        public void ClearAll()
        {
            RestoreTransparency();
            ClearSets();
            _active.Clear();
            try { _app.ActiveView?.Update(); } catch { }
        }

        public void Dispose()
        {
            SaveColors();
            ClearAll();
        }

        private void ClearSets()
        {
            foreach (HighlightSet set in _heldSets)
            {
                try
                {
                    set.Clear();
                }
                catch
                {
                }

                try
                {
                    set.Delete();
                }
                catch
                {
                }
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
                return "All highlight types off.";
            }

            AssemblyDocument asm = _app.ActiveDocument as AssemblyDocument ?? _assembly;
            if (asm == null)
            {
                return "Open an assembly first.";
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

            int transparentOk = 0;
            int itemsAdded = 0;
            int typesWithItems = 0;
            try
            {
                ComponentOccurrences top = asm.ComponentDefinition.Occurrences;

                // Pass 1: collect geometry per type (no HighlightSets yet).
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
                        ComponentOccurrence occ = HighlightEngine.ResolvePath(top, path);
                        if (occ == null || occ.Definition?.Document as PartDocument == null)
                        {
                            continue;
                        }

                        if (ApplyTransparency(occ))
                        {
                            transparentOk++;
                        }

                        HighlightEngine.CollectOutlineItems(occ, path, itemSeen, items);
                    }

                    if (items.Count == 0)
                    {
                        continue;
                    }

                    pending.Add(Tuple.Create(type, items, GetColor(type)));
                }

                // Pass 2+3: create each set, hold it, then fill (never release earlier sets mid-loop).
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

                    int addedHere = 0;
                    foreach (object item in entry.Item2)
                    {
                        if (HighlightEngine.TryAddToSet(set, item))
                        {
                            addedHere++;
                        }
                    }

                    if (addedHere > 0)
                    {
                        itemsAdded += addedHere;
                        typesWithItems++;
                    }
                }
            }
            finally
            {
                try { _app.ScreenUpdating = savedUpdating; } catch { }
            }

            try { _app.ActiveView?.Update(); } catch { }

            if (typesWithItems == 0)
            {
                return "No matching parts found for the selected type(s).";
            }

            return $"{transparentOk} translucent, {itemsAdded} edges, {typesWithItems} color set(s).";
        }

        private bool ApplyTransparency(ComponentOccurrence occ)
        {
            try
            {
                bool was = false;
                try { was = occ.Transparent; } catch { }

                _transparency.Add(new TransparencyRestore
                {
                    Occurrence = occ,
                    WasTransparent = was
                });

                occ.Transparent = true;
                return occ.Transparent;
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

                try
                {
                    entry.Occurrence.Transparent = entry.WasTransparent;
                }
                catch
                {
                }
            }

            _transparency.Clear();
        }
    }
}
