using static Aion.GameServer.GeoEngine.RetailPath.RetailCollisionMath;

namespace Aion.GameServer.GeoEngine.RetailPath;

/// <summary>
/// Complete inactive admitted ordinary-ground World::MoveByPath225d80 plan.
/// Geometry is calculated through the owned static/typed-field collision scene.
/// No routing, clock, actor mutation, packet, timer or world move is executed.
/// Java NpcMoveController was read first; native fixed distance/index protocol is explicit.
/// </summary>
public static class RetailNpcMoveByPathPlan
{
    public static RetailNpcPathFollowResult Plan(RetailNpcPathFollowObservation observation,
        RetailCollisionScene scene, RetailNpcPathFollowLimits limits)
    {
        var run = new Execution(observation, scene, limits);
        try { return run.Run(); }
        catch (FollowStop stop) { return run.Reject(stop.Status, stop.Message, stop.Geometry); }
        catch (RetailCollisionStop stop) { return run.Reject(RetailNpcPathFollowStatus.UnsupportedArithmetic, stop.Message, stop.Status); }
    }
    private sealed class FollowStop(RetailNpcPathFollowStatus status, string detail, RetailCollisionStatus? geometry = null) : Exception(detail)
    { internal RetailNpcPathFollowStatus Status => status; internal RetailCollisionStatus? Geometry => geometry; }
    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    private static void Fail(RetailNpcPathFollowStatus status, string detail, RetailCollisionStatus? geometry = null) => throw new FollowStop(status, detail, geometry);

    private sealed class Execution(RetailNpcPathFollowObservation input, RetailCollisionScene scene, RetailNpcPathFollowLimits limits)
    {
        private readonly List<RetailNpcPathFollowOperation> operations = [];
        private readonly List<RetailCollisionQueryResult> queries = [];
        private readonly List<RetailNpcPathGeometryCall> geometryCalls = [];
        private RetailSearchPoint desired, next;
        private bool outputsWritten, snapshotVerified;
        private int index, progress, visits, queryTrace;

