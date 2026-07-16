using System.Collections.Generic;
using Inventor;

namespace SkinChannelPunch.Core
{
    internal sealed class SkinPartExtents
    {
        public double MinHeightCm { get; set; }
        public double MaxHeightCm { get; set; }
        public double MinWidthCm { get; set; }
        public double MaxWidthCm { get; set; }
        public double HeightIn { get; set; }
        public double WidthIn { get; set; }
    }

    /// <summary>
    /// Skin flat stock: height = largest range-box axis, width = middle axis (part space).
    /// </summary>
    internal static class SkinPartExtentsHelper
    {
        public static SkinPartExtents Measure(PartComponentDefinition partDefinition)
        {
            Box box = partDefinition.RangeBox;
            Point min = box.MinPoint;
            Point max = box.MaxPoint;
            double sizeX = max.X - min.X;
            double sizeY = max.Y - min.Y;
            double sizeZ = max.Z - min.Z;

            double size0 = sizeX;
            double size1 = sizeY;
            double size2 = sizeZ;
            int heightIndex = 0;
            if (size1 > size0) heightIndex = 1;
            if (size2 > (heightIndex == 0 ? size0 : heightIndex == 1 ? size1 : size0))
            {
                heightIndex = 2;
            }

            double heightCm = heightIndex == 0 ? sizeX : heightIndex == 1 ? sizeY : sizeZ;
            double minHeight = heightIndex == 0 ? min.X : heightIndex == 1 ? min.Y : min.Z;
            double maxHeight = heightIndex == 0 ? max.X : heightIndex == 1 ? max.Y : max.Z;

            int widthIndex = heightIndex == 0 ? (size1 >= size2 ? 1 : 2) : heightIndex == 1 ? (size0 >= size2 ? 0 : 2) : (size0 >= size1 ? 0 : 1);
            double widthCm = widthIndex == 0 ? sizeX : widthIndex == 1 ? sizeY : sizeZ;
            double minWidth = widthIndex == 0 ? min.X : widthIndex == 1 ? min.Y : min.Z;
            double maxWidth = widthIndex == 0 ? max.X : widthIndex == 1 ? max.Y : max.Z;

            return new SkinPartExtents
            {
                MinHeightCm = minHeight,
                MaxHeightCm = maxHeight,
                MinWidthCm = minWidth,
                MaxWidthCm = maxWidth,
                HeightIn = heightCm / PatternConstants.InchesToCm,
                WidthIn = widthCm / PatternConstants.InchesToCm,
            };
        }

        public static Point BuildModelPoint(
            Application app,
            int heightIndex,
            int widthIndex,
            double widthCoordCm,
            double heightCoordCm)
        {
            double x = 0;
            double y = 0;
            double z = 0;

            if (heightIndex == 0)
            {
                x = heightCoordCm;
            }
            else if (widthIndex == 0)
            {
                x = widthCoordCm;
            }

            if (heightIndex == 1)
            {
                y = heightCoordCm;
            }
            else if (widthIndex == 1)
            {
                y = widthCoordCm;
            }

            if (heightIndex == 2)
            {
                z = heightCoordCm;
            }
            else if (widthIndex == 2)
            {
                z = widthCoordCm;
            }

            return app.TransientGeometry.CreatePoint(x, y, z);
        }

        public static Point CopyPointWithHeightCoord(
            Application app,
            Point sourcePart,
            int heightIndex,
            double heightCoordCm)
        {
            double x = sourcePart.X;
            double y = sourcePart.Y;
            double z = sourcePart.Z;

            if (heightIndex == 0)
            {
                x = heightCoordCm;
            }
            else if (heightIndex == 1)
            {
                y = heightCoordCm;
            }
            else
            {
                z = heightCoordCm;
            }

            return app.TransientGeometry.CreatePoint(x, y, z);
        }

        public static double GetCoordCm(Point point, int axisIndex)
        {
            if (axisIndex == 0)
            {
                return point.X;
            }

            if (axisIndex == 1)
            {
                return point.Y;
            }

            return point.Z;
        }

        public static UnitVector GetUnitAxisPart(Application app, int axisIndex)
        {
            if (axisIndex == 0)
            {
                return app.TransientGeometry.CreateUnitVector(1, 0, 0);
            }

            if (axisIndex == 1)
            {
                return app.TransientGeometry.CreateUnitVector(0, 1, 0);
            }

            return app.TransientGeometry.CreateUnitVector(0, 0, 1);
        }

        public static void GetAxisIndices(PartComponentDefinition partDefinition, out int heightIndex, out int widthIndex)
        {
            Box box = partDefinition.RangeBox;
            double sizeX = box.MaxPoint.X - box.MinPoint.X;
            double sizeY = box.MaxPoint.Y - box.MinPoint.Y;
            double sizeZ = box.MaxPoint.Z - box.MinPoint.Z;

            heightIndex = 0;
            if (sizeY > sizeX) heightIndex = 1;
            double heightSize = heightIndex == 0 ? sizeX : heightIndex == 1 ? sizeY : sizeZ;
            if (sizeZ > heightSize) heightIndex = 2;

            widthIndex = heightIndex == 0 ? (sizeY >= sizeZ ? 1 : 2) : heightIndex == 1 ? (sizeX >= sizeZ ? 0 : 2) : (sizeX >= sizeY ? 0 : 1);
        }

        /// <summary>
        /// Pattern inset is measured up from the physical bottom of the wall (assembly Y-).
        /// On many skin parts the range-box minimum is not the physical bottom.
        /// </summary>
        public static bool ResolvePhysicalBottomAtMaxHeight(
            Application app,
            IReadOnlyList<ComponentOccurrence> skinChain,
            int heightIndex,
            int widthIndex,
            SkinPartExtents extents)
        {
            double widthMidCm = (extents.MinWidthCm + extents.MaxWidthCm) * 0.5;
            Point minHeightPart = BuildModelPoint(
                app,
                heightIndex,
                widthIndex,
                widthMidCm,
                extents.MinHeightCm);
            Point maxHeightPart = BuildModelPoint(
                app,
                heightIndex,
                widthIndex,
                widthMidCm,
                extents.MaxHeightCm);

            Point minRoot = OccurrenceTransformHelper.PartPointToRoot(app, skinChain, minHeightPart);
            Point maxRoot = OccurrenceTransformHelper.PartPointToRoot(app, skinChain, maxHeightPart);

            // ISG wall height follows assembly Y; lower Y is the physical bottom of the skin.
            return maxRoot.Y < minRoot.Y;
        }

        public static double HeightCmFromBottomInset(
            SkinPartExtents extents,
            bool physicalBottomAtMaxHeight,
            double insetFromBottomIn)
        {
            double insetCm = insetFromBottomIn * PatternConstants.InchesToCm;
            return physicalBottomAtMaxHeight
                ? extents.MaxHeightCm - insetCm
                : extents.MinHeightCm + insetCm;
        }
    }
}
