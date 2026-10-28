using System.Security.Cryptography;

namespace Aion.GameServer.GeoEngine.RetailPath;

/// <summary>
/// Inactive ordinary-ground replay of NPCServer 0x140223230, 0x140224d60 and 0x140225390/430/530.
/// No live adapter, routing registration, JPS normalization, guessed collision or actor mutation.
/// </summary>
public static class RetailGroundPathSearch
{
    public const string SupportedNpcBinarySha256 = "c24e600107352268d377c9a7361e52d848e96af8deff61259e031b98b7130350";
    public const long SupportedNpcBinaryBytes = 24_889_856;

    public static RetailGroundSearchResult Search(RetailGroundSearchObservation observation,
        IRetailSearchPolicyReplay policy, RetailSearchDefensiveLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(observation); ArgumentNullException.ThrowIfNull(policy);
        return new Run(observation, policy, limits ?? new()).Execute();
    }

    private sealed class SearchStop(RetailGroundSearchStatus status, RetailGroundSearchTermination reason, string detail) : Exception(detail)
    { internal RetailGroundSearchStatus Status { get; } = status; internal RetailGroundSearchTermination Reason { get; } = reason; }
    private sealed class Node(RetailPathSurfaceRef surface, int index)
    {
        internal RetailPathSurfaceRef Surface { get; } = surface;
        internal RetailGroundSearchQueue.Entry Entry { get; } = new(index);
        internal float G;
        internal byte State;
        internal Node? Parent;
    }

    private sealed class Run(RetailGroundSearchObservation o, IRetailSearchPolicyReplay policy, RetailSearchDefensiveLimits limits)
    {
        private readonly Dictionary<(RetailPathNode, uint), Node> byIdentity = [];
        private readonly List<Node> nodes = [];
        private readonly List<RetailSearchPolicyConsumption> consumed = [];
        private int pops, calls, fieldDiagnostics;
        private bool poolReached;
        private RetailGroundSearchQueue queue = null!;
        private RetailPathSurfaceQuery surfaces = null!;
        private RetailSearchSourceValidation sourceValidation = new(false, false, false);

        internal RetailGroundSearchResult Execute()
        {
            try { return ExecuteCore(); }
            catch (SearchStop stop) { return Result(stop.Status, stop.Reason, detail: stop.Message); }
            catch (RetailGroundSearchQueue.QueueSupportException ex)
            { return Result(RetailGroundSearchStatus.DefensiveResourceLimit, RetailGroundSearchTermination.DefensiveLimit, detail: ex.Message); }
        }

