using Aion.GameServer.Ai.Lifecycle;
using Aion.GameServer.GeoEngine.RetailPath;

namespace Aion.GameServer.Tests;

public sealed class RetailNpcFollowStatePlanTests
{
    [Theory]
    [InlineData(4, false)] [InlineData(8, false)] [InlineData(9, true)] [InlineData(3, true)] [InlineData(-1, true)] [InlineData(int.MinValue, true)]
    public void RawEventAdmissionIsSignedAndStateExistenceIsIndependent(int ev, bool admits)
    {
        var s = new ApproachScript(); s.SetFrame(s.Frame with { Actor = s.Actor with { FieldB8 = 1 } });
        var result = RetailNpcFollowStatePlan.Replay(s.Follow(ev));
        Assert.Equal(RetailNpcApproachStatus.Replayed, result.Status);
        Assert.Equal(admits, result.Commands.Any(c => c.Kind == RetailNpcApproachCommandKind.PopState));
        Assert.Empty(RetailNpcFollowStatePlan.Replay(s.Follow(ev, state: false)).Commands);
    }

    [Fact]
    public void SameWorldIdDoesNotReplaceNativeWorldPointerIdentity()
    {
        var s = new ApproachScript(); s.SetFrame(s.Frame with { TargetWorld = s.Frame.TargetWorld! with { Identity = "another-instance-pointer" } });
        Validity(s); var result = RetailNpcFollowStatePlan.Replay(s.Follow());
        Assert.Equal(RetailNpcApproachStatus.Replayed, result.Status);
        Assert.Equal(new[] { RetailNpcApproachCommandKind.FailureTickWrite, RetailNpcApproachCommandKind.FailedGoalWrite, RetailNpcApproachCommandKind.PopState }, result.Commands.Select(c => c.Kind));
        Assert.DoesNotContain(result.Consumed, c => c.Request.Operation == RetailNpcApproachOperation.Clock);
    }

    [Theory]
    [InlineData(1000u, false)] [InlineData(1001u, true)]
    public void StateExpiryUsesAbsoluteUnsignedStrictGreaterThan(uint now, bool expires)
    {
        var s = new ApproachScript(); Validity(s); s.Clock("FollowStart", now);
        if (!expires) s.Clock("FollowMovementGate", now, s.NextFrame() with { Actor = s.Actor with { MovementEndTick = 2000 } });
        var result = RetailNpcFollowStatePlan.Replay(s.Follow(end: 1000));
        Assert.Equal(RetailNpcApproachStatus.Replayed, result.Status);
        Assert.Equal(expires, result.Commands.Any(c => c.Kind == RetailNpcApproachCommandKind.PopState));
        if (!expires) Assert.Equal(500, Assert.Single(result.Commands).DelayMilliseconds);
    }

    [Fact]
    public void LateMovementEndReloadCanPreventANewApproachEvenWhenTheInitialValueWasDue()
    {
        var s = new ApproachScript(); Validity(s); s.Clock("FollowStart", 100);
        s.Clock("FollowMovementGate", 101, s.NextFrame() with { Actor = s.Actor with { MovementEndTick = 1000 } });
        var result = RetailNpcFollowStatePlan.Replay(s.Follow());
        Assert.Equal(RetailNpcApproachStatus.Replayed, result.Status);
        var only = Assert.Single(result.Commands); Assert.Equal(RetailNpcApproachCommandKind.ScheduleEvent, only.Kind);
        Assert.Equal(500, only.DelayMilliseconds); Assert.Equal(9, only.Code);
    }

