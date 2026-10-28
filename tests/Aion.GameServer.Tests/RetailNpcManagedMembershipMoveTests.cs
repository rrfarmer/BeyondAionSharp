using System.Collections.Immutable;
using Aion.GameServer.GeoEngine.RetailPath;
using O = Aion.GameServer.GeoEngine.RetailPath.RetailNpcManagedMembershipObservation;
using M = Aion.GameServer.GeoEngine.RetailPath.RetailNpcManagedMembershipMove;
using V = Aion.GameServer.GeoEngine.RetailPath.RetailNpcManagedVisibility;

namespace Aion.GameServer.Tests;

public sealed class RetailNpcManagedMembershipMoveTests
{
    [Theory]
    [InlineData(31f,32f,0,1,0,0)] [InlineData(32f,31f,1,0,0,0)]
    [InlineData(63f,64f,1,2,0,1)] [InlineData(64f,63f,2,1,1,0)]
    public void Cross32And64ComputeWholeMotionMembershipVisibilityAndOrderedDependencies(float oldX,float newX,int oldCell,int newCell,int oldRegion,int newRegion)
    {
        // Reverse movement swaps the source fixture's enter/leave coordinates, so use empty
        // recipients here; the nonempty ordered transaction is independently covered below.
        var input=ManagedMembershipFixture.Make(oldX,newX,observers:false);var r=RetailNpcManagedVisibilityTests.Complete(input);var k=r.Known!;
        Assert.Equal(ManagedMembershipFixture.V(newX),k.Motion.State.Endpoint120);Assert.Equal(2100u,k.Motion.State.Due1858);Assert.Equal(1103u,k.Motion.State.Start185c);
        Assert.Equal(ManagedMembershipFixture.V(newX-oldX,0,0),k.Motion.State.Velocity186c);
        Assert.Equal(oldCell,input.Scene.Members[0].Cell);Assert.Equal(newCell,Assert.Single(k.Membership).Cell);
        Assert.NotEqual(input.Scene.Members[0].LeaseIdentity,k.Membership[0].LeaseIdentity);Assert.False(k.NativeMemoryPoststate);
        int At(string stage)=>Array.FindIndex(k.Events.ToArray(),e=>e.Stage==stage);
        Assert.True(At("InsertNewManagedMembershipLeaseBeforeEndpoint")<At("EndpointWorldHeadingPublished"));
        Assert.True(At("EndpointWorldHeadingPublished")<At("ManagedDependencyNativeOldRegionReleaseFirst"));
        Assert.Equal(oldRegion,Assert.Single(k.Events,e=>e.Stage=="ManagedDependencyNativeOldRegionReleaseFirst").After);
        Assert.Equal(oldRegion!=newRegion,k.Events.Any(e=>e.Stage=="ManagedDependencyNativeNewRegionReleaseSecond"));
        if(oldRegion!=newRegion)Assert.Equal(newRegion,Assert.Single(k.Events,e=>e.Stage=="ManagedDependencyNativeNewRegionReleaseSecond").After);
        Assert.Equal(4,k.Events.Count(e=>e.Stage.StartsWith("MaterialSkip:",StringComparison.Ordinal)));
    }
    [Theory]
    [InlineData(31f,32f)] [InlineData(32f,31f)] [InlineData(63f,64f)] [InlineData(64f,63f)]
    public void NonemptyEnterStayLeaveOwnersAreCompleteAcrossBothBoundaries(float oldX,float newX)
    {
        var r=RetailNpcManagedVisibilityTests.Complete(ManagedMembershipFixture.Make(oldX,newX));
        Assert.Equal(3,r.Known!.ProvisionalIntents.Length);Assert.Equal(1,r.Known.ProvisionalIntents.Count(x=>x.Meaning.Kind==V.Kind.Spawn));
        Assert.Equal(1,r.Known.ProvisionalIntents.Count(x=>x.Meaning.Kind==V.Kind.Move));Assert.Equal(1,r.Known.ProvisionalIntents.Count(x=>x.Meaning.Kind==V.Kind.Delete));
        Assert.Equal("world-generation",r.Candidate!.Value.ExpectedWorldGeneration);Assert.Equal("npc-life",r.Candidate.Value.SourceActorLifetime);
    }
    [Fact]
    public void SameCellRunsTheSameWholeOwnerWithoutRetiringItsMembershipLease()
    {
        var i=ManagedMembershipFixture.Make(12,18,observers:false);var r=RetailNpcManagedVisibilityTests.Complete(i);
        Assert.Equal(ManagedMembershipFixture.V(18),r.Known!.Motion.State.Endpoint120);Assert.Equal(i.Scene.Members.ToArray(),r.Known.Membership.ToArray());
        Assert.Equal(ManagedMembershipFixture.V(6,0,0),r.Known.Motion.State.Velocity186c);
        Assert.DoesNotContain(r.Known.Events,e=>e.Stage=="RetireOldManagedMembershipLease");
    }
    [Fact]
    public void KnownBoundsFailureContinuesMaterialAtRetainedEndpointWithoutRollingBackMotion()
    {
        var i=ManagedMembershipFixture.Make(31,128,observers:false);var r=RetailNpcManagedVisibilityTests.Complete(i);var k=r.Known!;
        Assert.True(r.Candidate!.Value.WorldBoundsRejected);Assert.Equal(ManagedMembershipFixture.V(31),k.Motion.State.Endpoint120);
        Assert.Equal(ManagedMembershipFixture.V(97,0,0),k.Motion.State.Velocity186c);Assert.Equal(2100u,k.Motion.State.Due1858);
        Assert.Equal(i.Scene.Members.ToArray(),k.Membership.ToArray());Assert.Empty(k.ProvisionalIntents);Assert.Equal((byte)1,k.Motion.Material);
    }
    [Fact]
    public void PrivateCandidateHasAnExplicitOnceLedgerAndNeverExecutesHostEffects()
    {
        var i=ManagedMembershipFixture.Make();var r=RetailNpcManagedVisibilityTests.Complete(i);var owner=M.Owner.FromDeclaredInput(i);
        var first=owner.CalculateOnce(r.Candidate!);Assert.Equal(M.OnceStatus.ComputedFirstApplication,first.Status);Assert.True(first.State.Consumed);
        Assert.Equal(1ul,first.State.Revision);Assert.Same(r.Candidate!.Value.State,first.State.Calculated);
        var repeated=first.State.CalculateOnce(r.Candidate);Assert.Equal(M.OnceStatus.AlreadyComputed,repeated.Status);Assert.Same(first.State,repeated.State);
        Assert.False(r.Candidate.HostEffectsExecuted);Assert.False(r.Candidate.EncodedFor48);Assert.False(r.CanApplyToHost);
    }
    [Fact]
    public void AnUnchangedGenerationLabelCannotHideDifferentOwnedBeforeBytes()
    {
        var i=ManagedMembershipFixture.Make();var r=RetailNpcManagedVisibilityTests.Complete(i);
        var changed=ManagedMembershipFixture.UserImage(i,0,u=>O.Put32(u,0x7358,2));var owner=M.Owner.FromDeclaredInput(changed);
        var result=owner.CalculateOnce(r.Candidate!);Assert.Equal(M.OnceStatus.StaleOwner,result.Status);Assert.False(result.State.Consumed);Assert.Null(result.State.Calculated);
    }
    [Theory]
    [InlineData(0)] [InlineData(1)]
    public void ActualHeadingAndSequenceEarlyReturnDoNotConsumeUnreachedOriginalClocks(int guard)
    {
        var i=ManagedMembershipFixture.Make(observers:false);var old=RetailNpcInterserverMoveCodec.DecodeCanonical(i.CanonicalMove.AsSpan()).Record!;
        var wrong=guard==0?old with {Heading=120}:old with {Sequence=1};
        var r=M.Calculate(i with {CanonicalMove=RetailNpcInterserverMoveCodec.EncodeCanonical(wrong).Bytes,Clocks=[]});
        Assert.Equal(O.Status.NativeEarlyReturn,r.Status);Assert.NotNull(r.Candidate);Assert.Empty(r.Known!.ProvisionalIntents);
        Assert.Equal(i.Npc.MotionSource.Motion,r.Known.Motion.State);Assert.Equal(i.Scene.Members.ToArray(),r.Known.Membership.ToArray());
    }
    public static IEnumerable<object[]> Flags()=>Enumerable.Range(0,256).Select(x=>new object[]{(byte)x});
    [Theory,MemberData(nameof(Flags))]
    public void AllCanonicalFlagValuesPreserveNativeStateAndMoveSnapshotRules(byte flags)
    {
        var i=ManagedMembershipFixture.Make();bool extended=(flags&0xc0)==0xc0;
        var frame=RetailNpcInterserverMoveCodec.EncodeCanonical(new(7,ManagedMembershipFixture.V(32),0,flags,0,extended?ManagedMembershipFixture.V(32):null,1000)).Bytes;
        var r=RetailNpcManagedVisibilityTests.Complete(i with {CanonicalMove=frame});
        Assert.Equal((byte)(flags&0xed),r.Known!.Motion.Flags);
        Assert.Equal(extended?2100u:1100u,r.Known.Motion.State.Due1858);Assert.Equal(1103u,r.Known.Motion.State.Start185c);
        var moves=r.Known.ProvisionalIntents.Where(p=>p.Meaning.Kind==V.Kind.Move).Select(p=>(V.Move)p.Meaning).ToArray();
        Assert.Equal(flags==8?0:1,moves.Length);
        if(flags!=8){Assert.Equal((byte)(flags&0xef),moves[0].Flags);Assert.Equal(ManagedMembershipFixture.V(31),moves[0].EffectiveOld);
            Assert.Equal(extended&&(flags&0x20)!=0?ManagedMembershipFixture.V(32):default,moves[0].Goal);}
    }
    [Theory]
    [InlineData(0)] [InlineData(1)]
    public void ActualComputedGeometryNpcRouteFollowAndEmissionComposeThroughCrossCellMain(int mode)
    {
        int oldX=mode==0?31:63;var fixture=MovementOwnerFixture.Create(size:128);var state=fixture.State;
        if(oldX==63)
        {
            var cells=state.World.Cells.Select(c=>c with {OrderedObjects=c.Index==1?new uint[]{7}:Array.Empty<uint>()}).ToArray();
            state=state with {Actor=state.Actor with {Position=new(63,1,0)},World=state.World with {Cells=cells,Wrappers=[state.World.Wrappers[0] with {ParentCell=1}]}};
        }
        var npc=RetailNpcMovementOwnerPlan.Calculate(state,fixture.Scene,fixture.Goal(oldX+1,1),1000,0,fixture.Limits);
        Assert.True(npc.WholeEffectListApplicable,npc.Failure?.ToString());
        Assert.Contains(npc.Children,c=>c.Program=="FindPath computed geometry/policy/search");
        var emission=Assert.Single(npc.Effects,e=>e.Kind==RetailNpcMovementEffectKind.NativeInterserverMove);
        var endpoint=emission.Position!.Value;var goal=emission.Desired!.Value;
        Assert.Equal((float)(oldX+1),endpoint.X);Assert.Equal(0f,endpoint.Z);
        var i=ManagedMembershipFixture.Make(oldX,endpoint.X);
        var frame=RetailNpcInterserverMoveCodec.EncodeCanonical(new(state.Actor.ObjectId,ManagedMembershipFixture.V(endpoint.X,endpoint.Y,endpoint.Z),
            (byte)emission.Heading!.Value,(byte)emission.Flags!.Value,(byte)state.Actor.PacketState178,
            ManagedMembershipFixture.V(goal.X,goal.Y,goal.Z),emission.Duration!.Value)).Bytes;
        var r=RetailNpcManagedVisibilityTests.Complete(i with {CanonicalMove=frame});
        Assert.Equal(ManagedMembershipFixture.V(endpoint.X,endpoint.Y,endpoint.Z),r.Known!.Motion.State.Endpoint120);
        Assert.Equal(unchecked(1100u+emission.Duration.Value),r.Known.Motion.State.Due1858);
        Assert.Equal(3,r.Known.ProvisionalIntents.Length);Assert.False(npc.HostEffectsExecuted);Assert.False(r.CanApplyToHost);
        Assert.DoesNotContain(r.Known.Events,e=>e.Stage.Contains("old owner rejected",StringComparison.Ordinal));
    }
}