        private RetailGroundSearchResult ExecuteCore()
        {
            ValidateObservation();
            ValidateSources();
            if (o.NativeSelector is 1 or 2) Stop(RetailGroundSearchStatus.UnsupportedSelector, RetailGroundSearchTermination.UnsupportedMode,
                "JPS/comparison mode requires warm-workspace and shared-output ownership closure; no A* substitution.");
            if (!o.Bounds.Contains(o.Start) || !o.Bounds.Contains(o.Goal)) return NoRoute(RetailGroundSearchTermination.RejectedBounds);
            if (Near(o.Start, o.Goal)) return NoRoute(RetailGroundSearchTermination.EqualEndpoints);
            if (!o.Actor.NpcPresent || o.Actor.NativeVirtual14Rejects || o.Actor.NativeVirtual19Rejects) return NoRoute(RetailGroundSearchTermination.ActorGuard);
            if (o.Actor.NativeVirtual18AlternateMotion) Stop(RetailGroundSearchStatus.UnsupportedActor, RetailGroundSearchTermination.UnsupportedMode,
                "Alternate motion/wall-pass is outside this ordinary-ground observation domain.");

            float separation = Squared(o.Start, o.Goal);
            if (separation >= 10000f)
            {
                var direct = Segment(o.Start, o.Goal, RetailSearchPolicyPurpose.DirectDistanceBranch);
                return direct.Blocked ? NoRoute(RetailGroundSearchTermination.SegmentBlocked) : Direct(o.Goal);
            }
            if (!o.NativePathTablesAvailable || o.StartSurface.State == RetailSearchSurfaceState.Missing)
            {
                var fallback = Segment(o.Start, o.Goal, RetailSearchPolicyPurpose.MissingStartFallback);
                if (!fallback.Blocked) return Direct(o.Goal);
                return Near(o.Start, fallback.ClippedEndpoint) ? NoRoute(RetailGroundSearchTermination.NoProgress) : Direct(fallback.ClippedEndpoint);
            }
            var start = Resolve(o.StartSurface, o.Start);
            if (o.GoalSurface.State == RetailSearchSurfaceState.Missing) return NoRoute(RetailGroundSearchTermination.MissingGoal);
            var goal = Resolve(o.GoalSurface, o.Goal);
            if (Shortcut(start, goal, out _) && !Segment(o.Start, o.Goal, RetailSearchPolicyPurpose.InitialShortcut).Blocked) return Direct(o.Goal);

            if (o.Queue == RetailGroundQueueKind.FibonacciHeap)
            {
                if (o.ObservedHeapRankCapacity is not > 0) Stop(RetailGroundSearchStatus.UnsupportedHeapCapacity, RetailGroundSearchTermination.UnsupportedMode,
                    "Configured F-heap requires an explicitly observed initialized rank capacity; PE zero-fill is not that observation.");
                if (o.ObservedHeapRankCapacity > limits.MaxHeapRankCapacity) Defensive("Observed heap rank capacity exceeds defensive support.");
            }
            queue = new(o.Queue, o.ObservedHeapRankCapacity ?? 0);
            var first = GetNode(start)!;
            first.G = 0; first.Parent = null; first.State = 1; first.Entry.Key = Distance(o.Start, o.Goal) * 100f;
            queue.Add(first.Entry);
            float dx = o.Goal.X - o.Start.X, dy = o.Goal.Y - o.Start.Y, dz = o.Goal.Z - o.Start.Z;
            float radius = (dy * dy * 2f + dx * dx * 2f) + dz * dz * 2f;
            radius = System.MathF.Max(2500f, radius); Finite(radius);
            while (!queue.IsEmpty)
            {
                if (pops >= 49_999) return NoRoute(RetailGroundSearchTermination.NativePopLimit);
                if (pops >= limits.MaxPops) Defensive("Defensive pop limit reached before native termination.");
                var current = nodes[queue.Pop().Label]; pops++;
                for (int direction = 0; direction < 8; direction++)
                {
                    var target = direction < 4 ? Neighbor(current.Surface, direction) : Diagonal(current.Surface,
                        direction is 4 or 6 ? 0 : 2, direction < 6 ? 1 : 3);
                    if (target is null) continue;
                    var next = GetNode(target);
                    if (next is null || next.State == 2) continue;
                    if (next.State == 0)
                    {
                        var point = Position(next.Surface);
                        if (Squared(point, o.Start) > radius && Squared(point, o.Goal) > radius) { next.State = 2; continue; }
                        float cost = Cost(current.Surface, target);
                        if (cost >= 2000f)
                        {
                            if (!Same(target, goal)) next.State = 2;
                            continue;
                        }
                        next.G = current.G + cost; Finite(next.G);
                        next.Entry.Key = Distance(point, o.Goal) * 100f + next.G; Finite(next.Entry.Key);
                        next.Parent = current; next.State = 1; queue.Add(next.Entry);
                    }
                    else if (current.G + 50f < next.G)
                    {
                        float candidate = current.G + Cost(current.Surface, target); Finite(candidate);
                        if (candidate < next.G)
                        {
                            next.G = candidate; next.Parent = current;
                            float key = Distance(Position(next.Surface), o.Goal) * 100f + next.G; Finite(key);
                            queue.Decrease(next.Entry, key);
                        }
                    }
                }
                current.State = 2;
                // Native checks after neighbor processing, including for an already-popped goal.
                if (Same(current.Surface, goal)) return Generate(first, current);
            }
            return NoRoute(poolReached ? RetailGroundSearchTermination.NativeNodePoolLimit : RetailGroundSearchTermination.QueueExhausted);
        }