        internal RetailNpcPathFollowResult Run()
        {
            Validate();
            F(input.DistanceBudget);
            index = input.HeaderIndex;
            if (input.DistanceBudget < .1f) return Result(4); // Before native output stores/actor reads.
            Point(input.Source); desired = next = input.Source; outputsWritten = true;
            if (!scene.Bounds.Contains(input.Source) || input.HeaderCount < 1) return Result(1, false);
            var actor = input.Actor;
            if (actor.Virtual18Target != 0x14007dfa0 || actor.RawPose5a0 is null || actor.RawKind32c is null || actor.UpperC8 is null)
                Fail(RetailNpcPathFollowStatus.UnsupportedActor, "Actual pinned NPC virtual18 target, pose/kind and current raw upper bound are required.");
            uint pose = actor.RawPose5a0!.Value & 15;
            if (pose is 3 or 13 or 14 || actor.RawKind32c == 0x10)
                Fail(RetailNpcPathFollowStatus.UnsupportedActor, "The native alternate-motion branch is outside this ordinary-ground unit.");
            if (actor.NearestEvenNormalArithmetic != true)
                Fail(RetailNpcPathFollowStatus.UnsupportedArithmetic, "Explicit nearest-even normal/zero binary32 arithmetic context is required.");
            if (actor.Progress82c is null || !Digest(actor.EvidenceSha256))
                Fail(RetailNpcPathFollowStatus.UnsupportedProgress, "Current progress and its serialized ownership evidence are required.");
            float height = F(actor.UpperC8!.Value);
            height = height <= 1.5f ? 1.5f : height < 2f ? height : 2f;
            var consumed = RetailNpcPathProgressPlan.Consume(new(input.Epoch, input.Epoch,
                actor.SerializedProgressOwnership, scene.Sources.NativeNpcBinary.Sha256, actor.EvidenceSha256), actor.Progress82c!.Value, index);
            if (!consumed.Success) Fail(consumed.Status, consumed.Detail ?? "Progress exchange unavailable.");
            index = consumed.PathIndex!.Value; progress = consumed.Progress!.Value;
            foreach (var op in consumed.Operations) AddOperation(op.Operation, op.OldValue, op.NewValue, op.NativeAddress);
            while (true)
            {
                if (visits >= limits.MaxWaypointVisits) Fail(RetailNpcPathFollowStatus.DefensiveLimit, "Waypoint visit budget; native result is withheld.");
                visits++;
                if (input.DistanceBudget <= 0 || input.HeaderCount <= index) return Result(1);
                if (index < 0) Fail(RetailNpcPathFollowStatus.InvalidInput, "Negative native path index would access before owned point storage.");
                var waypoint = input.Points[index]; Point(waypoint);
                if (Near(input.Source, waypoint))
                {
                    Advance("DuplicateWaypoint", "0x140226025");
                    if (input.HeaderCount <= index) return Result(2);
                    continue;
                }
                desired = waypoint;
                var delta = Subtract(waypoint, input.Source);
                float length = F(MathF.Sqrt(Squared(input.Source, waypoint)));
                F(input.SquaredStopTolerance);
                bool wholeFits = !(input.DistanceBudget < length);
                if (wholeFits)
                {
                    if (input.SquaredStopTolerance <= 0)
                    {
                        next = waypoint; SetProgress(1, "0x140226f02");
                        return Result(0); // Native early branch intentionally performs no geometry query.
                    }
                    Point(input.Target);
                    if (input.SquaredStopTolerance < Squared(input.Target, waypoint))
                    { next = waypoint; SetProgress(1, "0x140226f02"); return Result(0); }
                    float factor = Reach(input.Source, delta, input.Target);
                    var candidate = Along(input.Source, delta, factor);
                    if (!Support(waypoint, candidate, height, false, out candidate)) return Result(1);
                    if (input.SquaredStopTolerance < SquaredXY(input.Target, candidate)) desired = next = waypoint;
                    else desired = next = candidate;
                    if (!Near(input.Source, next))
                    {
                        if (!(factor < 1f)) SetProgress(1, "0x140226f02");
                        return Result(0);
                    }
                    Advance("UnchangedFullCandidate", "0x140226dfe");
                    if (input.HeaderCount <= index) return Result(2);
                    continue;
                }
                Point(input.Target);
                float fraction;
                if (Near(waypoint, input.Target) && input.SquaredStopTolerance > 0 &&
                    Squared(input.Target, input.Source) < Add(Multiply(input.DistanceBudget, input.DistanceBudget), input.SquaredStopTolerance))
                    fraction = Reach(input.Source, delta, waypoint);
                else fraction = Divide(input.DistanceBudget, length);
                var partial = Along(input.Source, delta, fraction);
                if (!Support(waypoint, partial, height, true, out partial)) return Result(1);
                if (SquaredXYZ(input.Target, partial) <= input.SquaredStopTolerance) desired = partial;
                next = partial;
                if (!Near(input.Source, next)) return Result(0);
                Advance("UnchangedPartialCandidate", "0x1402267b7");
                if (input.HeaderCount <= index) return Result(2);
            }
        }

