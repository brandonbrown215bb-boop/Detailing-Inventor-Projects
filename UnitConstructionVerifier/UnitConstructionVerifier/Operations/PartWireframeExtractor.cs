using System;
using System.Collections.Generic;
using System.Globalization;
using Inventor;

namespace UnitConstructionVerifier.Operations
{
    internal static class PartWireframeExtractor
    {
        private const double StrokeToleranceCm = 0.02;
        private const int CircleSegments = 24;

        public static WireframeData? ExtractFromOccurrence(ComponentOccurrence occurrence)
        {
            if (occurrence == null)
            {
                return null;
            }

            try
            {
                if (occurrence.Definition?.Document is PartDocument partDoc)
                {
                    WireframeData? fromPart = ExtractFromPartDocument(partDoc);
                    if (fromPart != null)
                    {
                        return fromPart;
                    }
                }
            }
            catch
            {
            }

            try
            {
                return ExtractFromSurfaceBodies(occurrence.SurfaceBodies);
            }
            catch
            {
                return null;
            }
        }

        private static WireframeData? ExtractFromPartDocument(PartDocument partDoc)
        {
            var segments = new List<(double, double, double, double, double, double)>();
            var seenEdges = new HashSet<int>();
            var seenSketchSegments = new HashSet<string>(StringComparer.Ordinal);

            ExtractFromSurfaceBodiesInto(partDoc.ComponentDefinition.SurfaceBodies, seenEdges, segments);

            Application? app = partDoc.Parent as Application;
            CollectStandardHoleFeatures(partDoc.ComponentDefinition, app, seenEdges, seenSketchSegments, segments);

            if (partDoc.ComponentDefinition is SheetMetalComponentDefinition smDef)
            {
                CollectSheetMetalCutFeatureFaces(smDef, seenEdges, segments);
                CollectSheetMetalCutSketches(smDef, app, seenSketchSegments, segments);
            }

            if (segments.Count == 0)
            {
                return null;
            }

            ComputeBounds(segments, out double cx, out double cy, out double cz, out double radius);
            return new WireframeData(segments, cx, cy, cz, radius);
        }

        private static WireframeData? ExtractFromSurfaceBodies(SurfaceBodies bodies)
        {
            var segments = new List<(double, double, double, double, double, double)>();
            var seenEdges = new HashSet<int>();
            ExtractFromSurfaceBodiesInto(bodies, seenEdges, segments);

            if (segments.Count == 0)
            {
                return null;
            }

            ComputeBounds(segments, out double cx, out double cy, out double cz, out double radius);
            return new WireframeData(segments, cx, cy, cz, radius);
        }

        private static void ExtractFromSurfaceBodiesInto(
            SurfaceBodies bodies,
            HashSet<int> seenEdges,
            List<(double, double, double, double, double, double)> segments)
        {
            if (bodies == null || bodies.Count == 0)
            {
                return;
            }

            for (int b = 1; b <= bodies.Count; b++)
            {
                SurfaceBody body;
                try
                {
                    body = bodies[b];
                }
                catch
                {
                    continue;
                }

                CollectFaceLoopEdges(body, seenEdges, segments);
                CollectBodyEdges(body, seenEdges, segments);
            }
        }

        private static void CollectStandardHoleFeatures(
            PartComponentDefinition compDef,
            Application? app,
            HashSet<int> seenEdges,
            HashSet<string> seenSketchSegments,
            List<(double, double, double, double, double, double)> segments)
        {
            CollectInventorHoleFeatures(compDef, app, seenEdges, seenSketchSegments, segments);
            CollectCylindricalFaceHoleCircles(compDef, app, seenSketchSegments, segments);
            CollectCircularExtrudeCutProfiles(compDef, app, seenSketchSegments, segments);
        }

        /// <summary>
        /// Inventor HoleFeature placement (see Ce3 InventorSidecar TryFindClosestHoleSketchPoint).
        /// </summary>
        private static void CollectInventorHoleFeatures(
            PartComponentDefinition compDef,
            Application? app,
            HashSet<int> seenEdges,
            HashSet<string> seenSketchSegments,
            List<(double, double, double, double, double, double)> segments)
        {
            HoleFeatures holeFeatures;
            try
            {
                holeFeatures = compDef.Features.HoleFeatures;
            }
            catch
            {
                return;
            }

            var seenHoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 1; i <= holeFeatures.Count; i++)
            {
                HoleFeature hole;
                try
                {
                    hole = holeFeatures[i];
                }
                catch
                {
                    continue;
                }

                string holeKey;
                try
                {
                    holeKey = hole.Name;
                }
                catch
                {
                    holeKey = i.ToString(CultureInfo.InvariantCulture);
                }

                if (!seenHoles.Add(holeKey))
                {
                    continue;
                }

                try
                {
                    if (hole.Suppressed)
                    {
                        continue;
                    }
                }
                catch
                {
                }

                CollectHoleFeatureFaces(hole, seenEdges, segments);

                PlanarSketch? sketch = null;
                try
                {
                    sketch = hole.Sketch;
                }
                catch
                {
                }

                if (sketch == null)
                {
                    continue;
                }

                CollectSketchWireframe(sketch, app, seenSketchSegments, segments);
                CollectHoleCenterPointCircles(hole, sketch, app, seenSketchSegments, segments);
            }
        }

