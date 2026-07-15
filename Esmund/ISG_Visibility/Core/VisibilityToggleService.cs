using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using InvApp = Inventor.Application;
using Inventor;
using VisTog.Data;
using VisTog.UI;

namespace VisTog.Core
{
    public sealed class VisibilityToggleService
    {
        public ToggleStockResult ToggleStocks(AssemblyDocument assemblyDocument, IEnumerable<string> stockNumbers)
        {
            if (assemblyDocument == null)
            {
                throw new ArgumentNullException(nameof(assemblyDocument));
            }

            var targetStocks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (stockNumbers != null)
            {
                foreach (string stock in stockNumbers)
                {
                    if (!string.IsNullOrWhiteSpace(stock))
                    {
                        targetStocks.Add(stock);
                    }
                }
            }

            if (targetStocks.Count == 0)
            {
                return ToggleStockResult.Failed("Stock number is required.");
            }

            var occurrences = new List<ComponentOccurrence>();
            AssemblyOccurrenceWalker.CollectOccurrences(
                assemblyDocument.ComponentDefinition.Occurrences,
                occurrences);

            bool foundOne = false;
            bool currentState = true;

            foreach (ComponentOccurrence occurrence in occurrences)
            {
                string stock = StockPropertyHelper.GetStockNumber(occurrence);
                if (!targetStocks.Contains(stock))
                {
                    continue;
                }

                currentState = occurrence.Visible;
                foundOne = true;
                break;
            }

            if (!foundOne)
            {
                return ToggleStockResult.Failed("No parts found for the selected item.");
            }

            bool newState = !currentState;
            int count = 0;

            foreach (ComponentOccurrence occurrence in occurrences)
            {
                string stock = StockPropertyHelper.GetStockNumber(occurrence);
                if (!targetStocks.Contains(stock))
                {
                    continue;
                }

                occurrence.Visible = newState;
                count++;
            }

            return ToggleStockResult.Ok(count, newState);
        }

        public ToggleStockResult ToggleStock(AssemblyDocument assemblyDocument, string stockNumber)
        {
            return ToggleStocks(assemblyDocument, new[] { stockNumber });
        }

        public int SetStocksVisibility(AssemblyDocument assemblyDocument, IEnumerable<string> stockNumbers, bool visible)
        {
            if (assemblyDocument == null)
            {
                throw new ArgumentNullException(nameof(assemblyDocument));
            }

            var targetStocks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (stockNumbers != null)
            {
                foreach (string stock in stockNumbers)
                {
                    if (!string.IsNullOrWhiteSpace(stock))
                    {
                        targetStocks.Add(stock);
                    }
                }
            }

            if (targetStocks.Count == 0)
            {
                return 0;
            }

            var occurrences = new List<ComponentOccurrence>();
            AssemblyOccurrenceWalker.CollectOccurrences(
                assemblyDocument.ComponentDefinition.Occurrences,
                occurrences);

            int count = 0;
            foreach (ComponentOccurrence occurrence in occurrences)
            {
                string stock = StockPropertyHelper.GetStockNumber(occurrence);
                if (!targetStocks.Contains(stock))
                {
                    continue;
                }

                occurrence.Visible = visible;
                count++;
            }

            return count;
        }

        public int SetAllKnownStocksVisible(AssemblyDocument assemblyDocument, bool visible)
        {
            return SetStocksVisibility(assemblyDocument, VisTogRulesCatalog.GetAllKnownStocks(), visible);
        }

