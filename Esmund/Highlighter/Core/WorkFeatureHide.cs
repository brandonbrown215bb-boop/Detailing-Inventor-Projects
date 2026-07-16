using Inventor;

namespace Highlighter.Core
{
    /// <summary>
    /// VisTog-proven work feature suppression (ObjectVisibility + geometry proxies).
    /// </summary>
    internal static class WorkFeatureHide
    {
        public static void SuppressDocument(Document document)
        {
            if (document == null)
            {
                return;
            }

            try
            {
                ObjectVisibility visibility = null;
                if (document is AssemblyDocument assemblyDocument)
                {
                    visibility = assemblyDocument.ObjectVisibility;
                }
                else if (document is PartDocument partDocument)
                {
                    visibility = partDocument.ObjectVisibility;
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
                try { visibility.UCSWorkPoints = false; } catch { }
                try { visibility.UCSWorkAxes = false; } catch { }
                try { visibility.UCSWorkPlanes = false; } catch { }
            }
            catch
            {
            }
        }

        public static void HideUnderOccurrence(ComponentOccurrence occurrence)
        {
            if (occurrence == null)
            {
                return;
            }

            try
            {
                HideOnOccurrence(occurrence);

                if (occurrence.DefinitionDocumentType != DocumentTypeEnum.kAssemblyDocumentObject)
                {
                    return;
                }

                foreach (ComponentOccurrence child in occurrence.SubOccurrences)
                {
                    HideUnderOccurrence(child);
                }
            }
            catch
            {
            }
        }

        private static void HideOnOccurrence(ComponentOccurrence occurrence)
        {
            try
            {
                HideViaProxy(occurrence);
            }
            catch
            {
            }

            try
            {
                Document document = null;
                try { document = occurrence.ReferencedDocumentDescriptor.ReferencedDocument as Document; } catch { }
                if (document == null)
                {
                    try { document = occurrence.Definition?.Document as Document; } catch { }
                }

                if (document is PartDocument partDocument)
                {
                    HideOnDefinition(partDocument.ComponentDefinition);
                }
                else if (document is AssemblyDocument assemblyDocument)
                {
                    HideOnDefinition(assemblyDocument.ComponentDefinition);
                }
            }
            catch
            {
            }
        }

        private static void HideViaProxy(ComponentOccurrence occurrence)
        {
            ComponentDefinition def = null;
            try { def = occurrence.Definition; } catch { return; }
            if (def == null)
            {
                return;
            }

            try
            {
                if (def is PartComponentDefinition partDef)
                {
                    HideWorkPointsViaProxy(occurrence, partDef.WorkPoints);
                    HideWorkAxesViaProxy(occurrence, partDef.WorkAxes);
                    HideWorkPlanesViaProxy(occurrence, partDef.WorkPlanes);
                }
                else if (def is AssemblyComponentDefinition asmDef)
                {
                    HideWorkPointsViaProxy(occurrence, asmDef.WorkPoints);
                    HideWorkAxesViaProxy(occurrence, asmDef.WorkAxes);
                    HideWorkPlanesViaProxy(occurrence, asmDef.WorkPlanes);
                }
            }
            catch
            {
            }
        }

        private static void HideOnDefinition(object definition)
        {
            try
            {
                if (definition is PartComponentDefinition partDef)
                {
                    foreach (WorkPoint wp in partDef.WorkPoints)
                    {
                        try { wp.Visible = false; } catch { }
                    }

                    foreach (WorkAxis wa in partDef.WorkAxes)
                    {
                        try { wa.Visible = false; } catch { }
                    }

                    foreach (WorkPlane wp in partDef.WorkPlanes)
                    {
                        try { wp.Visible = false; } catch { }
                    }
                }
                else if (definition is AssemblyComponentDefinition asmDef)
                {
                    foreach (WorkPoint wp in asmDef.WorkPoints)
                    {
                        try { wp.Visible = false; } catch { }
                    }

                    foreach (WorkAxis wa in asmDef.WorkAxes)
                    {
                        try { wa.Visible = false; } catch { }
                    }

                    foreach (WorkPlane wp in asmDef.WorkPlanes)
                    {
                        try { wp.Visible = false; } catch { }
                    }
                }
            }
            catch
            {
            }
        }

        private static void HideWorkPointsViaProxy(ComponentOccurrence occurrence, WorkPoints workPoints)
        {
            if (occurrence == null || workPoints == null)
            {
                return;
            }

            foreach (WorkPoint workPoint in workPoints)
            {
                try
                {
                    object result;
                    occurrence.CreateGeometryProxy(workPoint, out result);
                    if (result is WorkPointProxy proxy)
                    {
                        proxy.Visible = false;
                    }
                }
                catch
                {
                }

                try { workPoint.Visible = false; } catch { }
            }
        }

        private static void HideWorkAxesViaProxy(ComponentOccurrence occurrence, WorkAxes workAxes)
        {
            if (occurrence == null || workAxes == null)
            {
                return;
            }

            foreach (WorkAxis workAxis in workAxes)
            {
                try
                {
                    object result;
                    occurrence.CreateGeometryProxy(workAxis, out result);
                    if (result is WorkAxisProxy proxy)
                    {
                        proxy.Visible = false;
                    }
                }
                catch
                {
                }

                try { workAxis.Visible = false; } catch { }
            }
        }

        private static void HideWorkPlanesViaProxy(ComponentOccurrence occurrence, WorkPlanes workPlanes)
        {
            if (occurrence == null || workPlanes == null)
            {
                return;
            }

            foreach (WorkPlane workPlane in workPlanes)
            {
                try
                {
                    object result;
                    occurrence.CreateGeometryProxy(workPlane, out result);
                    if (result is WorkPlaneProxy proxy)
                    {
                        proxy.Visible = false;
                    }
                }
                catch
                {
                }

                try { workPlane.Visible = false; } catch { }
            }
        }
    }
}
