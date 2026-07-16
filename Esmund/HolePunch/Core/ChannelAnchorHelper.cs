using System;
using System.Collections.Generic;
using Inventor;

namespace SkinChannelPunch.Core
{
    /// <summary>
    /// Channel cross-section in part space. Width is the ~1.5 in flange dimension.
    /// </summary>
    internal sealed class ChannelCrossSection
    {
        public Point CenterPart { get; set; }
        public UnitVector HeightAxisPart { get; set; }
        public UnitVector WidthAxisPart { get; set; }
        public double HeightIn { get; set; }
        public double WidthIn { get; set; }
        public double DepthIn { get; set; }
    }

    internal sealed class PunchTargetFrame
    {
        public ChannelCrossSection ChannelSection { get; set; }
        public FacePlanarGeometry FaceGeometry { get; set; }
        public Point RowAnchorPart { get; set; }
        public double SkinBottomIn { get; set; }
        public double SkinTopIn { get; set; }
        public string ChannelDescription { get; set; }
        public string SkinDescription { get; set; }
        /// <summary>How pattern Up was chosen (skin part +Z vs longest face edge).</summary>
        public string PatternUpSource { get; set; }
    }

    internal static class ChannelAnchorHelper
    {
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

            if (!TryAnalyzeChannel(app, channelOcc, out ChannelCrossSection section, out error))
            {
                return false;
            }

            if (!FacePlanarGeometryBuilder.TryAnalyze(app, partFace, out FacePlanarGeometry faceGeometry, out error))
            {
                return false;
            }

            Point skinFacePointPart = partFace.PointOnFace;
            if (!TryBuildRowAnchorPart(app, section, skinFacePointPart, out Point rowAnchorPart, out error))
            {
                return false;
            }

            FacePlanarGeometryBuilder.OrientAcrossTowardPoint(app, faceGeometry, rowAnchorPart);

            target = new PunchTargetFrame
            {
                ChannelSection = section,
                FaceGeometry = faceGeometry,
                RowAnchorPart = rowAnchorPart,
                SkinBottomIn = 0,
                SkinTopIn = faceGeometry.SpanIn,
                ChannelDescription = WallPartPropertiesHelper.DescribeOccurrence(channelOcc),
                SkinDescription = WallPartPropertiesHelper.DescribeOccurrence(skinOcc),
            };
            return true;
        }

        public static bool TryAnalyzeChannel(
            Application app,
            ComponentOccurrence channelOcc,
            out ChannelCrossSection section,
            out string error)
        {
            section = null;
            error = string.Empty;

            if (!(channelOcc.Definition is PartComponentDefinition partDefinition))
            {
                error = "Channel occurrence is not a part.";
                return false;
            }

            Box rangeBox = partDefinition.RangeBox;
            Point min = rangeBox.MinPoint;
            Point max = rangeBox.MaxPoint;
            double sizeX = max.X - min.X;
            double sizeY = max.Y - min.Y;
            double sizeZ = max.Z - min.Z;

            double targetWidthCm = PatternConstants.ChannelFlangeWidthIn * PatternConstants.InchesToCm;
            var dimensions = new List<(double SizeCm, UnitVector AxisPositive)>
            {
                (sizeX, app.TransientGeometry.CreateUnitVector(1, 0, 0)),
                (sizeY, app.TransientGeometry.CreateUnitVector(0, 1, 0)),
                (sizeZ, app.TransientGeometry.CreateUnitVector(0, 0, 1)),
            };

            dimensions.Sort((a, b) => b.SizeCm.CompareTo(a.SizeCm));
            double heightCm = dimensions[0].SizeCm;
            UnitVector heightAxis = dimensions[0].AxisPositive;

            int widthIndex = 1;
            double widthDelta = Math.Abs(dimensions[1].SizeCm - targetWidthCm);
            double depthDelta = Math.Abs(dimensions[2].SizeCm - targetWidthCm);
            if (depthDelta < widthDelta)
            {
                widthIndex = 2;
            }

            double widthCm = dimensions[widthIndex].SizeCm;
            UnitVector widthAxis = dimensions[widthIndex].AxisPositive;
            double depthCm = dimensions[3 - widthIndex].SizeCm;

            Point center = app.TransientGeometry.CreatePoint(
                (min.X + max.X) * 0.5,
                (min.Y + max.Y) * 0.5,
                (min.Z + max.Z) * 0.5);

            section = new ChannelCrossSection
            {
                CenterPart = center,
                HeightAxisPart = heightAxis,
                WidthAxisPart = widthAxis,
                HeightIn = heightCm / PatternConstants.InchesToCm,
                WidthIn = widthCm / PatternConstants.InchesToCm,
                DepthIn = depthCm / PatternConstants.InchesToCm,
            };
            return true;
        }

        private static bool TryBuildRowAnchorPart(
            Application app,
            ChannelCrossSection section,
            Point towardPointPart,
            out Point rowAnchorPart,
            out string error)
        {
            rowAnchorPart = null;
            error = string.Empty;

            TransientGeometry tg = app.TransientGeometry;
            Vector toTarget = tg.CreateVector(
                towardPointPart.X - section.CenterPart.X,
                towardPointPart.Y - section.CenterPart.Y,
                towardPointPart.Z - section.CenterPart.Z);

            UnitVector widthAxis = section.WidthAxisPart;
            if (Dot(toTarget, widthAxis) < 0)
            {
                widthAxis = Negate(app, widthAxis);
            }

            double halfFlangeCm = PatternConstants.FlangeHalfWidthIn * PatternConstants.InchesToCm;
            rowAnchorPart = tg.CreatePoint(
                section.CenterPart.X + (widthAxis.X * halfFlangeCm),
                section.CenterPart.Y + (widthAxis.Y * halfFlangeCm),
                section.CenterPart.Z + (widthAxis.Z * halfFlangeCm));
            return true;
        }

        private static UnitVector Negate(Application app, UnitVector vector)
        {
            return app.TransientGeometry.CreateUnitVector(-vector.X, -vector.Y, -vector.Z);
        }

        private static double Dot(Vector vector, UnitVector axis)
        {
            return (vector.X * axis.X) + (vector.Y * axis.Y) + (vector.Z * axis.Z);
        }
    }
}
