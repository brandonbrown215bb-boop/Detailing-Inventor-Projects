using System;
using InvApp = Inventor.Application;
using Inventor;

namespace SkinChannelPunch.Core
{
    internal static class InteractionHelper
    {
        public static ComponentOccurrence PickOccurrence(InvApp app, AssemblyDocument assemblyDocument, string prompt)
        {
            PrepareForPick(app, assemblyDocument);

            object picked = app.CommandManager.Pick(SelectionFilterEnum.kAssemblyOccurrenceFilter, prompt);
            if (picked == null)
            {
                return null;
            }

            if (picked is ComponentOccurrence occurrence)
            {
                return occurrence;
            }

            return null;
        }

        public static bool TryPickSkinFace(
            InvApp app,
            AssemblyDocument assemblyDocument,
            string prompt,
            out Face face,
            out ComponentOccurrence skinOcc)
        {
            face = null;
            skinOcc = null;
            PrepareForPick(app, assemblyDocument);

            object picked = app.CommandManager.Pick(SelectionFilterEnum.kPartFacePlanarFilter, prompt);
            if (picked == null)
            {
                return false;
            }

            face = picked as Face;
            if (face == null)
            {
                return false;
            }

            // Prefer ContainingOccurrence — returns the leaf in the active assembly context
            // (works when the skin lives under a nested unit IAM).
            if (face is FaceProxy faceProxy)
            {
                try
                {
                    skinOcc = faceProxy.ContainingOccurrence;
                }
                catch
                {
                    skinOcc = null;
                }
            }

            if (skinOcc == null)
            {
                skinOcc = FindOccurrenceForFace(assemblyDocument, face);
            }

            return skinOcc != null;
        }

        private static void PrepareForPick(InvApp app, AssemblyDocument assemblyDocument)
        {
            try { app.CommandManager.StopAllActiveCommands(); } catch { }
            try { assemblyDocument.SelectSet.Clear(); } catch { }
            try { app.ActiveView?.Update(); } catch { }
        }

        public static ComponentOccurrence FindOccurrenceForFace(AssemblyDocument assemblyDocument, Face face)
        {
            if (face == null)
            {
                return null;
            }

            if (face is FaceProxy faceProxy)
            {
                try
                {
                    ComponentOccurrence containing = faceProxy.ContainingOccurrence;
                    if (containing != null)
                    {
                        return containing;
                    }
                }
                catch
                {
                }
            }

            return FindOccurrenceForFaceRecursive(assemblyDocument.ComponentDefinition.Occurrences, face);
        }

        private static ComponentOccurrence FindOccurrenceForFaceRecursive(
            ComponentOccurrences occurrences,
            Face face)
        {
            for (int i = 1; i <= occurrences.Count; i++)
            {
                ComponentOccurrence hit = TryMatchOccurrenceOrChildren(occurrences[i], face);
                if (hit != null)
                {
                    return hit;
                }
            }

            return null;
        }

        private static ComponentOccurrence FindOccurrenceForFaceRecursive(
            ComponentOccurrencesEnumerator occurrences,
            Face face)
        {
            foreach (ComponentOccurrence occurrence in occurrences)
            {
                ComponentOccurrence hit = TryMatchOccurrenceOrChildren(occurrence, face);
                if (hit != null)
                {
                    return hit;
                }
            }

            return null;
        }

        private static ComponentOccurrence TryMatchOccurrenceOrChildren(ComponentOccurrence occurrence, Face face)
        {
            try
            {
                object proxy = null;
                occurrence.CreateGeometryProxy(face, out proxy);
                if (proxy != null)
                {
                    return occurrence;
                }
            }
            catch
            {
            }

            if (occurrence.DefinitionDocumentType == DocumentTypeEnum.kAssemblyDocumentObject)
            {
                try
                {
                    return FindOccurrenceForFaceRecursive(occurrence.SubOccurrences, face);
                }
                catch
                {
                }
            }

            return null;
        }
    }
}
