using static Aion.GameServer.GeoEngine.RetailPath.RetailCollisionMath;

namespace Aion.GameServer.GeoEngine.RetailPath;

public enum RetailCollisionQueryKind { PredictionProjection, GroundProjection, BooleanSegment, ContactSegment }

/// <summary>Complete admitted static and typed field native consumers. Geometry is calculated; unresolved consumed scene/constant state rejects visibly.</summary>
public static class RetailCollisionQuery
{
    private static readonly string[] boolean = ["0x1402781e0","0x140278400","0x140278690","0x140278a50","0x140278e70","0x140278f90","0x140279190","0x1402794b0","0x1402781e0","0x140278400","0x140278690","0x140278a50","0x140279820","0x140279830","0x140279920","0x140279b60"];
    private static readonly string[] prediction = ["0x140280450","0x1402804b0","0x140280600","0x140280700","0x140280870","0x1402808d0","0x140280a20","0x140280af0","0x140280450","0x1402804b0","0x140280600","0x140280700","0x140280c40","0x140280c50","0x140280d10","0x140280d60"];
    private static readonly string[] ground = ["0x14027d430","0x14027d490","0x14027d5d0","0x14027d6d0","0x14027d840","0x14027d8a0","0x14027d9e0","0x14027dab0","0x14027dc00","0x14027dc10","0x14027dcd0","0x14027dd20","0x14027dc00","0x14027dc10","0x14027dcd0","0x14027dd20"];
    private static readonly string[] contact = ["0x140279df0","0x14027a080","0x14027a470","0x14027aa00","0x14027b060","0x14027b1b0","0x14027b480","0x14027b8e0","0x140279df0","0x14027a080","0x14027a470","0x14027aa00","0x14027be00","0x14027be10","0x14027bf90","0x14027c2d0"];