        private RetailGroundSearchResult Generate(Node start, Node goal)
        {
            var reverse = new List<Node> { goal };
            for (var cursor = goal; !ReferenceEquals(cursor, start);)
            {
                if (reverse.Count >= 20_000) return NoRoute(RetailGroundSearchTermination.NativePathPointLimit);
                if (reverse.Count >= limits.MaxOutputPoints) Defensive("Defensive point-chain limit reached.");
                cursor = cursor.Parent ?? throw new InvalidOperationException("Search parent chain lost its start.");
                reverse.Add(cursor);
            }
            reverse.Reverse();
            var points = reverse.Count == 1 ? new List<RetailSearchPoint> { o.Start, o.Goal } :
                reverse.Select((n, index) => index == 0 ? o.Start : index == reverse.Count - 1 ? o.Goal : Position(n.Surface)).ToList();
            if (points.Count > limits.MaxOutputPoints) Defensive("Defensive output point limit reached.");
            InspectFields(points, RetailSearchPolicyPurpose.BeforeSmoothing);
            int third = 2;
            while (third < points.Count)
            {
                if (Smooth(points[third - 2], points[third])) points.RemoveAt(third - 1);
                else third++;
            }
            InspectFields(points, RetailSearchPolicyPurpose.AfterSmoothing);
            return Result(RetailGroundSearchStatus.Completed, RetailGroundSearchTermination.None, points,
                reverse.Select(n => n.Surface).ToArray());
        }

        private bool Smooth(RetailSearchPoint from, RetailSearchPoint to)
        {
            if (!o.NativePathTablesAvailable) return !Segment(from, to, RetailSearchPolicyPurpose.Smoothing).Blocked;
            // Intermediate world points need actual endpoint lookups, not a nearest-height fallback.
            var a = Lookup(from); var b = Lookup(to);
            if (a is null || b is null) return false;
            return Shortcut(a, b, out _) && !Segment(from, to, RetailSearchPolicyPurpose.Smoothing).Blocked;
        }

        private RetailPathSurfaceRef? Lookup(RetailSearchPoint point)
        {
            var result = surfaces.EnumerateSurfaceCandidates(point.X, point.Y, point.Z);
            if (result.Status == RetailSurfaceQueryStatus.OutsideWorld) return null;
            if (result.Status == RetailSurfaceQueryStatus.ResourceLimit) Defensive(result.Detail ?? "Endpoint candidate enumeration exceeded its budget.");
            if (result.Status != RetailSurfaceQueryStatus.Complete)
                Stop(result.Status == RetailSurfaceQueryStatus.Ambiguous ? RetailGroundSearchStatus.AmbiguousSurface : RetailGroundSearchStatus.UnsupportedSurface,
                    RetailGroundSearchTermination.SurfaceBoundary, result.Detail ?? $"Unresolved endpoint surface lookup: {result.Status}.");
            return result.Candidates.FirstOrDefault(c => c.Status == RetailSurfaceCandidateStatus.WithinTolerance)?.Surface;
        }

