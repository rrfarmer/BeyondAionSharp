using Aion.GameServer.GeoEngine.RetailPath;

namespace Aion.GameServer.Tests;

public sealed class RetailNpcMoveByPathPlanTests
{
    private static readonly RetailSearchPoint Source = new(1, 1, 0), Goal = new(5, 1, 0);
    private static readonly RetailNpcPathFollowLimits Limits = new(1000, 1000, 100, 100000, CollisionFixture.QueryLimits);

    [Theory]
    [InlineData(-1f)] [InlineData(0f)] [InlineData(.099f)]
    public void InsufficientBudgetReturnsRawFourWithoutOutputsOrActorStateReads(float budget)
    {
        var scene = Floor(); var actor = Actor() with { Virtual18Target = null, UpperC8 = null, Progress82c = null };
        var result = RetailNpcMoveByPathPlan.Plan(Observe([Goal], budget: budget, actor: actor), scene, Limits);
        Assert.Equal(4u, result.NativeValue); Assert.False(result.OutputsWritten);
        Assert.Null(result.DesiredWaypoint); Assert.Null(result.NextPosition); Assert.Empty(result.Operations); Assert.Empty(result.Queries);
    }
    [Fact]
    public void BudgetEqualityPointOneIsConsumedAndAdvancesBinary32PartialDistance()
    {
        var result = RetailNpcMoveByPathPlan.Plan(Observe([Goal], budget: .1f), Floor(), Limits);
        Assert.Equal(0u, result.NativeValue); Assert.True(result.OutputsWritten);
        Assert.Equal(new RetailSearchPoint(1.1f, 1, 0), result.NextPosition); Assert.Equal(Goal, result.DesiredWaypoint);
        Assert.Equal(2, result.Queries.Count); Assert.Equal(0, result.FinalProgress);
    }
    [Theory]
    [InlineData(0)] [InlineData(-1)]
    public void BadCountReturnsNativeOneAfterSourceOutputStoresBeforeActorReads(int count)
    {
        var result = RetailNpcMoveByPathPlan.Plan(Observe([], count: count, actor: Actor() with { UpperC8 = null }), Floor(), Limits);
        Assert.Equal(1u, result.NativeValue); Assert.True(result.OutputsWritten);
        Assert.Equal(Source, result.NextPosition); Assert.Equal(Source, result.DesiredWaypoint); Assert.Empty(result.Operations);
    }
    [Fact]
    public void OutsideSourceReturnsNativeOneBeforeProgressConsume()
    {
        var result = RetailNpcMoveByPathPlan.Plan(Observe([Goal], source: new(-1, 1, 0)), Floor(), Limits);
        Assert.Equal(1u, result.NativeValue); Assert.Equal(new RetailSearchPoint(-1, 1, 0), result.NextPosition); Assert.Empty(result.Operations);
    }
    [Fact]
    public void FullFitWithZeroToleranceIntentionallySkipsGeometryAndUnconsumedTarget()
    {
        var prepared = CollisionFixture.Prepared(CollisionFixture.Read(CollisionFixture.Mesh([]), CollisionFixture.Collision()));
        var scene = CollisionFixture.Scene(prepared, CollisionFixture.Terrain((_, _) => 13), []); // would reject a consumed field region
        var result = RetailNpcMoveByPathPlan.Plan(Observe([Goal], budget: 4, target: new(float.NaN, 0, 0)), scene, Limits with { MaxQueries = 0 });
        Assert.Equal(0u, result.NativeValue); Assert.Equal(Goal, result.NextPosition); Assert.Equal(Goal, result.DesiredWaypoint);
        Assert.Equal(1, result.FinalProgress); Assert.Empty(result.Queries);
    }
    [Fact]
    public void PartialGroundMovementComputesContactAndPredictionWithDistinctOutputs()
    {
        var result = RetailNpcMoveByPathPlan.Plan(Observe([Goal], budget: 1), Floor(), Limits);
        Assert.Equal(0u, result.NativeValue); Assert.Equal(new RetailSearchPoint(2, 1, 0), result.NextPosition);
        Assert.Equal(Goal, result.DesiredWaypoint); Assert.Equal(0, result.FinalProgress);
        Assert.Contains(result.Queries[0].Trace, t => t.NativeFunction == RetailCollisionQuery.DispatchFunction(RetailCollisionQueryKind.ContactSegment, 0));
        Assert.Contains(result.Queries[1].Trace, t => t.NativeFunction == RetailCollisionQuery.DispatchFunction(RetailCollisionQueryKind.PredictionProjection, 0));
        Assert.DoesNotContain(result.Queries.SelectMany(q => q.Trace), t => t.NativeFunction == RetailCollisionQuery.DispatchFunction(RetailCollisionQueryKind.GroundProjection, 0));
        Assert.False(result.ExecutesMovement); Assert.False(result.RuntimeVerified); Assert.False(result.ActivationReady);
    }
    [Fact]
    public void PartialTargetToleranceCanReplaceDesiredButNeverCollapsesProgressSemantics()
    {
        var result = RetailNpcMoveByPathPlan.Plan(Observe([Goal], budget: 1, tolerance: 9), Floor(), Limits);
        Assert.Equal(new RetailSearchPoint(2, 1, 0), result.NextPosition); Assert.Equal(result.NextPosition, result.DesiredWaypoint);
        Assert.Equal(0, result.FinalProgress); Assert.DoesNotContain(result.Operations, o => o.Operation == "SegmentReachFactorSquaredToleranceTimesPoint9");
    }
    [Fact]
    public void FullAndPartialStopFactorBranchesConsumeSquaredToleranceOnce()
    {
        var whole = RetailNpcMoveByPathPlan.Plan(Observe([Goal], budget: 4, tolerance: 1), Floor(), Limits);
        Assert.Equal(0u, whole.NativeValue); Assert.InRange(whole.NextPosition!.Value.X, 4.05f, 4.052f);
        Assert.Equal(whole.NextPosition, whole.DesiredWaypoint); Assert.Equal(0, whole.FinalProgress);
        Assert.Single(whole.Operations, o => o.Operation == "SegmentReachFactorSquaredToleranceTimesPoint9");
        var partial = RetailNpcMoveByPathPlan.Plan(Observe([Goal], budget: 1, tolerance: 16), Floor(), Limits);
        Assert.Equal(0u, partial.NativeValue); Assert.InRange(partial.NextPosition!.Value.X, 1.204f, 1.207f);
        Assert.Single(partial.Operations, o => o.Operation == "SegmentReachFactorSquaredToleranceTimesPoint9");
    }
    [Fact]
    public void DuplicateAndProgressAcknowledgementAdvanceDifferentIndices()
    {
        var result = RetailNpcMoveByPathPlan.Plan(Observe([Source, Source, Goal]), Floor(), Limits);
        Assert.Equal(0u, result.NativeValue); Assert.Equal(2, result.FinalPathIndex);
        Assert.Equal(2, result.Operations.Count(o => o.Operation == "DuplicateWaypoint"));
        var acknowledged = RetailNpcMoveByPathPlan.Plan(Observe([Source, Goal], actor: Actor() with { Progress82c = 2 }), Floor(), Limits);
        Assert.Equal(1, acknowledged.FinalPathIndex); Assert.DoesNotContain(acknowledged.Operations, o => o.Operation == "DuplicateWaypoint");
        var exhausted = RetailNpcMoveByPathPlan.Plan(Observe([Source, Source]), Floor(), Limits);
        Assert.Equal(2u, exhausted.NativeValue); Assert.Equal(Source, exhausted.NextPosition);
        Assert.Equal(2, exhausted.FinalPathIndex);
    }
    [Fact]
    public void AcknowledgedIndexAtEndReturnsNativeOneRatherThanDuplicateArrivalTwo()
    {
        var result = RetailNpcMoveByPathPlan.Plan(Observe([Goal], actor: Actor() with { Progress82c = 2 }), Floor(), Limits);
        Assert.Equal(1u, result.NativeValue); Assert.Equal(1, result.FinalPathIndex); Assert.Equal(0, result.FinalProgress);
        Assert.Equal(Source, result.NextPosition); Assert.Empty(result.Queries);
    }
    [Theory]
    [InlineData(.0001f)] [InlineData(-.0001f)]
    public void DuplicateEpsilonIsStrictAtBothSignedEdges(float delta)
    {
        var source = new RetailSearchPoint(0, 1, 0); var point = new RetailSearchPoint(delta, 1, 0);
        var result = RetailNpcMoveByPathPlan.Plan(Observe([point], source: source, budget: 1), Floor(), Limits);
        Assert.Equal(0u, result.NativeValue); Assert.Equal(point, result.NextPosition); Assert.Equal(0, result.FinalPathIndex);
        Assert.DoesNotContain(result.Operations, o => o.Operation == "DuplicateWaypoint");
    }
    [Fact]
    public void PureVerticalPartialKeepsNativeUnchangedSupportArrivalAndDistinctDesired()
    {
        var goal = new RetailSearchPoint(1, 1, 4);
        var result = RetailNpcMoveByPathPlan.Plan(Observe([goal], target: goal), Floor(), Limits);
        Assert.Equal(2u, result.NativeValue); Assert.Equal(Source, result.NextPosition); Assert.Equal(goal, result.DesiredWaypoint);
        Assert.Single(result.Queries); Assert.Contains(result.Operations, o => o.Operation == "PureVerticalSkipsXYContactLoop");
    }
    [Fact]
    public void NineComputedWallContactsExhaustAndCopyWholeWaypointBeyondPartialBudget()
    {
        var result = RetailNpcMoveByPathPlan.Plan(Observe([Goal], budget: 1), Wall(2), Limits);
        Assert.Equal(0u, result.NativeValue); Assert.Equal(Goal, result.NextPosition); Assert.Equal(1, result.FinalProgress);
        Assert.Equal(19, result.Queries.Count);
        Assert.Equal(9, result.Queries.Count(q => q.Trace.Any(t => t.NativeFunction == RetailCollisionQuery.DispatchFunction(RetailCollisionQueryKind.ContactSegment, 2))));
        Assert.Contains(result.Operations, o => o.Operation == "NineContactAttemptsExhaustedCopyWholeWaypoint");
        Assert.Equal(4f, result.NextPosition!.Value.X - Source.X); // original distance budget was1
    }
    [Fact]
    public void FullBranchNineContactsCopyWaypointWithoutPartialBranchProgressWrite()
    {
        // Native stop factor is(4-sqrt(binary32(.9)))/4, between0and1. The wall remains before it.
        var result = RetailNpcMoveByPathPlan.Plan(Observe([Goal], budget: 4, tolerance: 1), Wall(2), Limits);
        Assert.Equal(0u, result.NativeValue); Assert.Equal(Goal, result.DesiredWaypoint); Assert.Equal(Goal, result.NextPosition);
        Assert.Equal(19, result.GeometryCalls.Count); Assert.Equal(9, result.GeometryCalls.Count(q => q.Kind == RetailCollisionQueryKind.ContactSegment));
        Assert.Equal(0, result.FinalProgress);
        Assert.Contains(result.Operations, o => o.Operation == "NineContactAttemptsExhaustedCopyWholeWaypoint");
        Assert.DoesNotContain(result.Operations, o => o.Operation == "ExchangeProgress" && o.NewValue == 1);
    }
    [Theory]
    [InlineData(0x3f000000u, 0x40200000u)] [InlineData(0x3efffff8u, 0x405fffffu)]
    [InlineData(0x3f000004u, 0x40200001u)] [InlineData(0xbf000000u, 0x3fc00000u)]
    [InlineData(0xbefffff8u, 0x40200001u)] [InlineData(0xbf000004u, 0x3fbffffeu)]
    public void ContactSupportRaiseUsesStrictPointFiveAfterBinary32HeightAddition(uint supportBits, uint nextOriginBits)
    {
        float z = BitConverter.UInt32BitsToSingle(supportBits);
        CollisionFace wall = new(new(2, 0, 0), new(2, 64, 0), new(2, 0, 128), 25);
        CollisionFace support = new(new(0, 0, z), new(64, 0, z), new(0, 64, z), 25);
        var raw = CollisionFixture.Read(CollisionFixture.Mesh([wall, support]), CollisionFixture.Collision((_, _) =>
            [new(0, 0, 1), new(2, 0, 1), new(4, 0, 1)]));
        var scene = CollisionFixture.Scene(CollisionFixture.Prepared(raw), CollisionFixture.Terrain((_, _) => 14));
        var result = RetailNpcMoveByPathPlan.Plan(Observe([Goal], budget: 2, source: new(1, 1, z)), scene, Limits);
        Assert.Equal(0u, result.NativeValue); Assert.True(result.GeometryCalls.Count >= 3);
        Assert.Equal(RetailCollisionQueryKind.ContactSegment, result.GeometryCalls[0].Kind);
        Assert.Equal(RetailCollisionQueryKind.PredictionProjection, result.GeometryCalls[1].Kind);
        Assert.Equal(z, result.GeometryCalls[1].Result.Point!.Value.Z);
        Assert.Equal(RetailCollisionQueryKind.ContactSegment, result.GeometryCalls[2].Kind);
        Assert.Equal(nextOriginBits, BitConverter.SingleToUInt32Bits(result.GeometryCalls[2].Input.Z));
    }
    [Fact]
    public void ContactBeyondCandidateAndAtWaypointTerminateWithoutNineAttemptFallback()
    {
        var beyond = RetailNpcMoveByPathPlan.Plan(Observe([Goal], budget: .5f), Wall(2), Limits);
        Assert.Equal(new RetailSearchPoint(1.5f, 1, 0), beyond.NextPosition); Assert.Equal(2, beyond.Queries.Count);
        Assert.DoesNotContain(beyond.Operations, o => o.Operation == "NineContactAttemptsExhaustedCopyWholeWaypoint");
        var atWaypoint = RetailNpcMoveByPathPlan.Plan(Observe([Goal], budget: 1), Wall(5), Limits);
        Assert.Equal(new RetailSearchPoint(2, 1, 0), atWaypoint.NextPosition); Assert.Equal(2, atWaypoint.Queries.Count);
    }
    [Theory]
    [InlineData(.5f, 1.5f)] [InlineData(1.75f, 1.75f)] [InlineData(4f, 2f)]
    public void ProbeHeightUsesNativeClampAndContactArgumentsRemainAuditable(float rawHeight, float expected)
    {
        var result = RetailNpcMoveByPathPlan.Plan(Observe([Goal], actor: Actor() with { UpperC8 = rawHeight }), Floor(), Limits);
        Assert.Equal(0u, result.NativeValue); var call = result.GeometryCalls[0];
        Assert.Equal(expected, call.Input.Z); Assert.Equal(expected, call.End!.Value.Z);
        Assert.Equal(0u, call.IgnoredEditorId); Assert.Equal(0x3d016f00u, BitConverter.SingleToUInt32Bits(call.ShortSegmentTolerance!.Value));
        Assert.Equal(RetailCollisionQueryKind.ContactSegment, call.Kind); Assert.True(result.PathSnapshotBytesVerified);
        Assert.NotNull(result.GeometrySources); Assert.NotNull(result.SceneDeclaration);
    }
    [Fact]
    public void StackedIneligibleSurfaceStillParticipatesInPredictionAndFinalTestsRetainTwoVsThreeDimensions()
    {
        var raw = CollisionFixture.Read(CollisionFixture.Mesh([CollisionFixture.Face(1, reversed: true)]),
            CollisionFixture.Collision((_, _) => [new(0, 0)]));
        var scene = CollisionFixture.Scene(CollisionFixture.Prepared(raw), CollisionFixture.Terrain((_, _) => 2));
        var source = new RetailSearchPoint(.5f, .5f, 0); var goal = new RetailSearchPoint(1.5f, .5f, 0);
        var whole = RetailNpcMoveByPathPlan.Plan(Observe([goal], source: source, target: goal, budget: 1, tolerance: .25f), scene, Limits);
        Assert.Equal(0u, whole.NativeValue); Assert.Equal(1f, whole.NextPosition!.Value.Z);
        Assert.Equal(whole.NextPosition, whole.DesiredWaypoint); // final full branch compares only XY
        var partial = RetailNpcMoveByPathPlan.Plan(Observe([goal], source: source, target: goal, budget: .75f, tolerance: .25f), scene, Limits);
        Assert.Equal(0u, partial.NativeValue); Assert.Equal(new RetailSearchPoint(1.25f, .5f, 1), partial.NextPosition);
        Assert.Equal(goal, partial.DesiredWaypoint); // final partial branch includes Z
        var eligible = RetailCollisionQuery.ProjectGround(scene, scene.Stamp, new(1.25f, .5f, 2), CollisionFixture.QueryLimits);
        Assert.Equal(0f, eligible.Point!.Value.Z);
    }
    [Fact]
    public void NoSupportReturnsNativeOneButUnknownSceneReturnsNoNativeOutcomeOrCommands()
    {
        var missing = RetailNpcMoveByPathPlan.Plan(Observe([Goal]), Floor(12), Limits);
        Assert.Equal(1u, missing.NativeValue); Assert.Equal(Source, missing.NextPosition); Assert.Equal(Goal, missing.DesiredWaypoint);
        var unknown = RetailNpcMoveByPathPlan.Plan(Observe([Goal]), Floor(13, []), Limits);
        Assert.Equal(RetailNpcPathFollowStatus.UnsupportedGeometry, unknown.Status); Assert.Null(unknown.NativeValue);
        Assert.Null(unknown.NextPosition); Assert.Empty(unknown.Operations); Assert.True(unknown.RequiresAdapterRejectionInvalidation);
        Assert.Equal(RetailCollisionStatus.UnsupportedDynamicScene, unknown.GeometryFailure);
    }
    [Theory]
    [InlineData(3)] [InlineData(13)] [InlineData(14)]
    public void AlternativeRawPoseIsExplicitlyUnsupported(int pose)
    {
        var result = RetailNpcMoveByPathPlan.Plan(Observe([Goal], actor: Actor() with { RawPose5a0 = (uint)pose }), Floor(), Limits);
        Assert.Equal(RetailNpcPathFollowStatus.UnsupportedActor, result.Status); Assert.Empty(result.Operations);
    }
    [Theory]
    [InlineData("virtual")] [InlineData("kind")] [InlineData("height")] [InlineData("progress")] [InlineData("owner")] [InlineData("arithmetic")]
    public void UnknownActorBodyOrOwnerNeverFillsDefaults(string fault)
    {
        var actor = Actor();
        actor = fault switch
        {
            "virtual" => actor with { Virtual18Target = null }, "kind" => actor with { RawKind32c = 16 },
            "height" => actor with { UpperC8 = null }, "progress" => actor with { Progress82c = null },
            "owner" => actor with { SerializedProgressOwnership = null }, _ => actor with { NearestEvenNormalArithmetic = null }
        };
        var result = RetailNpcMoveByPathPlan.Plan(Observe([Goal], actor: actor), Floor(), Limits);
        Assert.False(result.HasNativeOutcome); Assert.Null(result.NextPosition); Assert.Empty(result.Operations);
    }
    [Theory]
    [InlineData("hash")] [InlineData("scene")] [InlineData("path-count")] [InlineData("negative-index")]
    public void SourceMismatchOrUnsafeOwnedHeaderRejectsWithoutPartialCommands(string fault)
    {
        var observation = Observe([Goal], count: fault == "path-count" ? 2 : null, index: fault == "negative-index" ? -1 : 0,
            epoch: fault == "scene" ? Epoch() with { SceneRevision = "stale" } : null, badHash: fault == "hash");
        var result = RetailNpcMoveByPathPlan.Plan(observation, Floor(), Limits);
        Assert.False(result.HasNativeOutcome); Assert.Empty(result.Operations); Assert.Null(result.NativeValue);
    }
    [Theory]
    [InlineData("points")] [InlineData("visits")] [InlineData("queries")] [InlineData("trace")]
    public void ManagedBudgetsAreNotFabricatedNativeOne(string budget)
    {
        var limits = budget switch
        {
            "points" => Limits with { MaxPathPoints = 0 }, "visits" => Limits with { MaxWaypointVisits = 1 },
            "queries" => Limits with { MaxQueries = 0 }, _ => Limits with { MaxTraceEntries = 1 }
        };
        var result = RetailNpcMoveByPathPlan.Plan(Observe(budget == "visits" ? [Source, Goal] : [Goal]), Floor(), limits);
        Assert.Equal(RetailNpcPathFollowStatus.DefensiveLimit, result.Status); Assert.Null(result.NativeValue); Assert.Empty(result.Operations);
    }
    [Fact]
    public void ObservationOwnsRawPointsAndMetadataHashWithoutNormalizingSignedZero()
    {
        var points = new[] { Goal }; var observation = Observe(points); points[0] = Source;
        Assert.Equal(Goal, observation.Points[0]);
        Assert.Equal(0u, RetailNpcMoveByPathPlan.Plan(observation, Floor(), Limits).NativeValue);
        var other = RetailNpcPathFollowObservation.ComputeSnapshotSha256([Goal], 1, 0, new(BitConverter.UInt32BitsToSingle(0x80000000), 0, 0), 10);
        var original = RetailNpcPathFollowObservation.ComputeSnapshotSha256([Goal], 1, 0, default, 10);
        Assert.NotEqual(other, original);
    }

