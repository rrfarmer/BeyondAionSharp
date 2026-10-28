using System.Buffers.Binary;
using System.Security.Cryptography;
using Aion.GameServer.GeoEngine.RetailPath;

namespace Aion.GameServer.Tests;

public sealed class RetailGroundPathSearchTests
{
    [Theory]
    [InlineData(RetailGroundQueueKind.LinkedList)] [InlineData(RetailGroundQueueKind.FibonacciHeap)]
    public void Cost500IsSearchedButNotSmoothedAndExactRequestedEndpointsSurvive(RetailGroundQueueKind kind)
    {
        var f = Fixture([(1, 1, 0), (2, 1, 0)], (1, 1), (3, 1), kind);
        var observation = f.Observation with { Start = new(0.81f, 0.72f, 0), Goal = new(1.9f, 0.68f, 0) };
        var policy = new Replay { Cost = 500 };
        var result = RetailGroundPathSearch.Search(observation, policy);
        Assert.Equal(RetailGroundSearchStatus.Completed, result.Status);
        Assert.Equal(new[] { observation.Start, new RetailSearchPoint(1.25f, 0.75f, 0), observation.Goal }, result.Points);
        Assert.Equal(new[] { 1, 2, 3 }, result.NodeChain.Select(s => s.CellX));
        Assert.Equal(3, result.PoppedNodes); Assert.Equal(3, result.AllocatedNodes);
        Assert.True(result.NativeCallerClearsPreviousPath);
        Assert.Equal(f.Observation.Sources, result.Sources); Assert.True(result.SourceValidation.PathBytesVerified); Assert.True(result.SyntheticFixture);
    }

    [Theory]
    [InlineData(499f, true)] [InlineData(500f, false)] [InlineData(1999f, false)] [InlineData(2000f, false)]
    public void ShortcutAndNewOpenAdmissionKeepDifferentThresholds(float cost, bool direct)
    {
        var f = Fixture([(1, 1, 0)], (1, 1), (2, 1));
        var result = RetailGroundPathSearch.Search(f.Observation, new Replay { Cost = cost });
        if (cost >= 2000)
        {
            Assert.False(result.Success); Assert.Equal(RetailGroundSearchTermination.QueueExhausted, result.Termination);
            Assert.Equal(2, result.AllocatedNodes); // blocked goal is allocated but not enqueued
            Assert.Equal(1, result.PoppedNodes);
        }
        else Assert.Equal(direct ? RetailGroundSearchStatus.DirectOrClippedResult : RetailGroundSearchStatus.Completed, result.Status);
    }

    [Theory]
    [InlineData(99.5f, RetailSearchPolicyPurpose.MissingStartFallback)]
    [InlineData(100f, RetailSearchPolicyPurpose.DirectDistanceBranch)]
    public void Distance100BranchIsNotMissingDataFallback(float distance, RetailSearchPolicyPurpose expected)
    {
        var f = Fixture([], (1, 1), (2, 1));
        var observation = f.Observation with
        {
            Goal = f.Observation.Start with { X = f.Observation.Start.X + distance },
            Bounds = new(new(0, 0, -10240), new(512, 512, 10240)), NativePathTablesAvailable = false,
            StartSurface = new(RetailSearchSurfaceState.Missing, null), GoalSurface = new(RetailSearchSurfaceState.Missing, null)
        };
        var policy = new Replay();
        var result = RetailGroundPathSearch.Search(observation, policy);
        Assert.True(result.Success); Assert.Equal(0, result.AllocatedNodes);
        Assert.Equal(expected, Assert.Single(policy.Segments).Purpose);
    }

