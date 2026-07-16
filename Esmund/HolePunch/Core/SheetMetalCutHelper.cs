using System;
using Inventor;

namespace SkinChannelPunch.Core
{
    internal static class SheetMetalCutHelper
    {
        public static FaceSketchResult CreateSketchOnFace(
            Application app,
            SheetMetalComponentDefinition smcd,
            Face partFace,
            FacePlanarGeometry faceGeometry)
        {
            var result = new FaceSketchResult();

            if (faceGeometry != null &&
                TryCreateSketchWithFaceOrientation(smcd, partFace, faceGeometry, out PlanarSketch orientedSketch))
            {
                result.Sketch = orientedSketch;
                result.UsesFaceFrameCoords = true;
                return result;
            }

            try
            {
                result.TemporaryWorkPlane = smcd.WorkPlanes.AddByPlaneAndOffset(partFace, 0.0, true);
                result.Sketch = smcd.Sketches.Add(result.TemporaryWorkPlane, false);
                return result;
            }
            catch
            {
                result.TemporaryWorkPlane = null;
            }

            if (faceGeometry?.BottomVertex != null)
            {
                try
                {
                    result.TemporaryWorkPlane = smcd.WorkPlanes.AddByPlaneAndPoint(
                        partFace,
                        faceGeometry.BottomVertex,
                        true);
                    result.Sketch = smcd.Sketches.Add(result.TemporaryWorkPlane, false);
                    return result;
                }
                catch
                {
                    result.TemporaryWorkPlane = null;
                }
            }

            Plane targetPlane = partFace.Geometry as Plane;
            if (targetPlane == null)
            {
                throw new InvalidOperationException("Skin face is not planar.");
            }

            Point origin = faceGeometry?.OriginPart ?? partFace.PointOnFace;
            UnitVector xAxis = faceGeometry?.AcrossPart ?? ComputeInPlaneAxis(app, targetPlane.Normal);
            result.TemporaryWorkPlane = smcd.WorkPlanes.AddFixed(
                origin,
                targetPlane.Normal,
                xAxis,
                true);
            result.Sketch = smcd.Sketches.Add(result.TemporaryWorkPlane, false);
            return result;
        }

        public static void CreateSingleCutFromSketch(
            SheetMetalComponentDefinition smcd,
            PlanarSketch sketch)
        {
            Profile profile = CreateProfileForHoles(sketch);

            SheetMetalFeatures smf = (SheetMetalFeatures)(object)smcd.Features;
            CutFeatures cfs = smf.CutFeatures;
            CutDefinition cdef = cfs.CreateCutDefinition(profile);
            ApplyThroughAllExtent(cdef);
            cfs.Add(cdef);
        }

        public static void AddCircle(PlanarSketch sketch, Point2d center, double radiusCm)
        {
            try
            {
                sketch.SketchCircles.AddByCenterRadius(center, radiusCm);
            }
            catch
            {
                sketch.Edit();
                try
                {
                    sketch.SketchCircles.AddByCenterRadius(center, radiusCm);
                }
                finally
                {
                    sketch.ExitEdit();
                }
            }
        }

        private static bool TryCreateSketchWithFaceOrientation(
            SheetMetalComponentDefinition smcd,
            Face partFace,
            FacePlanarGeometry faceGeometry,
            out PlanarSketch sketch)
        {
            sketch = null;
            if (faceGeometry?.LongestEdge == null || faceGeometry.BottomVertex == null)
            {
                return false;
            }

            try
            {
                sketch = smcd.Sketches.AddWithOrientation(
                    partFace,
                    faceGeometry.LongestEdge,
                    faceGeometry.NaturalEdgeDirection,
                    false,
                    faceGeometry.BottomVertex,
                    false);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static UnitVector ComputeInPlaneAxis(Application app, UnitVector normal)
        {
            TransientGeometry tg = app.TransientGeometry;
            Vector reference = tg.CreateVector(0, 1, 0);
            if (Math.Abs(normal.Y) > 0.95)
            {
                reference = tg.CreateVector(1, 0, 0);
            }

            double x = (reference.Y * normal.Z) - (reference.Z * normal.Y);
            double y = (reference.Z * normal.X) - (reference.X * normal.Z);
            double z = (reference.X * normal.Y) - (reference.Y * normal.X);
            double length = Math.Sqrt((x * x) + (y * y) + (z * z));
            if (length < 1e-9)
            {
                reference = tg.CreateVector(0, 0, 1);
                x = (reference.Y * normal.Z) - (reference.Z * normal.Y);
                y = (reference.Z * normal.X) - (reference.X * normal.Z);
                z = (reference.X * normal.Y) - (reference.Y * normal.X);
                length = Math.Sqrt((x * x) + (y * y) + (z * z));
            }

            x /= length;
            y /= length;
            z /= length;
            return tg.CreateUnitVector(x, y, z);
        }

        private static Profile CreateProfileForHoles(PlanarSketch sketch)
        {
            try
            {
                return sketch.Profiles.AddForSolid(false);
            }
            catch
            {
                return sketch.Profiles.AddForSolid(true);
            }
        }

        private static void ApplyThroughAllExtent(CutDefinition cutDefinition)
        {
            try
            {
                cutDefinition.SetThroughAllExtent(PartFeatureExtentDirectionEnum.kNegativeExtentDirection);
                return;
            }
            catch
            {
            }

            try
            {
                cutDefinition.SetThroughAllExtent(PartFeatureExtentDirectionEnum.kPositiveExtentDirection);
                return;
            }
            catch
            {
            }

            cutDefinition.SetThroughAllExtent(PartFeatureExtentDirectionEnum.kSymmetricExtentDirection);
        }
    }
}
