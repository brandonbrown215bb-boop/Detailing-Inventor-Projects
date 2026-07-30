using System;
using System.Collections.Generic;
using System.Linq;
using Inventor;
using Newtonsoft.Json;
using UnitConstructionVerifier.Models;

namespace UnitConstructionVerifier.Extraction
{
    /// <summary>
    /// Reads the DOCUMENT_CONFIG_JSON attribute from an Inventor assembly's
    /// AttributeSets and deserializes it into a <see cref="ConfigData"/> object.
    /// </summary>
    public sealed class ConfigJsonExtractor
    {
        private const string AttrSetSearchName = "DOCUMENT_CONFIG_JSON";

        /// <summary>
        /// Scans all AttributeSets on the given assembly document and returns
        /// the first deserialized CONFIG_JSON found, or null if absent.
        /// </summary>
        public ConfigData? Extract(AssemblyDocument doc)
        {
            try
            {
                string docName = doc.FullFileName;
                DebugLogger.Log($"Extracting from {docName}");
                AttributeSets attrSets = doc.AttributeSets;
                DebugLogger.Log($"Doc has {attrSets.Count} AttributeSets");
                foreach (AttributeSet set in attrSets)
                {
                    foreach (Inventor.Attribute attr in set)
                    {
                        try
                        {
                            if (string.Equals(attr.Name, AttrSetSearchName,
                                    StringComparison.OrdinalIgnoreCase))
                            {
                                DebugLogger.Log($"Found attribute {attr.Name} on set {set.Name}");
                                string json = string.Empty;
                                if (attr.Value is byte[] bytes)
                                {
                                    json = System.Text.Encoding.UTF8.GetString(bytes);
                                    DebugLogger.Log($"Decoded byte[] value: length={json.Length}");
                                }
                                else if (attr.Value is System.Array arr)
                                {
                                    byte[] byteArr = new byte[arr.Length];
                                    System.Array.Copy(arr, byteArr, arr.Length);
                                    json = System.Text.Encoding.UTF8.GetString(byteArr);
                                    DebugLogger.Log($"Decoded System.Array value: length={json.Length}");
                                }
                                else
                                {
                                    json = attr.Value?.ToString() ?? string.Empty;
                                    DebugLogger.Log($"Got string value: length={json.Length}");
                                }

                                if (string.IsNullOrWhiteSpace(json))
                                {
                                    DebugLogger.Log("JSON string is empty or whitespace");
                                    continue;
                                }

                                try
                                {
                                    string cleanName = docName.Replace("\\", "_").Replace(":", "_").Replace("/", "_");
                                    string savePath = System.IO.Path.Combine(DebugLogger.LogDirectory, $"raw_{cleanName}.json");
                                    System.IO.File.WriteAllText(savePath, json);
                                    DebugLogger.Log($"Saved raw JSON to {savePath}");
                                }
                                catch (Exception ex)
                                {
                                    DebugLogger.Log($"Failed to save raw JSON: {ex.Message}");
                                }

                                var root = JsonConvert.DeserializeObject<ConfigJson>(json);
                                if (root?.Configuration != null)
                                {
                                    root.Configuration.SourceIamPath = docName;
                                    DebugLogger.Log($"Successfully deserialized configuration. Surface type: {root.Configuration.SurfaceType}");

                                    var segNames = root.Configuration.SurfaceSegmentList
                                        .Select(s => $"{s.SegmentType}-{s.SegmentTypeSuffix}")
                                        .ToList();
                                    DebugLogger.Log($"  Segments in JSON: {string.Join(", ", segNames)}");

                                    return root.Configuration;
                                }
                                else
                                {
                                    DebugLogger.Log("Deserialization succeeded but Configuration is null");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            DebugLogger.Log(ex, $"ConfigJsonExtractor.Extract inside attribute loop for set {set.Name}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log(ex, "ConfigJsonExtractor.Extract");
                System.Diagnostics.Debug.WriteLine($"[UCV] ConfigJsonExtractor: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Extracts all DOCUMENT_CONFIG_JSON payloads from the root document
        /// and all sub-assemblies referenced in it.
        /// </summary>
        public List<ConfigData> ExtractAll(AssemblyDocument rootDoc)
        {
            DebugLogger.Log($"ExtractAll started on root: {rootDoc.FullFileName}");
            var results = new List<ConfigData>();
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1. Check root
            var rootConfig = Extract(rootDoc);
            if (rootConfig != null)
            {
                results.Add(rootConfig);
                visited.Add(rootDoc.FullFileName);
            }

            // 2. Check all referenced assembly documents
            int refCount = 0;
            try { refCount = rootDoc.AllReferencedDocuments.Count; } catch {}
            DebugLogger.Log($"Root document has {refCount} referenced documents");

            foreach (Document refDoc in rootDoc.AllReferencedDocuments)
            {
                try
                {
                    string path = refDoc.FullFileName;
                    if (visited.Contains(path)) continue;
                    visited.Add(path);

                    bool isAssembly = refDoc.DocumentType == DocumentTypeEnum.kAssemblyDocumentObject;
                    DebugLogger.Log($"Referenced doc: {path} | isAssembly={isAssembly}");

                    if (isAssembly)
                    {
                        var asmSubDoc = (AssemblyDocument)refDoc;
                        var subConfig = Extract(asmSubDoc);
                        if (subConfig != null)
                        {
                            results.Add(subConfig);
                        }
                    }
                }
                catch (Exception ex)
                {
                    DebugLogger.Log(ex, $"ConfigJsonExtractor.ExtractAll referenced doc loop");
                }
            }

            DebugLogger.Log($"ExtractAll finished. Returning {results.Count} configs.");
            return results;
        }
    }
}