    [Fact]
    public void MissingStartMayClipButMissingGoalCannotGainGenericFallback()
    {
        var f = Fixture([], (1, 1), (3, 1));
        var clip = new RetailSearchPoint(1.1f, 0.75f, 0);
        var policy = new Replay { Segment = (from, to, purpose) => (true, clip) };
        var missingStart = f.Observation with { StartSurface = new(RetailSearchSurfaceState.Missing, null) };
        var clipped = RetailGroundPathSearch.Search(missingStart, policy);
        Assert.Equal(RetailGroundSearchStatus.DirectOrClippedResult, clipped.Status); Assert.Equal(clip, clipped.Points[^1]);
        var noProgress = RetailGroundPathSearch.Search(missingStart, new Replay { Segment = (from, to, purpose) => (true, from) });
        Assert.Equal(RetailGroundSearchTermination.NoProgress, noProgress.Termination);
        var missingGoal = RetailGroundPathSearch.Search(f.Observation with { GoalSurface = new(RetailSearchSurfaceState.Missing, null) }, new Replay());
        Assert.Equal(RetailGroundSearchTermination.MissingGoal, missingGoal.Termination); Assert.Equal(0, missingGoal.PolicyCalls);
    }

    [Fact]
    public void DirectDistanceCollisionDoesNotReturnItsClippedProgress()
    {
        var f = Fixture([], (1, 1), (2, 1));
        var observation = f.Observation with { Goal = new(101, 1, 0), Bounds = new(new(0, 0, -10240), new(512, 512, 10240)) };
        var result = RetailGroundPathSearch.Search(observation, new Replay { Segment = (from, to, purpose) => (true, new(10, 1, 0)) });
        Assert.Equal(RetailGroundSearchTermination.SegmentBlocked, result.Termination); Assert.Empty(result.Points);
    }

    [Fact]
    public void BoundsAreHalfOpenAndNearEndpointEpsilonIsStrict()
    {
        var f = Fixture([], (0, 0), (0, 0));
        var start = new RetailSearchPoint(0, 0, 0);
        var near = f.Observation with { Start = start, Goal = new(0.000099f, 0, 0) };
        Assert.Equal(RetailGroundSearchTermination.EqualEndpoints, RetailGroundPathSearch.Search(near, new Replay()).Termination);
        var exact = RetailGroundPathSearch.Search(near with { Goal = new(0.0001f, 0, 0) }, new Replay());
        Assert.Equal(RetailGroundSearchStatus.DirectOrClippedResult, exact.Status);
        var outside = RetailGroundPathSearch.Search(f.Observation with { Goal = new(128, 1, 0) }, new Replay());
        Assert.Equal(RetailGroundSearchTermination.RejectedBounds, outside.Termination); Assert.Equal(0, outside.PolicyCalls);
    }

    [Theory]
    [InlineData(1)] [InlineData(2)]
    public void JpsAndComparisonDoNotSilentlyRunAstar(int selector)
    {
        var f = Fixture([], (1, 1), (2, 1));
        var result = RetailGroundPathSearch.Search(f.Observation with { NativeSelector = selector }, new Replay());
        Assert.Equal(RetailGroundSearchStatus.UnsupportedSelector, result.Status); Assert.Equal(0, result.PolicyCalls);
    }

    [Theory]
    [InlineData(-1)] [InlineData(3)]
    public void NativeOtherSelectorValuesTakeTheDefaultAstarBranch(int selector)
    {
        var f = Fixture([(1, 1, 0)], (1, 1), (2, 1));
        Assert.True(RetailGroundPathSearch.Search(f.Observation with { NativeSelector = selector }, new Replay()).Success);
    }

    [Fact]
    public void GuardAndUnresolvedInputsFailVisiblyWithoutRuntimeDefaults()
    {
        var f = Fixture([(1, 1, 0)], (1, 1), (2, 1), RetailGroundQueueKind.FibonacciHeap);
        Assert.Equal(RetailGroundSearchStatus.UnsupportedHeapCapacity,
            RetailGroundPathSearch.Search(f.Observation with { ObservedHeapRankCapacity = null }, new Replay { Cost = 500 }).Status);
        Assert.Equal(RetailGroundSearchStatus.UnsupportedActor,
            RetailGroundPathSearch.Search(f.Observation with { Actor = f.Observation.Actor with { NativeVirtual18AlternateMotion = true } }, new Replay()).Status);
        Assert.Equal(RetailGroundSearchTermination.ActorGuard,
            RetailGroundPathSearch.Search(f.Observation with { Actor = f.Observation.Actor with { NativeVirtual19Rejects = true } }, new Replay()).Termination);
        Assert.Equal(RetailGroundSearchStatus.AmbiguousSurface,
            RetailGroundPathSearch.Search(f.Observation with { StartSurface = new(RetailSearchSurfaceState.Ambiguous, null) }, new Replay()).Status);
        Assert.Equal(RetailGroundSearchStatus.InvalidObservation,
            RetailGroundPathSearch.Search(f.Observation with { Goal = new(float.NaN, 0, 0) }, new Replay()).Status);
    }

