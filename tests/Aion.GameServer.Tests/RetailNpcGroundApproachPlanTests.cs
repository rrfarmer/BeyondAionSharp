using System.Buffers.Binary;
using System.Security.Cryptography;
using Aion.GameServer.GeoEngine.RetailPath;

namespace Aion.GameServer.Tests;

public sealed class RetailNpcGroundApproachPlanTests
{
    [Fact]
    public void CheckedNativeTablesPreserveTheirIndividualBytesAndSignedZero()
    {
        Assert.Equal(RetailNpcGroundApproachPlan.TableXSha256, Convert.ToHexString(SHA256.HashData(RetailNpcGroundApproachPlan.TableXBytes)).ToLowerInvariant());
        Assert.Equal(RetailNpcGroundApproachPlan.TableYSha256, Convert.ToHexString(SHA256.HashData(RetailNpcGroundApproachPlan.TableYBytes)).ToLowerInvariant());
        Assert.Equal(unchecked((int)0x80000000), BinaryPrimitives.ReadInt32LittleEndian(RetailNpcGroundApproachPlan.TableXBytes.Slice(90 * 4, 4)));
    }

    [Theory]
    [InlineData(0, false)] [InlineData(1, true)]
    public void VisibilityForceBypassesTheInclusiveOrdinaryRangeExit(int force, bool goalExpected)
    {
        var s = new ApproachScript(); s.Zone();
        if (goalExpected) { s.GroundGuards(); s.Project(s.Target.Position with { Z = 2 }, "GroundProjection", false); s.Goal(s.Target.Position, 0, 0); }
        var result = RetailNpcGroundApproachPlan.Replay(s.Observation(100, force, force == 1 ? -3 : -1));
        Assert.Equal(RetailNpcApproachStatus.Replayed, result.Status); Assert.Equal(0u, result.NativeResult);
        Assert.Equal(goalExpected ? 1 : 0, result.Commands.Count(c => c.Kind == RetailNpcApproachCommandKind.GoalRequest));
        Assert.Equal(goalExpected ? 0f : (float?)null, result.Commands.FirstOrDefault(c => c.Kind == RetailNpcApproachCommandKind.GoalRequest)?.Request?.SquaredReach);
        Assert.False(result.RuntimeVerified); Assert.False(result.ImplementsCollision); Assert.False(result.RequiresAdapterRejectionInvalidation);
    }

    [Fact]
    public void ReasonMinusThreeCannotBeReinterpretedAsAnUnforcedRequest()
    {
        var s = new ApproachScript();
        var result = RetailNpcGroundApproachPlan.Replay(s.Observation(100, 0, -3));
        Assert.Equal(RetailNpcApproachStatus.InvalidObservation, result.Status); Assert.Empty(result.Commands); Assert.Empty(result.Consumed);
    }

    [Fact]
    public void OrdinaryPrimaryRequestUsesSquaredReachAndExactGoalFlags()
    {
        var s = new ApproachScript(); s.BasicPrimary(0);
        var result = RetailNpcGroundApproachPlan.Replay(s.Observation());
        Assert.Equal(RetailNpcApproachStatus.Replayed, result.Status);
        var request = Assert.Single(result.Commands, c => c.Kind == RetailNpcApproachCommandKind.GoalRequest).Request!;
        Assert.Equal(16f, request.SquaredReach); Assert.Equal(2u, request.MovementFlags); Assert.Equal(1, request.ReuseFlag);
        Assert.Equal(s.Target.Id, request.ReferenceId); Assert.Equal(s.Target.Position, request.To);
        Assert.Equal(new uint?[] { 0, 0 }, result.Commands.Where(c => c.Kind == RetailNpcApproachCommandKind.FailureTickWrite).Select(c => c.Tick));
    }

