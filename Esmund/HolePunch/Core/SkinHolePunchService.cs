using System;
using System.Collections.Generic;
using System.Text;
using Inventor;

namespace SkinChannelPunch.Core
{
    internal sealed class SkinHolePunchResult
    {
        public int Created { get; set; }
        public int Failed { get; set; }
        public string Message { get; set; } = string.Empty;
        public int HoleCount { get; set; }
    }

    internal sealed class SkinHolePunchService
    {
        private readonly Application _app;

        public SkinHolePunchService(Application app)
        {
            _app = app;
        }

        public SkinHolePunchResult PunchVerticalPattern(
            AssemblyDocument assemblyDocument,
            ComponentOccurrence channelOcc,
            ComponentOccurrence skinOcc,
            Face skinFace,
            double bottomInsetIn,
            double topInsetIn,
            double maxCenterToCenterIn,
            double holeDiameterIn)
        {
            var result = new SkinHolePunchResult();

            if (!OccurrenceTransformHelper.TryGetChainFromRoot(assemblyDocument, skinOcc, out List<ComponentOccurrence> skinChain, out string chainError))
            {
                result.Message = chainError;
                return result;
            }

            if (!OccurrenceTransformHelper.TryGetChainFromRoot(assemblyDocument, channelOcc, out List<ComponentOccurrence> channelChain, out chainError))
            {
                result.Message = chainError;
                return result;
            }

            if (!AssemblyGeometryHelper.TryResolvePartFace(skinOcc, skinFace, out Face partFace, out string faceError))
            {
                result.Message = faceError;
                return result;
            }

            PartDocument partDocument = skinOcc.Definition.Document as PartDocument;
            if (partDocument == null)
            {
                result.Message = "Skin occurrence is not a part.";
                return result;
            }

            if (!(partDocument.ComponentDefinition is SheetMetalComponentDefinition smcd))
            {
                result.Message = "Skin part is not sheet metal.";
                return result;
            }

            CutFeature tempCut = null;
            WorkPlane tempPlane = null;
            bool usedTempSeed = false;

            try
            {
                if (!SeedSketchHelper.TryFindSeedForFace(smcd, partFace, out SeedCircleRef seed, out string seedError))
                {
                    if (!TempSeedHelper.TryCreate(
                        _app,
                        partDocument,
                        smcd,
                        partFace,
                        holeDiameterIn,
                        out seed,
                        out tempCut,
                        out tempPlane,
                        out string tempError))
                    {
                        result.Message = tempError;
                        return result;
                    }

                    usedTempSeed = true;

                    // Cut changes topology — refresh the part face from the assembly pick.
                    if (!AssemblyGeometryHelper.TryResolvePartFace(skinOcc, skinFace, out Face refreshedFace, out _))
                    {
                        // Keep prior face if proxy resolve fails; seed already holds PlanarEntity.
                    }
                    else
                    {
                        partFace = refreshedFace;
                    }
                }

                if (!AssemblyOriginAnchorHelper.TryBuildTarget(
                    _app,
                    channelOcc,
                    channelChain,
                    skinOcc,
                    skinChain,
                    partFace,
                    out PunchTargetFrame target,
                    out string frameError))
                {
                    result.Message = frameError;
                    return result;
                }

                VerticalPatternResult pattern = VerticalPatternCalculator.Compute(
                    target.SkinBottomIn,
                    target.SkinTopIn,
                    bottomInsetIn,
                    topInsetIn,
                    maxCenterToCenterIn);

                if (pattern.PositionsIn.Count == 0)
                {
                    result.Message = pattern.Message;
                    return result;
                }

                double rowAcrossIn = FacePlanarGeometryBuilder.GetAcrossCoordCm(_app, target.FaceGeometry, target.RowAnchorPart)
                    / PatternConstants.InchesToCm;

                result.HoleCount = pattern.PositionsIn.Count;

                Transaction transaction = null;
                try
                {
                    transaction = _app.TransactionManager.StartTransaction((_Document)partDocument, "Hole Punch");

                    // Seed supplies PlanarEntity only. UV frame MUST come from the new sketch —
                    // Sketches.Add(plane) often differs in origin/axes from an AddWithOrientation seed.
                    PlanarSketch sketch = SeedSketchHelper.CreateSketchFromSeed(smcd, seed.Sketch);
                    if (!SeedSketchPlacementHelper.TryBuildFrame(_app, sketch, out SketchPlaneFrame sketchFrame, out string sketchFrameError))
                    {
                        transaction.Abort();
                        result.Message = sketchFrameError;
                        return result;
                    }

                    double radiusCm = (holeDiameterIn * PatternConstants.InchesToCm) * 0.5;
                    string firstCircleError = string.Empty;

                    foreach (double holeUpIn in pattern.PositionsIn)
                    {
                        try
                        {
                            Point partPoint = FacePlanarGeometryBuilder.BuildHolePartPoint(
                                _app,
                                target.FaceGeometry,
                                target.RowAnchorPart,
                                holeUpIn);
                            Point2d skPoint = SeedSketchPlacementHelper.ModelPointToSketchCoords(sketchFrame, _app, partPoint);
                            SheetMetalCutHelper.AddCircle(sketch, skPoint, radiusCm);
                            result.Created++;
                        }
                        catch (Exception ex)
                        {
                            result.Failed++;
                            if (string.IsNullOrEmpty(firstCircleError))
                            {
                                firstCircleError = ex.Message;
                            }
                        }
                    }

                    if (result.Created == 0)
                    {
                        transaction.Abort();
                        result.Message = BuildFailureMessage(pattern.Message, result.Failed, firstCircleError);
                        return result;
                    }

                    try
                    {
                        SheetMetalCutHelper.CreateSingleCutFromSketch(smcd, sketch);
                    }
                    catch (Exception ex)
                    {
                        transaction.Abort();
                        result.Message =
                            $"{pattern.Message} Sketch had {result.Created} circle(s), but the cut failed: {ex.Message}";
                        return result;
                    }

                    transaction.End();
                    partDocument.Update();

                    var builder = new StringBuilder();
                    builder.Append(pattern.Message);
                    builder.Append(usedTempSeed ? " Temporary seed used then removed." : " Seed sketch reused.");
                    builder.Append(" Row across ");
                    builder.Append(rowAcrossIn.ToString("0.###"));
                    builder.Append(" in, face span ");
                    builder.Append(target.SkinTopIn.ToString("0.###"));
                    builder.Append(" in. One cut with ");
                    builder.Append(result.Created);
                    builder.Append(" hole(s).");
                    if (result.Failed > 0)
                    {
                        builder.Append(' ');
                        builder.Append(result.Failed);
                        builder.Append(" position(s) skipped.");
                    }

                    result.Message = builder.ToString();
                }
                catch (Exception ex)
                {
                    transaction?.Abort();
                    result.Message = $"Punch failed before cut creation: {ex.Message}";
                }
            }
            finally
            {
                if (usedTempSeed)
                {
                    // Delete only the disposable cut. Do not delete TemporaryWorkPlane —
                    // it may be the PlanarEntity of the real punch sketch when face-direct fails.
                    TempSeedHelper.TryDelete(_app, partDocument, tempCut, tempPlane: null);
                }
            }

            return result;
        }

        private static string BuildFailureMessage(string patternMessage, int failedCount, string firstCircleError)
        {
            var builder = new StringBuilder();
            builder.Append(patternMessage);
            builder.Append(" No cut was created (");
            builder.Append(failedCount);
            builder.Append(" hole position(s) failed).");

            if (!string.IsNullOrWhiteSpace(firstCircleError))
            {
                builder.Append(" First error: ");
                builder.Append(firstCircleError);
                builder.Append('.');
            }

            builder.Append(" Check channel/skin picks and face selection.");
            return builder.ToString();
        }
    }
}