    [Theory]
    [InlineData(false, "fixture")] [InlineData(true, "stale")]
    public void UnknownOrRevisionMismatchedPolicyCannotAssumeAClearCorridor(bool known, string revision)
    {
        var f = Fixture([(1, 1, 0)], (1, 1), (2, 1));
        var result = RetailGroundPathSearch.Search(f.Observation, new Replay { Known = known, Revision = revision });
        Assert.Equal(RetailGroundSearchStatus.UnsupportedPolicyObservation, result.Status);
        Assert.Single(result.ConsumedPolicy); Assert.Empty(result.Points);
        Assert.False(result.NativeCallerClearsPreviousPath); Assert.True(result.RequiresAdapterRejectionInvalidation);
    }

    [Fact]
    public void AssetBytesMetadataAndEndpointOwnershipAreCheckedWhileExternalDeclarationsStayExplicit()
    {
        var f = Fixture([(1, 1, 0)], (1, 1), (2, 1));
        var sources = f.Observation.Sources;
        var wrongBytes = sources with { PathIdentity = sources.PathIdentity with { Asset = sources.PathIdentity.Asset with { Sha256 = new string('a', 64) } } };
        var wrongDimensions = sources with { PathIdentity = sources.PathIdentity with { WorldWidthMetres = 256 } };
        var wrongGuid = sources with { PathIdentity = sources.PathIdentity with { BuildGuid = new Guid("00000000-0000-0000-0000-000000000001") } };
        var wrongNative = sources with { NativeNpcBinary = sources.NativeNpcBinary with { Sha256 = new string('a', 64) } };
        foreach (var mismatch in new[] { wrongBytes, wrongDimensions, wrongGuid, wrongNative })
        {
            var result = RetailGroundPathSearch.Search(f.Observation with { Sources = mismatch }, new Replay());
            Assert.Equal(RetailGroundSearchStatus.UnsupportedSourceIdentity, result.Status); Assert.Equal(0, result.PolicyCalls);
            Assert.True(result.RequiresAdapterRejectionInvalidation); Assert.False(result.SourceValidation.RuntimeVerified);
        }
        var another = Fixture([(1, 1, 0)], (1, 1), (2, 1));
        var foreign = RetailGroundPathSearch.Search(f.Observation with { StartSurface = another.Observation.StartSurface }, new Replay());
        Assert.Equal(RetailGroundSearchStatus.InvalidObservation, foreign.Status);
        var declared = RetailGroundPathSearch.Search(f.Observation with { SyntheticFixture = false }, new Replay());
        Assert.True(declared.Success); Assert.True(declared.SourceValidation.PathBytesVerified);
        Assert.True(declared.SourceValidation.NativeBinaryDeclarationMatchesPin);
        Assert.False(declared.SourceValidation.RuntimeVerified); Assert.False(declared.SourceValidation.CollisionBytesVerified);
    }