        /// <summary>
        /// Shows every known stock and turns on all configured surface roots (Roof/Wall/Base)
        /// in one surface pass (instead of three separate Roof/Wall/Base walks).
        /// </summary>
        public void RunAllOn(AssemblyDocument assemblyDocument, IEnumerable<string> surfaceDescriptions, InvApp app = null)
        {
            if (assemblyDocument == null)
            {
                throw new ArgumentNullException(nameof(assemblyDocument));
            }

            // Intentionally do not touch Application.ScreenUpdating — suspending it makes
            // Inventor's window/taskbar icon vanish until the long walk finishes.
            SetAllKnownStocksVisible(assemblyDocument, true);

            var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (surfaceDescriptions != null)
            {
                foreach (string description in surfaceDescriptions)
                {
                    if (!string.IsNullOrWhiteSpace(description))
                    {
                        targets.Add(description.Trim());
                    }
                }
            }

            if (targets.Count == 0)
            {
                return;
            }

            var surfaceRoots = new List<ComponentOccurrence>();
            CollectSurfacesByDescriptions(
                assemblyDocument.ComponentDefinition.Occurrences,
                targets,
                surfaceRoots);
            ShowSurfaceRoots(surfaceRoots, assemblyDocument);
        }

        /// <summary>
        /// Sets visibility for surface roots whose Project Description matches
        /// <paramref name="surfaceDescription"/> (e.g. "YC SURF WALL").
        /// </summary>
        public int SetVisibilityBySurfaceDescription(
            AssemblyDocument assemblyDocument,
            string surfaceDescription,
            bool visible,
            InvApp app = null)
        {
            if (assemblyDocument == null)
            {
                throw new ArgumentNullException(nameof(assemblyDocument));
            }

            if (string.IsNullOrWhiteSpace(surfaceDescription))
            {
                return 0;
            }

            string target = surfaceDescription.Trim();
            var surfaceRoots = new List<ComponentOccurrence>();
            CollectSurfacesByDescription(
                assemblyDocument.ComponentDefinition.Occurrences,
                target,
                surfaceRoots);

            if (surfaceRoots.Count == 0)
            {
                return 0;
            }

            if (!visible)
            {
                int hidden = 0;
                foreach (ComponentOccurrence surface in surfaceRoots)
                {
                    try
                    {
                        surface.Visible = false;
                        hidden++;
                    }
                    catch
                    {
                    }
                }

                return hidden;
            }

            return ShowSurfaceRoots(surfaceRoots, assemblyDocument);
        }

        /// <summary>
        /// Shows surface root occurrences and suppresses work-feature graphics on every
        /// document in each surface tree (ObjectVisibility + per-feature Visible).
        /// </summary>
        private static int ShowSurfaceRoots(IList<ComponentOccurrence> surfaceRoots, AssemblyDocument hostAssembly)
        {
            if (surfaceRoots == null || surfaceRoots.Count == 0)
            {
                return 0;
            }

            var documents = new Dictionary<string, Document>(StringComparer.OrdinalIgnoreCase);
            if (hostAssembly != null)
            {
                AddDocument(hostAssembly, documents);
            }

            foreach (ComponentOccurrence surface in surfaceRoots)
            {
                CollectDocumentsUnderOccurrence(surface, documents);
            }

            foreach (Document document in documents.Values)
            {
                SuppressWorkFeatureDisplay(document);
            }

            int count = 0;
            foreach (ComponentOccurrence surface in surfaceRoots)
            {
                try
                {
                    HideWorkFeaturesUnderOccurrence(surface);
                    surface.Visible = true;
                    HideWorkFeaturesUnderOccurrence(surface);
                    count++;
                }
                catch
                {
                }
            }

            foreach (Document document in documents.Values)
            {
                SuppressWorkFeatureDisplay(document);
            }

            foreach (ComponentOccurrence surface in surfaceRoots)
            {
                HideWorkFeaturesUnderOccurrence(surface);
            }

            return count;
        }

        private static void AddDocument(object document, IDictionary<string, Document> documents)
        {
            var inventorDocument = document as Document;
            if (inventorDocument == null || documents == null)
            {
                return;
            }

            string key = GetDocumentKey(inventorDocument);
            if (!documents.ContainsKey(key))
            {
                documents.Add(key, inventorDocument);
            }
        }