        private bool Shortcut(RetailPathSurfaceRef source, RetailPathSurfaceRef target, out bool costRejected)
        {
            costRejected = false;
            if (Same(source, target)) return true;
            int dx = target.CellX - source.CellX, dy = target.CellY - source.CellY;
            int xDirection = dx < 0 ? 2 : 0, yDirection = dy < 0 ? 3 : 1;
            dx = System.Math.Abs(dx); dy = System.Math.Abs(dy);
            int majorDirection, minorDirection, steps, error, straightDelta, diagonalDelta;
            if (dx < dy) { majorDirection = yDirection; minorDirection = xDirection; steps = dy; error = dx * 2 - dy; straightDelta = dx * 2; diagonalDelta = (dx - dy) * 2; }
            else { majorDirection = xDirection; minorDirection = yDirection; steps = dx; error = dy * 2 - dx; straightDelta = dy * 2; diagonalDelta = (dy - dx) * 2; }
            if (steps > limits.MaxShortcutSteps) Defensive("Defensive line-trace step limit reached.");
            var current = source;
            for (int step = 0; step < steps; step++)
            {
                if (ReferenceEquals(current.Node, target.Node))
                {
                    BeforePolicy(); var value = policy.SameNodeShortcutCapability(current.Node);
                    Consume(value.Stamp, "SameNodeShortcutCapability", null);
                    if (value.Value) return true;
                }
                var next = error < 1 ? Neighbor(current, majorDirection) : Diagonal(current, majorDirection, minorDirection);
                if (next is null) return false;
                float cost = Cost(current, next);
                current = next;
                if (cost >= 2000f) costRejected = true;
                if (cost >= 500f) return false;
                error += error < 1 ? straightDelta : diagonalDelta;
            }
            return Same(current, target);
        }

