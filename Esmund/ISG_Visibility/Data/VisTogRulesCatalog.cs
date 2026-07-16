using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Web.Script.Serialization;

namespace VisTog.Data
{
    public sealed class VisTogRulesDocument
    {
        public int version { get; set; }
        public List<ToggleRuleSpec> toggles { get; set; }
        public Dictionary<string, PresetRuleSpec> presets { get; set; }
        public List<ButtonLayoutSpec> buttons { get; set; }
        public List<SimpleButtonSpec> simpleButtons { get; set; }
    }

    public sealed class ToggleRuleSpec
    {
        public string id { get; set; }
        public string zone { get; set; }
        public string label { get; set; }
        public string stock { get; set; }
        public List<string> stocks { get; set; }
    }

    public sealed class PresetRuleSpec
    {
        public List<string> stocks { get; set; }
        public bool visible { get; set; }
    }

    public sealed class ButtonLayoutSpec
    {
        public string path { get; set; }
        public string label { get; set; }
        public string ruleId { get; set; }
    }

    public sealed class SimpleButtonSpec
    {
        public string zone { get; set; }
        public string label { get; set; }
        public List<string> ruleIds { get; set; }
    }

    public static class VisTogRulesCatalog
    {
        private const string RulesFileName = "vis-tog-rules.json";
        private static VisTogRulesDocument _cached;

        public static VisTogRulesDocument Load()
        {
            if (_cached != null)
            {
                return _cached;
            }

            string json = TryReadRulesJson();
            if (string.IsNullOrWhiteSpace(json))
            {
                string expectedPath = GetExpectedRulesPath();
                throw new System.IO.FileNotFoundException(
                    "VisTog could not load " + RulesFileName + ". Expected file at:" + System.Environment.NewLine + expectedPath
                    + System.Environment.NewLine + "Copy vis-tog-rules.json next to VisTog.dll, or reinstall from VisTog.zip.",
                    expectedPath);
            }

            var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            _cached = serializer.Deserialize<VisTogRulesDocument>(json) ?? new VisTogRulesDocument
            {
                toggles = new List<ToggleRuleSpec>(),
                presets = new Dictionary<string, PresetRuleSpec>(),
                buttons = new List<ButtonLayoutSpec>()
            };

            if (_cached.toggles == null)
            {
                _cached.toggles = new List<ToggleRuleSpec>();
            }

            if (_cached.presets == null)
            {
                _cached.presets = new Dictionary<string, PresetRuleSpec>();
            }

            if (_cached.buttons == null)
            {
                _cached.buttons = new List<ButtonLayoutSpec>();
            }

            if (_cached.simpleButtons == null)
            {
                _cached.simpleButtons = new List<SimpleButtonSpec>();
            }

            return _cached;
        }

        public static string GetExpectedRulesPath()
        {
            return System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(typeof(VisTogRulesCatalog).Assembly.Location) ?? string.Empty,
                RulesFileName);
        }

        public static ToggleRuleSpec FindToggle(string ruleId)
        {
            return Load().toggles.FirstOrDefault(t => string.Equals(t.id, ruleId, StringComparison.OrdinalIgnoreCase));
        }

        public static IEnumerable<string> GetStocksForToggle(ToggleRuleSpec toggle)
        {
            if (toggle == null)
            {
                yield break;
            }

            if (toggle.stocks != null)
            {
                foreach (string stock in toggle.stocks)
                {
                    if (!string.IsNullOrWhiteSpace(stock))
                    {
                        yield return stock;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(toggle.stock))
            {
                yield return toggle.stock;
            }
        }

        public static IEnumerable<string> GetAllKnownStocks()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ToggleRuleSpec toggle in Load().toggles)
            {
                foreach (string stock in GetStocksForToggle(toggle))
                {
                    if (seen.Add(stock))
                    {
                        yield return stock;
                    }
                }
            }
        }

        public static IEnumerable<string> GetStocksForRuleIds(IEnumerable<string> ruleIds)
        {
            if (ruleIds == null)
            {
                yield break;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string ruleId in ruleIds)
            {
                ToggleRuleSpec toggle = FindToggle(ruleId);
                if (toggle == null)
                {
                    continue;
                }

                foreach (string stock in GetStocksForToggle(toggle))
                {
                    if (seen.Add(stock))
                    {
                        yield return stock;
                    }
                }
            }
        }

        public static bool TryGetPreset(string ruleId, out PresetRuleSpec preset)
        {
            return Load().presets.TryGetValue(ruleId, out preset);
        }

        private static string TryReadRulesJson()
        {
            string filePath = GetExpectedRulesPath();
            if (System.IO.File.Exists(filePath))
            {
                return System.IO.File.ReadAllText(filePath);
            }

            return ReadEmbeddedRulesJson();
        }

        private static string ReadEmbeddedRulesJson()
        {
            Assembly assembly = typeof(VisTogRulesCatalog).Assembly;
            string resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(name => name.EndsWith(RulesFileName, StringComparison.OrdinalIgnoreCase));

            if (resourceName == null)
            {
                return null;
            }

            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            using (var reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }
    }
}
