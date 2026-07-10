using System;
using System.IO;
using Inventor;
using UnitConstructionVerifier.Models;

namespace UnitConstructionVerifier.Operations
{
    public sealed class IptPropertyWriter
    {
        private readonly Inventor.Application _app;

        public IptPropertyWriter(Inventor.Application app)
        {
            _app = app ?? throw new ArgumentNullException(nameof(app));
        }

        /// <summary>
        /// Updates specific properties on an Inventor IPT part document.
        /// </summary>
        public bool UpdatePartProperties(string iptPath, PartPropertyEdits edits, out string errorMessage)
        {
            errorMessage = string.Empty;
            PartDocument doc = null;
            bool wasOpened = false;

            try
            {
                // Optimization: Search already-loaded documents first.
                foreach (Document openDoc in _app.Documents)
                {
                    if (string.Equals(openDoc.FullFileName, iptPath, StringComparison.OrdinalIgnoreCase))
                    {
                        doc = openDoc as PartDocument;
                        break;
                    }
                }

                // If not found in the open documents collection, open it silently in the background
                if (doc == null)
                {
                    if (!System.IO.File.Exists(iptPath))
                    {
                        errorMessage = "File not found on disk.";
                        return false;
                    }

                    // Check file system read-only attribute
                    var fileInfo = new FileInfo(iptPath);
                    if (fileInfo.IsReadOnly)
                    {
                        errorMessage = "File is read-only on disk (may be checked in to Vault).";
                        return false;
                    }

                    doc = _app.Documents.Open(iptPath, OpenVisible: false) as PartDocument;
                    wasOpened = true;
                }

                if (doc == null)
                {
                    errorMessage = "Failed to open document as a PartDocument.";
                    return false;
                }

                PropertySets sets = doc.PropertySets;
                PropertySet userDefined = sets["Inventor User Defined Properties"];

                // Symmetrical Sync: Keep Gauge and Thickness in sync if only one is updated
                if (edits.Thickness != null && edits.MtlGauge == null)
                {
                    string material = edits.YCMATL;
                    if (string.IsNullOrEmpty(material))
                    {
                        material = ReadUserProperty(userDefined, "YCMATL");
                        if (string.IsNullOrEmpty(material))
                        {
                            material = ReadUserProperty(userDefined, "INPUT_PARAMETER_MaterialType");
                        }
                    }
                    string mappedGauge = MaterialsConfig.MapGauge(edits.Thickness, material);
                    if (!string.IsNullOrEmpty(mappedGauge) && mappedGauge != edits.Thickness)
                    {
                        edits.MtlGauge = mappedGauge;
                    }
                }
                else if (edits.MtlGauge != null && edits.Thickness == null)
                {
                    // Resolve the material context to apply the correct JCI 5-digit thickness material code
                    string material = edits.YCMATL;
                    if (string.IsNullOrEmpty(material))
                    {
                        material = ReadUserProperty(userDefined, "YCMATL");
                        if (string.IsNullOrEmpty(material))
                        {
                            material = ReadUserProperty(userDefined, "INPUT_PARAMETER_MaterialType");
                        }
                    }

                    string mappedThick = MapGaugeToThicknessDecimal(edits.MtlGauge, material);
                    if (!string.IsNullOrEmpty(mappedThick))
                    {
                        edits.Thickness = mappedThick;
                    }
                }

                bool dirty = false;

                // 1. Thickness
                if (edits.Thickness != null)
                {
                    dirty |= WriteUserProperty(userDefined, "Thickness", edits.Thickness);
                    
                    // Update sheet metal parameter if applicable
                    dirty |= WriteModelParameter(doc, "Thickness", edits.Thickness);
                }

                // 2. YCMATL (Material style)
                if (edits.YCMATL != null)
                {
                    dirty |= WriteUserProperty(userDefined, "YCMATL", edits.YCMATL);
                    
                    // Physical Material Sync
                    dirty |= SyncPhysicalMaterial(doc, edits.YCMATL);
                }

                // 3. MtlGauge (Gauge)
                if (edits.MtlGauge != null)
                {
                    dirty |= WriteUserProperty(userDefined, "INPUT_PARAMETER_Mtl_Gauge", edits.MtlGauge);
                }

                // Update and save
                if (dirty)
                {
                    doc.Update();
                    if (wasOpened)
                    {
                        doc.Save();
                        doc.Close(SkipSave: false);
                    }
                }
                else if (wasOpened)
                {
                    doc.Close(SkipSave: true);
                }

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                if (wasOpened && doc != null)
                {
                    try { doc.Close(SkipSave: true); } catch {}
                }
                return false;
            }
        }