    [Fact]
    public void OptionalTerrainRequiresItsOwnMatchingBytesAndIdentityRatherThanPathGuidAlone()
    {
        var f = Fixture([(1, 1, 0)], (1, 1), (2, 1));
        var raw = new byte[128 + 8 + 4 * 4];
        // Native HeightMap40 header-present tag and one2m interval per axis. Flat path nodes need no terrain projection.
        BinaryPrimitives.WriteUInt32LittleEndian(raw, 0xaa55aa55);
        BinaryPrimitives.WriteInt32LittleEndian(raw.AsSpan(128), 1); BinaryPrimitives.WriteInt32LittleEndian(raw.AsSpan(132), 1);
        var read = RetailHeightMap40Reader.Read(raw); Assert.True(read.Success, read.Error?.ToString());
        var terrain = read.Data!;
        var identity = new RetailSearchMapAssetIdentity(Asset("synthetic-terrain", raw), terrain.BuildGuid, 2, 2);
        var sources = f.Observation.Sources with { Terrain = terrain, TerrainIdentity = identity };
        var valid = RetailGroundPathSearch.Search(f.Observation with { Sources = sources }, new Replay());
        Assert.True(valid.Success); Assert.True(valid.SourceValidation.TerrainBytesVerified); // projection still unavailable due dimension mismatch
        Assert.Contains(valid.SurfaceCompatibilityDiagnostics, d => d.Contains("dimensions differ"));
        var mismatch = sources with { TerrainIdentity = identity with { Asset = identity.Asset with { Sha256 = new string('b', 64) } } };
        Assert.Equal(RetailGroundSearchStatus.UnsupportedSourceIdentity,
            RetailGroundPathSearch.Search(f.Observation with { Sources = mismatch }, new Replay()).Status);
        Assert.Equal(RetailGroundSearchStatus.UnsupportedSourceIdentity,
            RetailGroundPathSearch.Search(f.Observation with { Sources = sources with { TerrainIdentity = null } }, new Replay()).Status);
    }

    [Theory]
    [InlineData(-1f)] [InlineData(float.NaN)] [InlineData(float.PositiveInfinity)]
    public void InvalidActorCostsDoNotBecomeNativeNoRoute(float cost)
    {
        var f = Fixture([(1, 1, 0)], (1, 1), (2, 1));
        var result = RetailGroundPathSearch.Search(f.Observation, new Replay { Cost = cost });
        Assert.Equal(RetailGroundSearchStatus.UnsupportedArithmetic, result.Status); Assert.Empty(result.Points);
    }

    [Fact]
    public void UnknownFieldEvidenceAfterGraphSuccessCannotProduceACompletedResult()
    {
        var f = Fixture([(1, 1, 0)], (1, 1), (2, 1));
        var result = RetailGroundPathSearch.Search(f.Observation, new Replay { Cost = 500, UnknownFields = true });
        Assert.Equal(RetailGroundSearchStatus.UnsupportedPolicyObservation, result.Status); Assert.Empty(result.Points);
        Assert.Equal("FieldObjectCollision", result.ConsumedPolicy[^1].Operation);
        Assert.Equal(RetailSearchPolicyPurpose.BeforeSmoothing, result.ConsumedPolicy[^1].Purpose);
    }

    [Fact]
    public void UnavailableTerrainLookupIsAnUnsupportedSurfaceRatherThanAMissingStartFallback()
    {
        var f = Fixture([(1, 1, 0)], (1, 1), (2, 1), terrainNodes: true);
        var result = RetailGroundPathSearch.Search(f.Observation, new Replay());
        Assert.Equal(RetailGroundSearchStatus.UnsupportedSurface, result.Status); Assert.Equal(0, result.PolicyCalls);
    }

    [Fact]
    public void EveryDefensiveLimitIsDifferentFromNoRoute()
    {
        var f = Fixture([(1, 1, 0)], (1, 1), (2, 1));
        var policy = new Replay { Cost = 500 };
        Assert.Equal(RetailGroundSearchStatus.DefensiveResourceLimit, RetailGroundPathSearch.Search(f.Observation, policy, new() { MaxPolicyCalls = 0 }).Status);
        Assert.Equal(RetailGroundSearchStatus.DefensiveResourceLimit, RetailGroundPathSearch.Search(f.Observation, policy, new() { MaxPops = 0 }).Status);
        Assert.Equal(RetailGroundSearchStatus.DefensiveResourceLimit, RetailGroundPathSearch.Search(f.Observation, policy, new() { MaxNodes = 1 }).Status);
        Assert.Equal(RetailGroundSearchStatus.DefensiveResourceLimit, RetailGroundPathSearch.Search(f.Observation, policy, new() { MaxShortcutSteps = 0 }).Status);
        var longer = Fixture([(1, 1, 0), (2, 1, 0)], (1, 1), (3, 1));
        Assert.Equal(RetailGroundSearchStatus.DefensiveResourceLimit, RetailGroundPathSearch.Search(longer.Observation, policy, new() { MaxOutputPoints = 2 }).Status);
        var heap = Fixture([(1, 1, 0)], (1, 1), (2, 1), RetailGroundQueueKind.FibonacciHeap);
        Assert.Equal(RetailGroundSearchStatus.DefensiveResourceLimit, RetailGroundPathSearch.Search(heap.Observation, policy, new() { MaxHeapRankCapacity = 1 }).Status);
    }