    [Theory]
    [InlineData(101f, 0u, 0, false)] [InlineData(101.0001f, 0u, 0, true)]
    [InlineData(121f, 1u, 0, false)] [InlineData(121f, 0u, 1, false)]
    public void HundredMetreLeashIsInclusiveAndItsOwnerFieldsAreIndependent(float targetX, uint master, int field568, bool pops)
    {
        var s = new ApproachScript(target: new(targetX, 1, 0));
        s.SetFrame(s.Frame with { Actor = s.Actor with { MasterId = master, Field568 = field568, MovementEndTick = 2000 } });
        Validity(s); s.Clock("FollowStart", 100);
        if (!pops) s.Clock("FollowMovementGate", 100);
        var result = RetailNpcFollowStatePlan.Replay(s.Follow());
        Assert.Equal(RetailNpcApproachStatus.Replayed, result.Status);
        Assert.Equal(pops, result.Commands.Any(c => c.Kind == RetailNpcApproachCommandKind.PopState));
        Assert.Equal(!pops, result.Commands.Any(c => c.Kind == RetailNpcApproachCommandKind.ScheduleEvent));
    }

    [Theory]
    [InlineData(0u)] [InlineData(2u)] [InlineData(4u)]
    public void NativeApproachOutcomeOwnsTheFiveHundredMillisecondFollowSchedule(uint native)
    {
        var s = new ApproachScript(); s.SetFrame(s.Frame with { Actor = s.Actor with { Field32c = 0x10 }, FailureTick = 77 });
        BeginMove(s, 100); s.Zone(); s.Raw(RetailNpcApproachOperation.StateCode7da70, "ApproachStateCode", 0);
        s.GroundGuards(); s.Project(new(11, 1, 2), "GroundProjection", false); s.Goal(s.Target.Position, 6.25f, native);
        var result = RetailNpcFollowStatePlan.Replay(s.Follow());
        Assert.Equal(RetailNpcApproachStatus.Replayed, result.Status);
        Assert.Equal(500, Assert.Single(result.Commands, c => c.Kind == RetailNpcApproachCommandKind.ScheduleEvent).DelayMilliseconds);
        Assert.DoesNotContain(result.Consumed, c => c.Request.Operation == RetailNpcApproachOperation.CompareExchangeFailureTick);
        Assert.Equal(0u, result.FinalFrame!.FailureTick);
        Assert.False(result.RuntimeVerified); Assert.False(result.ExecutesCommands);
    }

    [Fact]
    public void FirstFailureUsesTheEarlierCapturedClockCopiesFloatBitsAndDoesNotAddARetryTask()
    {
        var s = new ApproachScript(target: new(BitConverter.Int32BitsToSingle(0x41312345), 1, 0));
        BeginMove(s, 1000, 1100); s.Zone(reject: true); s.Cas("FollowFailureStart", 1000, 0);
        var result = RetailNpcFollowStatePlan.Replay(s.Follow());
        Assert.Equal(RetailNpcApproachStatus.Replayed, result.Status);
        var tick = Assert.Single(result.Commands, c => c.Kind == RetailNpcApproachCommandKind.FailureTickWrite);
        Assert.Equal(1000u, tick.Tick);
        var failed = Assert.Single(result.Commands, c => c.Kind == RetailNpcApproachCommandKind.FailedGoalWrite).Point!.Value;
        Assert.Equal(0x41312345, BitConverter.SingleToInt32Bits(failed.X));
        Assert.DoesNotContain(result.Commands, c => c.Kind == RetailNpcApproachCommandKind.ScheduleEvent);
        Assert.DoesNotContain(result.Commands, c => c.Kind == RetailNpcApproachCommandKind.GoalRequest);
    }

