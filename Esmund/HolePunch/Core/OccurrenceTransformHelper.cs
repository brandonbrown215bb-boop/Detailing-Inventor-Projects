using System.Collections.Generic;
using Inventor;

namespace SkinChannelPunch.Core
{
    internal static class OccurrenceTransformHelper
    {
        public static bool TryGetChainFromRoot(
            AssemblyDocument rootAssembly,
            ComponentOccurrence target,
            out List<ComponentOccurrence> chain,
            out string error)
        {
            chain = new List<ComponentOccurrence>();
            error = string.Empty;

            if (target == null)
            {
                error = "Occurrence is missing.";
                return false;
            }

            if (rootAssembly == null)
            {
                error = "Root assembly is missing.";
                return false;
            }

            // Prefer ParentOccurrence walk — correct for leaves picked in a parent IAM.
            if (TryBuildChainFromParentLinks(target, chain)
                && ChainStartsUnderRoot(rootAssembly, chain))
            {
                return true;
            }

            chain.Clear();
            // Must walk SubOccurrences (active-assembly context), not nestedAssembly.Occurrences
            // (document-local occurrences that fail ReferenceEquals and break transforms).
            if (FindChainRecursive(rootAssembly.ComponentDefinition.Occurrences, target, chain))
            {
                return true;
            }

            chain.Clear();
            error = "Could not resolve the occurrence path from the active assembly (nested context).";
            return false;
        }

        private static bool TryBuildChainFromParentLinks(
            ComponentOccurrence target,
            List<ComponentOccurrence> chain)
        {
            var stack = new Stack<ComponentOccurrence>();
            ComponentOccurrence current = target;
            int guard = 0;
            while (current != null && guard < 64)
            {
                guard++;
                stack.Push(current);
                try
                {
                    current = current.ParentOccurrence;
                }
                catch
                {
                    current = null;
                }
            }

            if (stack.Count == 0)
            {
                return false;
            }

            while (stack.Count > 0)
            {
                chain.Add(stack.Pop());
            }

            return true;
        }

        private static bool ChainStartsUnderRoot(
            AssemblyDocument rootAssembly,
            IReadOnlyList<ComponentOccurrence> chain)
        {
            if (chain == null || chain.Count == 0)
            {
                return false;
            }

            ComponentOccurrences topLevel = rootAssembly.ComponentDefinition.Occurrences;
            for (int i = 1; i <= topLevel.Count; i++)
            {
                if (ReferenceEquals(topLevel[i], chain[0]))
                {
                    return true;
                }
            }

            return false;
        }

        public static Point ParentSpacePointToRoot(
            Application app,
            IReadOnlyList<ComponentOccurrence> chainFromRoot,
            Point pointInImmediateParentSpace)
        {
            Point point = app.TransientGeometry.CreatePoint(
                pointInImmediateParentSpace.X,
                pointInImmediateParentSpace.Y,
                pointInImmediateParentSpace.Z);

            for (int i = chainFromRoot.Count - 2; i >= 0; i--)
            {
                Matrix step = app.TransientGeometry.CreateMatrix();
                step.SetToIdentity();
                step.TransformBy(chainFromRoot[i].Transformation);
                point.TransformBy(step);
            }

            return point;
        }

        public static Vector ParentSpaceVectorToRoot(
            Application app,
            IReadOnlyList<ComponentOccurrence> chainFromRoot,
            Vector vectorInImmediateParentSpace)
        {
            Point zero = app.TransientGeometry.CreatePoint(0, 0, 0);
            Point tip = app.TransientGeometry.CreatePoint(
                vectorInImmediateParentSpace.X,
                vectorInImmediateParentSpace.Y,
                vectorInImmediateParentSpace.Z);
            Point zeroRoot = ParentSpacePointToRoot(app, chainFromRoot, zero);
            Point tipRoot = ParentSpacePointToRoot(app, chainFromRoot, tip);
            return app.TransientGeometry.CreateVector(
                tipRoot.X - zeroRoot.X,
                tipRoot.Y - zeroRoot.Y,
                tipRoot.Z - zeroRoot.Z);
        }