        private static void SuppressWorkFeatureDisplay(Document document)
        {
            if (document == null)
            {
                return;
            }

            try
            {
                ObjectVisibility visibility = null;
                var assemblyDocument = document as AssemblyDocument;
                if (assemblyDocument != null)
                {
                    visibility = assemblyDocument.ObjectVisibility;
                }
                else
                {
                    var partDocument = document as PartDocument;
                    if (partDocument != null)
                    {
                        visibility = partDocument.ObjectVisibility;
                    }
                }

                if (visibility == null)
                {
                    return;
                }

                visibility.AllWorkFeatures = false;
                visibility.UserWorkPoints = false;
                visibility.OriginWorkPoints = false;
                visibility.UserWorkAxes = false;
                visibility.UserWorkPlanes = false;
                visibility.OriginWorkAxes = false;
                visibility.OriginWorkPlanes = false;
                visibility.UCSWorkPoints = false;
                visibility.UCSWorkAxes = false;
                visibility.UCSWorkPlanes = false;
            }
            catch
            {
            }
        }

        private static void CollectDocumentsUnderOccurrence(
            ComponentOccurrence occurrence,
            IDictionary<string, Document> documents)
        {
            if (occurrence == null || documents == null)
            {
                return;
            }

            try
            {
                Document document = GetReferencedDocument(occurrence);
                if (document != null)
                {
                    string key = GetDocumentKey(document);
                    if (!documents.ContainsKey(key))
                    {
                        documents.Add(key, document);
                    }
                }

                if (occurrence.DefinitionDocumentType != DocumentTypeEnum.kAssemblyDocumentObject)
                {
                    return;
                }

                foreach (ComponentOccurrence child in occurrence.SubOccurrences)
                {
                    CollectDocumentsUnderOccurrence(child, documents);
                }
            }
            catch
            {
            }
        }

        private static Document GetReferencedDocument(ComponentOccurrence occurrence)
        {
            if (occurrence == null)
            {
                return null;
            }

            try
            {
                var document = occurrence.ReferencedDocumentDescriptor.ReferencedDocument as Document;
                if (document != null)
                {
                    return document;
                }
            }
            catch
            {
            }

            try
            {
                ComponentDefinition definition = occurrence.Definition;
                if (definition is PartComponentDefinition partDefinition)
                {
                    return partDefinition.Document as Document;
                }

                if (definition is AssemblyComponentDefinition assemblyDefinition)
                {
                    return assemblyDefinition.Document as Document;
                }
            }
            catch
            {
            }

            return null;
        }

