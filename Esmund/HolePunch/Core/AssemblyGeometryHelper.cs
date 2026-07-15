using System;
using Inventor;

namespace SkinChannelPunch.Core
{
    internal static class AssemblyGeometryHelper
    {
        public static bool TryResolvePartFace(
            ComponentOccurrence skinOcc,
            Face pickedFace,
            out Face partFace,
            out string error)
        {
            partFace = null;
            error = string.Empty;

            try
            {
                if (pickedFace == null)
                {
                    error = "No face was picked.";
                    return false;
                }

                // Assembly picks are FaceProxy; NativeObject is already in part space.
                if (pickedFace is FaceProxy faceProxy)
                {
                    Face nativeFromProxy = faceProxy.NativeObject;
                    if (nativeFromProxy != null && nativeFromProxy.SurfaceType == SurfaceTypeEnum.kPlaneSurface)
                    {
                        partFace = nativeFromProxy;
                        return true;
                    }
                }

                object proxy = null;
                skinOcc.CreateGeometryProxy(pickedFace, out proxy);
                Face proxyFace = proxy as Face;
                if (proxyFace == null)
                {
                    error = "Could not create a part-space face proxy.";
                    return false;
                }

                if (proxyFace.SurfaceType != SurfaceTypeEnum.kPlaneSurface)
                {
                    error = "Selected face is not planar.";
                    return false;
                }

                if (proxyFace is FaceProxy createdProxy)
                {
                    Face nativeFromCreated = createdProxy.NativeObject;
                    if (nativeFromCreated != null && nativeFromCreated.SurfaceType == SurfaceTypeEnum.kPlaneSurface)
                    {
                        partFace = nativeFromCreated;
                        return true;
                    }
                }

                PartDocument partDocument = skinOcc.Definition.Document as PartDocument;
                if (partDocument != null)
                {
                    Application app = partDocument.Parent as Application;
                    partFace = FindNativeFace(app, skinOcc, partDocument.ComponentDefinition, proxyFace) ?? proxyFace;
                }
                else
                {
                    partFace = proxyFace;
                }

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// Match a proxy face to the native part face. Proxy PointOnFace is in assembly
        /// space; native planes are in part space — transform before comparing.
        /// </summary>
        private static Face FindNativeFace(
            Application app,
            ComponentOccurrence skinOcc,
            PartComponentDefinition partComponentDefinition,
            Face referenceFace)
        {
            if (app == null || skinOcc == null || referenceFace == null)
            {
                return null;
            }

            Point referencePointAsm = referenceFace.PointOnFace;
            Matrix rootToPart = skinOcc.Transformation.Copy();
            rootToPart.Invert();
            Point referencePointPart = app.TransientGeometry.CreatePoint(
                referencePointAsm.X,
                referencePointAsm.Y,
                referencePointAsm.Z);
            referencePointPart.TransformBy(rootToPart);

            Face bestFace = null;
            double bestDistance = double.MaxValue;

            foreach (SurfaceBody body in partComponentDefinition.SurfaceBodies)
            {
                foreach (Face face in body.Faces)
                {
                    if (face.SurfaceType != SurfaceTypeEnum.kPlaneSurface)
                    {
                        continue;
                    }

                    try
                    {
                        double distance = DistancePointToFace(face, referencePointPart);
                        if (distance < bestDistance)
                        {
                            bestDistance = distance;
                            bestFace = face;
                        }
                    }
                    catch
                    {
                    }
                }
            }

            return bestDistance < 0.05 ? bestFace : null;
        }

        private static double DistancePointToFace(Face face, Point point)
        {
            Plane plane = face.Geometry as Plane;
            if (plane == null)
            {
                return double.MaxValue;
            }

            Point root = plane.RootPoint;
            UnitVector normal = plane.Normal;
            double dx = point.X - root.X;
            double dy = point.Y - root.Y;
            double dz = point.Z - root.Z;
            return Math.Abs((dx * normal.X) + (dy * normal.Y) + (dz * normal.Z));
        }
    }
}