        public static Point PartPointToRoot(
            Application app,
            IReadOnlyList<ComponentOccurrence> chainFromRoot,
            Point partPoint)
        {
            Matrix partToRoot = BuildPartToRootMatrix(app, chainFromRoot);
            Point copy = app.TransientGeometry.CreatePoint(partPoint.X, partPoint.Y, partPoint.Z);
            copy.TransformBy(partToRoot);
            return copy;
        }

        public static Point RootPointToPartSpace(
            Application app,
            IReadOnlyList<ComponentOccurrence> chainFromRoot,
            Point rootPoint)
        {
            Matrix partToRoot = BuildPartToRootMatrix(app, chainFromRoot);
            Matrix rootToPart = app.TransientGeometry.CreateMatrix();
            rootToPart.SetToIdentity();
            rootToPart.TransformBy(partToRoot);
            rootToPart.Invert();

            Point copy = app.TransientGeometry.CreatePoint(rootPoint.X, rootPoint.Y, rootPoint.Z);
            copy.TransformBy(rootToPart);
            return copy;
        }

        public static UnitVector PartVectorToRoot(
            Application app,
            IReadOnlyList<ComponentOccurrence> chainFromRoot,
            UnitVector partVector)
        {
            Vector vector = app.TransientGeometry.CreateVector(partVector.X, partVector.Y, partVector.Z);
            Vector rootVector = PartVectorToRootVector(app, chainFromRoot, vector);
            return Normalize(app, rootVector);
        }

        public static Vector PartVectorToRootVector(
            Application app,
            IReadOnlyList<ComponentOccurrence> chainFromRoot,
            Vector partVector)
        {
            Point zero = app.TransientGeometry.CreatePoint(0, 0, 0);
            Point tip = app.TransientGeometry.CreatePoint(partVector.X, partVector.Y, partVector.Z);
            Point zeroRoot = PartPointToRoot(app, chainFromRoot, zero);
            Point tipRoot = PartPointToRoot(app, chainFromRoot, tip);
            return app.TransientGeometry.CreateVector(
                tipRoot.X - zeroRoot.X,
                tipRoot.Y - zeroRoot.Y,
                tipRoot.Z - zeroRoot.Z);
        }

        private static Matrix BuildPartToRootMatrix(
            Application app,
            IReadOnlyList<ComponentOccurrence> chainFromRoot)
        {
            Matrix partToRoot = app.TransientGeometry.CreateMatrix();
            partToRoot.SetToIdentity();
            foreach (ComponentOccurrence occurrence in chainFromRoot)
            {
                partToRoot.TransformBy(occurrence.Transformation);
            }

            return partToRoot;
        }

        private static UnitVector Normalize(Application app, Vector vector)
        {
            double length = System.Math.Sqrt((vector.X * vector.X) + (vector.Y * vector.Y) + (vector.Z * vector.Z));
            if (length < 1e-9)
            {
                return null;
            }

            return app.TransientGeometry.CreateUnitVector(
                vector.X / length,
                vector.Y / length,
                vector.Z / length);
        }

        private static bool FindChainRecursive(
            ComponentOccurrences occurrences,
            ComponentOccurrence target,
            List<ComponentOccurrence> chain)
        {
            for (int i = 1; i <= occurrences.Count; i++)
            {
                if (TryVisitOccurrence(occurrences[i], target, chain))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool FindChainRecursive(
            ComponentOccurrencesEnumerator occurrences,
            ComponentOccurrence target,
            List<ComponentOccurrence> chain)
        {
            foreach (ComponentOccurrence occurrence in occurrences)
            {
                if (TryVisitOccurrence(occurrence, target, chain))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryVisitOccurrence(
            ComponentOccurrence occurrence,
            ComponentOccurrence target,
            List<ComponentOccurrence> chain)
        {
            chain.Add(occurrence);

            if (ReferenceEquals(occurrence, target))
            {
                return true;
            }

            if (occurrence.DefinitionDocumentType == DocumentTypeEnum.kAssemblyDocumentObject)
            {
                try
                {
                    if (FindChainRecursive(occurrence.SubOccurrences, target, chain))
                    {
                        return true;
                    }
                }
                catch
                {
                }
            }

            chain.RemoveAt(chain.Count - 1);
            return false;
        }
    }
}