    [Fact]
    public void NativeGoalCheckOccursAfterItsNeighborsAndBadIncomingNonGoalStaysClosed()
    {
        // S(1,1)->D(2,1) first costs2000 and closes D; S->E(1,2)->F(2,2)->D later cannot reopen it.
        var f = Fixture([(1, 1, 0), (1, 1, 1), (1, 2, 0), (2, 2, 3), (2, 1, 0)], (1, 1), (3, 1));
        var policy = new Replay { CostFor = (a, b) => a.CellX == 1 && a.CellY == 1 && b.CellX == 2 && b.CellY == 1 ? 2000 : 500 };
        var result = RetailGroundPathSearch.Search(f.Observation, policy);
        Assert.False(result.Success); Assert.Equal(RetailGroundSearchTermination.QueueExhausted, result.Termination);
        Assert.DoesNotContain(policy.Costs, c => c.From.CellX == 2 && c.From.CellY == 1);

        var postGoal = Fixture([(1, 1, 0), (2, 1, 0)], (1, 1), (2, 1));
        var postPolicy = new Replay { Cost = 500 };
        Assert.True(RetailGroundPathSearch.Search(postGoal.Observation, postPolicy).Success);
        Assert.Contains(postPolicy.Costs, c => c.From.CellX == 2 && c.To.CellX == 3);
    }

    [Fact]
    public void RejectedGoalStaysNewSoAnotherIncomingEdgeCanEnqueueIt()
    {
        // Unlike a rejected non-goal, D remains new after S->D costs2000.
        var f = Fixture([(1, 1, 0), (1, 1, 1), (1, 2, 0), (2, 2, 3)], (1, 1), (2, 1));
        var policy = new Replay { CostFor = (a, b) => a.CellX == 1 && a.CellY == 1 && b.CellX == 2 && b.CellY == 1 ? 2000 : 500 };
        var result = RetailGroundPathSearch.Search(f.Observation, policy);
        Assert.True(result.Success);
        Assert.Equal(new[] { (1, 1), (1, 2), (2, 2), (2, 1) }, result.NodeChain.Select(s => (s.CellX, s.CellY)));
    }

    [Fact]
    public void AstarRequiresBothOrdersForADiagonalRatherThanJpsAnyOrder()
    {
        var f = Fixture([(1, 1, 0), (2, 1, 1)], (1, 1), (2, 2));
        var result = RetailGroundPathSearch.Search(f.Observation, new Replay { Cost = 500 });
        Assert.True(result.Success);
        Assert.Equal(new[] { (1, 1), (2, 1), (2, 2) }, result.NodeChain.Select(n => (n.CellX, n.CellY)));
        Assert.Equal(3, result.PoppedNodes);
    }