    [Theory]
    [InlineData(100u, 30100u, false)] [InlineData(100u, 30101u, true)]
    [InlineData(0xfffffff0u, 0xfffffff1u, true)]
    public void FailureDeadlinePreservesAbsoluteUint32EqualityAndWrap(uint previous, uint captured, bool dismiss)
    {
        var s = new ApproachScript(); s.SetFrame(s.Frame with { FailureTick = previous });
        BeginMove(s, captured); s.Zone(reject: true); s.Cas("FollowFailureStart", captured, previous);
        if (dismiss) s.RawDismiss(0);
        var result = RetailNpcFollowStatePlan.Replay(s.Follow());
        Assert.Equal(RetailNpcApproachStatus.Replayed, result.Status);
        if (dismiss)
        {
            Assert.Equal(new[] { RetailNpcApproachCommandKind.Dismiss, RetailNpcApproachCommandKind.Remove }, result.Commands.Where(c => c.Kind is RetailNpcApproachCommandKind.Dismiss or RetailNpcApproachCommandKind.Remove).Select(c => c.Kind));
            Assert.Equal(2, result.Commands.Last().Code);
            Assert.DoesNotContain(result.Commands, c => c.Kind == RetailNpcApproachCommandKind.ScheduleEvent);
        }
        else
        {
            Assert.Equal(3000, Assert.Single(result.Commands, c => c.Kind == RetailNpcApproachCommandKind.ScheduleEvent).DelayMilliseconds);
            Assert.Equal(unchecked(captured + 3000), Assert.Single(result.Commands, c => c.Kind == RetailNpcApproachCommandKind.MovementEndTickWrite).Tick);
        }
    }

    [Fact]
    public void SuccessfulDismissDoesNotAlsoRequestRemove()
    {
        var s = new ApproachScript(); s.SetFrame(s.Frame with { FailureTick = 1 });
        BeginMove(s, 40000); s.Zone(reject: true); s.Cas("FollowFailureStart", 40000, 1); s.RawDismiss(1);
        var result = RetailNpcFollowStatePlan.Replay(s.Follow());
        Assert.Equal(RetailNpcApproachStatus.Replayed, result.Status);
        Assert.Single(result.Commands, c => c.Kind == RetailNpcApproachCommandKind.Dismiss);
        Assert.DoesNotContain(result.Commands, c => c.Kind == RetailNpcApproachCommandKind.Remove);
    }

    [Fact]
    public void FollowReadsPostApproachFailureTickInsteadOfInventingASecondIndependentTimer()
    {
        var s = new ApproachScript(); BeginMove(s, 1000, suppressApproachFailure: false);
        s.Zone(reject: true); s.Clock("ApproachFailureClock", 900); s.Cas("ApproachFailureStart", 900, 0);
        s.SetProjectedFrame(s.Frame with { FailedGoal = new(11, 1, 1.5f) });
        s.Cas("FollowFailureStart", 1000, 900);
        var result = RetailNpcFollowStatePlan.Replay(s.Follow());
        Assert.Equal(RetailNpcApproachStatus.Replayed, result.Status);
        Assert.Equal(900u, Assert.Single(result.Commands, c => c.Kind == RetailNpcApproachCommandKind.FailureTickWrite).Tick);
        Assert.Equal(new RetailSearchPoint(11, 1, 1.5f), Assert.Single(result.Commands, c => c.Kind == RetailNpcApproachCommandKind.FailedGoalWrite).Point);
        Assert.Equal(900u, result.FinalFrame!.FailureTick);
        Assert.Equal(3000, result.Commands.Last().DelayMilliseconds);
    }

    [Theory]
    [InlineData(0f, false)] [InlineData(.0001f, true)]
    public void InRangeStationaryTargetAndExactVelocityEpsilonHaveDifferentOwnership(float vectorX, bool moving)
    {
        var s = new ApproachScript(target: new(2, 1, 0), vector: new(vectorX, 0, 0));
        s.SetFrame(s.Frame with { Actor = s.Actor with { MasterId = s.Target.Id, MovementEndTick = 1000 } });
        Validity(s); s.Clock("FollowStart", 100);
        s.Raw(RetailNpcApproachOperation.TargetVectorZero, "FollowTargetVectorZero", moving ? 0u : 1u);
        s.Clock(moving ? "FollowMovementGate" : "FollowDirectionGate", 100);
        var result = RetailNpcFollowStatePlan.Replay(s.Follow());
        Assert.Equal(RetailNpcApproachStatus.Replayed, result.Status); Assert.Equal(500, Assert.Single(result.Commands).DelayMilliseconds);
        Assert.Equal(moving ? "FollowMovementGate" : "FollowDirectionGate", result.Consumed.Last().Request.Purpose);
    }