    private static RetailNpcPathFollowEpoch Epoch() => new("actor", "actor1", "serialized1", "path1", "synthetic-world", "instance1", "scene1", "asset1");
    private static RetailNpcPathFollowActor Actor() => new(0x14007dfa0, 0, 0, 2, 0, true, true, new('a', 64));
    private static RetailNpcPathFollowObservation Observe(IReadOnlyList<RetailSearchPoint> points, float budget = 1,
        float tolerance = 0, RetailSearchPoint? source = null, RetailSearchPoint? target = null, int? count = null, int index = 0,
        RetailNpcPathFollowActor? actor = null, RetailNpcPathFollowEpoch? epoch = null, bool badHash = false)
    {
        int n = count ?? points.Count;
        var receipt = new RetailNpcPathFollowPathReceipt("synthetic-owned-waypoints",
            badHash ? new('0', 64) : RetailNpcPathFollowObservation.ComputeSnapshotSha256(points, n, index, default, 1000),
            new('b', 64), "synthetic-explicit-path-producer-only");
        return RetailNpcPathFollowObservation.Create(points, n, index, default, source ?? Source, target ?? Goal,
            budget, tolerance, 0, actor ?? Actor(), epoch ?? Epoch(), receipt, true, 1000);
    }
    private static RetailCollisionScene Floor(byte selector = 0, IReadOnlyList<RetailCollisionRegionObservation>? regions = null) =>
        CollisionFixture.Scene(CollisionFixture.Prepared(CollisionFixture.Read(CollisionFixture.Mesh([]), CollisionFixture.Collision())),
            CollisionFixture.Terrain((_, _) => selector), regions);
    private static RetailCollisionScene Wall(float x)
    {
        CollisionFace wall = new(new(x, 0, 0), new(x, 64, 0), new(x, 0, 128), 25);
        // Ray probe height2 consumes the native height-bin2 band. A band0-only asset would skip this wall.
        var raw = CollisionFixture.Read(CollisionFixture.Mesh([wall]), CollisionFixture.Collision((_, _) => [new(2, 0)]));
        return CollisionFixture.Scene(CollisionFixture.Prepared(raw), CollisionFixture.Terrain((_, _) => 2));
    }
}
