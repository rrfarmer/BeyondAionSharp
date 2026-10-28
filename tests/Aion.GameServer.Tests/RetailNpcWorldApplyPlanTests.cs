using Aion.GameServer.GeoEngine.RetailPath;

namespace Aion.GameServer.Tests;

public sealed class RetailNpcWorldApplyPlanTests
{
    [Fact]
    public void Same32CellWritesPoseWithNetZeroReferencesAndNoWrapperDelay()
    {
        var f=MovementOwnerFixture.Create();var state=f.State with {ClockReads=MovementOwnerFixture.Clocks("MoveBegin","MoveEnd"),Actor=f.State.Actor with {Progress82c=1}};
        var r=RetailNpcWorldApplyPlan.Plan(state,f.Scene,new(31.5f,1,0),f.Limits);
        Assert.True(r.WholeEffectListApplicable,r.Failure?.ToString());Assert.Equal(1u,r.NativeRawResult);Assert.Equal(10,r.State.Actor.References8);
        Assert.Equal(2,r.State.Actor.Progress82c);Assert.Equal("old",r.State.Actor.WrapperIdentity80);Assert.Empty(r.State.DelayedWrappers.Slots);
        Assert.Equal(new(31.5f,1,0),r.State.Actor.Position);Assert.Equal(new(31,1,0),r.State.Actor.SamplingPosition9d8);
    }
    [Fact]
    public void Cross32UnlinksDelaysWritesThenReinsertsAndAcknowledges()
    {
        var f=MovementOwnerFixture.Create();var s=f.State with {ClockReads=MovementOwnerFixture.Clocks("MoveBegin","DelayTimestamp","MoveEnd"),Actor=f.State.Actor with {Progress82c=1}};
        var r=RetailNpcWorldApplyPlan.Plan(s,f.Scene,new(32,1,0),f.Limits);
        Assert.Equal(1u,r.NativeRawResult);Assert.True(r.WholeEffectListApplicable,r.Failure?.ToString());Assert.Equal(11,r.State.Actor.References8);Assert.Equal(1,r.State.Actor.DelayedReferencesC);
        Assert.Equal(1,r.State.DelayedWrappers.NpcCount146955148);Assert.Equal(2,r.State.Actor.Progress82c);Assert.Equal(1,r.State.Actor.InWorldAc);
        var old=r.State.World.Wrappers.Single(w=>w.Identity=="old");Assert.Equal((byte)1,old.DelayFreed3f);Assert.Equal(2,r.State.World.Wrappers.Count);
        var writes=r.Writes.ToArray();int xyz=Array.FindIndex(writes,w=>w.Target=="actor+90/94/98");int put=Array.FindIndex(writes,w=>w.Target=="actor+ac.exchange"&&w.NewValue=="1");
        Assert.True(xyz>=0&&put>xyz);Assert.Equal(0u,r.State.Actor.SamplingCount9d4);
    }
    [Fact]
    public void PositionChangesBeforeOutOfRangePutFailureAndNoAckOrMoveEndReadOccurs()
    {
        var f=MovementOwnerFixture.Create();var s=f.State with {World=f.State.World with {CellCount32=1},Actor=f.State.Actor with {Progress82c=1},ClockReads=MovementOwnerFixture.Clocks("MoveBegin","DelayTimestamp")};
        var r=RetailNpcWorldApplyPlan.Plan(s,f.Scene,new(32,1,0),f.Limits);
        Assert.Equal(0u,r.NativeRawResult);Assert.True(r.WholeEffectListApplicable,r.Failure?.ToString());Assert.Equal(new(32,1,0),r.State.Actor.Position);
        Assert.Equal(1,r.State.Actor.Progress82c);Assert.Equal(0,r.State.Actor.InWorldAc);Assert.Null(r.State.Actor.WrapperIdentity80);Assert.Equal(9,r.State.Actor.References8);
        Assert.DoesNotContain(r.Writes,w=>w.Target=="actor+82c.compareExchange(1,2)");Assert.Single(r.State.DelayedWrappers.Slots);
    }
    [Fact]
    public void RemoveFailureKeepsPositionAndNoAckButRetainsPreapplySamplingWrite()
    {
        var f=MovementOwnerFixture.Create();var s=f.State with {World=f.State.World with {Wrappers=[new("old",7,3,null,null,0,0xbb5a,0xa5)]},Actor=f.State.Actor with {Progress82c=1,SamplingPosition9d8=new(0,0,0)},ClockReads=MovementOwnerFixture.Clocks("MoveBegin")};
        var r=RetailNpcWorldApplyPlan.Plan(s,f.Scene,new(32,1,0),f.Limits);
        Assert.Equal(0u,r.NativeRawResult);Assert.Equal(new(31,1,0),r.State.Actor.Position);Assert.Equal(1,r.State.Actor.Progress82c);Assert.Equal(10,r.State.Actor.References8);
        Assert.Equal(new(31,1,0),r.State.Actor.SamplingPosition9d8);Assert.Equal(1u,r.State.Globals.MoveNpcCalls174c54720);
    }
    [Theory]
    [InlineData(0)] [InlineData(2)] [InlineData(7)]
    public void AckChangesOnlyExactlyProgress1(int progress)
    {
        var f=MovementOwnerFixture.Create();var s=f.State with {Actor=f.State.Actor with {Progress82c=progress},ClockReads=MovementOwnerFixture.Clocks("MoveBegin","MoveEnd")};
        var r=RetailNpcWorldApplyPlan.Plan(s,f.Scene,new(31.5f,1,0),f.Limits);Assert.Equal(progress,r.State.Actor.Progress82c);
    }
    [Fact]
    public void HalfOpenEndpointBoundaryReturnsBeforeTemporaryReferenceAndAck()
    {
        var f=MovementOwnerFixture.Create();var s=f.State with {ClockReads=MovementOwnerFixture.Clocks("MoveBegin")};
        var r=RetailNpcWorldApplyPlan.Plan(s,f.Scene,new(64,1,0),f.Limits);Assert.Equal(0u,r.NativeRawResult);Assert.Equal(10,r.State.Actor.References8);Assert.Equal(new(31,1,0),r.State.Actor.Position);
        Assert.DoesNotContain(r.Writes,w=>w.Target=="actor+8.atomic");
    }
    [Fact]
    public void ActualWatcherPredicateIsReachedAfterApplyAndAckWithoutCommittingAWholeEffectList()
    {
        var f=MovementOwnerFixture.Create();var s=f.State with {Actor=f.State.Actor with {Progress82c=1,ScriptMove8dc=1},World=f.State.World with {Cells=f.State.World.Cells.Select(c=>c.Index==0?c with {ScriptMoveCount=1}:c).ToArray()},ClockReads=MovementOwnerFixture.Clocks("MoveBegin","DelayTimestamp")};
        var r=RetailNpcWorldApplyPlan.Plan(s,f.Scene,new(32,1,0),f.Limits);Assert.Equal(RetailMovementCalculationStatus.IncompleteAtChild,r.Status);Assert.False(r.WholeEffectListApplicable);
        Assert.Equal(new(32,1,0),r.State.Actor.Position);Assert.Equal(2,r.State.Actor.Progress82c);Assert.Equal(1,r.State.World.Cells.Single(c=>c.Index==1).ScriptMoveCount);
    }
    [Theory]
    [InlineData("ring")] [InlineData("rtti")] [InlineData("allocation")]
    public void UnknownLifetimeOrStorageChildRetainsOnlyItsComputedPrefix(string boundary)
    {
        var f=MovementOwnerFixture.Create();var s=f.State with {ClockReads=MovementOwnerFixture.Clocks("MoveBegin","DelayTimestamp")};
        if(boundary=="ring")s=s with {DelayedWrappers=s.DelayedWrappers with {WriteIndex=1,Slots=new Dictionary<int,RetailNpcDelayedWrapper>{{0,new("previous",99,0,1)}}},ClockReads=MovementOwnerFixture.Clocks("MoveBegin")};
        if(boundary=="rtti")s=s with {Globals=s.Globals with {ObjectWrapperRtti174c5e224=0}};
        if(boundary=="allocation")s=s with {Storage=s.Storage with {AvailableWrapperSlots=0}};
        var r=RetailNpcWorldApplyPlan.Plan(s,f.Scene,new(32,1,0),f.Limits);Assert.Equal(RetailMovementCalculationStatus.IncompleteAtChild,r.Status);Assert.False(r.WholeEffectListApplicable);Assert.Null(r.NativeRawResult);
    }
    [Fact]
    public void ChangedClockRevisionIsStaleBeforeAnyPositionApply()
    {
        var f=MovementOwnerFixture.Create();var clock=new RetailNpcMovementClockRead("MoveBegin",0x140237813,1000,"different-request","life1");
        var r=RetailNpcWorldApplyPlan.Plan(f.State with {ClockReads=[clock]},f.Scene,new(32,1,0),f.Limits);
        Assert.Equal(RetailMovementCalculationStatus.StaleOwnership,r.Status);Assert.Equal(new(31,1,0),r.State.Actor.Position);Assert.Equal(1u,r.State.Globals.MoveNpcCalls174c54720);
    }
    [Theory]
    [InlineData(63f,64f,1,2)] [InlineData(64f,63f,2,1)]
    public void Cross64LocksAreAcquiredInAscendingOrderInEitherDirection(float source,float target,int oldCell,int newCell)
    {
        var f=MovementOwnerFixture.Create(size:128);
        var s=f.State with {Actor=f.State.Actor with {Position=new(source,1,0),Progress82c=1},World=f.State.World with {
            Cells=f.State.World.Cells.Select(c=>c with {OrderedObjects=c.Index==oldCell?new uint[]{7}:[]}).ToArray(),Wrappers=[new("old",7,oldCell,null,null,0,0xbb5a,0xa5)]},ClockReads=MovementOwnerFixture.Clocks("MoveBegin","DelayTimestamp","MoveEnd")};
        var r=RetailNpcWorldApplyPlan.Plan(s,f.Scene,new(target,1,0),f.Limits);
        Assert.True(r.WholeEffectListApplicable,r.Failure?.ToString());Assert.Equal(1u,r.NativeRawResult);
        Assert.Contains(r.Children,c=>c.Program=="RCU lock order"&&c.EvaluatedGuard=="ascending 0,1");
        Assert.Empty(r.State.World.Cells.Single(c=>c.Index==oldCell).OrderedObjects);Assert.Equal(new uint[]{7},r.State.World.Cells.Single(c=>c.Index==newCell).OrderedObjects);
    }
    [Theory]
    [InlineData("history")] [InlineData("first-debug-source")]
    public void ReferenceAtomicIncrementPrecedesAnActiveMetadataChild(string child)
    {
        var f=MovementOwnerFixture.Create();var a=child=="history"?f.State.Actor with {ReferenceHistory14=1}:f.State.Actor with {DebugSource28=0};
        var r=RetailNpcWorldApplyPlan.Plan(f.State with {Actor=a,ClockReads=MovementOwnerFixture.Clocks("MoveBegin")},f.Scene,new(32,1,0),f.Limits);
        Assert.Equal(RetailMovementCalculationStatus.IncompleteAtChild,r.Status);Assert.Equal(11,r.State.Actor.References8);Assert.Equal(new(31,1,0),r.State.Actor.Position);Assert.False(r.WholeEffectListApplicable);
    }
}