    [Fact]
    public void StationaryMasterDirectionIsRequestedOnlyWhenMovementIsDue()
    {
        var s = new ApproachScript(target: new(2, 1, 0)); s.SetFrame(s.Frame with { Actor = s.Actor with { MasterId = s.Target.Id } });
        Validity(s); s.Clock("FollowStart", 100); s.Raw(RetailNpcApproachOperation.TargetVectorZero, "FollowTargetVectorZero", 1); s.Clock("FollowDirectionGate", 100);
        var result = RetailNpcFollowStatePlan.Replay(s.Follow());
        var command = Assert.Single(result.Commands); Assert.Equal(RetailNpcApproachCommandKind.ChangeDirection, command.Kind);
        Assert.Equal(17, command.Code); Assert.Equal(1, command.Request!.Flag);
    }

    [Theory]
    [InlineData("clock")] [InlineData("state")] [InlineData("epoch")]
    public void MissingLateFrameOrWrongRevisionCannotApplyPartialCommands(string fault)
    {
        var s = new ApproachScript(); BeginMove(s, 100); s.Zone(reject: true); s.Cas("FollowFailureStart", 100, 0);
        int i = fault == "state" ? s.Steps.FindIndex(x => x.Request.Operation == RetailNpcApproachOperation.ChangeState) : s.Steps.FindIndex(x => x.Request.Operation == RetailNpcApproachOperation.Clock);
        s.Steps[i] = fault == "epoch" ? s.Steps[i] with { Receipt = s.Steps[i].Receipt with { PathRevision = "old-path" } } : s.Steps[i] with { AfterFrame = null };
        var result = RetailNpcFollowStatePlan.Replay(s.Follow());
        Assert.Equal(RetailNpcApproachStatus.UnsupportedEvidence, result.Status); Assert.Empty(result.Commands);
        Assert.True(result.RequiresAdapterRejectionInvalidation);
    }

    [Fact]
    public void IgnoredFollowEventCannotSilentlyAcceptAnUnconsumedOperation()
    {
        var s = new ApproachScript(); s.Raw(RetailNpcApproachOperation.ActorVirtual22, "UnconsumedIgnoredEvent", 1);
        var result = RetailNpcFollowStatePlan.Replay(s.Follow(ev: 4));
        Assert.Equal(RetailNpcApproachStatus.UnsupportedEvidence, result.Status); Assert.Empty(result.Consumed); Assert.Empty(result.Commands);
        Assert.True(result.RequiresAdapterRejectionInvalidation);
    }

    private static void Validity(ApproachScript s)
    {
        s.Raw(RetailNpcApproachOperation.ActorVirtual22, "FollowActor22", 1);
        s.Raw(RetailNpcApproachOperation.ActorVirtual14, "FollowActorGuard", 0);
        s.Raw(RetailNpcApproachOperation.TargetVirtualB0, "FollowTargetValid", 1);
    }
    private static void BeginMove(ApproachScript s, uint start, uint? gate = null, bool suppressApproachFailure = true)
    {
        if (suppressApproachFailure) s.SetFrame(s.Frame with { Actor = s.Actor with { Field32c = 0x10 } });
        Validity(s); s.Clock("FollowStart", start); s.Clock("FollowMovementGate", gate ?? start);
        s.Advance(new(RetailNpcApproachOperation.ChangeState, "FollowMoveState", Flag: 1, Code: 0x19, Reason: 0x1b));
    }
}

internal static class ApproachFollowScriptExtensions
{
    internal static void RawDismiss(this ApproachScript script, uint raw) => script.Add(new(RetailNpcApproachOperation.Dismiss, "FollowDismiss", Code: 0), raw);
}