        private static void CollectHoleCenterPointCircles(
            HoleFeature hole,
            PlanarSketch sketch,
            Application? app,
            HashSet<string> seenSketchSegments,
            List<(double, double, double, double, double, double)> segments)
        {
            if (!TryGetHoleDiameterCm(hole, out double diameterCm) || diameterCm <= 1e-6)
            {
                return;
            }

            double radius = diameterCm * 0.5;
            if (!TryGetSketchPlaneBasis(sketch, app, out _, out _, out _,
                    out double uxX, out double uxY, out double uxZ,
                    out double uyX, out double uyY, out double uyZ))
            {
                return;
            }

            ObjectCollection? centers;
            try
            {
                centers = hole.HoleCenterPoints;
            }
            catch
            {
                centers = null;
            }

            if (centers != null && centers.Count > 0)
            {
                for (int j = 1; j <= centers.Count; j++)
                {
                    if (centers[j] is not SketchPoint centerPoint)
                    {
                        continue;
                    }

                    try
                    {
                        Point center = sketch.SketchToModelSpace(centerPoint.Geometry);
                        AddCircleSegments(
                            center.X,
                            center.Y,
                            center.Z,
                            radius,
                            uxX,
                            uxY,
                            uxZ,
                            uyX,
                            uyY,
                            uyZ,
                            seenSketchSegments,
                            segments);
                    }
                    catch
                    {
                    }
                }

                return;
            }

            CollectHolePlacementCirclesFromSketchCircles(hole, sketch, radius, app, seenSketchSegments, segments);
        }

        private static void CollectHolePlacementCirclesFromSketchCircles(
            HoleFeature hole,
            PlanarSketch sketch,
            double radius,
            Application? app,
            HashSet<string> seenSketchSegments,
            List<(double, double, double, double, double, double)> segments)
        {
            if (!TryGetSketchPlaneBasis(sketch, app, out _, out _, out _,
                    out double uxX, out double uxY, out double uxZ,
                    out double uyX, out double uyY, out double uyZ))
            {
                return;
            }

            SketchCircles circles;
            try
            {
                circles = sketch.SketchCircles;
            }
            catch
            {
                return;
            }

            for (int i = 1; i <= circles.Count; i++)
            {
                SketchCircle circle;
                try
                {
                    circle = circles[i];
                }
                catch
                {
                    continue;
                }

                try
                {
                    Point center = sketch.SketchToModelSpace(circle.CenterSketchPoint.Geometry);
                    AddCircleSegments(
                        center.X,
                        center.Y,
                        center.Z,
                        radius,
                        uxX,
                        uxY,
                        uxZ,
                        uyX,
                        uyY,
                        uyZ,
                        seenSketchSegments,
                        segments);
                }
                catch
                {
                }
            }
        }