    [Theory]
    [InlineData(RetailGroundQueueKind.LinkedList)] [InlineData(RetailGroundQueueKind.FibonacciHeap)]
    public void BothOrdersMayEndOnDifferentLayersAndAstarKeepsTheSecond(RetailGroundQueueKind kind)
    {
        using var ms = new MemoryStream(); using var writer = new BinaryWriter(ms);
        var header = new byte[128]; BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(16), 5); BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(18), 6);
        writer.Write(header); writer.Write(4); writer.Write(0); writer.Write(32);
        for (int i = 0; i < 32; i++) writer.Write(i == 0 ? 0x81u : 0u);
        void Grid(int height, byte mode, uint[] edges)
        { writer.Write((byte)1); writer.Write(mode); foreach (uint edge in edges) writer.Write(edge); writer.Write(height); writer.Write(new byte[512]); }
        writer.Write((byte)1); Grid(0, 15, [0x7fffffff, 0x7fffffff, uint.MaxValue, uint.MaxValue]); // S at31,31
        writer.Write((byte)1); Grid(0, 15, [uint.MaxValue, 0xfffffffe, uint.MaxValue, uint.MaxValue]); // E->lower D
        writer.Write((byte)1); Grid(0, 14, [0, uint.MaxValue, uint.MaxValue, uint.MaxValue]); // N->portal layer1
        writer.Write((byte)2); Grid(200, 15, [uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue]);
        Grid(0, 15, [uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue]);
        var bytes = ms.ToArray(); var parsed = RetailPathDataReader.Read(bytes, new(32, 32)); Assert.True(parsed.Success, parsed.Error?.ToString());
        var query = new RetailPathSurfaceQuery(parsed.Data!);
        var a = query.GetSurface(31, 31, 0).Target!; var b = query.GetSurface(32, 32, 1).Target!;
        var observation = Observe(a, b, bytes, 32, kind) with { Goal = new(b.CentreXMetres, b.CentreYMetres, 2f) };
        var policy = new Replay
        {
            CostFor = (from, to) => from.CellX == 31 && from.CellY == 31 && to.CellX == 32 && to.CellY == 32 ? 50 : 500,
            Segment = (from, to, purpose) => (purpose == RetailSearchPolicyPurpose.InitialShortcut, to)
        };
        var result = RetailGroundPathSearch.Search(observation, policy);
        Assert.Equal(RetailGroundSearchStatus.Completed, result.Status); Assert.Equal(2, result.NodeChain.Count);
        Assert.Equal(1, result.NodeChain[^1].Layer); Assert.Equal(2f, result.Points[^1].Z);
        var bounded = RetailGroundPathSearch.Search(observation with { SurfaceQueryOptions = new() { MaxCandidates = 1 } }, new Replay());
        Assert.Equal(RetailGroundSearchStatus.DefensiveResourceLimit, bounded.Status); Assert.Equal(0, bounded.PolicyCalls);
    }

    [Fact]
    public void ZeroBresenhamErrorTakesAMajorStepBeforeItsDiagonal()
    {
        var f = Fixture([(1, 1, 0), (2, 1, 0), (2, 1, 1), (3, 1, 1), (2, 2, 0)], (1, 1), (3, 2));
        var policy = new Replay();
        Assert.Equal(RetailGroundSearchStatus.DirectOrClippedResult, RetailGroundPathSearch.Search(f.Observation, policy).Status);
        Assert.Equal(2, policy.Costs.Count);
        Assert.Equal((2, 1), (policy.Costs[0].To.CellX, policy.Costs[0].To.CellY));
        Assert.Equal((3, 2), (policy.Costs[1].To.CellX, policy.Costs[1].To.CellY));
    }

    [Fact]
    public void SmoothingRetriesRetainedAnchorAndFieldCollisionsAreDiagnosticOnly()
    {
        var f = Fixture([(1, 1, 0), (2, 1, 0), (3, 1, 0)], (1, 1), (4, 1));
        var policy = new Replay
        {
            Cost = 50, AllFieldsCollide = true,
            Segment = (from, to, purpose) => (purpose == RetailSearchPolicyPurpose.InitialShortcut, to)
        };
        var result = RetailGroundPathSearch.Search(f.Observation, policy);
        Assert.Equal(RetailGroundSearchStatus.Completed, result.Status);
        Assert.Equal(new[] { f.Observation.Start, f.Observation.Goal }, result.Points);
        var smoothing = policy.Segments.Where(s => s.Purpose == RetailSearchPolicyPurpose.Smoothing).ToArray();
        Assert.Equal(2, smoothing.Length);
        Assert.All(smoothing, s => Assert.Equal(f.Observation.Start, s.From));
        Assert.Equal(1.75f, smoothing[0].To.X); Assert.Equal(f.Observation.Goal, smoothing[1].To);
        Assert.Equal(4, result.DiagnosticFieldCollisions); // three original segments plus one smoothed, each logs after retry
        Assert.Contains(policy.Fields, f => f.From.Z == 1f && f.To.Z == 1f); // native upper retry capped at1m
    }

    [Fact]
    public void SameNodeCapabilityCanSkipTopologyCostButStillRequiresActorSegment()
    {
        var f = Fixture([], (1, 1), (5, 1), plainNodes: true);
        var policy = new Replay { Capability = true };
        Assert.True(RetailGroundPathSearch.Search(f.Observation, policy).Success);
        Assert.Empty(policy.Costs); Assert.Single(policy.Segments);
    }

    [Fact]
    public void Binary32RadiusIsInclusiveAndKeepsItsFiftyMetreFloor()
    {
        // Start->detour100 half-metre cells away->goal is the only directed route; radius floor is50m.
        var edges = new List<(int, int, int)>();
        for (int y = 1; y < 101; y++) edges.Add((1, y, 1));
        edges.Add((1, 101, 0));
        for (int y = 101; y > 1; y--) edges.Add((2, y, 3));
        var f = Fixture(edges, (1, 1), (2, 1));
        var boundary = RetailGroundPathSearch.Search(f.Observation, new Replay { Cost = 500 });
        Assert.True(boundary.Success);
        Assert.Contains(boundary.NodeChain, n => n.CellY == 101);
        edges.Remove((1, 101, 0)); edges.Add((1, 101, 1)); edges.Add((1, 102, 0)); edges.Add((2, 102, 3));
        var beyond = Fixture(edges, (1, 1), (2, 1));
        Assert.False(RetailGroundPathSearch.Search(beyond.Observation, new Replay { Cost = 500 }).Success);
    }

    [Theory]
    [InlineData(20000, RetailGroundSearchTermination.NativePathPointLimit)]
    [InlineData(20992, RetailGroundSearchTermination.NativeNodePoolLimit)]
    public void LongDirectedSnakeSeparatesNativePointAndDistinctNodeLimits(int targetIndex, RetailGroundSearchTermination expected)
    {
        var edges = new List<(int, int, int)>();
        for (int index = 0; index < targetIndex; index++)
        {
            var a = Snake(index); var b = Snake(index + 1);
            edges.Add((a.X, a.Y, b.Y > a.Y ? 1 : b.X > a.X ? 0 : 2));
        }
        var target = Snake(targetIndex);
        var f = Fixture(edges, (0, 0), target);
        var result = RetailGroundPathSearch.Search(f.Observation, new Replay { Cost = 500 });
        Assert.False(result.Success); Assert.Equal(expected, result.Termination);
        Assert.True(result.PoppedNodes <= 20_480);
        if (expected == RetailGroundSearchTermination.NativeNodePoolLimit) Assert.Equal(20_480, result.AllocatedNodes);
        else { Assert.Equal(20_001, result.AllocatedNodes); Assert.Equal(20_001, result.PoppedNodes); }
    }

    private static (int X, int Y) Snake(int index) => (index / 128 % 2 == 0 ? index % 128 : 127 - index % 128, index / 128);
    private sealed record Setup(RetailGroundSearchObservation Observation);
    private static Setup Fixture(IEnumerable<(int X, int Y, int Direction)> edges, (int X, int Y) start, (int X, int Y) goal,
        RetailGroundQueueKind queue = RetailGroundQueueKind.LinkedList, bool terrainNodes = false, bool plainNodes = false)
    {
        const int width = 128, sectors = 8;
        var masks = new byte[sectors * sectors][];
        var boundaryMasks = new uint[sectors * sectors][];
        for (int i = 0; i < masks.Length; i++) { masks[i] = new byte[512]; boundaryMasks[i] = [uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue]; }
        foreach (var (x, y, direction) in edges)
        {
            int cell = (y & 31) * 32 + (x & 31);
            int sector = y / 32 * sectors + x / 32;
            masks[sector][cell / 2] |= (byte)((1 << direction) << ((cell & 1) * 4));
            int tx = x + (direction == 0 ? 1 : direction == 2 ? -1 : 0), ty = y + (direction == 1 ? 1 : direction == 3 ? -1 : 0);
            if (tx / 32 != x / 32 || ty / 32 != y / 32)
                boundaryMasks[sector][direction] &= ~(1u << ((direction & 1) == 0 ? y & 31 : x & 31));
        }
        using var ms = new MemoryStream(); using var writer = new BinaryWriter(ms);
        var header = new byte[128]; BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(16), 5); BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(18), 6);
        writer.Write(header); writer.Write(4); writer.Write(0); writer.Write(0);
        for (int sector = 0; sector < masks.Length; sector++)
        {
            var mask = masks[sector];
            writer.Write((byte)1); writer.Write((byte)((terrainNodes ? 2 : 0) + (plainNodes ? 0 : 1))); writer.Write((byte)15);
            for (int d = 0; d < 4; d++) writer.Write(boundaryMasks[sector][d]);
            if (!terrainNodes) writer.Write(0);
            if (!plainNodes) writer.Write(mask);
        }
        var bytes = ms.ToArray();
        var read = RetailPathDataReader.Read(bytes, new(width, width)); Assert.True(read.Success, read.Error?.ToString());
        var query = new RetailPathSurfaceQuery(read.Data!);
        var a = query.GetSurface(start.X, start.Y, 0).Target!; var b = query.GetSurface(goal.X, goal.Y, 0).Target!;
        return new(Observe(a, b, bytes, width, queue));
    }

    private static RetailGroundSearchObservation Observe(RetailPathSurfaceRef a, RetailPathSurfaceRef b, byte[] bytes, int width, RetailGroundQueueKind queue) =>
        new(new(a.CentreXMetres, a.CentreYMetres, 0), new(b.CentreXMetres, b.CentreYMetres, 0),
            new(new(0, 0, -10240), new(width, width, 10240)), new(true, false, false, false, 2f), 0, queue,
            queue == RetailGroundQueueKind.FibonacciHeap ? 16 : null, true,
            new(RetailSearchSurfaceState.Resolved, a), new(RetailSearchSurfaceState.Resolved, b), "fixture",
            Sources(a.Owner, bytes), new(), true);

    private static RetailSearchAssetIdentity Asset(string identity, ReadOnlySpan<byte> bytes) =>
        new(identity, Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), bytes.Length);
    private static RetailSearchSourceReceipts Sources(RetailPathData path, byte[] bytes) => new(path,
        new(Asset("synthetic-path", bytes), path.BuildGuid, path.WorldWidthMetres, path.WorldHeightMetres), null, null,
        new("pinned-NPCServer-evidence-declaration", RetailGroundPathSearch.SupportedNpcBinarySha256, RetailGroundPathSearch.SupportedNpcBinaryBytes),
        null, new("synthetic-ground-actor", "synthetic-static-context", "synthetic-policy-no-collision-file", "explicit-offline-fixture"));

    private sealed class Replay : IRetailSearchPolicyReplay
    {
        internal bool Known = true, Capability, AllFieldsCollide, UnknownFields;
        internal string Revision = "fixture";
        internal float Cost = 50;
        internal Func<RetailPathSurfaceRef, RetailPathSurfaceRef, float>? CostFor;
        internal Func<RetailSearchPoint, RetailSearchPoint, RetailSearchPolicyPurpose, (bool Blocked, RetailSearchPoint Endpoint)>? Segment;
        internal List<(RetailPathSurfaceRef From, RetailPathSurfaceRef To)> Costs = [];
        internal List<(RetailSearchPoint From, RetailSearchPoint To, RetailSearchPolicyPurpose Purpose)> Segments = [], Fields = [];
        private RetailSearchReplayStamp Stamp => new(Known, Revision, "explicit-synthetic-branch-fixture");
        public RetailSearchCostObservation TransitionCost(RetailPathSurfaceRef source, RetailPathSurfaceRef target)
        { Costs.Add((source, target)); return new(Stamp, CostFor?.Invoke(source, target) ?? Cost); }
        public RetailSearchBooleanObservation SameNodeShortcutCapability(RetailPathNode node) => new(Stamp, Capability);
        public RetailSearchSegmentObservation CheckSegment(RetailSearchPoint source, RetailSearchPoint target, RetailSearchPolicyPurpose purpose)
        { Segments.Add((source, target, purpose)); var result = Segment?.Invoke(source, target, purpose) ?? (false, target); return new(Stamp, result.Item1, result.Item2); }
        public RetailSearchBooleanObservation FieldObjectCollision(RetailSearchPoint source, RetailSearchPoint target, RetailSearchPolicyPurpose purpose)
        { Fields.Add((source, target, purpose)); return new(Stamp with { Known = Known && !UnknownFields }, AllFieldsCollide); }
    }
}
