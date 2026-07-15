using System;
using System.Collections.Generic;
using Inventor;

namespace SkinChannelPunch.Core
{
    /// <summary>
    /// Finds the channel flange edge nearest the skin on the picked face, then offsets
    /// 0.75 in along face width (toward the channel) for the hole row.
    /// </summary>
    internal static class AssemblyOriginAnchorHelper
    {
        public static bool TryBuildRowAnchorPart(
            Application app,
            ComponentOccurrence channelOcc,
            IReadOnlyList<ComponentOccurrence> channelChain,
            ComponentOccurrence skinOcc,
            IReadOnlyList<ComponentOccurrence> skinChain,
            Face partFace,
            FacePlanarGeometry faceGeometry,
            out Point rowAnchorPart,
            out double rowAlongAcrossIn,
            out string error)
        {
            rowAnchorPart = null;
            rowAlongAcrossIn = 0;
            error = string.Empty;

            if (!ChannelAnchorHelper.TryAnalyzeChannel(app, channelOcc, out ChannelCrossSection section, out error))
            {
                return false;
            }

            Point channelCenterRoot = OccurrenceTransformHelper.PartPointToRoot(app, channelChain, section.CenterPart);
            UnitVector channelWidthRoot = OccurrenceTransformHelper.PartVectorToRoot(app, channelChain, section.WidthAxisPart);
            if (channelWidthRoot == null)
            {
                error = "Could not resolve channel width axis in assembly space.";
                return false;
            }

            double halfFlangeCm = PatternConstants.FlangeHalfWidthIn * PatternConstants.InchesToCm;
            Point flangeEdgePlusRoot = OffsetAlong(app, channelCenterRoot, channelWidthRoot, halfFlangeCm);
            Point flangeEdgeMinusRoot = OffsetAlong(app, channelCenterRoot, channelWidthRoot, -halfFlangeCm);

            Point channelCenterPart = OccurrenceTransformHelper.RootPointToPartSpace(app, skinChain, channelCenterRoot);
            Point flangePlusPart = OccurrenceTransformHelper.RootPointToPartSpace(app, skinChain, flangeEdgePlusRoot);
            Point flangeMinusPart = OccurrenceTransformHelper.RootPointToPartSpace(app, skinChain, flangeEdgeMinusRoot);

            FacePlanarGeometryBuilder.OrientAcrossTowardPoint(app, faceGeometry, channelCenterPart);

            if (!FacePlanarGeometryBuilder.TryMeasureAcrossSpanCm(
                app,
                partFace,
                faceGeometry,
                out double minAcrossCm,
                out double maxAcrossCm,
                out error))
            {
                return false;
            }

            double channelAcrossCm = FacePlanarGeometryBuilder.GetAcrossCoordCm(app, faceGeometry, channelCenterPart);
            double flangePlusAcrossCm = FacePlanarGeometryBuilder.GetAcrossCoordCm(app, faceGeometry, flangePlusPart);
            double flangeMinusAcrossCm = FacePlanarGeometryBuilder.GetAcrossCoordCm(app, faceGeometry, flangeMinusPart);

            bool channelNearMinAcross = Math.Abs(channelAcrossCm - minAcrossCm) <= Math.Abs(channelAcrossCm - maxAcrossCm);
            double skinFacingFlangeAcrossCm = channelNearMinAcross
                ? Math.Min(flangePlusAcrossCm, flangeMinusAcrossCm)
                : Math.Max(flangePlusAcrossCm, flangeMinusAcrossCm);

            double rowInsetCm = PatternConstants.RowInsetFromFlangeIn * PatternConstants.InchesToCm;
            double rowAcrossCm = channelNearMinAcross
                ? skinFacingFlangeAcrossCm + rowInsetCm
                : skinFacingFlangeAcrossCm - rowInsetCm;

            double rowUpCm = FacePlanarGeometryBuilder.GetUpCoordCm(app, faceGeometry, channelCenterPart);
            rowAnchorPart = FacePlanarGeometryBuilder.BuildPointFromFaceCoords(app, faceGeometry, rowUpCm, rowAcrossCm);
            rowAlongAcrossIn = rowAcrossCm / PatternConstants.InchesToCm;
            return true;
        }

        public static bool TryBuildTarget(
            Application app,
            ComponentOccurrence channelOcc,
            IReadOnlyList<ComponentOccurrence> channelChain,
            ComponentOccurrence skinOcc,
            IReadOnlyList<ComponentOccurrence> skinChain,
            Face partFace,
            out PunchTargetFrame target,
            out string error)
        {
            target = null;
            error = string.Empty;

            if (!FacePlanarGeometryBuilder.TryAnalyze(app, partFace, out FacePlanarGeometry faceGeometry, out error))
            {
                return false;
            }

            // Direction only: skin IPT part +Z (FloorZLength axis). Origin stays bottom vertex, not center.
            string patternUpSource = "longest face edge";
            UnitVector partZ = app.TransientGeometry.CreateUnitVector(0, 0, 1);
            if (FacePlanarGeometryBuilder.TryReorientUpFromPreferredDirection(
                    app,
                    partFace,
                    faceGeometry,
                    partZ,
                    out _))
            {
                patternUpSource = "skin IPT part +Z";
            }

            // Walls often map part +Z to assembly -Y; flip so 7" top inset is at physical top.
            FacePlanarGeometryBuilder.OrientUpTowardAssemblyPlusY(app, skinChain, partFace, faceGeometry);

            if (!TryBuildRowAnchorPart(
                app,
                channelOcc,
                channelChain,
                skinOcc,
                skinChain,
                partFace,
                faceGeometry,
                out Point rowAnchorPart,
                out _,
                out error))
            {
                return false;
            }

            if (!ChannelAnchorHelper.TryAnalyzeChannel(app, channelOcc, out ChannelCrossSection section, out error))
            {
                return false;
            }

            target = new PunchTargetFrame
            {
                ChannelSection = section,
                FaceGeometry = faceGeometry,
                RowAnchorPart = rowAnchorPart,
                SkinBottomIn = 0,
                SkinTopIn = faceGeometry.SpanIn,
                ChannelDescription = WallPartPropertiesHelper.DescribeOccurrence(channelOcc),
                SkinDescription = WallPartPropertiesHelper.DescribeOccurrence(skinOcc),
                PatternUpSource = patternUpSource,
            };
            return true;
        }

        private static Point OffsetAlong(Application app, Point origin, UnitVector axis, double distanceCm)
        {
            return app.TransientGeometry.CreatePoint(
                origin.X + (axis.X * distanceCm),
                origin.Y + (axis.Y * distanceCm),
                origin.Z + (axis.Z * distanceCm));
        }
    }
}