        private bool WriteUserProperty(PropertySet set, string name, string value)
        {
            try
            {
                Property prop = set[name];
                if (string.Equals(prop.Value?.ToString(), value, StringComparison.OrdinalIgnoreCase))
                    return false;

                prop.Value = value;
                return true;
            }
            catch
            {
                // Property does not exist; create it
                set.Add(value, name);
                return true;
            }
        }

        private bool WriteModelParameter(PartDocument doc, string name, string valStr)
        {
            try
            {
                if (doc.ComponentDefinition is SheetMetalComponentDefinition smDef)
                {
                    // Clean numeric conversion (e.g. 2.0" or 0.056 -> double)
                    string cleanVal = valStr.Replace("\"", "").Trim();
                    if (double.TryParse(cleanVal, out double inches))
                    {
                        // Convert to centimeters (Inventor's internal parameter unit)
                        double cmValue = inches * 2.54;
                        Parameters pms = smDef.Parameters;
                        
                        foreach (Parameter p in pms)
                        {
                            if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
                            {
                                // Disable sheet metal style override first
                                if (smDef.UseSheetMetalStyleThickness)
                                {
                                    smDef.UseSheetMetalStyleThickness = false;
                                }

                                double currentVal = 0;
                                if (p.Value is double dVal)
                                {
                                    currentVal = dVal;
                                }
                                else
                                {
                                    currentVal = Convert.ToDouble(p.Value);
                                }

                                if (Math.Abs(currentVal - cmValue) > 0.0001)
                                {
                                    double oldCmValue = currentVal;
                                    double newCmValue = cmValue;

                                    p.Value = cmValue;

                                    // Adjust cut features extents
                                    AdjustCutFeatureExtents(doc, oldCmValue, newCmValue);

                                    return true;
                                }
                                break;
                            }
                        }
                    }
                }
            }
            catch {}
            return false;
        }

        private void AdjustCutFeatureExtents(PartDocument doc, double oldCmValue, double newCmValue)
        {
            try
            {
                PartComponentDefinition compDef = doc.ComponentDefinition;
                foreach (PartFeature feature in compDef.Features)
                {
                    bool isCut = false;

                    if (feature is ExtrudeFeature extrude)
                    {
                        isCut = (extrude.Operation == PartFeatureOperationEnum.kCutOperation);
                    }
                    else if (feature is HoleFeature hole)
                    {
                        isCut = (hole.ExtentType != PartFeatureExtentEnum.kThroughAllExtent);
                    }
                    else if (feature is CutFeature)
                    {
                        isCut = true;
                    }

                    if (isCut)
                    {
                        foreach (Parameter p in feature.Parameters)
                        {
                            double currentVal = 0;
                            if (p.Value is double dVal)
                            {
                                currentVal = dVal;
                            }
                            else
                            {
                                currentVal = Convert.ToDouble(p.Value);
                            }

                            // If the cut depth matches the old thickness, adjust it
                            if (Math.Abs(currentVal - oldCmValue) < 0.001)
                            {
                                try
                                {
                                    // Try making it fully parametric using the "Thickness" parameter
                                    p.Expression = "Thickness";
                                }
                                catch
                                {
                                    p.Value = newCmValue;
                                }
                            }
                        }
                    }
                }
            }
            catch {}
        }

        private bool SyncPhysicalMaterial(PartDocument doc, string ycmAtlVal)
        {
            try
            {
                string targetMaterialName = MapToPhysicalMaterialName(ycmAtlVal);
                if (string.IsNullOrEmpty(targetMaterialName)) return false;

                // Try local material list
                foreach (Material mtl in doc.Materials)
                {
                    if (string.Equals(mtl.Name, targetMaterialName, StringComparison.OrdinalIgnoreCase))
                    {
                        if (doc.ActiveMaterial == null || !string.Equals(doc.ActiveMaterial.Name, targetMaterialName, StringComparison.OrdinalIgnoreCase))
                        {
                            doc.ActiveMaterial = (Asset)mtl;
                            return true;
                        }
                        return false;
                    }
                }

                // Try copying from active material library
                Asset libMtl = _app.ActiveMaterialLibrary.MaterialAssets[targetMaterialName];
                if (libMtl != null)
                {
                    Asset localAsset = libMtl.CopyTo(doc);
                    doc.ActiveMaterial = localAsset;
                    return true;
                }
            }
            catch {}
            return false;
        }

