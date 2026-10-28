using Aion.GameServer.GeoEngine.RetailPath;

namespace Aion.GameServer.Tests;

public sealed class RetailNpcMoveToGoalPlanTests
{
    [Fact]
    public void FreshSearchTransfersHeaderStagesPendingAndDoesNotApplyWorldOrSchedule()
    {
        var f=MovementOwnerFixture.Create();var r=RetailNpcMoveToGoalPlan.Plan(f.State with {ClockReads=MovementOwnerFixture.Clocks("FindBegin","FindSuccessEnd")},f.Scene,f.Goal(),f.Limits);
        Assert.Equal(0u,r.NativeRawResult);Assert.True(r.WholeEffectListApplicable,r.Failure?.ToString());Assert.Equal(new(31,1,0),r.State.Actor.Position);
        Assert.Equal(new(32,1,0),r.State.Motion.Next18);Assert.Equal(1u,r.State.Motion.Pending8);Assert.Equal(1,r.State.Path!.Index);Assert.Equal(1,r.State.Actor.Progress82c);
        Assert.Empty(r.State.Timers.Entries);Assert.DoesNotContain(r.Effects,e=>e.Kind==RetailNpcMovementEffectKind.NativeInterserverMove);
        Assert.Equal(0,r.State.Actor.PathLock290);
    }
    [Theory]
    [InlineData("null-world",1u)] [InlineData("dead-state",4u)] [InlineData("table-zero",4u)] [InlineData("forbidden-row",4u)] [InlineData("near",2u)]
    public void NativeEarlyReturnGuardsDoNotReadUnreachedClocks(string condition,uint raw)
    {
        var f=MovementOwnerFixture.Create();var state=f.State with {ClockReads=[]};var request=f.Goal();
        state=condition switch {"null-world"=>state with {Actor=state.Actor with {WorldPointerIdentity=null}},"dead-state"=>state with {Actor=state.Actor with {State598=2}},"table-zero"=>state with {Actor=state.Actor with {Pose5a0=0}},"forbidden-row"=>state with {Actor=state.Actor with {Pose5a0=0x101}},_=>state};
        if(condition=="near")request=request with {Goal=state.Actor.Position};
        var r=RetailNpcMoveToGoalPlan.Plan(state,f.Scene,request,f.Limits);Assert.Equal(raw,r.NativeRawResult);Assert.True(r.WholeEffectListApplicable,r.Failure?.ToString());Assert.Null(r.State.Path);
    }
    [Fact]
    public void RepeatFailedGoalSamplesBeginThenReturnsWithoutReadingOwnedPath()
    {
        var f=MovementOwnerFixture.Create();var state=f.State with {Actor=f.State.Actor with {LastFailedGoalA1c=new(32,1,0)},ClockReads=MovementOwnerFixture.Clocks("FindBegin")};
        var r=RetailNpcMoveToGoalPlan.Plan(state,f.Scene,f.Goal(),f.Limits);Assert.Equal(1u,r.NativeRawResult);Assert.Null(r.State.Path);
        Assert.DoesNotContain(r.Children,c=>c.Program=="HasPathToMove(goal)");Assert.DoesNotContain(r.Writes,w=>w.Target=="actor+290.exchange");
    }
    [Theory]
    [InlineData(false,1u)] [InlineData(true,5u)]
    public void NativeNoRouteClearsPreviousPathAndRecordsFailedGoalWhileSlowNativeDomainCanReturn5(bool slowDomain,uint expected)
    {
        var f=MovementOwnerFixture.Create();var p=new RetailNpcOwnedMovementPath([new(31,1,0),new(40,1,0)],2,0,new(40,1,0),5,"prior");
        var state=f.State with {Path=p,Storage=f.State.Storage with {UsedPathPointSlots=2},ClockReads=[new("FindBegin",0x140123491,1000,"request1","life1"),new("FindFailureEnd",0x140123923,1400,"request1","life1")],World=f.State.World with {WorldId=slowDomain?0x80000000u:900010000u}};
        var r=RetailNpcMoveToGoalPlan.Plan(state,f.Scene,f.Goal(100),f.Limits);
        Assert.Equal(expected,r.NativeRawResult);Assert.True(r.WholeEffectListApplicable,r.Failure?.ToString());Assert.Null(r.State.Path);Assert.Equal(new(100,1,0),r.State.Actor.LastFailedGoalA1c);
        Assert.Equal(2,r.Children.Count(c=>c.Program=="FindPath computed geometry/policy/search"));
    }
    [Theory]
    [InlineData(1)] [InlineData(2)]
    public void ConfiguredUnsupportedSelectorDoesNotBecomeOrdinaryAStarOrNativeNoRoute(int selector)
    {
        var f=MovementOwnerFixture.Create();var r=RetailNpcMoveToGoalPlan.Plan(f.State with {ClockReads=MovementOwnerFixture.Clocks("FindBegin")},f.Scene,f.Goal() with {NativeSelector=selector},f.Limits);
        Assert.Equal(RetailMovementCalculationStatus.UnsupportedConsumedDomain,r.Status);Assert.Null(r.NativeRawResult);Assert.False(r.WholeEffectListApplicable);Assert.Null(r.State.Path);
    }
    [Fact]
    public void SuppliedPathCloneResetsIndexThenFollowConsumesDuplicateSource()
    {
        var f=MovementOwnerFixture.Create();var supplied=new RetailNpcOwnedMovementPath([new(31,1,0),new(32,1,0)],2,1,new(32,1,0),9,"supplied");
        var r=RetailNpcMoveToGoalPlan.Plan(f.State with {ClockReads=[],UnconsumedPathWord30c=5},f.Scene,f.Goal() with {SuppliedPath=supplied},f.Limits);
        Assert.Equal(0u,r.NativeRawResult);Assert.Equal(1,r.State.Path!.Index);Assert.Equal(5u,r.State.Path.Extra30c);Assert.Equal(1,r.State.Actor.Progress82c);Assert.Equal(1,supplied.Index);
    }
    [Fact]
    public void ReusePreservesIndexButAssignsRemainingCountAfterSimplifyingTheSubpath()
    {
        var f=MovementOwnerFixture.Create();var old=new RetailNpcOwnedMovementPath([new(31,1,0),new(35,1,0),new(40,1,0)],3,1,new(40,1,0),9,"old-path");
        var state=f.State with {Path=old,Storage=f.State.Storage with {UsedPathPointSlots=3},ClockReads=MovementOwnerFixture.Clocks("FindBegin")};
        var r=RetailNpcMoveToGoalPlan.Plan(state,f.Scene,f.Goal(50) with {AllowReuse=true},f.Limits);
        Assert.Equal(0u,r.NativeRawResult);Assert.True(r.WholeEffectListApplicable,r.Failure?.ToString());
        Assert.Equal(2,r.State.Path!.Count);Assert.Equal(1,r.State.Path.Index);Assert.Equal(4,r.State.Path.Points.Count);
        Assert.Equal(new(50,1,0),r.State.Path.Points[2]);Assert.Equal(9u,r.State.Path.Extra30c);
        Assert.Equal(0,r.State.Actor.Progress82c); // Normal partial raw0 must not fabricate1.
        Assert.Contains(r.Writes,w=>w.Target=="path.count ODD remaining count"&&w.NewValue=="2");
    }
    [Fact]
    public void FollowDuplicateExhaustionReturns2AndInvalidatesButLowBudget4RetainsPath()
    {
        var f=MovementOwnerFixture.Create();var duplicates=new RetailNpcOwnedMovementPath([new(31,1,0),new(31,1,0)],2,0,new(32,1,0),0,"dup");
        var r=RetailNpcMoveToGoalPlan.Plan(f.State with {ClockReads=[]},f.Scene,f.Goal() with {SuppliedPath=duplicates},f.Limits);
        Assert.Equal(2u,r.NativeRawResult);Assert.Null(r.State.Path);
        var slow=f.State with {Actor=f.State.Actor with {Speed5e0=.1f},ClockReads=MovementOwnerFixture.Clocks("FindBegin","FindSuccessEnd")};
        r=RetailNpcMoveToGoalPlan.Plan(slow,f.Scene,f.Goal(),f.Limits);Assert.Equal(4u,r.NativeRawResult);Assert.NotNull(r.State.Path);Assert.Equal(0u,r.State.Motion.Pending8);
    }
    [Fact]
    public void PathReservationLimitIsManagedRejectionRatherThanFabricatedNativeFalse()
    {
        var f=MovementOwnerFixture.Create();var r=RetailNpcMoveToGoalPlan.Plan(f.State with {Storage=f.State.Storage with {AvailablePathPointSlots=1},ClockReads=MovementOwnerFixture.Clocks("FindBegin")},f.Scene,f.Goal(),f.Limits);
        Assert.Equal(RetailMovementCalculationStatus.DefensiveLimit,r.Status);Assert.Null(r.NativeRawResult);Assert.False(r.WholeEffectListApplicable);
    }
    [Fact]
    public void EmptySuppliedCloneReturnsNative1AndClearsPriorPathWithoutInventingAnAllocation()
    {
        var f=MovementOwnerFixture.Create();var old=new RetailNpcOwnedMovementPath([new(31,1,0),new(40,1,0)],2,0,new(40,1,0),9,"prior");
        var s=f.State with {Path=old,Storage=f.State.Storage with {UsedPathPointSlots=2},ClockReads=[]};
        var r=RetailNpcMoveToGoalPlan.Plan(s,f.Scene,f.Goal() with {SuppliedPath=new([],0,0,new(32,1,0),88,"empty")},f.Limits);
        Assert.Equal(1u,r.NativeRawResult);Assert.True(r.WholeEffectListApplicable);Assert.Null(r.State.Path);Assert.Equal(9u,r.State.UnconsumedPathWord30c);
        Assert.DoesNotContain(r.Effects,e=>e.Kind==RetailNpcMovementEffectKind.ModelAllocation);
    }
    [Fact]
    public void SuppliedCloneCopiesOnlyCountPointsAndDoesNotCopyTheAdjacentWord()
    {
        var f=MovementOwnerFixture.Create();var supplied=new RetailNpcOwnedMovementPath([new(31,1,0),new(32,1,0),new(63,63,0)],2,0,new(32,1,0),99,"supplied");
        var r=RetailNpcMoveToGoalPlan.Plan(f.State with {ClockReads=[],UnconsumedPathWord30c=5},f.Scene,f.Goal() with {SuppliedPath=supplied},f.Limits);
        Assert.Equal(2,r.State.Path!.Points.Count);Assert.Equal(5u,r.State.Path.Extra30c);Assert.Equal(5u,r.State.UnconsumedPathWord30c);
    }
    [Theory]
    [InlineData(0u,0u)] [InlineData(1u,1u)] [InlineData(2u,0u)] [InlineData(3u,0u)] [InlineData(4u,4u)]
    public void NativeSetBit2BranchStagesZeroInsteadOfPropagatingTheRequestWord(uint requestFlags,uint expected)
    {
        var f=MovementOwnerFixture.Create();var r=RetailNpcMoveToGoalPlan.Plan(f.State with {ClockReads=MovementOwnerFixture.Clocks("FindBegin","FindSuccessEnd")},f.Scene,f.Goal() with {MotionFlags=requestFlags},f.Limits);
        Assert.Equal(0u,r.NativeRawResult);Assert.Equal(expected,r.State.Motion.Waypoint34);
    }
    [Fact]
    public void EffectfulAdmissionSetsCcBeforeSlot19PreventsMovement()
    {
        var f=MovementOwnerFixture.Create();var r=RetailNpcMoveToGoalPlan.Plan(f.State with {Actor=f.State.Actor with {Pose5a0=5},ClockReads=[]},f.Scene,f.Goal(),f.Limits);
        Assert.Equal(4u,r.NativeRawResult);Assert.Equal(1,r.State.Motion.Cc);Assert.Null(r.State.Path);
    }
}