        private RetailPathSurfaceRef? Diagonal(RetailPathSurfaceRef source, int a, int b)
        {
            var first = Neighbor(source, a);
            if (first is null || Neighbor(first, b) is null) return null;
            var second = Neighbor(source, b);
            return second is null ? null : Neighbor(second, a);
        }
        private RetailPathSurfaceRef? Neighbor(RetailPathSurfaceRef source, int direction)
        {
            var result = surfaces.GetCardinalNeighbor(source, direction);
            if (result.Status is RetailSurfaceQueryStatus.NoLink or RetailSurfaceQueryStatus.OutsideWorld) return null;
            if (result.Status == RetailSurfaceQueryStatus.ResourceLimit) Defensive(result.Detail ?? "Directed-neighbor query exceeded its support budget.");
            if (result.Status != RetailSurfaceQueryStatus.Found || result.Target is null)
                Stop(RetailGroundSearchStatus.UnsupportedSurface, RetailGroundSearchTermination.SurfaceBoundary, result.Detail ?? result.Status.ToString());
            return result.Target;
        }
        private Node? GetNode(RetailPathSurfaceRef surface)
        {
            var key = (surface.Node, surface.NativeId);
            if (byIdentity.TryGetValue(key, out var existing))
            {
                if (existing.Surface.CellX != surface.CellX || existing.Surface.CellY != surface.CellY)
                    Stop(RetailGroundSearchStatus.UnsupportedSurface, RetailGroundSearchTermination.SurfaceBoundary,
                        "One native identity acquired different cell coordinates; pooled alias/history semantics require a matched observation.");
                return existing;
            }
            if (nodes.Count >= 20_480) { poolReached = true; return null; }
            if (nodes.Count >= limits.MaxNodes) Defensive("Defensive node limit reached before native pool exhaustion.");
            var node = new Node(surface, nodes.Count); nodes.Add(node); byIdentity.Add(key, node); return node;
        }
        private RetailPathSurfaceRef Resolve(RetailSearchSurfaceObservation value, RetailSearchPoint point)
        {
            if (value.State == RetailSearchSurfaceState.Ambiguous) Stop(RetailGroundSearchStatus.AmbiguousSurface,
                RetailGroundSearchTermination.SurfaceBoundary, "Observed endpoint lookup is ambiguous.");
            if (value.State != RetailSearchSurfaceState.Resolved || value.Surface is null)
                Stop(RetailGroundSearchStatus.UnsupportedSurface, RetailGroundSearchTermination.SurfaceBoundary, "Endpoint lookup is unsupported or missing.");
            var surface = value.Surface!;
            if (!ReferenceEquals(surface.Owner, o.Sources.Path)) Stop(RetailGroundSearchStatus.InvalidObservation,
                RetailGroundSearchTermination.InvalidInput, "Endpoint belongs to a different loaded path file.");
            if ((int)(point.X * 2f) != surface.CellX || (int)(point.Y * 2f) != surface.CellY)
                Stop(RetailGroundSearchStatus.InvalidObservation, RetailGroundSearchTermination.InvalidInput, "Endpoint cell does not match native quantization.");
            var selected = Lookup(point);
            if (selected is null || !Same(selected, surface)) Stop(RetailGroundSearchStatus.InvalidObservation,
                RetailGroundSearchTermination.InvalidInput, "Resolved observation differs from the first supported native-order surface candidate.");
            Position(surface); return surface;
        }
        private RetailSearchPoint Position(RetailPathSurfaceRef surface)
        {
            var height = surfaces.ReadHeight(surface);
            float z = height.Metres;
            if (height.Status == RetailSurfaceHeightStatus.NoGround && surface.Node.Type is >= 4 and < 16)
                z = height.RawHeight.Kind == RetailPathHeightKind.NoGround ? -10240f : height.RawHeight.Centimetres / 100f;
            else if (height.Status != RetailSurfaceHeightStatus.Resolved) Stop(RetailGroundSearchStatus.UnsupportedSurface,
                RetailGroundSearchTermination.SurfaceBoundary, "Unresolved/foreign surface height.");
            Finite(z); return new(surface.CentreXMetres, surface.CentreYMetres, z);
        }
        private float Cost(RetailPathSurfaceRef from, RetailPathSurfaceRef to)
        {
            BeforePolicy(); var value = policy.TransitionCost(from, to); Consume(value.Stamp, "TransitionCost", null);
            if (!float.IsFinite(value.Cost) || value.Cost < 0) Stop(RetailGroundSearchStatus.UnsupportedArithmetic,
                RetailGroundSearchTermination.UnknownPolicy, "Negative/nonfinite actor cost is outside replay support.");
            return value.Cost;
        }
        private RetailSearchSegmentObservation Segment(RetailSearchPoint from, RetailSearchPoint to, RetailSearchPolicyPurpose purpose)
        {
            BeforePolicy(); var value = policy.CheckSegment(from, to, purpose); Consume(value.Stamp, "CheckSegment", purpose);
            if (!value.ClippedEndpoint.IsFinite || (!value.Blocked && value.ClippedEndpoint != to))
                Stop(RetailGroundSearchStatus.UnsupportedPolicyObservation, RetailGroundSearchTermination.UnknownPolicy, "Segment result has an unsupported/noncoherent endpoint.");
            return value;
        }
        private void InspectFields(IReadOnlyList<RetailSearchPoint> points, RetailSearchPolicyPurpose purpose)
        {
            for (int i = 1; i < points.Count; i++)
            {
                if (!Field(points[i - 1], points[i], purpose)) continue;
                float offset = System.MathF.Min(o.Actor.UpperBoundMetres, 1f);
                var from = points[i - 1] with { Z = points[i - 1].Z + offset };
                var to = points[i] with { Z = points[i].Z + offset };
                if (!from.IsFinite || !to.IsFinite) Stop(RetailGroundSearchStatus.UnsupportedArithmetic, RetailGroundSearchTermination.UnknownPolicy, "Field retry height overflow.");
                if (Field(from, to, purpose)) fieldDiagnostics++;
            }
        }
        private bool Field(RetailSearchPoint from, RetailSearchPoint to, RetailSearchPolicyPurpose purpose)
        {
            BeforePolicy(); var value = policy.FieldObjectCollision(from, to, purpose);
            Consume(value.Stamp, "FieldObjectCollision", purpose); return value.Value;
        }
        private void BeforePolicy() { if (calls >= limits.MaxPolicyCalls) Defensive("Defensive policy-call limit reached."); calls++; }
        private void Consume(RetailSearchReplayStamp stamp, string operation, RetailSearchPolicyPurpose? purpose)
        {
            consumed.Add(new(calls, operation, purpose, stamp.Revision, stamp.Evidence));
            if (!stamp.Known || string.IsNullOrWhiteSpace(stamp.Evidence) || stamp.Revision != o.PolicyRevision)
                Stop(RetailGroundSearchStatus.UnsupportedPolicyObservation, RetailGroundSearchTermination.UnknownPolicy, "Missing, unknown or revision-mismatched policy response.");
        }
        private void ValidateObservation()
        {
            if (!o.Start.IsFinite || !o.Goal.IsFinite || !o.Bounds.Minimum.IsFinite || !o.Bounds.Maximum.IsFinite ||
                o.Bounds.Minimum.X >= o.Bounds.Maximum.X || o.Bounds.Minimum.Y >= o.Bounds.Maximum.Y || o.Bounds.Minimum.Z >= o.Bounds.Maximum.Z ||
                !float.IsFinite(o.Actor.UpperBoundMetres) || o.Actor.UpperBoundMetres < 0 ||
                string.IsNullOrWhiteSpace(o.PolicyRevision) || o.Sources is null || o.SurfaceQueryOptions is null ||
                o.StartSurface is null || o.GoalSurface is null || !Enum.IsDefined(o.Queue) ||
                limits.MaxNodes < 1 || limits.MaxPops < 0 || limits.MaxOutputPoints < 2 || limits.MaxPolicyCalls < 0 || limits.MaxShortcutSteps < 0 || limits.MaxHeapRankCapacity < 1)
                Stop(RetailGroundSearchStatus.InvalidObservation, RetailGroundSearchTermination.InvalidInput, "Invalid explicit observation or support limits.");
            if (o.Start.X < 0 || o.Start.Y < 0 || o.Goal.X < 0 || o.Goal.Y < 0 ||
                o.Start.X * 2f >= 32768f || o.Start.Y * 2f >= 32768f || o.Goal.X * 2f >= 32768f || o.Goal.Y * 2f >= 32768f)
                Stop(RetailGroundSearchStatus.UnsupportedSurface, RetailGroundSearchTermination.SurfaceBoundary, "Signed coordinate narrowing/wrapping is unsupported.");
        }
        private void ValidateSources()
        {
            var s = o.Sources;
            if (s.Path is null || s.PathIdentity is null || s.PolicyContext is null ||
                !ValidAsset(s.PathIdentity.Asset) || !ValidAsset(s.NativeNpcBinary) ||
                (s.CollisionAsset is not null && !ValidAsset(s.CollisionAsset)) ||
                string.IsNullOrWhiteSpace(s.PolicyContext.ActorIdentity) || string.IsNullOrWhiteSpace(s.PolicyContext.DynamicContextIdentity) ||
                string.IsNullOrWhiteSpace(s.PolicyContext.CollisionContextIdentity) || string.IsNullOrWhiteSpace(s.PolicyContext.Evidence) ||
                o.SurfaceQueryOptions.MaxIndexedSparseRecords < 0 || o.SurfaceQueryOptions.MaxCandidates < 1)
                Stop(RetailGroundSearchStatus.InvalidObservation, RetailGroundSearchTermination.InvalidInput, "Invalid asset/context declarations or surface query support limits.");
            if (!s.NativeNpcBinary.Sha256.Equals(SupportedNpcBinarySha256, StringComparison.OrdinalIgnoreCase) ||
                s.NativeNpcBinary.ByteLength != SupportedNpcBinaryBytes)
                SourceMismatch("Declared NPC binary does not match this unit's pinned native evidence.");
            sourceValidation = sourceValidation with { NativeBinaryDeclarationMatchesPin = true };
            if (!Matches(s.PathIdentity, s.Path.RawFile, s.Path.BuildGuid, s.Path.WorldWidthMetres, s.Path.WorldHeightMetres))
                SourceMismatch("Loaded path bytes/GUID/declared dimensions differ from the path identity receipt.");
            sourceValidation = sourceValidation with { PathBytesVerified = true };
            if ((s.Terrain is null) != (s.TerrainIdentity is null)) SourceMismatch("Terrain object and individual identity receipt must be supplied together.");
            if (s.Terrain is { } terrain)
            {
                if (s.TerrainIdentity is null || !ValidAsset(s.TerrainIdentity.Asset) ||
                    !Matches(s.TerrainIdentity, terrain.RawFile, terrain.BuildGuid, terrain.WorldWidthMetres, terrain.WorldHeightMetres))
                    SourceMismatch("Loaded terrain bytes/GUID/declared dimensions differ from its individual identity receipt.");
                sourceValidation = sourceValidation with { TerrainBytesVerified = true };
            }
            // Construct from those exact verified objects: an opaque query cannot establish which private sources it owns.
            surfaces = new(s.Path, s.Terrain, o.SurfaceQueryOptions);
        }
        private static bool ValidAsset(RetailSearchAssetIdentity? asset) => asset is not null &&
            !string.IsNullOrWhiteSpace(asset.Identity) && asset.ByteLength >= 0 && asset.Sha256 is { Length: 64 } &&
            asset.Sha256.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F');
        private static bool Matches(RetailSearchMapAssetIdentity identity, ReadOnlyMemory<byte> bytes, Guid? guid, int width, int height) =>
            identity.Asset.ByteLength == bytes.Length && identity.BuildGuid == guid && identity.WorldWidthMetres == width && identity.WorldHeightMetres == height &&
            Convert.ToHexString(SHA256.HashData(bytes.Span)).Equals(identity.Asset.Sha256, StringComparison.OrdinalIgnoreCase);
        private static void SourceMismatch(string detail) => Stop(RetailGroundSearchStatus.UnsupportedSourceIdentity, RetailGroundSearchTermination.InvalidInput, detail);
        private RetailGroundSearchResult Direct(RetailSearchPoint goal) => Result(RetailGroundSearchStatus.DirectOrClippedResult,
            RetailGroundSearchTermination.None, [o.Start, goal]);
        private RetailGroundSearchResult NoRoute(RetailGroundSearchTermination reason) => Result(RetailGroundSearchStatus.NoRoute, reason);
        private RetailGroundSearchResult Result(RetailGroundSearchStatus status, RetailGroundSearchTermination reason,
            IReadOnlyList<RetailSearchPoint>? points = null, IReadOnlyList<RetailPathSurfaceRef>? chain = null, string? detail = null) =>
            new(status, reason, Array.AsReadOnly(points?.ToArray() ?? []), Array.AsReadOnly(chain?.ToArray() ?? []), nodes.Count, pops, calls,
                fieldDiagnostics, consumed.AsReadOnly(), detail)
            { Sources = o.Sources, SourceValidation = sourceValidation,
                SurfaceCompatibilityDiagnostics = surfaces?.CompatibilityDiagnostics ?? Array.Empty<string>(),
                PolicyRevision = o.PolicyRevision, SyntheticFixture = o.SyntheticFixture };
        private static bool Same(RetailPathSurfaceRef a, RetailPathSurfaceRef b) => ReferenceEquals(a.Node, b.Node) && a.NativeId == b.NativeId;
        private static bool Near(RetailSearchPoint a, RetailSearchPoint b) => a.X - b.X < 0.0001f && a.X - b.X > -0.0001f &&
            a.Y - b.Y < 0.0001f && a.Y - b.Y > -0.0001f && a.Z - b.Z < 0.0001f && a.Z - b.Z > -0.0001f;
        private static float Squared(RetailSearchPoint a, RetailSearchPoint b)
        { float x = a.X - b.X, y = a.Y - b.Y, z = a.Z - b.Z; float value = (y * y + x * x) + z * z; Finite(value); return value; }
        private static float Distance(RetailSearchPoint a, RetailSearchPoint b) => System.MathF.Sqrt(Squared(a, b));
        private static void Finite(float value) { if (!float.IsFinite(value)) Stop(RetailGroundSearchStatus.UnsupportedArithmetic, RetailGroundSearchTermination.InvalidInput, "Binary32 arithmetic overflow/nonfinite result."); }
        private static void Defensive(string detail) => Stop(RetailGroundSearchStatus.DefensiveResourceLimit, RetailGroundSearchTermination.DefensiveLimit, detail);
        [System.Diagnostics.CodeAnalysis.DoesNotReturn]
        private static void Stop(RetailGroundSearchStatus status, RetailGroundSearchTermination reason, string detail) => throw new SearchStop(status, reason, detail);
    }
}