        /// <summary>
        /// Cylindrical BRep faces (Ce3 InventorSidecar CollectCylindricalHoleCandidates).
        /// </summary>
        private static void CollectCylindricalFaceHoleCircles(
            PartComponentDefinition compDef,
            Application? app,
            HashSet<string> seenSketchSegments,
            List<(double, double, double, double, double, double)> segments)
        {
            var raw = new List<(double x, double y, double z, double d, double ax, double ay, double az)>();
            CollectCylindricalFaceCandidates(compDef, raw);

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach ((double x, double y, double z, double d, double ax, double ay, double az) in raw)
            {
                string key = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0:0.####}:{1:0.####}:{2:0.####}:{3:0.####}",
                    Math.Round(x, 4),
                    Math.Round(y, 4),
                    Math.Round(z, 4),
                    Math.Round(d, 4));
                if (!seen.Add(key))
                {
                    continue;
                }

                if (!TryGetCircleBasisFromAxis(ax, ay, az, out double uxX, out double uxY, out double uxZ, out double uyX, out double uyY, out double uyZ))
                {
                    continue;
                }

                AddCircleSegments(
                    x,
                    y,
                    z,
                    d * 0.5,
                    uxX,
                    uxY,
                    uxZ,
                    uyX,
                    uyY,
                    uyZ,
                    seenSketchSegments,
                    segments);
            }
        }

        private static void CollectCylindricalFaceCandidates(
            PartComponentDefinition compDef,
            List<(double x, double y, double z, double d, double ax, double ay, double az)> raw)
        {
            SurfaceBodies bodies = compDef.SurfaceBodies;
            for (int bi = 1; bi <= bodies.Count; bi++)
            {
                SurfaceBody body;
                try
                {
                    body = bodies[bi];
                }
                catch
                {
                    continue;
                }

                Faces faces;
                try
                {
                    faces = body.Faces;
                }
                catch
                {
                    continue;
                }

                for (int fi = 1; fi <= faces.Count; fi++)
                {
                    Face face;
                    try
                    {
                        face = faces[fi];
                    }
                    catch
                    {
                        continue;
                    }

                    try
                    {
                        if (face.SurfaceType != SurfaceTypeEnum.kCylinderSurface)
                        {
                            continue;
                        }
                    }
                    catch
                    {
                        continue;
                    }

                    if (face.Geometry is not Cylinder cyl)
                    {
                        continue;
                    }

                    double radiusCm = TryGetCylinderRadiusCm(cyl);
                    if (radiusCm <= 1e-6)
                    {
                        continue;
                    }

                    GetCylindricalFaceReferencePointCm(face, cyl, out double xCm, out double yCm, out double zCm);
                    TryGetAxisVector(cyl, out double ax, out double ay, out double az);
                    raw.Add((xCm, yCm, zCm, radiusCm * 2.0, ax, ay, az));
                }
            }
        }

        /// <summary>
        /// Circular cut extrudes (ISG HasHoles pattern) — common on sheet metal without HoleFeature.
        /// </summary>
        private static void CollectCircularExtrudeCutProfiles(
            PartComponentDefinition compDef,
            Application? app,
            HashSet<string> seenSketchSegments,
            List<(double, double, double, double, double, double)> segments)
        {
            ExtrudeFeatures extrudes;
            try
            {
                extrudes = compDef.Features.ExtrudeFeatures;
            }
            catch
            {
                return;
            }

            for (int i = 1; i <= extrudes.Count; i++)
            {
                ExtrudeFeature extrude;
                try
                {
                    extrude = extrudes[i];
                }
                catch
                {
                    continue;
                }

                try
                {
                    if (extrude.Suppressed || extrude.Operation != PartFeatureOperationEnum.kCutOperation)
                    {
                        continue;
                    }
                }
                catch
                {
                    continue;
                }

                Profile? profile;
                try
                {
                    profile = extrude.Definition.Profile;
                }
                catch
                {
                    continue;
                }

                if (profile?.Parent is not PlanarSketch sketch)
                {
                    continue;
                }

                CollectSketchWireframe(sketch, app, seenSketchSegments, segments);

                for (int p = 1; p <= profile.Count; p++)
                {
                    ProfilePath path;
                    try
                    {
                        path = profile[p];
                    }
                    catch
                    {
                        continue;
                    }

                    for (int e = 1; e <= path.Count; e++)
                    {
                        ProfileEntity entity;
                        try
                        {
                            entity = path[e];
                        }
                        catch
                        {
                            continue;
                        }

                        try
                        {
                            if (entity.Curve is Circle2d)
                            {
                                CollectSketchEntityWireframe(
                                    entity.SketchEntity,
                                    sketch,
                                    app,
                                    seenSketchSegments,
                                    segments);
                            }
                        }
                        catch
                        {
                        }
                    }
                }
            }
        }

        private static bool TryGetHoleDiameterCm(HoleFeature hole, out double diameterCm)
        {
            diameterCm = 0;
            try
            {
                object diameterValue = hole.HoleDiameter.Value;
                diameterCm = diameterValue is double d
                    ? d
                    : Convert.ToDouble(diameterValue, CultureInfo.InvariantCulture);
                return diameterCm > 1e-6;
            }
            catch
            {
                return false;
            }
        }

        private static double TryGetCylinderRadiusCm(Cylinder cyl)
        {
            try
            {
                object radius = cyl.Radius;
                if (radius is Parameter parameter)
                {
                    return Convert.ToDouble(parameter.Value, CultureInfo.InvariantCulture);
                }

                return Convert.ToDouble(radius, CultureInfo.InvariantCulture);
            }
            catch
            {
                return 0;
            }
        }

        private static void GetCylindricalFaceReferencePointCm(Face face, Cylinder cyl, out double xCm, out double yCm, out double zCm)
        {
            try
            {
                Box rangeBox = face.Evaluator.RangeBox;
                Point min = rangeBox.MinPoint;
                Point max = rangeBox.MaxPoint;
                xCm = (min.X + max.X) * 0.5;
                yCm = (min.Y + max.Y) * 0.5;
                zCm = (min.Z + max.Z) * 0.5;
                return;
            }
            catch
            {
            }

            try
            {
                Point basePoint = cyl.BasePoint;
                xCm = basePoint.X;
                yCm = basePoint.Y;
                zCm = basePoint.Z;
            }
            catch
            {
                xCm = yCm = zCm = 0;
            }
        }

        private static bool TryGetAxisVector(Cylinder cyl, out double ax, out double ay, out double az)
        {
            ax = ay = az = 0;
            try
            {
                UnitVector axis = cyl.AxisVector;
                ax = axis.X;
                ay = axis.Y;
                az = axis.Z;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryGetCircleBasisFromAxis(
            double ax,
            double ay,
            double az,
            out double uxX,
            out double uxY,
            out double uxZ,
            out double uyX,
            out double uyY,
            out double uyZ)
        {
            Normalize(ax, ay, az, out double nx, out double ny, out double nz);
            if (Math.Abs(nx) < 1e-9 && Math.Abs(ny) < 1e-9 && Math.Abs(nz) < 1e-9)
            {
                uxX = uxY = uxZ = uyX = uyY = uyZ = 0;
                return false;
            }

            double refX = Math.Abs(nx) < 0.9 ? 1 : 0;
            double refY = Math.Abs(nx) < 0.9 ? 0 : 1;
            double refZ = 0;

            uxX = (refY * nz) - (refZ * ny);
            uxY = (refZ * nx) - (refX * nz);
            uxZ = (refX * ny) - (refY * nx);
            Normalize(uxX, uxY, uxZ, out uxX, out uxY, out uxZ);

            uyX = (ny * uxZ) - (nz * uxY);
            uyY = (nz * uxX) - (nx * uxZ);
            uyZ = (nx * uxY) - (ny * uxX);
            Normalize(uyX, uyY, uyZ, out uyX, out uyY, out uyZ);
            return true;
        }

        private static void CollectHoleFeatureFaces(
            HoleFeature hole,
            HashSet<int> seenEdges,
            List<(double, double, double, double, double, double)> segments)
        {
            Faces faces;
            try
            {
                faces = hole.Faces;
            }
            catch
            {
                return;
            }

            CollectFeatureFaces(faces, seenEdges, segments);
        }

        private static void CollectFeatureFaces(
            Faces faces,
            HashSet<int> seenEdges,
            List<(double, double, double, double, double, double)> segments)
        {
            for (int f = 1; f <= faces.Count; f++)
            {
                Face face;
                try
                {
                    face = faces[f];
                }
                catch
                {
                    continue;
                }

                CollectFaceEdges(face, seenEdges, segments);
            }
        }

        private static void CollectSheetMetalCutFeatureFaces(
            SheetMetalComponentDefinition smDef,
            HashSet<int> seenEdges,
            List<(double, double, double, double, double, double)> segments)
        {
            try
            {
                SheetMetalFeatures smf = (SheetMetalFeatures)(object)smDef.Features;
                CutFeatures cutFeatures = smf.CutFeatures;
                for (int i = 1; i <= cutFeatures.Count; i++)
                {
                    CutFeature cut;
                    try
                    {
                        cut = cutFeatures[i];
                    }
                    catch
                    {
                        continue;
                    }

                    try
                    {
                        if (cut.Suppressed)
                        {
                            continue;
                        }
                    }
                    catch
                    {
                    }

                    CollectCutFeatureFaces(cut, seenEdges, segments);
                }
            }
            catch
            {
            }
        }

        private static void CollectSheetMetalCutSketches(
            SheetMetalComponentDefinition smDef,
            Application? app,
            HashSet<string> seenSketchSegments,
            List<(double, double, double, double, double, double)> segments)
        {
            try
            {
                SheetMetalFeatures smf = (SheetMetalFeatures)(object)smDef.Features;
                CutFeatures cutFeatures = smf.CutFeatures;
                for (int i = 1; i <= cutFeatures.Count; i++)
                {
                    CutFeature cut;
                    try
                    {
                        cut = cutFeatures[i];
                    }
                    catch
                    {
                        continue;
                    }

                    try
                    {
                        if (cut.Suppressed)
                        {
                            continue;
                        }
                    }
                    catch
                    {
                    }

                    if (cut.Definition is not CutDefinition cutDefinition)
                    {
                        continue;
                    }

                    Profile? profile = cutDefinition.Profile;
                    if (profile == null)
                    {
                        continue;
                    }

                    PlanarSketch? sketch = profile.Parent as PlanarSketch;
                    if (sketch != null)
                    {
                        CollectSketchWireframe(sketch, app, seenSketchSegments, segments);
                    }

                    if (sketch != null)
                    {
                        CollectCutProfileEntities(profile, sketch, app, seenSketchSegments, segments);
                    }
                }
            }
            catch
            {
            }
        }

        private static void CollectCutProfileEntities(
            Profile profile,
            PlanarSketch sketch,
            Application? app,
            HashSet<string> seenSketchSegments,
            List<(double, double, double, double, double, double)> segments)
        {
            for (int p = 1; p <= profile.Count; p++)
            {
                ProfilePath path;
                try
                {
                    path = profile[p];
                }
                catch
                {
                    continue;
                }

                for (int i = 1; i <= path.Count; i++)
                {
                    ProfileEntity profileEntity;
                    try
                    {
                        profileEntity = path[i];
                    }
                    catch
                    {
                        continue;
                    }

                    try
                    {
                        CollectSketchEntityWireframe(
                            profileEntity.SketchEntity,
                            sketch,
                            app,
                            seenSketchSegments,
                            segments);
                    }
                    catch
                    {
                    }
                }
            }
        }

        private static void CollectSketchEntityWireframe(
            SketchEntity entity,
            PlanarSketch sketch,
            Application? app,
            HashSet<string> seenSketchSegments,
            List<(double, double, double, double, double, double)> segments)
        {
            if (entity is SketchLine line)
            {
                if (TryAddSketchLine3d(line, seenSketchSegments, segments))
                {
                    return;
                }

                try
                {
                    AddModelSegment(
                        sketch.SketchToModelSpace(line.StartSketchPoint.Geometry),
                        sketch.SketchToModelSpace(line.EndSketchPoint.Geometry),
                        seenSketchSegments,
                        segments);
                }
                catch
                {
                }

                return;
            }

            if (entity is SketchCircle circle)
            {
                if (!TryGetSketchPlaneBasis(sketch, app, out _, out _, out _,
                        out double uxX, out double uxY, out double uxZ,
                        out double uyX, out double uyY, out double uyZ))
                {
                    return;
                }

                try
                {
                    Point center = sketch.SketchToModelSpace(circle.CenterSketchPoint.Geometry);
                    AddCircleSegments(
                        center.X, center.Y, center.Z,
                        circle.Radius,
                        uxX, uxY, uxZ,
                        uyX, uyY, uyZ,
                        seenSketchSegments,
                        segments);
                }
                catch
                {
                }

                return;
            }

            if (entity is SketchArc arc)
            {
                try
                {
                    Point start = sketch.SketchToModelSpace(arc.StartSketchPoint.Geometry);
                    Point end = sketch.SketchToModelSpace(arc.EndSketchPoint.Geometry);
                    Point center = sketch.SketchToModelSpace(arc.CenterSketchPoint.Geometry);
                    AddArcSegments(start, end, center, seenSketchSegments, segments);
                }
                catch
                {
                }
            }
        }

        private static void CollectCutFeatureFaces(
            CutFeature cut,
            HashSet<int> seenEdges,
            List<(double, double, double, double, double, double)> segments)
        {
            Faces faces;
            try
            {
                faces = cut.Faces;
            }
            catch
            {
                return;
            }

            CollectFeatureFaces(faces, seenEdges, segments);
        }

        private static void CollectSketchWireframe(
            PlanarSketch sketch,
            Application? app,
            HashSet<string> seenSketchSegments,
            List<(double, double, double, double, double, double)> segments)
        {
            CollectSketchLines(sketch, seenSketchSegments, segments);
            CollectSketchCircles(sketch, app, seenSketchSegments, segments);
            CollectSketchArcs(sketch, seenSketchSegments, segments);
        }

        private static void CollectSketchLines(
            PlanarSketch sketch,
            HashSet<string> seenSketchSegments,
            List<(double, double, double, double, double, double)> segments)
        {
            SketchLines lines;
            try
            {
                lines = sketch.SketchLines;
            }
            catch
            {
                return;
            }

            for (int i = 1; i <= lines.Count; i++)
            {
                SketchLine line;
                try
                {
                    line = lines[i];
                }
                catch
                {
                    continue;
                }

                if (TryAddSketchLine3d(line, seenSketchSegments, segments))
                {
                    continue;
                }

                try
                {
                    AddModelSegment(
                        sketch.SketchToModelSpace(line.StartSketchPoint.Geometry),
                        sketch.SketchToModelSpace(line.EndSketchPoint.Geometry),
                        seenSketchSegments,
                        segments);
                }
                catch
                {
                }
            }
        }

        private static void CollectSketchCircles(
            PlanarSketch sketch,
            Application? app,
            HashSet<string> seenSketchSegments,
            List<(double, double, double, double, double, double)> segments)
        {
            SketchCircles circles;
            try
            {
                circles = sketch.SketchCircles;
            }
            catch
            {
                return;
            }

            if (!TryGetSketchPlaneBasis(sketch, app, out double ox, out double oy, out double oz,
                    out double uxX, out double uxY, out double uxZ,
                    out double uyX, out double uyY, out double uyZ))
            {
                return;
            }

            for (int i = 1; i <= circles.Count; i++)
            {
                SketchCircle circle;
                try
                {
                    circle = circles[i];
                }
                catch
                {
                    continue;
                }

                double radius;
                Point center;
                try
                {
                    radius = circle.Radius;
                    center = sketch.SketchToModelSpace(circle.CenterSketchPoint.Geometry);
                }
                catch
                {
                    continue;
                }

                AddCircleSegments(
                    center.X, center.Y, center.Z,
                    radius,
                    uxX, uxY, uxZ,
                    uyX, uyY, uyZ,
                    seenSketchSegments,
                    segments);
            }
        }

        private static void CollectSketchArcs(
            PlanarSketch sketch,
            HashSet<string> seenSketchSegments,
            List<(double, double, double, double, double, double)> segments)
        {
            SketchArcs arcs;
            try
            {
                arcs = sketch.SketchArcs;
            }
            catch
            {
                return;
            }

            for (int i = 1; i <= arcs.Count; i++)
            {
                SketchArc arc;
                try
                {
                    arc = arcs[i];
                }
                catch
                {
                    continue;
                }

                try
                {
                    Point start = sketch.SketchToModelSpace(arc.StartSketchPoint.Geometry);
                    Point end = sketch.SketchToModelSpace(arc.EndSketchPoint.Geometry);
                    Point center = sketch.SketchToModelSpace(arc.CenterSketchPoint.Geometry);
                    AddArcSegments(start, end, center, seenSketchSegments, segments);
                }
                catch
                {
                }
            }
        }

        private static bool TryAddSketchLine3d(
            SketchLine line,
            HashSet<string> seenSketchSegments,
            List<(double, double, double, double, double, double)> segments)
        {
            try
            {
                object geometry3d = line.Geometry3d;
                if (geometry3d == null)
                {
                    return false;
                }

                dynamic seg = geometry3d;
                Point start = seg.StartPoint;
                Point end = seg.EndPoint;
                AddModelSegment(start, end, seenSketchSegments, segments);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryGetSketchPlaneBasis(
            PlanarSketch sketch,
            Application? app,
            out double ox,
            out double oy,
            out double oz,
            out double uxX,
            out double uxY,
            out double uxZ,
            out double uyX,
            out double uyY,
            out double uyZ)
        {
            ox = oy = oz = 0;
            uxX = uxY = uxZ = 0;
            uyX = uyY = uyZ = 0;

            if (app == null)
            {
                return false;
            }

            try
            {
                TransientGeometry tg = app.TransientGeometry;
                Point origin = sketch.SketchToModelSpace(tg.CreatePoint2d(0, 0));
                Point xTip = sketch.SketchToModelSpace(tg.CreatePoint2d(1, 0));
                Point yTip = sketch.SketchToModelSpace(tg.CreatePoint2d(0, 1));

                ox = origin.X;
                oy = origin.Y;
                oz = origin.Z;
                Normalize(xTip.X - origin.X, xTip.Y - origin.Y, xTip.Z - origin.Z, out uxX, out uxY, out uxZ);
                Normalize(yTip.X - origin.X, yTip.Y - origin.Y, yTip.Z - origin.Z, out uyX, out uyY, out uyZ);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void AddCircleSegments(
            double cx,
            double cy,
            double cz,
            double radius,
            double uxX,
            double uxY,
            double uxZ,
            double uyX,
            double uyY,
            double uyZ,
            HashSet<string> seenSketchSegments,
            List<(double, double, double, double, double, double)> segments)
        {
            double prevX = 0;
            double prevY = 0;
            double prevZ = 0;

            for (int i = 0; i <= CircleSegments; i++)
            {
                double angle = (Math.PI * 2.0 * i) / CircleSegments;
                double localX = radius * Math.Cos(angle);
                double localY = radius * Math.Sin(angle);
                double x = cx + (localX * uxX) + (localY * uyX);
                double y = cy + (localX * uxY) + (localY * uyY);
                double z = cz + (localX * uxZ) + (localY * uyZ);

                if (i > 0)
                {
                    TryAddSketchSegment(prevX, prevY, prevZ, x, y, z, seenSketchSegments, segments);
                }

                prevX = x;
                prevY = y;
                prevZ = z;
            }
        }

        private static void AddArcSegments(
            Point start,
            Point end,
            Point center,
            HashSet<string> seenSketchSegments,
            List<(double, double, double, double, double, double)> segments)
        {
            double radius = Distance(start.X, start.Y, start.Z, center.X, center.Y, center.Z);
            if (radius <= 1e-6)
            {
                AddModelSegment(start, end, seenSketchSegments, segments);
                return;
            }

            Normalize(start.X - center.X, start.Y - center.Y, start.Z - center.Z,
                out double sx, out double sy, out double sz);
            Normalize(end.X - center.X, end.Y - center.Y, end.Z - center.Z,
                out double ex, out double ey, out double ez);

            int steps = Math.Max(6, (int)(CircleSegments * 0.5));
            double prevX = start.X;
            double prevY = start.Y;
            double prevZ = start.Z;

            for (int i = 1; i <= steps; i++)
            {
                double t = (double)i / steps;
                double bx = (sx * (1 - t)) + (ex * t);
                double by = (sy * (1 - t)) + (ey * t);
                double bz = (sz * (1 - t)) + (ez * t);
                Normalize(bx, by, bz, out bx, out by, out bz);

                double x = center.X + (bx * radius);
                double y = center.Y + (by * radius);
                double z = center.Z + (bz * radius);
                TryAddSketchSegment(prevX, prevY, prevZ, x, y, z, seenSketchSegments, segments);
                prevX = x;
                prevY = y;
                prevZ = z;
            }
        }

        private static void AddModelSegment(
            Point start,
            Point end,
            HashSet<string> seenSketchSegments,
            List<(double, double, double, double, double, double)> segments)
        {
            TryAddSketchSegment(start.X, start.Y, start.Z, end.X, end.Y, end.Z, seenSketchSegments, segments);
        }

        private static void TryAddSketchSegment(
            double x1,
            double y1,
            double z1,
            double x2,
            double y2,
            double z2,
            HashSet<string> seenSketchSegments,
            List<(double, double, double, double, double, double)> segments)
        {
            if (Distance(x1, y1, z1, x2, y2, z2) <= 1e-6)
            {
                return;
            }

            string key = BuildSegmentKey(x1, y1, z1, x2, y2, z2);
            if (!seenSketchSegments.Add(key))
            {
                return;
            }

            segments.Add((x1, y1, z1, x2, y2, z2));
        }

        private static string BuildSegmentKey(
            double x1,
            double y1,
            double z1,
            double x2,
            double y2,
            double z2)
        {
            string a = FormatKeyPoint(x1, y1, z1);
            string b = FormatKeyPoint(x2, y2, z2);
            return string.CompareOrdinal(a, b) <= 0 ? a + "|" + b : b + "|" + a;
        }

        private static string FormatKeyPoint(double x, double y, double z)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0:0.###},{1:0.###},{2:0.###}",
                x,
                y,
                z);
        }

        private static void Normalize(
            double x,
            double y,
            double z,
            out double nx,
            out double ny,
            out double nz)
        {
            double length = Math.Sqrt((x * x) + (y * y) + (z * z));
            if (length <= 1e-9)
            {
                nx = ny = nz = 0;
                return;
            }

            nx = x / length;
            ny = y / length;
            nz = z / length;
        }

        private static void CollectFaceEdges(
            Face face,
            HashSet<int> seenEdges,
            List<(double, double, double, double, double, double)> segments)
        {
            EdgeLoops loops;
            try
            {
                loops = face.EdgeLoops;
            }
            catch
            {
                return;
            }

            for (int l = 1; l <= loops.Count; l++)
            {
                EdgeLoop loop;
                try
                {
                    loop = loops[l];
                }
                catch
                {
                    continue;
                }

                Edges edges;
                try
                {
                    edges = loop.Edges;
                }
                catch
                {
                    continue;
                }

                for (int e = 1; e <= edges.Count; e++)
                {
                    Edge edge;
                    try
                    {
                        edge = edges[e];
                    }
                    catch
                    {
                        continue;
                    }

                    if (!TryTrackEdge(edge, seenEdges))
                    {
                        continue;
                    }

                    AddEdgeSegments(edge, segments);
                }
            }
        }

        private static void CollectFaceLoopEdges(
            SurfaceBody body,
            HashSet<int> seenEdges,
            List<(double, double, double, double, double, double)> segments)
        {
            Faces faces;
            try
            {
                faces = body.Faces;
            }
            catch
            {
                return;
            }

            for (int f = 1; f <= faces.Count; f++)
            {
                Face face;
                try
                {
                    face = faces[f];
                }
                catch
                {
                    continue;
                }

                CollectFaceEdges(face, seenEdges, segments);
            }
        }

        private static void CollectBodyEdges(
            SurfaceBody body,
            HashSet<int> seenEdges,
            List<(double, double, double, double, double, double)> segments)
        {
            Edges edges;
            try
            {
                edges = body.Edges;
            }
            catch
            {
                return;
            }

            for (int i = 1; i <= edges.Count; i++)
            {
                Edge edge;
                try
                {
                    edge = edges[i];
                }
                catch
                {
                    continue;
                }

                if (!TryTrackEdge(edge, seenEdges))
                {
                    continue;
                }

                AddEdgeSegments(edge, segments);
            }
        }

        private static bool TryTrackEdge(Edge edge, HashSet<int> seenEdges)
        {
            try
            {
                return seenEdges.Add(edge.TransientKey);
            }
            catch
            {
                return true;
            }
        }

        private static void AddEdgeSegments(
            Edge edge,
            List<(double, double, double, double, double, double)> segments)
        {
            if (TryAddStrokeSegments(edge, StrokeToleranceCm, segments))
            {
                return;
            }

            if (TryAddStrokeSegments(edge, StrokeToleranceCm * 0.25, segments))
            {
                return;
            }

            if (TryGetVertexPoint(edge.StartVertex, out double x1, out double y1, out double z1) &&
                TryGetVertexPoint(edge.StopVertex, out double x2, out double y2, out double z2))
            {
                if (Distance(x1, y1, z1, x2, y2, z2) > 1e-6)
                {
                    segments.Add((x1, y1, z1, x2, y2, z2));
                }
            }
        }

        private static bool TryAddStrokeSegments(
            Edge edge,
            double tolerance,
            List<(double, double, double, double, double, double)> segments)
        {
            try
            {
                CurveEvaluator evaluator = edge.Evaluator;
                evaluator.GetParamExtents(out double startParam, out double endParam);
                evaluator.GetStrokes(startParam, endParam, tolerance, out int _, out double[] strokes);
                if (strokes == null || strokes.Length < 6)
                {
                    return false;
                }

                for (int i = 0; i + 5 < strokes.Length; i += 3)
                {
                    segments.Add((
                        strokes[i + 0],
                        strokes[i + 1],
                        strokes[i + 2],
                        strokes[i + 3],
                        strokes[i + 4],
                        strokes[i + 5]));
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryGetVertexPoint(Vertex vertex, out double x, out double y, out double z)
        {
            x = y = z = 0;

            if (vertex == null)
            {
                return false;
            }

            try
            {
                Point point = vertex.Point;
                x = point.X;
                y = point.Y;
                z = point.Z;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static double Distance(double x1, double y1, double z1, double x2, double y2, double z2)
        {
            double dx = x2 - x1;
            double dy = y2 - y1;
            double dz = z2 - z1;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        private static void ComputeBounds(
            List<(double X1, double Y1, double Z1, double X2, double Y2, double Z2)> segments,
            out double cx,
            out double cy,
            out double cz,
            out double radius)
        {
            double minX = double.MaxValue;
            double minY = double.MaxValue;
            double minZ = double.MaxValue;
            double maxX = double.MinValue;
            double maxY = double.MinValue;
            double maxZ = double.MinValue;

            foreach ((double x1, double y1, double z1, double x2, double y2, double z2) in segments)
            {
                UpdateMinMax(x1, y1, z1, ref minX, ref minY, ref minZ, ref maxX, ref maxY, ref maxZ);
                UpdateMinMax(x2, y2, z2, ref minX, ref minY, ref minZ, ref maxX, ref maxY, ref maxZ);
            }

            cx = (minX + maxX) * 0.5;
            cy = (minY + maxY) * 0.5;
            cz = (minZ + maxZ) * 0.5;

            double dx = maxX - minX;
            double dy = maxY - minY;
            double dz = maxZ - minZ;
            radius = Math.Max(dx, Math.Max(dy, dz)) * 0.5;
            if (radius <= 0)
            {
                radius = 1.0;
            }
        }

        private static void UpdateMinMax(
            double x,
            double y,
            double z,
            ref double minX,
            ref double minY,
            ref double minZ,
            ref double maxX,
            ref double maxY,
            ref double maxZ)
        {
            if (x < minX) minX = x;
            if (y < minY) minY = y;
            if (z < minZ) minZ = z;
            if (x > maxX) maxX = x;
            if (y > maxY) maxY = y;
            if (z > maxZ) maxZ = z;
        }
    }
}