        private string MapToPhysicalMaterialName(string ycmAtl)
        {
            if (string.IsNullOrWhiteSpace(ycmAtl)) return string.Empty;
            string norm = ycmAtl.ToUpperInvariant();
            if (norm.Contains("GALV")) return "Steel, Galvanized";
            if (norm.Contains("COLD ROLL")) return "Steel, Mild";
            if (norm.Contains("HOT ROLL")) return "Steel, Mild";
            if (norm.Contains("ALM") || norm.Contains("ALUM")) return "Aluminum 6061";
            return string.Empty;
        }

        private string ReadUserProperty(PropertySet set, string name)
        {
            try
            {
                return set[name]?.Value?.ToString() ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private string MapGaugeToThicknessDecimal(string gauge, string material)
        {
            if (string.IsNullOrWhiteSpace(gauge)) return string.Empty;
            string normGauge = gauge.Trim();
            string normMaterial = (material ?? string.Empty).Trim().ToUpperInvariant();

            // 1. Try direct lookup in JCI ThicknessMap database using gauge + material name (e.g. "16STL GALV PPC")
            string lookupKey = (normGauge + normMaterial).Replace(" ", "");
            foreach (var kvp in MaterialsConfig.ThicknessMap)
            {
                string cleanMapKey = kvp.Key.Replace(" ", "").ToUpperInvariant();
                if (string.Equals(cleanMapKey, lookupKey, StringComparison.OrdinalIgnoreCase))
                {
                    return kvp.Value;
                }
            }

            // 2. Try lookup with normalized material mapping (e.g. "STEEL" -> "STL GALV" -> "16STL GALV")
            string normalizedMtl = MaterialsConfig.MapMaterial(normMaterial).Replace(" ", "").ToUpperInvariant();
            string normalizedLookupKey = (normGauge + normalizedMtl);
            foreach (var kvp in MaterialsConfig.ThicknessMap)
            {
                string cleanMapKey = kvp.Key.Replace(" ", "").ToUpperInvariant();
                if (string.Equals(cleanMapKey, normalizedLookupKey, StringComparison.OrdinalIgnoreCase))
                {
                    return kvp.Value;
                }
            }

            // 3. Fallback to standard nominal gauge lookup if database entry doesn't exist
            string baseThick = string.Empty;
            foreach (var kvp in MaterialsConfig.GaugeMappings)
            {
                if (string.Equals(kvp.Value, normGauge, StringComparison.OrdinalIgnoreCase))
                {
                    baseThick = kvp.Key;
                    break;
                }
            }

            if (string.IsNullOrEmpty(baseThick))
            {
                switch (normGauge)
                {
                    case "24": baseThick = "0.022"; break;
                    case "22": baseThick = "0.028"; break;
                    case "20": baseThick = "0.036"; break;
                    case "18": baseThick = "0.0478"; break;
                    case "16": baseThick = "0.056"; break;
                    case "14": baseThick = "0.0747"; break;
                    case "12": baseThick = "0.1046"; break;
                    case "10": baseThick = "0.127"; break;
                }
            }

            if (string.IsNullOrEmpty(baseThick)) return string.Empty;

            // Suffix generation fallback if mapping entry isn't defined
            bool isGalv = normMaterial.Contains("GALV") || normalizedMtl.Contains("GALV");
            if (isGalv)
            {
                if (double.TryParse(baseThick, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double val))
                {
                    string formatted = val.ToString("F4", System.Globalization.CultureInfo.InvariantCulture);
                    return formatted + "1";
                }
            }

            return baseThick;
        }
    }

    public sealed class PartPropertyEdits
    {
        public string? Thickness { get; set; }
        public string? YCMATL    { get; set; }
        public string? MtlGauge   { get; set; }
    }
}
