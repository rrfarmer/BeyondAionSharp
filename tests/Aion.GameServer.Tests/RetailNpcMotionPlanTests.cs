using Aion.GameServer.GeoEngine.RetailPath;

namespace Aion.GameServer.Tests;

public sealed class RetailNpcMotionPlanTests
{
    private static RetailNpcMovementSnapshot Pending(MovementOwnerFixture f)=>f.State with {Motion=f.State.Motion with {Pending8=1,DesiredC=new(32,1,0),Next18=new(32,1,0)},ClockReads=MovementOwnerFixture.Clocks("MoveBegin","DelayTimestamp","MoveEnd","AddTimer")};
    [Theory]
    [InlineData(0u,0u,333u,0xe0u,0x40403133u)]
    [InlineData(100u,0u,633u,0xe2u,0x3fca3631u)]
    [InlineData(0u,50u,100u,0xe4u,0x41200000u)]
    public void DurationFlagsAndVelocityUseOrderedNativeArithmetic(uint oldHeading,uint durationOverride,uint duration,uint flags,uint velocityWord)
    {
        var f=MovementOwnerFixture.Create();var state=Pending(f);state=state with {Actor=state.Actor with {HeadingB0=oldHeading},Motion=state.Motion with {Duration28=durationOverride}};
        var r=RetailNpcMotionPlan.Update(state,f.Scene,1000,0,f.Limits);Assert.True(r.WholeEffectListApplicable,r.Failure?.ToString());
        var move=Assert.Single(r.Effects,e=>e.Kind==RetailNpcMovementEffectKind.NativeInterserverMove);Assert.Equal(duration,move.Duration);Assert.Equal(flags,move.Flags);
        Assert.Equal(velocityWord,BitConverter.SingleToUInt32Bits(r.State.Actor.Velocity.X));Assert.Equal(unchecked(1000+duration),r.State.Motion.Deadline4);
    }
    [Fact]
    public void NonpositiveEffectiveSendSpeedUses5000msAndDoesNotAddTurnDelay()
    {
        var f=MovementOwnerFixture.Create();var state=Pending(f);state=state with {Actor=state.Actor with {SelectedSpeed5a4=-1,HeadingB0=60}};
        var r=RetailNpcMotionPlan.Update(state,f.Scene,1000,0,f.Limits);var move=Assert.Single(r.Effects,e=>e.Kind==RetailNpcMovementEffectKind.NativeInterserverMove);
        Assert.Equal(5000u,move.Duration);Assert.Equal(0xe0u,move.Flags);Assert.Equal(.2f,r.State.Actor.Velocity.X);
    }
    [Fact]
    public void PositiveTemplateTurnTimeStopsAfterNativeVelocityPrefixBeforeSend()
    {
        var f=MovementOwnerFixture.Create();var state=Pending(f);state=state with {Actor=state.Actor with {HeadingB0=60,TemplateTurnTimeE0=1}};
        var r=RetailNpcMotionPlan.Update(state,f.Scene,1000,0,f.Limits);
        Assert.Equal(RetailMovementCalculationStatus.IncompleteAtChild,r.Status);Assert.Equal("positive-template-turn-time",r.Failure!.Boundary);
        Assert.NotEqual(default,r.State.Actor.Velocity);Assert.DoesNotContain(r.Effects,e=>e.Kind==RetailNpcMovementEffectKind.NativeInterserverMove);
    }
    [Fact]
    public void GateDefersPendingWithoutInventingATimerAndStopClearsOnlyOnNonzeroDeadline()
    {
        var f=MovementOwnerFixture.Create();var s=Pending(f) with {ClockReads=[]};s=s with {Motion=s.Motion with {StartGate0=2000}};
        var r=RetailNpcMotionPlan.Update(s,f.Scene,1000,0,f.Limits);Assert.True(r.WholeEffectListApplicable);Assert.Equal(1u,r.State.Motion.Pending8);Assert.Empty(r.State.Timers.Entries);Assert.Empty(r.Effects);
        s=f.State with {ClockReads=[],Actor=f.State.Actor with {Velocity=new(1,0,0)},Motion=f.State.Motion with {Deadline4=500,Flags38=16}};
        r=RetailNpcMotionPlan.Update(s,f.Scene,1000,0,f.Limits);Assert.Equal(default,r.State.Actor.Velocity);Assert.Equal(0u,r.State.Motion.Deadline4);
        Assert.Equal(16u,Assert.Single(r.Effects,e=>e.Kind==RetailNpcMovementEffectKind.NativeInterserverStop).Flags);
    }
    [Theory]
    [InlineData(0)] [InlineData(2)] [InlineData(4)] [InlineData(6)]
    public void ReachedPartyOrMultiplierChildCannotBeReplacedByArbitrarySpeed(int state)
    {
        var f=MovementOwnerFixture.Create();var r=RetailNpcMoveToGoalPlan.Plan(f.State with {Actor=f.State.Actor with {SpeedStateObjectPlus8=state},ClockReads=[]},f.Scene,f.Goal(),f.Limits);
        Assert.Equal(RetailMovementCalculationStatus.IncompleteAtChild,r.Status);Assert.Equal(3,r.State.Actor.SelectedSpeed5a4);Assert.Null(r.NativeRawResult);
    }
    [Fact]
    public void TimerClosingReturnsZeroAfterWorldApplyWithoutIOOrRegistration()
    {
        var f=MovementOwnerFixture.Create();var s=Pending(f);s=s with {Globals=s.Globals with {ClosingIo171d640ec=1},ClockReads=MovementOwnerFixture.Clocks("MoveBegin","DelayTimestamp","MoveEnd")};
        var r=RetailNpcMotionPlan.Update(s,f.Scene,1000,0,f.Limits);Assert.True(r.WholeEffectListApplicable);Assert.Equal(new(32,1,0),r.State.Actor.Position);
        Assert.Equal(0,r.State.Actor.PendingIo10);Assert.Empty(r.State.Timers.Entries);Assert.Equal(0u,r.State.Motion.Deadline4);
    }
    [Fact]
    public void CancelLeavesProgressStartGateAndQueuedRegistrationsAlone()
    {
        var f=MovementOwnerFixture.Create();var t=new RetailNpcMovementTimerEntry(20,7,1,0,7,"life1","q");
        var s=f.State with {ClockReads=[],Actor=f.State.Actor with {Progress82c=2},Motion=f.State.Motion with {StartGate0=100,Deadline4=20,Pending8=1},Timers=f.State.Timers with {Entries=new[]{t}}};
        var r=RetailNpcMotionPlan.Cancel(s,f.Scene,f.Limits);Assert.True(r.WholeEffectListApplicable);Assert.Equal(2,r.State.Actor.Progress82c);Assert.Equal(100u,r.State.Motion.StartGate0);Assert.Equal(t,Assert.Single(r.State.Timers.Entries));
    }
    [Fact]
    public void NonzeroActionIsExchangedBeforeIncompleteSendActionChild()
    {
        var f=MovementOwnerFixture.Create();var s=f.State with {ClockReads=[],Motion=f.State.Motion with {Action44=1,Action48=25}};
        var r=RetailNpcMotionPlan.Update(s,f.Scene,1000,0,f.Limits);Assert.Equal(RetailMovementCalculationStatus.IncompleteAtChild,r.Status);Assert.Equal(0u,r.State.Motion.Action44);Assert.Equal("SendActionPacket",r.Failure!.Boundary);
    }
}
