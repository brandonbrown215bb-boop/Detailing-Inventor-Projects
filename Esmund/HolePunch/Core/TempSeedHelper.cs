using System;
using Inventor;

namespace SkinChannelPunch.Core
{
    /// <summary>
    /// Disposable sheet-metal cut used only to establish a sketch plane when the skin
    /// has no existing seed circle. Placement of the real pattern does not use this cut.
    /// </summary>
    internal static class TempSeedHelper
    {
        public const string TempCutName = "HP_TEMP_SEED";

        public static bool TryCreate(
            Application app,
            PartDocument partDocument,
            SheetMetalComponentDefinition smcd,
            Face partFace,
            double holeDiameterIn,
            out SeedCircleRef seed,
            out CutFeature tempCut,
            out WorkPlane tempPlane,
            out string error)
        {
            seed = null;
            tempCut = null;
            tempPlane = null;
            error = string.Empty;

            Transaction transaction = null;
            try
            {
                FacePlanarGeometryBuilder.TryAnalyze(app, partFace, out FacePlanarGeometry faceGeometry, out _);

                transaction = app.TransactionManager.StartTransaction((_Document)partDocument, "Hole Punch temp seed");

                FaceSketchResult sketchResult = SheetMetalCutHelper.CreateSketchOnFace(
                    app,
                    smcd,
                    partFace,
                    faceGeometry);
                tempPlane = sketchResult.TemporaryWorkPlane;
                PlanarSketch sketch = sketchResult.Sketch;

                double radiusCm = Math.Max(1e-6, (holeDiameterIn * PatternConstants.InchesToCm) * 0.5);
                // Dummy location — only the plane matters; real holes use channel/face math.
                Point2d center = app.TransientGeometry.CreatePoint2d(
                    PatternConstants.InchesToCm,
                    PatternConstants.InchesToCm);
                SheetMetalCutHelper.AddCircle(sketch, center, radiusCm);

                int cutsBefore = CountCuts(smcd);
                SheetMetalCutHelper.CreateSingleCutFromSketch(smcd, sketch);
                tempCut = FindNewestCut(smcd, cutsBefore);
                if (tempCut != null)
                {
                    try
                    {
                        tempCut.Name = TempCutName;
                    }
                    catch
                    {
                        // Name is convenience only.
                    }
                }

                SketchCircle circle = sketch.SketchCircles.Count > 0 ? sketch.SketchCircles[1] : null;
                if (circle == null)
                {
                    transaction.Abort();
                    error = "Temporary seed sketch was created without a circle.";
                    TryDeleteQuiet(app, partDocument, tempCut, tempPlane);
                    tempCut = null;
                    tempPlane = null;
                    return false;
                }

                seed = new SeedCircleRef
                {
                    Sketch = sketch,
                    Circle = circle,
                    CenterPoint = circle.CenterSketchPoint,
                    CenterPart = sketch.SketchToModelSpace(circle.CenterSketchPoint.Geometry),
                    DiameterCm = Math.Max(1e-6, circle.Radius * 2.0),
                };

                transaction.End();
                partDocument.Update();
                return true;
            }
            catch (Exception ex)
            {
                transaction?.Abort();
                TryDeleteQuiet(app, partDocument, tempCut, tempPlane);
                tempCut = null;
                tempPlane = null;
                seed = null;
                error = "Could not create a temporary seed cut: " + ex.Message;
                return false;
            }
        }

        public static void TryDelete(
            Application app,
            PartDocument partDocument,
            CutFeature tempCut,
            WorkPlane tempPlane)
        {
            if (tempCut == null && tempPlane == null)
            {
                return;
            }

            Transaction transaction = null;
            try
            {
                transaction = app.TransactionManager.StartTransaction((_Document)partDocument, "Hole Punch delete temp seed");

                if (tempCut != null)
                {
                    try
                    {
                        tempCut.Delete();
                    }
                    catch
                    {
                        CutFeature byName = FindCutByName(
                            (SheetMetalComponentDefinition)partDocument.ComponentDefinition,
                            TempCutName);
                        byName?.Delete();
                    }
                }

                if (tempPlane != null)
                {
                    try
                    {
                        tempPlane.Delete();
                    }
                    catch
                    {
                        // Plane may already be consumed / deleted with the cut.
                    }
                }

                transaction.End();
                partDocument.Update();
            }
            catch
            {
                transaction?.Abort();
            }
        }

        private static void TryDeleteQuiet(
            Application app,
            PartDocument partDocument,
            CutFeature tempCut,
            WorkPlane tempPlane)
        {
            try
            {
                TryDelete(app, partDocument, tempCut, tempPlane);
            }
            catch
            {
            }
        }

        private static int CountCuts(SheetMetalComponentDefinition smcd)
        {
            SheetMetalFeatures smf = (SheetMetalFeatures)(object)smcd.Features;
            return smf.CutFeatures.Count;
        }

        private static CutFeature FindNewestCut(SheetMetalComponentDefinition smcd, int previousCount)
        {
            SheetMetalFeatures smf = (SheetMetalFeatures)(object)smcd.Features;
            CutFeatures cfs = smf.CutFeatures;
            if (cfs.Count <= previousCount)
            {
                return null;
            }

            return cfs[cfs.Count];
        }

        private static CutFeature FindCutByName(SheetMetalComponentDefinition smcd, string name)
        {
            SheetMetalFeatures smf = (SheetMetalFeatures)(object)smcd.Features;
            CutFeatures cfs = smf.CutFeatures;
            for (int i = 1; i <= cfs.Count; i++)
            {
                try
                {
                    CutFeature cut = cfs[i];
                    if (string.Equals(cut.Name, name, StringComparison.OrdinalIgnoreCase))
                    {
                        return cut;
                    }
                }
                catch
                {
                }
            }

            return null;
        }
    }
}