        private bool Support(RetailSearchPoint waypoint, RetailSearchPoint candidate, float height,
            bool partialBranch, out RetailSearchPoint supported)
        {
            supported = candidate;
            bool xyDistinct = !NearXY(input.Source, waypoint);
            if (xyDistinct)
            {
                var origin = new RetailSearchPoint(input.Source.X, input.Source.Y, Add(input.Source.Z, height));
                var end = new RetailSearchPoint(waypoint.X, waypoint.Y, Add(waypoint.Z, height));
                supported = new(candidate.X, candidate.Y, Add(candidate.Z, height));
                bool exhausted = true;
                for (int attempt = 0; attempt < 9; attempt++)
                {
                    var contact = Query(false, origin, end);
                    if (contact.NativeResult == 0) { exhausted = false; break; }
                    var hit = contact.Point!.Value;
                    if (NearXY(hit, waypoint) || SquaredXY(waypoint, hit) < SquaredXY(waypoint, candidate))
                    { exhausted = false; break; }
                    var projection = Query(true, new(hit.X, hit.Y, Add(hit.Z, height)), default);
                    if (projection.NativeResult == 0) return false;
                    var support = projection.Point!.Value;
                    float raised = Add(support.Z, height);
                    if (MathF.Abs(Subtract(raised, end.Z)) < .5f) raised = Add(raised, 1f);
                    origin = new(support.X, support.Y, raised);
                    supported = new(candidate.X, candidate.Y, raised);
                }
                if (exhausted)
                {
                    AddOperation("NineContactAttemptsExhaustedCopyWholeWaypoint", null, null, partialBranch ? "0x140226526" : "0x140226b8f");
                    supported = waypoint;
                    if (partialBranch) SetProgress(1, "0x14022664e");
                }
            }
            else AddOperation("PureVerticalSkipsXYContactLoop");
            if (!scene.Bounds.Contains(supported)) return false;
            var final = Query(true, supported, default);
            if (final.NativeResult == 0) return false;
            supported = final.Point!.Value;
            return true; // Native has no added post-projection world bounds check.
        }
        private float Reach(RetailSearchPoint source, RetailSearchPoint delta, RetailSearchPoint target)
        {
            var result = RetailNpcSegmentReachFactor.Calculate(source, delta, target,
                Multiply(input.SquaredStopTolerance, .9f), scene.Sources.NativeNpcBinary.Sha256.ToLowerInvariant());
            if (result.Status != RetailNpcSegmentReachStatus.Calculated || result.Factor is null)
                Fail(RetailNpcPathFollowStatus.UnsupportedArithmetic, result.Detail ?? "Stop-factor calculation is unsupported.");
            AddOperation("SegmentReachFactorSquaredToleranceTimesPoint9", null, null, "0x140225af0");
            return result.Factor!.Value; // No clamp, including native fallback>1.
        }
        private RetailCollisionQueryResult Query(bool project, RetailSearchPoint a, RetailSearchPoint b)
        {
            if (queries.Count >= limits.MaxQueries) Fail(RetailNpcPathFollowStatus.DefensiveLimit, "Collision/support query budget.");
            int remaining = limits.MaxTraceEntries - queryTrace - operations.Count;
            if (remaining < 1) Fail(RetailNpcPathFollowStatus.DefensiveLimit, "Aggregate operation/query trace budget.");
            var perQuery = limits.QueryLimits with { MaxTraceEntries = System.Math.Min(remaining, limits.QueryLimits.MaxTraceEntries) };
            var result = project ? RetailCollisionQuery.ProjectPrediction(scene, scene.Stamp, a, perQuery)
                : RetailCollisionQuery.FirstContact(scene, scene.Stamp, a, b, input.IgnoredEditorId,
                    BitConverter.UInt32BitsToSingle(0x3d016f00), perQuery);
            queries.Add(result); queryTrace += result.Trace.Count;
            geometryCalls.Add(new(geometryCalls.Count + 1, project ? RetailCollisionQueryKind.PredictionProjection : RetailCollisionQueryKind.ContactSegment,
                a, project ? null : b, project ? null : input.IgnoredEditorId, project ? null : BitConverter.UInt32BitsToSingle(0x3d016f00), result));
            if (result.Status != RetailCollisionStatus.Computed || result.NativeResult is null ||
                result.NativeResult == 1 && result.Point is null)
                Fail(result.Status == RetailCollisionStatus.DefensiveLimit ? RetailNpcPathFollowStatus.DefensiveLimit : RetailNpcPathFollowStatus.UnsupportedGeometry,
                    result.Detail ?? "Calculated geometry outcome unavailable.", result.Status);
            return result;
        }
        private void Validate()
        {
            if (input is null || scene is null || limits is null || limits.QueryLimits is null || limits.MaxPathPoints < 0 ||
                limits.MaxWaypointVisits < 1 || limits.MaxQueries < 0 || limits.MaxTraceEntries < 1)
                Fail(RetailNpcPathFollowStatus.InvalidInput, "Owned observation, scene and explicit defensive budgets are required.");
            if (input.Points.Count > limits.MaxPathPoints) Fail(RetailNpcPathFollowStatus.DefensiveLimit, "Owned path point budget.");
            if (input.HeaderCount > input.Points.Count)
                Fail(RetailNpcPathFollowStatus.InvalidInput, "Native count exceeds its individually owned point snapshot.");
            if (!Text(input.PathReceipt.SnapshotIdentity) || !Digest(input.PathReceipt.ProducerEvidenceSha256) ||
                !Text(input.PathReceipt.ProducerEvidence) || !Digest(input.PathReceipt.SnapshotSha256) || !input.SnapshotMatches())
                Fail(RetailNpcPathFollowStatus.SourceIdentityMismatch, "Path header/metadata/point bytes differ from their explicit snapshot receipt.");
            snapshotVerified = true;
            var e = input.Epoch; var d = scene.Declaration;
            if (new[] { e.ActorIdentity, e.ActorRevision, e.OwnershipEpoch, e.PathRevision, e.WorldIdentity,
                e.InstanceIdentity, e.SceneRevision, e.AssetRevision }.Any(s => !Text(s)))
                Fail(RetailNpcPathFollowStatus.InvalidInput, "Actor/path/world/scene ownership epochs must be explicit.");
            if (e.WorldIdentity != d.WorldIdentity || e.InstanceIdentity != d.InstanceIdentity ||
                e.SceneRevision != d.SceneRevision || e.AssetRevision != d.AssetRevision)
                Fail(RetailNpcPathFollowStatus.StaleSource, "Owned actor/path scene stamp differs from the calculated geometry snapshot.");
        }
        private void SetProgress(int value, string address)
        { AddOperation("ExchangeProgress", progress, value, address); progress = value; }
        private void Advance(string reason, string address)
        {
            if (index == int.MaxValue) Fail(RetailNpcPathFollowStatus.UnsupportedArithmetic, "Native signed path-index increment wraps.");
            AddOperation(reason, index, index + 1, address); index++;
        }
        private void AddOperation(string operation, int? old = null, int? value = null, string? address = null)
        {
            if (operations.Count + queryTrace >= limits.MaxTraceEntries) Fail(RetailNpcPathFollowStatus.DefensiveLimit, "Aggregate operation/query trace budget.");
            operations.Add(new(operations.Count + 1, operation, old, value, address));
        }
        private RetailNpcPathFollowResult Result(uint native, bool progressConsumed = true) => new(RetailNpcPathFollowStatus.Planned,
            native, outputsWritten ? desired : null, outputsWritten ? next : null, outputsWritten,
            index, progressConsumed && operations.Count > 0 ? progress : input.Actor.Progress82c,
            operations.AsReadOnly(), queries.AsReadOnly(), null, input.Epoch, input.PathReceipt)
            { GeometrySources = scene.Sources, SceneDeclaration = scene.Declaration, ActorDeclaration = input.Actor,
              PathSnapshotBytesVerified = snapshotVerified, SyntheticFixture = input.SyntheticFixture, GeometryCalls = geometryCalls.AsReadOnly() };
        internal RetailNpcPathFollowResult Reject(RetailNpcPathFollowStatus status, string detail, RetailCollisionStatus? geometry = null) =>
            new(status, null, null, null, false, null, null, Array.Empty<RetailNpcPathFollowOperation>(), queries.AsReadOnly(),
                detail, input?.Epoch, input?.PathReceipt)
            { GeometryFailure = geometry, GeometrySources = scene?.Sources, SceneDeclaration = scene?.Declaration,
              ActorDeclaration = input?.Actor, PathSnapshotBytesVerified = snapshotVerified, SyntheticFixture = input?.SyntheticFixture ?? false,
              GeometryCalls = geometryCalls.AsReadOnly() };
    }
    private static bool NearScalar(float a, float b) { float d = Subtract(a, b); return d < .0001f && -.0001f < d; }
    private static bool NearXY(RetailSearchPoint a, RetailSearchPoint b) => NearScalar(a.X, b.X) && NearScalar(a.Y, b.Y);
    private static bool Near(RetailSearchPoint a, RetailSearchPoint b) => NearXY(a, b) && NearScalar(a.Z, b.Z);
    private static float SquaredXY(RetailSearchPoint a, RetailSearchPoint b)
    { float x = Subtract(a.X, b.X), y = Subtract(a.Y, b.Y); return Add(Multiply(x, x), Multiply(y, y)); }
    private static float SquaredXYZ(RetailSearchPoint a, RetailSearchPoint b)
    { float z = Subtract(a.Z, b.Z); return Add(SquaredXY(a, b), Multiply(z, z)); }
}