    public static string DispatchFunction(RetailCollisionQueryKind kind, byte selector)
    {
        if (selector >= 16) throw new ArgumentOutOfRangeException(nameof(selector));
        return kind switch
        {
            RetailCollisionQueryKind.PredictionProjection => prediction[selector], RetailCollisionQueryKind.GroundProjection => ground[selector],
            RetailCollisionQueryKind.BooleanSegment => boolean[selector], RetailCollisionQueryKind.ContactSegment => contact[selector],
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }
    public static RetailCollisionQueryResult ProjectPrediction(RetailCollisionScene scene, RetailCollisionQueryStamp stamp,
        RetailSearchPoint input, RetailCollisionQueryLimits limits) => Project(scene, stamp, input, limits, false);
    public static RetailCollisionQueryResult ProjectGround(RetailCollisionScene scene, RetailCollisionQueryStamp stamp,
        RetailSearchPoint input, RetailCollisionQueryLimits limits) => Project(scene, stamp, input, limits, true);
    public static RetailCollisionQueryResult SegmentBlocked(RetailCollisionScene scene, RetailCollisionQueryStamp stamp,
        RetailSearchPoint from, RetailSearchPoint to, int materialView, uint ignoredObjectId, float shortSegmentTolerance,
        RetailCollisionQueryLimits limits) => Segment(scene, stamp, from, to, materialView, ignoredObjectId, shortSegmentTolerance, limits, false);
    public static RetailCollisionQueryResult FirstContact(RetailCollisionScene scene, RetailCollisionQueryStamp stamp,
        RetailSearchPoint from, RetailSearchPoint to, uint ignoredObjectId, float shortSegmentTolerance,
        RetailCollisionQueryLimits limits) => Segment(scene, stamp, from, to, 0, ignoredObjectId, shortSegmentTolerance, limits, true);

    private static RetailCollisionQueryResult Project(RetailCollisionScene scene, RetailCollisionQueryStamp stamp,
        RetailSearchPoint input, RetailCollisionQueryLimits limits, bool onlyEligible)
    {
        var run = new Execution(scene, stamp, limits);
        try
        {
            run.Validate(); Point(input);
            if (!scene.Bounds.Contains(input)) { run.Guard("NativeProjectionOutsideBounds"); return run.Result(false, null); }
            int wx = Integer(input.X), wy = Integer(input.Y), x = wx >> 1, y = wy >> 1;
            var kind = onlyEligible ? RetailCollisionQueryKind.GroundProjection : RetailCollisionQueryKind.PredictionProjection;
            byte selector = run.Cell(wx, wy, x, y, kind);
            bool found = false; float score = 0; RetailSearchPoint? best = null;
            int family = selector >> 2;
            bool terrain = family < 2 || (!onlyEligible && family == 2);
            if (terrain)
            {
                float z = family == 1 ? Flat(scene.Terrain, x, y) : Bilinear(scene.Terrain, x, y, input);
                best = new(input.X, input.Y, z); score = ProjectionScore(input.Z, z); found = true;
            }
            if ((selector & 2) != 0)
            {
                var cell = scene.Prepared.Cell(x, y);
                int stopHeight = HeightBin(input.Z);
                var ray = new RetailSearchPoint(input.X, input.Y, 0);
                var delta = new RetailSearchPoint(0, 0, 1);
                for (int b = 0; b < cell.BandCount; b++)
                {
                    var band = scene.Prepared.Band(cell.FirstBand + b); run.Band(x, y, band);
                    if (stopHeight < band.Height && found) break;
                    for (int r = 0; r < band.ReferenceCount; r++)
                    {
                        uint tagged = scene.Prepared.Reference(band.FirstReference + r);
                        if (onlyEligible && (int)tagged < 0) break;
                        uint id = tagged & 0x7fffffffu;
                        var face = scene.Prepared.Raw.GetTriangle(id); run.Triangle(x, y, face);
                        if (!Intersect(face.A, face.B, face.C, ray, delta, false, out float z)) continue;
                        float candidate = ProjectionScore(input.Z, z);
                        if (!found || candidate < score)
                        { score = candidate; found = true; best = new(input.X, input.Y, z); }
                    }
                }
            }
            if ((selector & 1) != 0) run.ProjectDynamic(wx, wy, x, y, input, onlyEligible, ref found, ref score, ref best);
            return run.Result(found, best);
        }
        catch (RetailCollisionStop stop) { return run.Reject(stop); }
    }

    private static RetailCollisionQueryResult Segment(RetailCollisionScene scene, RetailCollisionQueryStamp stamp,
        RetailSearchPoint from, RetailSearchPoint to, int view, uint ignore, float tolerance,
        RetailCollisionQueryLimits limits, bool contactPoint)
    {
        var run = new Execution(scene, stamp, limits);
        try
        {
            run.Validate(); Point(from); Point(to); F(tolerance);
            if (Squared(from, to) < Multiply(tolerance, tolerance))
            { run.Guard("NativeShortSegment"); return run.Result(false, null); }
            float dx = Subtract(to.X, from.X), dy = Subtract(to.Y, from.Y);
            float absX = MathF.Abs(dx), absY = MathF.Abs(dy);
            bool yDominant = absX <= absY;
            int first = Integer(yDominant ? from.Y : from.X) / 2;
            int last = Integer(yDominant ? to.Y : to.X) / 2;
            long count = System.Math.Abs((long)last - first) + 1;
            if (count > int.MaxValue) Stop(RetailCollisionStatus.UnsupportedArithmetic, "Native signed cell-count arithmetic is outside the admitted nonwrapping domain.");
            float stepX = 0, stepY = 0;
            // Native's sole vertical cell precedes an unused0/0 XY increment. Do not turn that unused state into a consumed coordinate or invented no-hit.
            if (count > 1)
            {
                if (yDominant) { stepY = last < first ? -2f : 2f; stepX = Divide(Multiply(dx, 2f), absY); }
                else { stepX = last < first ? -2f : 2f; stepY = Divide(Multiply(dy, 2f), absX); }
            }
            float sampleX = from.X, sampleY = from.Y;
            int previousX = -1, previousY = -1;
            for (int i = 0; i < (int)count; i++)
            {
                int wx = Integer(sampleX), wy = Integer(sampleY), x = wx >> 1, y = wy >> 1;
                if (SegmentCell(run, from, to, wx, wy, x, y, view, ignore, contactPoint, out var point))
                    return run.Result(true, point);
                if (x != previousX && y != previousY && previousX != -1)
                {
                    if (SegmentCell(run, from, to, Twice(previousX), wy, previousX, y, view, ignore, contactPoint, out point))
                        return run.Result(true, point);
                    if (SegmentCell(run, from, to, wx, Twice(previousY), x, previousY, view, ignore, contactPoint, out point))
                        return run.Result(true, point);
                }
                previousX = x; previousY = y;
                if (i + 1 < (int)count) { sampleX = Add(sampleX, stepX); sampleY = Add(sampleY, stepY); }
            }
            return run.Result(false, null);
        }
        catch (RetailCollisionStop stop) { return run.Reject(stop); }
    }

    private static bool SegmentCell(Execution run, RetailSearchPoint from, RetailSearchPoint to,
        int wx, int wy, int x, int y, int view, uint ignore, bool contactPoint, out RetailSearchPoint? point)
    {
        point = null; var scene = run.Scene;
        if (wx < 0 || wy < 0 || wx >= scene.Prepared.Raw.Dimensions.WidthMetres || wy >= scene.Prepared.Raw.Dimensions.HeightMetres)
        {
            run.OutsideCell(wx, wy, x, y, contactPoint);
            return !contactPoint; // Native boolean blocks; contact returns no hit. No traversability claim follows.
        }
        var kind = contactPoint ? RetailCollisionQueryKind.ContactSegment : RetailCollisionQueryKind.BooleanSegment;
        byte selector = run.Cell(wx, wy, x, y, kind);
        bool found = false; float bestDistance = 0;
        int family = selector >> 2;
        var delta = Subtract(to, from);
        if (family != 3)
        {
            RetailSearchPoint? terrain = null;
            if (family == 1) terrain = FlatContact(scene.Terrain, x, y, from, to);
            else
            {
                TerrainTriangles(scene.Terrain, x, y, out var a, out var b, out var c, out var opposite);
                run.TerrainTriangle(x, y, "0x140297e80 first");
                if (Intersect(a, b, c, from, delta, true, out float t) && t >= 0 && t <= 1)
                    terrain = Along(from, delta, t);
                else
                {
                    run.TerrainTriangle(x, y, "0x140297e80 second");
                    if (Intersect(opposite, b, c, from, delta, true, out t) && t >= 0 && t <= 1)
                        terrain = Along(from, delta, t);
                }
            }
            if (terrain is { } hit)
            {
                if (!contactPoint) return true;
                found = true; point = hit; bestDistance = Squared(hit, from);
            }
        }
        if ((selector & 2) != 0)
        {
            var cell = scene.Prepared.Cell(x, y);
            int low = HeightBin(MathF.Min(from.Z, to.Z)), high = HeightBin(MathF.Max(from.Z, to.Z));
            for (int b = 0; b < cell.BandCount; b++)
            {
                var band = scene.Prepared.Band(cell.FirstBand + b); run.Band(x, y, band);
                if (band.Height < low) continue;
                if (high < band.Height) break;
                for (int r = 0; r < band.ReferenceCount; r++)
                {
                    uint id = scene.Prepared.Reference(band.FirstReference + r) & 0x7fffffffu;
                    var face = scene.Prepared.Raw.GetTriangle(id);
                    if (scene.MaterialPolicy is null) Stop(RetailCollisionStatus.MissingMaterialPolicy, "A consumed mesh triangle needs an explicit raw material-policy snapshot.");
                    if (scene.MaterialPolicy.Excludes(face.Material, contactPoint ? 0 : view)) continue;
                    run.Triangle(x, y, face);
                    if (!Intersect(face.A, face.B, face.C, from, delta, false, out float t) || t < 0 || t > 1) continue;
                    if (!contactPoint) return true;
                    var candidate = Along(from, delta, t); float distance = Squared(candidate, from);
                    if (!found || distance < bestDistance) { found = true; bestDistance = distance; point = candidate; }
                }
            }
        }
        if ((selector & 1) != 0)
            run.SegmentDynamic(wx, wy, x, y, ignore, from, delta, view, contactPoint, ref found, ref bestDistance, ref point);
        return found;
    }

    private static float ProjectionScore(float queryZ, float surfaceZ)
    {
        float value = Subtract(queryZ, surfaceZ);
        if (value < 0) { value = F(-value); if (.1f < value) value = Add(value, 10000f); }
        return value;
    }
    private static RetailSearchPoint? FlatContact(RetailHeightMap40Data terrain, int x, int y, RetailSearchPoint from, RetailSearchPoint to)
    {
        float height = Flat(terrain, x, y);
        if (Multiply(Subtract(to.Z, height), Subtract(from.Z, height)) > 0) return null;
        float difference = Subtract(from.Z, to.Z), factor = 0;
        if (difference >= .0001f || difference <= -.0001f) factor = Divide(Subtract(height, from.Z), Subtract(to.Z, from.Z));
        float hitX = Add(Multiply(Subtract(to.X, from.X), factor), from.X);
        float hitY = Add(Multiply(Subtract(to.Y, from.Y), factor), from.Y);
        // Exact native flat-family index bounds, intentionally not the scaled bilinear quad.
        return x <= hitX && hitX <= (long)x + 2 && y <= hitY && hitY <= (long)y + 2 ? new(hitX, hitY, height) : null;
    }
    private static float Flat(RetailHeightMap40Data terrain, int x, int y) => Multiply(terrain.GetRawWord(x, y) & 0xfffc, .03125f);
    private static float Bilinear(RetailHeightMap40Data terrain, int x, int y, RetailSearchPoint point)
    {
        float fractionX = Subtract(Multiply(point.X, .5f), x), fractionY = Subtract(Multiply(point.Y, .5f), y);
        float inverseX = Subtract(1f, fractionX), inverseY = Subtract(1f, fractionY);
        float top = Add(Multiply(terrain.GetRawWord(x, y) & 0xfffc, inverseX), Multiply(terrain.GetRawWord(x + 1, y) & 0xfffc, fractionX));
        top = Multiply(top, inverseY);
        float bottom = Add(Multiply(terrain.GetRawWord(x, y + 1) & 0xfffc, inverseX), Multiply(terrain.GetRawWord(x + 1, y + 1) & 0xfffc, fractionX));
        bottom = Multiply(bottom, fractionY);
        return Multiply(Add(top, bottom), .03125f);
    }
    private static void TerrainTriangles(RetailHeightMap40Data terrain, int x, int y, out RetailSearchPoint a,
        out RetailSearchPoint b, out RetailSearchPoint c, out RetailSearchPoint opposite)
    {
        float left = Multiply(x, 2f), bottom = Multiply(y, 2f);
        float right = Add(left, 2f), top = Add(bottom, 2f);
        a = new(left, bottom, Flat(terrain, x, y));
        b = new(right, bottom, Flat(terrain, x + 1, y));
        c = new(left, top, Flat(terrain, x, y + 1));
        opposite = new(right, top, Flat(terrain, x + 1, y + 1));
    }
    private static int Integer(float value)
    {
        F(value);
        if (value < int.MinValue || value >= 2147483648f) Stop(RetailCollisionStatus.UnsupportedArithmetic, "Consumed truncating int32 conversion is outside its nonwrapping domain.");
        return (int)value;
    }
    private static int HeightBin(float z) => (Integer(z) / 2) * 2;
    private static int Twice(int value)
    {
        long result = (long)value * 2;
        if (result is < int.MinValue or > int.MaxValue) Stop(RetailCollisionStatus.UnsupportedArithmetic, "Crossing-cell world coordinate overflows int32.");
        return (int)result;
    }

    private sealed class Execution(RetailCollisionScene scene, RetailCollisionQueryStamp stamp, RetailCollisionQueryLimits limits)
    {
        private readonly List<RetailCollisionTrace> trace = [];
        private readonly List<RetailFieldObjectObservation> fields = [];
        private int cells, bands, triangles, objects;
        internal RetailCollisionScene Scene => scene;
        internal void Validate()
        {
            if (scene is null || stamp is null || limits is null || limits.MaxVisitedCells < 1 || limits.MaxBands < 0 ||
                limits.MaxTriangles < 0 || limits.MaxSceneObjects < 0 || limits.MaxTraceEntries < 1)
                Stop(RetailCollisionStatus.InvalidInput, "Scene, stamp and explicit nonnegative operation budgets are required.");
            if (stamp != scene.Stamp) Stop(RetailCollisionStatus.StaleSource, "Request instance/scene/asset epochs do not match the owned snapshot.");
        }
        private void Trace(RetailCollisionTrace value)
        {
            if (trace.Count >= limits.MaxTraceEntries) Stop(RetailCollisionStatus.DefensiveLimit, "Trace budget; native success/no-hit is withheld.");
            trace.Add(value);
        }
        internal void Guard(string purpose) => Trace(new(RetailCollisionTraceKind.NativeGuard, -1, -1, NativeFunction: purpose));
        internal byte Cell(int wx, int wy, int x, int y, RetailCollisionQueryKind kind)
        {
            if (cells >= limits.MaxVisitedCells) Stop(RetailCollisionStatus.DefensiveLimit, "Visited-cell budget.");
            cells++;
            if (x < 0 || y < 0 || x >= scene.Prepared.Raw.SectorsX * 32 || y >= scene.Prepared.Raw.SectorsY * 32)
                Stop(RetailCollisionStatus.UnsupportedEmptySectorStorage, "A consumed cell falls outside the native floor-sized sector storage.");
            if (scene.Prepared.Cell(x, y).SharedEmptyStorage)
                Stop(RetailCollisionStatus.UnsupportedEmptySectorStorage, "The native shared empty-sector storage contents/lifetime are not an invented clear scene.");
            // HeightMap40 format index0 uses the SECOND byte plane directly, never the32/24 combination.
            byte selector = scene.Terrain.BytePlane2.Span[y * scene.Terrain.Stride + x];
            if (selector >= 16) Stop(RetailCollisionStatus.UnsupportedFormat, "Consumed direct40 selector is outside the checked16-entry table.");
            Trace(new(RetailCollisionTraceKind.Cell, x, y, selector, DispatchFunction(kind, selector)));
            return selector;
        }
        internal void OutsideCell(int wx, int wy, int x, int y, bool contactPoint)
        {
            if (cells >= limits.MaxVisitedCells) Stop(RetailCollisionStatus.DefensiveLimit, "Visited-cell budget.");
            cells++; Trace(new(RetailCollisionTraceKind.NativeGuard, x, y, NativeFunction: contactPoint ? "0x1402f1cb0 outside returns0" : "0x1402f14c0 outside returns1"));
        }
        internal void Band(int x, int y, RetailCollisionPreparedBand band)
        {
            if (bands >= limits.MaxBands) Stop(RetailCollisionStatus.DefensiveLimit, "Band budget.");
            bands++; Trace(new(RetailCollisionTraceKind.Band, x, y, FileOffset: band.HeaderFileOffset));
        }
        internal void Triangle(int x, int y, RetailCollisionTriangle triangle)
        {
            if (triangles >= limits.MaxTriangles) Stop(RetailCollisionStatus.DefensiveLimit, "Triangle-intersection budget.");
            triangles++; Trace(new(RetailCollisionTraceKind.MeshTriangle, x, y, NativeFunction: "0x1402980f0", TriangleId: triangle.Id, FileOffset: triangle.DescriptorFileOffset));
        }
        internal void TerrainTriangle(int x, int y, string native)
        {
            if (triangles >= limits.MaxTriangles) Stop(RetailCollisionStatus.DefensiveLimit, "Triangle-intersection budget.");
            triangles++; Trace(new(RetailCollisionTraceKind.TerrainTriangle, x, y, NativeFunction: native));
        }
        private IEnumerable<RetailFieldObjectGeometry> Objects(int wx, int wy, int x, int y, uint? ignore, string slot)
        {
            var region = scene.Region(wx, wy);
            foreach (var obj in region.OrderedObjects)
            {
                if (objects >= limits.MaxSceneObjects) Stop(RetailCollisionStatus.DefensiveLimit, "Scene-object budget.");
                objects++; Trace(new(RetailCollisionTraceKind.SceneObject, x, y, NativeFunction: slot, ObjectId: obj.ObjectId));
                if (ignore is uint ignored && ignored != 0 && obj.ObjectId == ignored) continue;
                var field = scene.Field(obj); fields.Add(field.Observation); yield return field;
            }
        }
        internal void ProjectDynamic(int wx, int wy, int x, int y, RetailSearchPoint input, bool onlyEligible,
            ref bool found, ref float score, ref RetailSearchPoint? best)
        {
            foreach (var field in Objects(wx, wy, x, y, null, onlyEligible ? "0x1d0 flag1" : "0x1d0 flag0"))
                field.Project(input, onlyEligible, face => Triangle(x, y, face), ref found, ref score, ref best);
        }
        internal void SegmentDynamic(int wx, int wy, int x, int y, uint ignore, RetailSearchPoint from,
            RetailSearchPoint delta, int view, bool contact, ref bool found, ref float distance, ref RetailSearchPoint? best)
        {
            foreach (var field in Objects(wx, wy, x, y, ignore, contact ? "0x1c8" : "0x1c0"))
            {
                if (!field.Segment(from, delta, contact ? 0 : view, contact, scene.MaterialPolicy, face => Triangle(x, y, face), out var candidate)) continue;
                if (!contact) { found = true; return; }
                var hit = candidate!.Value; float next = Squared(hit, from);
                if (!found || next < distance) { found = true; distance = next; best = hit; }
            }
        }
        internal RetailCollisionQueryResult Result(bool found, RetailSearchPoint? point) =>
            new(RetailCollisionStatus.Computed, found ? 1u : 0u, found ? point : null, trace.AsReadOnly(), cells, bands, triangles, objects,
                scene?.Declaration, Sources: scene?.Sources, CosineReceipt: scene?.Prepared.Cosine, MaterialPolicy: scene?.MaterialPolicy)
            { ConsumedFieldObjects = fields.AsReadOnly() };
        internal RetailCollisionQueryResult Reject(RetailCollisionStop error) =>
            new(error.Status, null, null, trace.AsReadOnly(), cells, bands, triangles, objects, scene?.Declaration, error.Message,
                scene?.Sources, scene?.Prepared.Cosine, scene?.MaterialPolicy) { ConsumedFieldObjects = fields.AsReadOnly() };
    }
}
