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
        /// <summary>What the last successful <see cref="UpdatePartProperties"/> saved, or null when it saved nothing.</summary>
        public PartWriteRecord? LastWrite { get; private set; }

        public bool UpdatePartProperties(string iptPath, PartPropertyEdits edits, out string errorMessage)
        {
            errorMessage = string.Empty;
            LastWrite = null;
            PartDocument doc = null;
            bool wasOpened = false;

            try
            {
                // Already loaded (as part of the open unit, usually)? A direct lookup: walking every open document cost
                // hundreds of COM calls per part on a full unit.
                try { doc = _app.Documents.get_ItemByName(iptPath) as PartDocument; }
                catch { doc = null; }

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

                if (!PartPropertyEditsMapper.HasAnyValue(edits))
                {
                    errorMessage = "No recognized property changes for this row.";
                    if (wasOpened)
                    {
                        try { doc.Close(SkipSave: true); } catch { }
                    }

                    return false;
                }

                // State before this write, so a later scan can tell a gauge / material edit from a reshape. Unsaved
                // edits already in the document count as a change: this save would carry them.
                string previousStatus = PartChangeStatus.Read((Inventor.Document)doc) ?? string.Empty;
                if (doc.Dirty) previousStatus = "UnsavedEdits";
                string versionBefore = Extraction.SurfaceChangeReader.PartVersion(iptPath);

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
                else if (edits.YCMATL != null && edits.Thickness == null)
                {
                    // Material changed without explicit thickness edit: Re-calculate precise 5-digit decimal thickness for the new material
                    string currentGauge = edits.MtlGauge ?? ReadUserProperty(userDefined, "INPUT_PARAMETER_Mtl_Gauge");
                    if (!string.IsNullOrEmpty(currentGauge))
                    {
                        string mappedThick = MapGaugeToThicknessDecimal(currentGauge, edits.YCMATL);
                        if (!string.IsNullOrEmpty(mappedThick))
                        {
                            edits.Thickness = mappedThick;
                        }
                    }
                }

                bool dirty = false;

                // 1. Thickness
                if (edits.Thickness != null)
                {
                    dirty |= WriteUserProperty(userDefined, "Thickness", edits.Thickness);
                    dirty |= WriteAttributeToSet(doc, "MOM_DATA", "ORIGINAL_SHEET_METAL_THICKNESS", edits.Thickness);
                    
                    // Update sheet metal parameter if applicable
                    dirty |= WriteModelParameter(doc, "Thickness", edits.Thickness);
                }

                // 2. YCMATL (Material style)
                string? activeMaterial = edits.YCMATL;
                if (activeMaterial != null)
                {
                    dirty |= WriteUserProperty(userDefined, "YCMATL", activeMaterial);
                    dirty |= WriteUserProperty(userDefined, "INPUT_PARAMETER_MaterialStyle", activeMaterial);
                    dirty |= WriteUserProperty(userDefined, "INPUT_PARAMETER_MaterialType", activeMaterial);
                    dirty |= WriteAttributeToSet(doc, "MOM_DATA", "MATERIAL_TYPE_CODE", activeMaterial);
                    dirty |= WriteAttributeToSet(doc, "MOM_PARAMETER_SET", "MaterialStyle", activeMaterial, createSetIfMissing: false);
                    dirty |= WriteAttributeToSet(doc, "MOM_PARAMETER_SET", "MaterialType", activeMaterial, createSetIfMissing: false);
                    
                    // Physical Material Sync
                    dirty |= SyncPhysicalMaterial(doc, activeMaterial);
                }
                else
                {
                    // Backfill MOM_DATA.MATERIAL_TYPE_CODE if currently blank/missing on an edited part
                    string existingMaterial = ReadUserProperty(userDefined, "YCMATL");
                    if (string.IsNullOrEmpty(existingMaterial))
                    {
                        existingMaterial = ReadUserProperty(userDefined, "INPUT_PARAMETER_MaterialStyle");
                    }

                    if (!string.IsNullOrEmpty(existingMaterial))
                    {
                        dirty |= WriteUserProperty(userDefined, "INPUT_PARAMETER_MaterialStyle", existingMaterial);
                        dirty |= WriteAttributeToSet(doc, "MOM_DATA", "MATERIAL_TYPE_CODE", existingMaterial, createSetIfMissing: true, onlyIfBlank: true);
                    }
                }

                // 3. MtlGauge (Gauge)
                if (edits.MtlGauge != null)
                {
                    dirty |= WriteUserProperty(userDefined, "INPUT_PARAMETER_Mtl_Gauge", edits.MtlGauge);
                    dirty |= WriteAttributeToSet(doc, "MOM_PARAMETER_SET", "Mtl_Gauge", edits.MtlGauge, createSetIfMissing: false);
                }

                // 4. Hard-Edit Flag, SQ Part Attributes & Visual Appearance Sync (Always stamped for edited rows)
                dirty |= WriteUserProperty(userDefined, "IS_HARD_EDITED", "TRUE");
                dirty |= WriteAttributeToSet(doc, "MOM_DATA", "PART_SOURCE", "SQPart");
                dirty |= WriteAttributeToSet(doc, "MOM_DATA", "DOCUMENT_STATUS", "Changed_SQ");
                dirty |= SyncHardEditAppearance(doc);

                // 5. Description Synchronization (Matching YCShell MarkAsChanged standard)
                try
                {
                    PropertySet tracking = sets["Design Tracking Properties"];
                    Property descProp = tracking["Description"];
                    string currentDesc = descProp?.Value?.ToString() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(currentDesc) &&
                        !currentDesc.StartsWith("SQ PART", StringComparison.OrdinalIgnoreCase))
                    {
                        descProp.Value = "SQ PART - " + currentDesc;
                        dirty = true;
                    }
                }
                catch { }

                // Update and save
                if (dirty)
                {
                    doc.Update();
                    doc.Save();
                    LastWrite = new PartWriteRecord
                    {
                        PartFile = iptPath,
                        PreviousStatus = previousStatus,
                        VersionBefore = versionBefore,
                        VersionAfter = Extraction.SurfaceChangeReader.PartVersion(iptPath),
                        WrittenAtUtc = DateTime.UtcNow,
                    };
                    if (wasOpened)
                    {
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

        private bool WriteAttributeToSet(PartDocument doc, string setName, string attributeName, string value, bool createSetIfMissing = true, bool onlyIfBlank = false)
        {
            try
            {
                AttributeSets sets = doc.AttributeSets;
                AttributeSet? targetSet = null;

                foreach (AttributeSet set in sets)
                {
                    if (string.Equals(set.Name, setName, StringComparison.OrdinalIgnoreCase))
                    {
                        targetSet = set;
                        break;
                    }
                }

                if (targetSet == null)
                {
                    if (!createSetIfMissing) return false;
                    targetSet = sets.Add(setName);
                }

                Inventor.Attribute? targetAttr = null;
                foreach (Inventor.Attribute attr in targetSet)
                {
                    if (string.Equals(attr.Name, attributeName, StringComparison.OrdinalIgnoreCase))
                    {
                        targetAttr = attr;
                        break;
                    }
                }

                if (onlyIfBlank && targetAttr != null && !string.IsNullOrWhiteSpace(targetAttr.Value?.ToString()))
                {
                    return false;
                }

                bool isDoubleAttr = string.Equals(attributeName, "ORIGINAL_SHEET_METAL_THICKNESS", StringComparison.OrdinalIgnoreCase) ||
                                    (targetAttr != null && targetAttr.ValueType == ValueTypeEnum.kDoubleType);

                if (isDoubleAttr)
                {
                    string cleanVal = (value ?? string.Empty).Replace("\"", "").Trim();
                    if (double.TryParse(cleanVal, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double dVal))
                    {
                        if (targetAttr != null)
                        {
                            if (targetAttr.ValueType != ValueTypeEnum.kDoubleType)
                            {
                                targetAttr.Delete();
                                targetSet.Add(attributeName, ValueTypeEnum.kDoubleType, dVal);
                                return true;
                            }

                            if (targetAttr.Value is double currentDVal && Math.Abs(currentDVal - dVal) < 0.000001)
                                return false;

                            targetAttr.Value = dVal;
                            return true;
                        }
                        else
                        {
                            targetSet.Add(attributeName, ValueTypeEnum.kDoubleType, dVal);
                            return true;
                        }
                    }
                }

                if (targetAttr != null)
                {
                    if (targetAttr.ValueType == ValueTypeEnum.kIntegerType)
                    {
                        if (int.TryParse(value, out int iVal))
                        {
                            if (targetAttr.Value is int currentIVal && currentIVal == iVal)
                                return false;

                            targetAttr.Value = iVal;
                            return true;
                        }
                    }

                    if (string.Equals(targetAttr.Value?.ToString(), value, StringComparison.OrdinalIgnoreCase))
                        return false;

                    targetAttr.Value = value;
                    return true;
                }
                else
                {
                    targetSet.Add(attributeName, ValueTypeEnum.kStringType, value);
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        private bool SyncHardEditAppearance(PartDocument doc)
        {
            try
            {
                string appearanceName = MaterialsConfig.EditedAppearance;
                if (string.IsNullOrWhiteSpace(appearanceName)) return false;

                Asset? targetAppearance = null;

                // 1. Check local document appearances
                foreach (Asset appAsset in doc.AppearanceAssets)
                {
                    if (string.Equals(appAsset.Name, appearanceName, StringComparison.OrdinalIgnoreCase))
                    {
                        targetAppearance = appAsset;
                        break;
                    }
                }

                // 2. Check ActiveAppearanceLibrary in Inventor Application if missing locally
                if (targetAppearance == null && _app != null)
                {
                    try
                    {
                        Asset libAppearance = _app.ActiveAppearanceLibrary.AppearanceAssets[appearanceName];
                        if (libAppearance != null)
                        {
                            targetAppearance = libAppearance.CopyTo(doc);
                        }
                    }
                    catch { }
                }

                // 3. Fall back to ActiveMaterialLibrary if needed
                if (targetAppearance == null && _app != null)
                {
                    try
                    {
                        Asset libAppearance = _app.ActiveMaterialLibrary.AppearanceAssets[appearanceName];
                        if (libAppearance != null)
                        {
                            targetAppearance = libAppearance.CopyTo(doc);
                        }
                    }
                    catch { }
                }

                if (targetAppearance != null)
                {
                    if (doc.ActiveAppearance == null || !string.Equals(doc.ActiveAppearance.Name, targetAppearance.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        doc.ActiveAppearance = targetAppearance;

                        // Also update Design Tracking "Appearance" property if present
                        try
                        {
                            PropertySet tracking = doc.PropertySets["Design Tracking Properties"];
                            tracking["Appearance"].Value = appearanceName;
                        }
                        catch { }

                        return true;
                    }
                }
            }
            catch { }

            return false;
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
                    if (double.TryParse(cleanVal, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double inches))
                    {
                        // Convert to centimeters (Inventor's internal parameter unit)
                        double cmValue = inches * 2.54;

                        if (smDef.UseSheetMetalStyleThickness)
                        {
                            smDef.UseSheetMetalStyleThickness = false;
                        }

                        Parameter? thicknessParam = TryGetSheetMetalThicknessParameter(smDef, name);
                        if (thicknessParam != null)
                        {
                            bool changed = TrySetParameterValueCm(thicknessParam, cmValue, out double oldCmValue);

                            // Also when the thickness is unchanged: repairs cuts an earlier write left halfway.
                            if (oldCmValue > 0)
                                changed |= AdjustCutFeatureExtents(doc, oldCmValue, cmValue);
                            return changed;
                        }
                    }
                }
            }
            catch {}
            return false;
        }

        private static Parameter? TryGetSheetMetalThicknessParameter(SheetMetalComponentDefinition smDef, string name)
        {
            try
            {
                if (string.Equals(name, "Thickness", StringComparison.OrdinalIgnoreCase))
                {
                    return smDef.Thickness;
                }
            }
            catch
            {
            }

            try
            {
                Parameters parameters = smDef.Parameters;
                foreach (Parameter parameter in parameters)
                {
                    if (string.Equals(parameter.Name, name, StringComparison.OrdinalIgnoreCase))
                    {
                        return parameter;
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        private static bool TrySetParameterValueCm(Parameter parameter, double cmValue, out double oldCmValue)
        {
            oldCmValue = 0;

            try
            {
                if (parameter.Value is double dVal)
                {
                    oldCmValue = dVal;
                }
                else
                {
                    oldCmValue = Convert.ToDouble(parameter.Value);
                }

                if (Math.Abs(oldCmValue - cmValue) < 1e-7)
                {
                    return false;
                }

                parameter.Value = cmValue;
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Keeps cuts that went through the old sheet going through the new one. See <see cref="CutDepthRule"/>.</summary>
        private static bool AdjustCutFeatureExtents(PartDocument doc, double oldCmValue, double newCmValue)
        {
            bool changed = false;
            foreach (PartFeature feature in doc.ComponentDefinition.Features)
            {
                // One feature failing must not stop the rest.
                try
                {
                    if (!TryGetCutDistance(feature, out Parameter distance, out bool symmetric)) continue;

                    string? expression = CutDepthRule.NewExpression(
                        symmetric, distance.Expression, Convert.ToDouble(distance.Value), oldCmValue, newCmValue);
                    if (expression == null) continue;

                    distance.Expression = expression;
                    changed = true;
                }
                catch (Exception ex)
                {
                    DebugLogger.Log(ex, $"AdjustCutFeatureExtents: {doc.FullFileName} {feature.Name}");
                }
            }
            return changed;
        }

        /// <summary>The distance of a cut, extruded cut or hole with a distance extent; false for any other feature.</summary>
        private static bool TryGetCutDistance(PartFeature feature, out Parameter distance, out bool symmetric)
        {
            distance = null!;
            symmetric = false;

            object? extent = feature switch
            {
                CutFeature cut => cut.Definition.Extent,
                ExtrudeFeature extrude when extrude.Operation == PartFeatureOperationEnum.kCutOperation => extrude.Definition.Extent,
                HoleFeature hole => hole.Extent,
                _ => null,
            };
            if (!(extent is DistanceExtent distanceExtent)) return false;

            distance = distanceExtent.Distance;
            symmetric = distanceExtent.Direction == PartFeatureExtentDirectionEnum.kSymmetricExtentDirection;
            return true;
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