    [Fact]
    public void NativePredictionRoundsMultiplyThenAddAndNeverLeadsDownward()
    {
        var s = new ApproachScript(target: new(10, 20, 30), vector: new(3, -6, -2)); s.Zone();
        var lead = new RetailSearchPoint(BitConverter.Int32BitsToSingle(0x41400418), 15.998f, 31.5f);
        s.Project(lead, "PredictionProjection", true, lead, RetailNpcApproachOperation.PredictionProjection2480);
        s.Geom(new(RetailNpcApproachOperation.Segment2257e0, "PredictionSegmentBlocked", s.Actor.Position, lead), 0);
        s.GroundGuards(); s.Project(lead with { Z = 33.5f }, "GroundProjection", false); s.Goal(lead, 16, 0);
        var result = RetailNpcGroundApproachPlan.Replay(s.Observation());
        Assert.Equal(RetailNpcApproachStatus.Replayed, result.Status);
        var actual = Assert.Single(result.Commands, c => c.Kind == RetailNpcApproachCommandKind.GoalRequest).Request!.To!.Value;
        Assert.Equal(0x41400418, BitConverter.SingleToInt32Bits(actual.X)); Assert.Equal(31.5f, actual.Z);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void RejectedPredictionRestoresActualTargetIncludingRemovingTheHeightLift(bool projectionSucceeds)
    {
        var s = new ApproachScript(vector: new(3, 0, 0)); s.Zone();
        var lead = new RetailSearchPoint(BitConverter.Int32BitsToSingle(0x41500418), 1, 1.5f);
        s.Project(lead, "PredictionProjection", projectionSucceeds, lead, RetailNpcApproachOperation.PredictionProjection2480);
        if (projectionSucceeds) s.Geom(new(RetailNpcApproachOperation.Segment2257e0, "PredictionSegmentBlocked", s.Actor.Position, lead), 1);
        s.GroundGuards(); s.Project(s.Target.Position with { Z = 2 }, "GroundProjection", false); s.Goal(s.Target.Position, 16, 0);
        Assert.Equal(RetailNpcApproachStatus.Replayed, RetailNpcGroundApproachPlan.Replay(s.Observation()).Status);
    }

    [Theory]
    [InlineData(25f, 13f)] [InlineData(4f, 0f)]
    public void GroundProjectionPreservesHorizontalBudgetOrClampsNegativeBudget(float input, float expected)
    {
        var s = new ApproachScript(actor: new(1, 1, 10), target: new(11, 1, 6)); s.Zone();
        if (input < 16) s.Raw(RetailNpcApproachOperation.StateCode7da70, "ApproachStateCode", 0);
        s.GroundGuards(); var point = new RetailSearchPoint(11, 1, input == 25 ? 8 : 10);
        s.Project(new(11, 1, 8), "GroundProjection", true, point); s.Goal(point, expected, 0);
        var result = RetailNpcGroundApproachPlan.Replay(s.Observation(input));
        Assert.Equal(RetailNpcApproachStatus.Replayed, result.Status);
    }

    [Fact]
    public void ValidCacheRequestsZeroToleranceAndSkipsTheGroundGuardCallbacks()
    {
        var s = new ApproachScript(); var cached = new RetailSearchPoint(10, 1, 0);
        s.SetFrame(s.Frame with { Cache = new(s.Target.Id, s.Target.Position, cached, 1) });
        s.Zone(); s.Goal(cached, 0, 2);
        var result = RetailNpcGroundApproachPlan.Replay(s.Observation());
        Assert.Equal(RetailNpcApproachStatus.Replayed, result.Status); Assert.Equal(2u, result.NativeResult);
        Assert.DoesNotContain(result.Consumed, c => c.Request.Operation == RetailNpcApproachOperation.ActorVirtual14);
    }

    [Fact]
    public void CacheAtExactSquaredReachIsRejectedRatherThanReused()
    {
        var s = new ApproachScript(); s.SetFrame(s.Frame with { Cache = new(s.Target.Id, s.Target.Position, new(7, 1, 0), 0) });
        s.BasicPrimary(0);
        var result = RetailNpcGroundApproachPlan.Replay(s.Observation());
        Assert.Equal(RetailNpcApproachStatus.Replayed, result.Status);
        Assert.Equal(16f, result.Commands.First(c => c.Kind == RetailNpcApproachCommandKind.GoalRequest).Request!.SquaredReach);
    }

    [Fact]
    public void FiveBinary32OrientationAttemptsRemainOrderedWhenNoPathExists()
    {
        var s = new ApproachScript(); s.Zone();
        s.Raw(RetailNpcApproachOperation.StateCode7da70, "ApproachStateCode", 1);
        s.Raw(RetailNpcApproachOperation.TargetVirtual198, "TargetVirtual198", 2);
        s.Raw(RetailNpcApproachOperation.HasAnyPath, "OrientationHasAnyPath", 0);
        s.GroundGuards(); s.Project(new(11, 1, 2), "GroundProjection", false);
        s.HasGoal(s.Target.Position, false);
        foreach (float x in new[] { 13f, 12.8f, 12.6f, 12.4f, 12.2f }) s.Project(new(x, 1, 2), "OrientationProjection", false);
        s.Goal(s.Target.Position, 4, 0);
        var result = RetailNpcGroundApproachPlan.Replay(s.Observation(4));
        Assert.Equal(RetailNpcApproachStatus.Replayed, result.Status);
        Assert.Equal(5, result.Consumed.Count(c => c.Request.Purpose == "OrientationProjection"));
    }

    [Fact]
    public void ExistingPathCanBypassArrivalInsideReachAndCanonicalizeItsBoundDestination()
    {
        var s = new ApproachScript(target: new(2, 1, 0));
        s.SetFrame(s.Frame with { Path = new("existing-path-instance", 3, 1, s.Target.Position, true) });
        s.Zone(); s.Raw(RetailNpcApproachOperation.StateCode7da70, "ApproachStateCode", 1);
        s.Raw(RetailNpcApproachOperation.TargetVirtual198, "TargetVirtual198", 2);
        s.Raw(RetailNpcApproachOperation.HasAnyPath, "OrientationHasAnyPath", 1);
        s.GroundGuards(); s.Project(new(2, 1, 2), "GroundProjection", false);
        s.HasGoal(s.Target.Position, true, s.Frame.Path.Destination); s.Goal(s.Target.Position, 4, 0);
        var result = RetailNpcGroundApproachPlan.Replay(s.Observation(4));
        Assert.Equal(RetailNpcApproachStatus.Replayed, result.Status);
        Assert.Equal(4f, Assert.Single(result.Commands, c => c.Kind == RetailNpcApproachCommandKind.GoalRequest).Request!.SquaredReach);
        Assert.DoesNotContain(result.Consumed, c => c.Request.Purpose == "OrientationProjection");
    }

    [Fact]
    public void OrientationCandidateNeedsProjectionShortcutAndSeparateVisibilityAndForcesZeroTolerance()
    {
        var s = new ApproachScript(); s.Zone();
        s.Raw(RetailNpcApproachOperation.StateCode7da70, "ApproachStateCode", 1); s.Raw(RetailNpcApproachOperation.TargetVirtual198, "TargetVirtual198", 2);
        s.Raw(RetailNpcApproachOperation.HasAnyPath, "OrientationHasAnyPath", 0); s.GroundGuards(); s.Project(new(11, 1, 2), "GroundProjection", false);
        s.HasGoal(s.Target.Position, false); var selected = new RetailSearchPoint(12.5f, 1, 0);
        s.Project(new(13, 1, 2), "OrientationProjection", true, selected);
        s.Geom(new(RetailNpcApproachOperation.Shortcut225430, "OrientationShortcut", s.Actor.Position, selected), 1);
        s.Geom(new(RetailNpcApproachOperation.Visibility23b2f0, "OrientationVisibility", selected, s.Target.Position, Flag: 1), 1);
        s.Goal(selected, 0, 0);
        Assert.Equal(RetailNpcApproachStatus.Replayed, RetailNpcGroundApproachPlan.Replay(s.Observation(4)).Status);
    }

    [Fact]
    public void SpecialTargetReferenceUsesItsRecordedOwnerInsteadOfItsObjectId()
    {
        var s = new ApproachScript(kind: 2); s.SetFrame(s.Frame with { Target = s.Target with { OwnerObjectIdentity = "owner-pointer" } });
        s.GroundGuards(); s.Project(new(11, 1, 2), "GroundProjection", false);
        s.Raw(RetailNpcApproachOperation.TargetSpecialFf950, "ReferenceTargetSpecial", 1); s.Raw(RetailNpcApproachOperation.TargetOwner68, "ReferenceOwner", 99);
        s.Goal(s.Target.Position, 16, 0, reference: 99);
        Assert.Equal(RetailNpcApproachStatus.Replayed, RetailNpcGroundApproachPlan.Replay(s.Observation()).Status);
    }

    [Fact]
    public void NativeWorldMismatchReturnsWithoutInventingFailureWritesOrClearingAPath()
    {
        var s = new ApproachScript(); s.SetFrame(s.Frame with { TargetWorld = s.Frame.TargetWorld! with { WorldId = 2 } });
        var result = RetailNpcGroundApproachPlan.Replay(s.Observation());
        Assert.Equal(1u, result.NativeResult); Assert.Empty(result.Commands); Assert.Empty(result.Consumed);
    }

    [Fact]
    public void MissingGeometryReceiptCannotTurnARecordedBooleanIntoCollisionImplementation()
    {
        var s = new ApproachScript(); s.BasicPrimary(0);
        int i = s.Steps.FindIndex(v => v.Request.Operation == RetailNpcApproachOperation.GroundProjection2600);
        s.Steps[i] = s.Steps[i] with { Geometry = null };
        var result = RetailNpcGroundApproachPlan.Replay(s.Observation());
        Assert.Equal(RetailNpcApproachStatus.UnsupportedGeometry, result.Status); Assert.Empty(result.Commands);
        Assert.True(result.RequiresAdapterRejectionInvalidation);
    }

    [Theory]
    [InlineData("revision")] [InlineData("request")] [InlineData("postframe")] [InlineData("capture")]
    public void MissingOrMismatchedStageEvidenceFailsAsARejection(string fault)
    {
        var s = new ApproachScript(); s.BasicPrimary(0);
        int i = fault == "postframe" ? s.Steps.Count - 1 : s.Steps.FindIndex(v => v.Geometry is not null);
        s.Steps[i] = fault switch
        {
            "revision" => s.Steps[i] with { Receipt = s.Steps[i].Receipt with { SceneRevision = "old-scene" } },
            "request" => s.Steps[i] with { Request = s.Steps[i].Request with { From = new(99, 1, 2) } },
            "postframe" => s.Steps[i] with { AfterFrame = null },
            _ => s.Steps[i] with { Geometry = s.Steps[i].Geometry! with { CaptureSha256 = "" } }
        };
        var result = RetailNpcGroundApproachPlan.Replay(s.Observation());
        Assert.NotEqual(RetailNpcApproachStatus.Replayed, result.Status); Assert.Empty(result.Commands); Assert.Null(result.NativeResult);
    }

    [Fact]
    public void AlternateMotionAndManagedLimitsNeverReturnNativeNoRoute()
    {
        var s = new ApproachScript(); s.Zone(); s.Raw(RetailNpcApproachOperation.ActorVirtual14, "ApproachActorGuard", 0);
        s.Raw(RetailNpcApproachOperation.ActorVirtual18, "ApproachActorMotion", 1);
        Assert.Equal(RetailNpcApproachStatus.UnsupportedMotion, RetailNpcGroundApproachPlan.Replay(s.Observation()).Status);
        var complete = new ApproachScript(); complete.BasicPrimary(0);
        Assert.Equal(RetailNpcApproachStatus.DefensiveLimit, RetailNpcGroundApproachPlan.Replay(complete.Observation(), new() { MaxRecordedOperations = 1 }).Status);
        var limited = RetailNpcGroundApproachPlan.Replay(complete.Observation(), new() { MaxCommands = 1 });
        Assert.Equal(RetailNpcApproachStatus.DefensiveLimit, limited.Status); Assert.Empty(limited.Commands);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void UnusedOperationsAreRejectedEvenAfterANativeEarlyReturn(bool worldMismatch)
    {
        var s = new ApproachScript();
        if (worldMismatch) s.SetFrame(s.Frame with { TargetWorld = s.Frame.TargetWorld! with { WorldId = 2 } });
        else s.BasicPrimary(0);
        s.Raw(RetailNpcApproachOperation.ActorVirtual14, "UnusedTail", 0);
        var result = RetailNpcGroundApproachPlan.Replay(s.Observation());
        Assert.Equal(RetailNpcApproachStatus.UnsupportedEvidence, result.Status);
        Assert.Null(result.NativeResult); Assert.Empty(result.Commands);
        Assert.Equal(s.Steps.Count - 1, result.Consumed.Count);
        Assert.Contains("Unused", result.Detail);
    }

    [Fact]
    public void OversizedTranscriptIsRejectedBeforeAnEarlyReturnCanHideItsTruncatedTail()
    {
        var s = new ApproachScript(); s.SetFrame(s.Frame with { TargetWorld = s.Frame.TargetWorld! with { WorldId = 2 } });
        s.Raw(RetailNpcApproachOperation.ActorVirtual14, "TailOne", 0);
        s.Raw(RetailNpcApproachOperation.ActorVirtual18, "TailTwo", 0);
        var result = RetailNpcGroundApproachPlan.Replay(s.Observation(), new() { MaxRecordedOperations = 1 });
        Assert.Equal(RetailNpcApproachStatus.DefensiveLimit, result.Status); Assert.Empty(result.Consumed);
        Assert.Empty(result.Commands); Assert.Null(result.NativeResult);
    }

    [Theory]
    [InlineData("path-bytes")] [InlineData("native-binary")]
    public void IndividualLoadedPathAndPinnedBinaryDeclarationsMustMatch(string fault)
    {
        var s = new ApproachScript(); s.BasicPrimary(0); var observation = s.Observation();
        var sources = observation.Sources;
        sources = fault == "path-bytes"
            ? sources with { PathIdentity = sources.PathIdentity with { Asset = sources.PathIdentity.Asset with { Sha256 = new string('0', 64) } } }
            : sources with { NativeNpcBinary = sources.NativeNpcBinary! with { Sha256 = new string('0', 64) } };
        var result = RetailNpcGroundApproachPlan.Replay(observation with { Sources = sources, SyntheticFixture = false });
        Assert.Equal(RetailNpcApproachStatus.UnsupportedEvidence, result.Status); Assert.Empty(result.Commands); Assert.Empty(result.Consumed);
        Assert.False(result.RuntimeVerified);
    }

    [Fact]
    public void TwelveAngularAttemptsKeepNativeTableOrderAndDoNotBecomeHistoryOrAProjectionFallback()
    {
        var s = AngularScript(); s.AngularPrelude(); s.AngularMisses();
        var result = RetailNpcGroundApproachPlan.Replay(s.Observation());
        Assert.Equal(RetailNpcApproachStatus.Replayed, result.Status); Assert.Equal(1u, result.NativeResult);
        Assert.Equal(12, result.Consumed.Count(c => c.Request.Purpose == "AngularOuterProjection"));
        Assert.Equal(12, result.Consumed.Count(c => c.Request.Purpose == "AngularWideProjection"));
        Assert.DoesNotContain(result.Consumed, c => c.Request.Operation == RetailNpcApproachOperation.TargetHistory);
        Assert.Single(result.Commands, c => c.Kind == RetailNpcApproachCommandKind.GoalRequest);
    }

    [Fact]
    public void AngularInnerRetryUsesPoint3751AndWritesAnOrdinaryCacheAfterTheGoalSucceeds()
    {
        var s = AngularScript(); s.AngularPrelude();
        s.Project(new(12.5f, 1, 2), "AngularOuterProjection", true); // distanceSquared6.25 exceeds ordinary margin3.79
        var inner = new RetailSearchPoint(11.7502f, 1, 0);
        s.Project(new(11.7502f, 1, 2), "AngularInnerProjection", true, inner);
        s.Geom(new(RetailNpcApproachOperation.SurfaceLookup, "AngularSurface", inner, inner), 1);
        s.Geom(new(RetailNpcApproachOperation.Visibility23b2f0, "AngularVisibility", inner, s.Target.Position, Flag: 1), 1);
        s.Goal(inner, 0, 0, "AngularGoal");
        var result = RetailNpcGroundApproachPlan.Replay(s.Observation());
        Assert.Equal(RetailNpcApproachStatus.Replayed, result.Status);
        var cache = Assert.Single(result.Commands, c => c.Kind == RetailNpcApproachCommandKind.CacheWrite).Cache!;
        Assert.Equal(0, cache.Marker); Assert.Equal(inner, cache.ApproachPosition); Assert.Equal(s.Target.Position, cache.TargetPosition);
        Assert.Equal(new[] { "PrimaryGoal", "AngularGoal" }, result.Commands.Where(c => c.Kind == RetailNpcApproachCommandKind.GoalRequest).Select(c => c.Purpose));
    }

    [Fact]
    public void ForcedVisibilityApproachCanAdmitTheWideTwoMetreRetryWithItsOwnRangeMargin()
    {
        var s = AngularScript(); s.AngularPrelude(force: 1);
        s.Project(new(12.5f, 1, 2), "AngularOuterProjection", false);
        var wide = new RetailSearchPoint(13, 1, 0); s.Project(new(13, 1, 2), "AngularWideProjection", true, wide);
        s.Geom(new(RetailNpcApproachOperation.SurfaceLookup, "AngularSurface", wide, wide), 1);
        s.Geom(new(RetailNpcApproachOperation.Visibility23b2f0, "AngularVisibility", wide, s.Target.Position, Flag: 1), 1);
        s.Goal(wide, 0, 0, "AngularGoal");
        Assert.Equal(RetailNpcApproachStatus.Replayed, RetailNpcGroundApproachPlan.Replay(s.Observation(16, 1, -3)).Status);
    }

    [Theory]
    [InlineData(4u)] [InlineData(5u)]
    public void NativeAngularGoalFourOrFiveEndsTheAngularLoop(uint raw)
    {
        var s = AngularScript(); s.AngularPrelude(); var point = new RetailSearchPoint(12.5f, 1, 0);
        s.Project(new(12.5f, 1, 2), "AngularOuterProjection", true, point);
        s.Geom(new(RetailNpcApproachOperation.SurfaceLookup, "AngularSurface", point, point), 1);
        s.Geom(new(RetailNpcApproachOperation.Visibility23b2f0, "AngularVisibility", point, s.Target.Position, Flag: 1), 1);
        s.Goal(point, 0, raw, "AngularGoal");
        var result = RetailNpcGroundApproachPlan.Replay(s.Observation());
        Assert.Equal(RetailNpcApproachStatus.Replayed, result.Status); Assert.Equal(raw, result.NativeResult);
        Assert.Single(result.Consumed, c => c.Request.Purpose == "AngularOuterProjection");
    }

    [Theory]
    [InlineData(100u, 110u, false)] [InlineData(100u, 111u, true)] [InlineData(0xfffffff0u, 5u, true)]
    public void AngularProcessingBudgetUsesStrictUnsignedElapsedAndEqualityStillAttemptsAProjection(uint start, uint check, bool stops)
    {
        var s = AngularScript(); s.SetFrame(s.Frame with { BudgetEnabled = 1, BudgetMilliseconds = 10 });
        s.AngularPrelude(start: start); s.Clock("AngularBudgetCheck", check);
        if (!stops)
        {
            s.Project(new(12.5f, 1, 2), "AngularOuterProjection", false); s.Project(new(13, 1, 2), "AngularWideProjection", false);
            s.Clock("AngularBudgetCheck", 111);
        }
        var result = RetailNpcGroundApproachPlan.Replay(s.Observation());
        Assert.Equal(RetailNpcApproachStatus.Replayed, result.Status); Assert.Equal(5u, result.NativeResult);
        Assert.Equal(stops ? 0 : 1, result.Consumed.Count(c => c.Request.Purpose == "AngularOuterProjection"));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void HistoryUsesFirstStrictRadiusMatchAndItsMarkerOneEvenAfterAngularBudgetStops(bool firstOnBoundary)
    {
        var s = AngularScript(target: new(11, 1, 5));
        s.SetFrame(s.Frame with { History = new(true, 1, 100), BudgetEnabled = 1, BudgetMilliseconds = 10 });
        s.AngularPrelude(); s.Clock("AngularBudgetCheck", 111);
        s.Geom(new(RetailNpcApproachOperation.SurfaceLookup, "ActualTargetSurface", s.Target.Position, s.Target.Position), 0);
        var slots = Enumerable.Repeat(new RetailSearchPoint(500, 500, 500), 20).ToArray();
        slots[0] = firstOnBoundary ? new(21, 1, 5) : new(10, 1, 0); slots[19] = new(11, 1, 0);
        var selected = firstOnBoundary ? slots[19] : slots[0];
        var history = new RetailNpcApproachHistorySnapshot(s.Target.Id, 0, 2, slots, "trace7", new string('c', 64));
        slots[0] = new(99, 99, 99); // Snapshot owns its20 slots.
        s.Add(new(RetailNpcApproachOperation.TargetHistory, "TargetHistory", To: s.Target.Position, SquaredReach: 100, ReferenceId: s.Target.Id), history: history);
        s.Goal(selected, 0, 0, "HistoryGoal");
        var result = RetailNpcGroundApproachPlan.Replay(s.Observation());
        Assert.Equal(RetailNpcApproachStatus.Replayed, result.Status); Assert.Equal(0u, result.NativeResult);
        var cache = Assert.Single(result.Commands, c => c.Kind == RetailNpcApproachCommandKind.CacheWrite).Cache!;
        Assert.Equal(1, cache.Marker); Assert.Equal(selected, cache.ApproachPosition);
        Assert.Contains(result.Commands, c => c.Purpose == "AngularProcessingBudget");
        Assert.DoesNotContain(result.Consumed, c => c.Request.Purpose == "AngularOuterProjection");
    }

    [Fact]
    public void MissingObservedCrtHeadingAndUnknownHistorySnapshotAreVisibleSupportBoundaries()
    {
        var s = AngularScript(); s.BasicPrimary(1); s.Clock("AngularBudgetStart", 100);
        s.Add(new(RetailNpcApproachOperation.FallbackHeadingIndex, "AngularHeading"));
        Assert.Equal(RetailNpcApproachStatus.UnsupportedHeading, RetailNpcGroundApproachPlan.Replay(s.Observation()).Status);
        var h = AngularScript(target: new(11, 1, 5)); h.SetFrame(h.Frame with { History = new(true, 1, 100), BudgetEnabled = 1, BudgetMilliseconds = 1 });
        h.AngularPrelude(); h.Clock("AngularBudgetCheck", 102);
        h.Geom(new(RetailNpcApproachOperation.SurfaceLookup, "ActualTargetSurface", h.Target.Position, h.Target.Position), 0);
        h.Add(new(RetailNpcApproachOperation.TargetHistory, "TargetHistory", To: h.Target.Position, SquaredReach: 100, ReferenceId: h.Target.Id));
        Assert.Equal(RetailNpcApproachStatus.UnsupportedEvidence, RetailNpcGroundApproachPlan.Replay(h.Observation()).Status);
    }

    [Fact]
    public void NativeFailureCompareExchangeUsesTheFreshReturnedTickAndOnlyTheWinningWriteStoresTheFailedGoal()
    {
        var s = new ApproachScript(); s.Zone(); s.Raw(RetailNpcApproachOperation.StateCode7da70, "ApproachStateCode", 0);
        s.GroundGuards(); s.Project(new(11, 1, 2), "GroundProjection", false); s.Goal(s.Target.Position, .5f, 1);
        s.Clock("ApproachFailureClock", 1234); s.Cas("ApproachFailureStart", 1234, 0);
        var result = RetailNpcGroundApproachPlan.Replay(s.Observation(.5f));
        Assert.Equal(RetailNpcApproachStatus.Replayed, result.Status);
        var write = Assert.Single(result.Commands, c => c.Kind == RetailNpcApproachCommandKind.FailureTickWrite);
        Assert.Equal(1234u, write.Tick); Assert.Equal("CMPXCHG NPC+0xa18", write.LockScope);
        Assert.Single(result.Commands, c => c.Kind == RetailNpcApproachCommandKind.FailedGoalWrite);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CompareExchangeLoserRequiresTheOldValueToBeObservedBeforeTheOperation(bool observeLateWrite)
    {
        var s = new ApproachScript(); s.Zone(reject: true);
        s.Clock("ApproachFailureClock", 1234, s.NextFrame() with { FailureTick = observeLateWrite ? 900u : 0u });
        s.Cas("ApproachFailureStart", 1234, 900);
        var result = RetailNpcGroundApproachPlan.Replay(s.Observation());
        Assert.Equal(observeLateWrite ? RetailNpcApproachStatus.Replayed : RetailNpcApproachStatus.UnsupportedEvidence, result.Status);
        Assert.DoesNotContain(result.Commands, c => c.Kind is RetailNpcApproachCommandKind.FailureTickWrite or RetailNpcApproachCommandKind.FailedGoalWrite);
        Assert.Equal(observeLateWrite ? 1u : (uint?)null, result.NativeResult);
        if (!observeLateWrite) Assert.Empty(result.Commands);
    }

    [Fact]
    public void CompareExchangePostFrameMustContainTheWinningValue()
    {
        var s = new ApproachScript(); s.Zone(reject: true); s.Clock("ApproachFailureClock", 1234); s.Cas("ApproachFailureStart", 1234, 0);
        s.Steps[^1] = s.Steps[^1] with { AfterFrame = s.Steps[^1].AfterFrame! with { FailureTick = 999 } };
        var result = RetailNpcGroundApproachPlan.Replay(s.Observation());
        Assert.Equal(RetailNpcApproachStatus.UnsupportedEvidence, result.Status); Assert.Empty(result.Commands); Assert.Null(result.NativeResult);
    }

    [Fact]
    public void ZoneRejectionRunsTheNativeFailureHandlerWithTheUnrestoredHeightLift()
    {
        var s = new ApproachScript(); s.Zone(reject: true); s.Clock("ApproachFailureClock", 1234); s.Cas("ApproachFailureStart", 1234, 0);
        var result = RetailNpcGroundApproachPlan.Replay(s.Observation());
        Assert.Equal(RetailNpcApproachStatus.Replayed, result.Status); Assert.Equal(1u, result.NativeResult);
        Assert.Equal(new RetailSearchPoint(11, 1, 1.5f), Assert.Single(result.Commands, c => c.Kind == RetailNpcApproachCommandKind.FailedGoalWrite).Point);
        Assert.DoesNotContain(result.Consumed, c => c.Request.Operation is RetailNpcApproachOperation.ActorVirtual14 or RetailNpcApproachOperation.GroundProjection2600);
    }

    [Fact]
    public void NegativeZeroSquaredToleranceSurvivesAProjectionThatDoesNotChangeHeight()
    {
        float negativeZero = BitConverter.Int32BitsToSingle(unchecked((int)0x80000000));
        var s = new ApproachScript(); s.Zone(); s.GroundGuards(); s.Project(new(11, 1, 2), "GroundProjection", true, s.Target.Position);
        s.Goal(s.Target.Position, negativeZero, 0);
        var result = RetailNpcGroundApproachPlan.Replay(s.Observation(negativeZero));
        Assert.Equal(RetailNpcApproachStatus.Replayed, result.Status);
        Assert.Equal(unchecked((int)0x80000000), BitConverter.SingleToInt32Bits(result.Commands.First(c => c.Kind == RetailNpcApproachCommandKind.GoalRequest).Request!.SquaredReach!.Value));
    }

    private static ApproachScript AngularScript(RetailSearchPoint? target = null)
    {
        var s = new ApproachScript(target: target); s.SetFrame(s.Frame with { Actor = s.Actor with { Field32c = 0x10 } }); return s;
    }
}

internal sealed class ApproachScript
{
    private static readonly RetailSearchSourceReceipts source = CreateSources();
    private int stage;
    internal RetailNpcApproachFrame Initial { get; private set; }
    internal RetailNpcApproachFrame Frame { get; private set; }
    internal RetailNpcApproachActor Actor => Frame.Actor;
    internal RetailNpcApproachTarget Target => Frame.Target!;
    internal List<RetailNpcApproachRecordedStep> Steps { get; } = [];
    internal ApproachScript(RetailSearchPoint? actor = null, RetailSearchPoint? target = null, RetailSearchPoint? vector = null, int kind = 1)
    {
        var world = new RetailNpcApproachWorld("world-pointer", 320050000, new(new(0, 0, -10240), new(128, 128, 10240)));
        Initial = Frame = new("initial", "actor0", "target0", "scene0", "path0", "synthetic-field-frame",
            new(1, actor ?? new(1, 1, 0), .5f, 2f, 0, 0, 0, 0, 0, 0, 0),
            new(2, target ?? new(11, 1, 0), vector ?? new(0, 0, 0), .5f, 2f, kind, 0, 0, null, 17), world, world,
            new("path-absent", 0, 0, new(0, 0, 0), false), new(0, new(0, 0, 0), new(0, 0, 0), 0), 0, new(0, 0, 0), new(false, 0, 0), 0, 100);
    }
    internal void SetFrame(RetailNpcApproachFrame frame) { Assert.Empty(Steps); Initial = Frame = frame; }
    internal void SetProjectedFrame(RetailNpcApproachFrame frame) => Frame = frame;
    internal RetailNpcGroundApproachObservation Observation(float reach = 16, int force = 0, int? reason = null) => new(Initial, reach, force, reason, source, Steps.ToArray(), true);
    internal RetailNpcFollowStateObservation Follow(int ev = 9, bool state = true, uint end = 0) => new(Initial, ev, state, end, source, Steps.ToArray(), true);
    internal void Raw(RetailNpcApproachOperation operation, string purpose, uint value) => Add(new(operation, purpose), value);
    internal void Zone(bool reject = false) => Raw(RetailNpcApproachOperation.TargetZone10, "TargetZone10", reject ? 1u : 0u);
    internal void GroundGuards() { Raw(RetailNpcApproachOperation.ActorVirtual14, "ApproachActorGuard", 0); Raw(RetailNpcApproachOperation.ActorVirtual18, "ApproachActorMotion", 0); }
    internal void Project(RetailSearchPoint point, string purpose, bool found, RetailSearchPoint? output = null,
        RetailNpcApproachOperation operation = RetailNpcApproachOperation.GroundProjection2600) => Geom(new(operation, purpose, point, point), found ? 1u : 0u, output ?? point);
    internal void HasGoal(RetailSearchPoint point, bool found, RetailSearchPoint? output = null) => Add(new(RetailNpcApproachOperation.HasGoalPath, "OrientationHasGoal", To: point), found ? 1u : 0u, output ?? point);
    internal void Geom(RetailNpcApproachRequest request, uint raw, RetailSearchPoint? output = null)
    {
        string native = request.Operation switch
        {
            RetailNpcApproachOperation.PredictionProjection2480 => "0x1402f2480", RetailNpcApproachOperation.GroundProjection2600 => "0x1402f2600",
            RetailNpcApproachOperation.Segment2257e0 => "0x1402257e0", RetailNpcApproachOperation.Shortcut225430 => "0x140225430",
            RetailNpcApproachOperation.Visibility23b2f0 => "0x14023b2f0", RetailNpcApproachOperation.SurfaceLookup => "0x1400fa8d0", _ => throw new InvalidOperationException()
        };
        Add(request, raw, output, geometry: new(native, source.PathIdentity.Asset.Sha256, null, source.CollisionAsset!.Sha256, new string('a', 64), true));
    }
    internal void Goal(RetailSearchPoint point, float reach, uint raw, string purpose = "PrimaryGoal", uint? reference = null, RetailNpcApproachFrame? after = null) =>
        Add(new(RetailNpcApproachOperation.MoveToGoal, purpose, To: point, SquaredReach: reach, ReferenceId: reference ?? Target.Id, MovementFlags: 2, ReuseFlag: 1), raw, after: after ?? NextFrame());
    internal void Clock(string purpose, uint tick, RetailNpcApproachFrame? after = null) => Add(new(RetailNpcApproachOperation.Clock, purpose), tick: tick, after: after ?? NextFrame());
    internal void Advance(RetailNpcApproachRequest request, RetailNpcApproachFrame? after = null) => Add(request, after: after ?? NextFrame());
    internal void Cas(string purpose, uint tick, uint old) => Add(new(RetailNpcApproachOperation.CompareExchangeFailureTick, purpose, TickValue: tick), old,
        after: NextFrame() with { FailureTick = old == 0 ? tick : old });
    internal RetailNpcApproachFrame NextFrame() => Frame with { Identity = "stage" + ++stage, ActorRevision = "actor" + stage, PathRevision = "path" + stage };
    internal void Add(RetailNpcApproachRequest request, uint? raw = null, RetailSearchPoint? output = null, uint? tick = null,
        RetailNpcApproachFrame? after = null, RetailNpcApproachGeometryReceipt? geometry = null, int? heading = null, RetailNpcApproachHistorySnapshot? history = null)
    {
        var receipt = new RetailNpcApproachReceipt(true, RetailGroundPathSearch.SupportedNpcBinarySha256, Frame.Identity,
            Frame.ActorRevision, Frame.TargetRevision, Frame.SceneRevision, Frame.PathRevision, "explicit-synthetic-native-observation");
        Steps.Add(new(request, receipt, raw, output, tick, heading, after, geometry, history));
        if (after is not null) Frame = after;
    }
    internal void BasicPrimary(uint result, float reach = 16)
    { if (Target.ObjectKind == 1) Zone(); GroundGuards(); Project(Target.Position with { Z = Target.Position.Z + 2 }, "GroundProjection", false); Goal(Target.Position, reach, result); }
    internal void AngularPrelude(int force = 0, uint start = 100)
    { BasicPrimary(1, force == 0 ? 16 : 0); Clock("AngularBudgetStart", start); Add(new(RetailNpcApproachOperation.FallbackHeadingIndex, "AngularHeading"), heading: 0); }
    internal void AngularMisses()
    {
        float z = Target.Position.Z == 5 ? 11 : 2;
        foreach (var (x, y) in new (float, float)[] { (1,0),(.866025f,.5f),(.5f,.866025f),(0,1),(-.5f,.866025f),(-.866025f,.5f),(-1,0),(-.866025f,-.5f),(-.5f,-.866025f),(0,-1),(.5f,-.866025f),(.866025f,-.5f) })
        {
            Project(new(1.5f * x + Target.Position.X, 1.5f * y + Target.Position.Y, z), "AngularOuterProjection", false);
            Project(new(2f * x + Target.Position.X, 2f * y + Target.Position.Y, z), "AngularWideProjection", false);
        }
    }
    private static RetailSearchSourceReceipts CreateSources()
    {
        using var memory = new MemoryStream(); using var writer = new BinaryWriter(memory);
        var header = new byte[128]; BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(16), 5); BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(18), 6);
        writer.Write(header); writer.Write(4); writer.Write(0); writer.Write(0);
        for (int i = 0; i < 64; i++)
        { writer.Write((byte)1); writer.Write((byte)0); writer.Write((byte)15); for (int d = 0; d < 4; d++) writer.Write(uint.MaxValue); writer.Write(0); }
        var raw = memory.ToArray(); var read = RetailPathDataReader.Read(raw, new(128, 128)); Assert.True(read.Success, read.Error?.ToString());
        return new(read.Data!, new(new("synthetic-path", Convert.ToHexString(SHA256.HashData(raw)).ToLowerInvariant(), raw.Length), Guid.Empty, 128, 128), null, null,
            new("pinned-native-declaration", RetailGroundPathSearch.SupportedNpcBinarySha256, RetailGroundPathSearch.SupportedNpcBinaryBytes),
            new("synthetic-collision-declaration", new string('b', 64), 1), new("synthetic-actor", "synthetic-scene", "synthetic-collision", "synthetic-replay-only"));
    }
}