        private static string GetDocumentKey(Document document)
        {
            if (document == null)
            {
                return string.Empty;
            }

            try
            {
                if (!string.IsNullOrWhiteSpace(document.FullFileName))
                {
                    return document.FullFileName;
                }
            }
            catch
            {
            }

            try
            {
                return document.InternalName ?? document.DisplayName ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static void HideWorkFeaturesUnderOccurrence(ComponentOccurrence occurrence)
        {
            if (occurrence == null)
            {
                return;
            }

            try
            {
                HideWorkFeaturesOnOccurrence(occurrence);

                if (occurrence.DefinitionDocumentType != DocumentTypeEnum.kAssemblyDocumentObject)
                {
                    return;
                }

                foreach (ComponentOccurrence child in occurrence.SubOccurrences)
                {
                    HideWorkFeaturesUnderOccurrence(child);
                }
            }
            catch
            {
            }
        }

        private static void HideWorkFeaturesOnOccurrence(ComponentOccurrence occurrence)
        {
            if (occurrence == null)
            {
                return;
            }

            // Assembly viewport uses geometry proxies — native WorkPoint.Visible can stay false
            // while WorkPointProxy.Visible remains true unless hidden in occurrence context.
            try
            {
                HideWorkFeaturesViaProxy(occurrence);
            }
            catch
            {
            }

            try
            {
                Document document = GetReferencedDocument(occurrence);
                if (document is PartDocument partDocument)
                {
                    HideWorkFeaturesOnDefinition(partDocument.ComponentDefinition);
                }
                else if (document is AssemblyDocument assemblyDocument)
                {
                    HideWorkFeaturesOnDefinition(assemblyDocument.ComponentDefinition);
                }
            }
            catch
            {
            }

            try
            {
                HideWorkFeaturesOnDefinition(occurrence.Definition);
            }
            catch
            {
            }
        }

        private static void HideWorkFeaturesViaProxy(ComponentOccurrence occurrence)
        {
            var partDef = occurrence.Definition as PartComponentDefinition;
            if (partDef != null)
            {
                HideWorkPointCollectionViaProxy(occurrence, partDef.WorkPoints);
                HideWorkAxisCollectionViaProxy(occurrence, partDef.WorkAxes);
                HideWorkPlaneCollectionViaProxy(occurrence, partDef.WorkPlanes);
                return;
            }

            var assemblyDef = occurrence.Definition as AssemblyComponentDefinition;
            if (assemblyDef != null)
            {
                HideWorkPointCollectionViaProxy(occurrence, assemblyDef.WorkPoints);
                HideWorkAxisCollectionViaProxy(occurrence, assemblyDef.WorkAxes);
                HideWorkPlaneCollectionViaProxy(occurrence, assemblyDef.WorkPlanes);
            }
        }

        private static void HideWorkPointCollectionViaProxy(ComponentOccurrence occurrence, WorkPoints workPoints)
        {
            if (occurrence == null || workPoints == null)
            {
                return;
            }

            foreach (WorkPoint workPoint in workPoints)
            {
                try
                {
                    object result = null;
                    occurrence.CreateGeometryProxy(workPoint, out result);
                    var proxy = result as WorkPointProxy;
                    if (proxy != null)
                    {
                        proxy.Visible = false;
                    }
                }
                catch
                {
                }

                try
                {
                    workPoint.Visible = false;
                }
                catch
                {
                }
            }
        }

        private static void HideWorkAxisCollectionViaProxy(ComponentOccurrence occurrence, WorkAxes workAxes)
        {
            if (occurrence == null || workAxes == null)
            {
                return;
            }

            foreach (WorkAxis workAxis in workAxes)
            {
                try
                {
                    object result = null;
                    occurrence.CreateGeometryProxy(workAxis, out result);
                    var proxy = result as WorkAxisProxy;
                    if (proxy != null)
                    {
                        proxy.Visible = false;
                    }
                }
                catch
                {
                }

                try
                {
                    workAxis.Visible = false;
                }
                catch
                {
                }
            }
        }

        private static void HideWorkPlaneCollectionViaProxy(ComponentOccurrence occurrence, WorkPlanes workPlanes)
        {
            if (occurrence == null || workPlanes == null)
            {
                return;
            }

            foreach (WorkPlane workPlane in workPlanes)
            {
                try
                {
                    object result = null;
                    occurrence.CreateGeometryProxy(workPlane, out result);
                    var proxy = result as WorkPlaneProxy;
                    if (proxy != null)
                    {
                        proxy.Visible = false;
                    }
                }
                catch
                {
                }

                try
                {
                    workPlane.Visible = false;
                }
                catch
                {
                }
            }
        }

        private static void HideWorkFeaturesOnDefinition(object definition)
        {
            if (definition == null)
            {
                return;
            }

            try
            {
                var partDef = definition as PartComponentDefinition;
                if (partDef != null)
                {
                    HideWorkPointCollection(partDef.WorkPoints);
                    HideWorkAxisCollection(partDef.WorkAxes);
                    HideWorkPlaneCollection(partDef.WorkPlanes);
                    return;
                }

                var assemblyDef = definition as AssemblyComponentDefinition;
                if (assemblyDef != null)
                {
                    HideWorkPointCollection(assemblyDef.WorkPoints);
                    HideWorkAxisCollection(assemblyDef.WorkAxes);
                    HideWorkPlaneCollection(assemblyDef.WorkPlanes);
                }
            }
            catch
            {
            }
        }

        private static void HideWorkPointCollection(WorkPoints workPoints)
        {
            if (workPoints == null)
            {
                return;
            }

            foreach (WorkPoint workPoint in workPoints)
            {
                try
                {
                    WorkPoint target = workPoint;
                    try
                    {
                        var proxy = workPoint as WorkPointProxy;
                        if (proxy != null && proxy.NativeObject != null)
                        {
                            target = proxy.NativeObject;
                        }
                    }
                    catch
                    {
                    }

                    target.Visible = false;
                }
                catch
                {
                }
            }
        }

        private static void HideWorkAxisCollection(WorkAxes workAxes)
        {
            if (workAxes == null)
            {
                return;
            }

            foreach (WorkAxis workAxis in workAxes)
            {
                try
                {
                    workAxis.Visible = false;
                }
                catch
                {
                }
            }
        }

        private static void HideWorkPlaneCollection(WorkPlanes workPlanes)
        {
            if (workPlanes == null)
            {
                return;
            }

            foreach (WorkPlane workPlane in workPlanes)
            {
                try
                {
                    workPlane.Visible = false;
                }
                catch
                {
                }
            }
        }

        /// <summary>
        /// True if any matching surface occurrence is currently visible; false if all hidden.
        /// Null when no matching surfaces exist.
        /// </summary>
        public bool? GetSurfaceDescriptionVisibility(
            AssemblyDocument assemblyDocument,
            string surfaceDescription)
        {
            if (assemblyDocument == null || string.IsNullOrWhiteSpace(surfaceDescription))
            {
                return null;
            }

            var surfaceRoots = new List<ComponentOccurrence>();
            CollectSurfacesByDescription(
                assemblyDocument.ComponentDefinition.Occurrences,
                surfaceDescription.Trim(),
                surfaceRoots);

            if (surfaceRoots.Count == 0)
            {
                return null;
            }

            foreach (ComponentOccurrence occurrence in surfaceRoots)
            {
                try
                {
                    if (occurrence.Visible)
                    {
                        return true;
                    }
                }
                catch
                {
                }
            }

            return false;
        }

        /// <summary>
        /// Collects distinct Project Descriptions under the assembly (for diagnostics).
        /// </summary>
        public IList<string> SampleDescriptions(AssemblyDocument assemblyDocument, int maxSamples = 12)
        {
            var samples = new List<string>();
            if (assemblyDocument == null)
            {
                return samples;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ComponentOccurrence occurrence in assemblyDocument.ComponentDefinition.Occurrences)
            {
                CollectDescriptionSamples(occurrence, seen, samples, maxSamples);
                if (samples.Count >= maxSamples)
                {
                    break;
                }
            }

            return samples;
        }

        private static void CollectDescriptionSamples(
            ComponentOccurrence occurrence,
            ISet<string> seen,
            IList<string> samples,
            int maxSamples)
        {
            if (occurrence == null || samples.Count >= maxSamples)
            {
                return;
            }

            try
            {
                string description = StockPropertyHelper.GetDescription(occurrence);
                if (!string.IsNullOrWhiteSpace(description) && seen.Add(description))
                {
                    samples.Add(description);
                }

                if (occurrence.DefinitionDocumentType != DocumentTypeEnum.kAssemblyDocumentObject)
                {
                    return;
                }

                foreach (ComponentOccurrence child in occurrence.SubOccurrences)
                {
                    CollectDescriptionSamples(child, seen, samples, maxSamples);
                    if (samples.Count >= maxSamples)
                    {
                        return;
                    }
                }
            }
            catch
            {
            }
        }

        private static void CollectSurfacesByDescriptions(
            ComponentOccurrences occurrences,
            ISet<string> targetDescriptions,
            ICollection<ComponentOccurrence> matches)
        {
            if (occurrences == null || matches == null || targetDescriptions == null || targetDescriptions.Count == 0)
            {
                return;
            }

            foreach (ComponentOccurrence occurrence in occurrences)
            {
                CollectSurfacesByDescriptions(occurrence, targetDescriptions, matches);
            }
        }

        private static void CollectSurfacesByDescriptions(
            ComponentOccurrence occurrence,
            ISet<string> targetDescriptions,
            ICollection<ComponentOccurrence> matches)
        {
            if (occurrence == null)
            {
                return;
            }

            try
            {
                string description = StockPropertyHelper.GetDescription(occurrence);
                if (!string.IsNullOrWhiteSpace(description)
                    && targetDescriptions.Contains(description.Trim()))
                {
                    matches.Add(occurrence);
                    return;
                }

                if (occurrence.DefinitionDocumentType != DocumentTypeEnum.kAssemblyDocumentObject)
                {
                    return;
                }

                foreach (ComponentOccurrence child in occurrence.SubOccurrences)
                {
                    CollectSurfacesByDescriptions(child, targetDescriptions, matches);
                }
            }
            catch
            {
            }
        }

        private static void CollectSurfacesByDescription(
            ComponentOccurrences occurrences,
            string targetDescription,
            ICollection<ComponentOccurrence> matches)
        {
            if (occurrences == null || matches == null)
            {
                return;
            }

            foreach (ComponentOccurrence occurrence in occurrences)
            {
                CollectSurfacesByDescription(occurrence, targetDescription, matches);
            }
        }

        private static void CollectSurfacesByDescription(
            ComponentOccurrence occurrence,
            string targetDescription,
            ICollection<ComponentOccurrence> matches)
        {
            if (occurrence == null)
            {
                return;
            }

            try
            {
                string description = StockPropertyHelper.GetDescription(occurrence);
                if (string.Equals(description, targetDescription, StringComparison.OrdinalIgnoreCase))
                {
                    matches.Add(occurrence);
                    return;
                }

                if (occurrence.DefinitionDocumentType != DocumentTypeEnum.kAssemblyDocumentObject)
                {
                    return;
                }

                foreach (ComponentOccurrence child in occurrence.SubOccurrences)
                {
                    CollectSurfacesByDescription(child, targetDescription, matches);
                }
            }
            catch
            {
            }
        }

        public void RunRule(AssemblyDocument assemblyDocument, string ruleId, InvApp app = null)
        {
            if (VisTogRulesCatalog.TryGetPreset(ruleId, out PresetRuleSpec preset))
            {
                SetStocksVisibility(assemblyDocument, preset.stocks, preset.visible);
                return;
            }

            ToggleRuleSpec toggle = VisTogRulesCatalog.FindToggle(ruleId);
            if (toggle == null)
            {
                return;
            }

            ToggleStocks(assemblyDocument, VisTogRulesCatalog.GetStocksForToggle(toggle));
        }

        public void RunRuleGroup(AssemblyDocument assemblyDocument, IEnumerable<string> ruleIds, InvApp app = null)
        {
            ToggleStocks(assemblyDocument, VisTogRulesCatalog.GetStocksForRuleIds(ruleIds));
        }
    }

    public sealed class ToggleStockResult
    {
        public bool Succeeded { get; private set; }
        public string Message { get; private set; }
        public int Count { get; private set; }
        public bool NewVisibility { get; private set; }

        public static ToggleStockResult Ok(int count, bool newVisibility)
        {
            return new ToggleStockResult
            {
                Succeeded = true,
                Count = count,
                NewVisibility = newVisibility,
                Message = string.Empty
            };
        }

        public static ToggleStockResult Failed(string message)
        {
            return new ToggleStockResult
            {
                Succeeded = false,
                Message = message ?? string.Empty
            };
        }
    }
}
