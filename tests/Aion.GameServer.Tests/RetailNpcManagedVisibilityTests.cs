using System.Buffers.Binary;
using System.Collections.Immutable;
using Aion.GameServer.Commons.Utils.RetailRandom;
using Aion.GameServer.GeoEngine.RetailPath;
using O = Aion.GameServer.GeoEngine.RetailPath.RetailNpcManagedMembershipObservation;
using M = Aion.GameServer.GeoEngine.RetailPath.RetailNpcManagedMembershipMove;
using V = Aion.GameServer.GeoEngine.RetailPath.RetailNpcManagedVisibility;

namespace Aion.GameServer.Tests;

public sealed class RetailNpcManagedVisibilityTests
{
    internal static M.Result Complete(O.Input input)
    {
        var r=M.Calculate(input);Assert.True(r.Status==O.Status.CompleteManagedTransaction,r.Failure?.Detail);
        Assert.NotNull(r.Candidate);Assert.NotNull(r.Known);Assert.False(r.CanApplyToHost);Assert.False(r.ActivationReady);Assert.False(r.NativeOracle);return r;
    }
    [Theory]
    [InlineData(100f,0f,10000)] [InlineData(100f,.5f,10000)] [InlineData(100f,1f,10001)]
    public void DppsSquaredRangeUsesSignedCvttRatherThanFloatDistance(float x,float y,int expected)
        =>Assert.Equal(expected,V.Distance(ManagedMembershipFixture.V(x,y,0),default));
    [Theory]
    [InlineData(46341f)] [InlineData(float.NaN)] [InlineData(float.PositiveInfinity)]
    public void FiniteButUnrepresentableAndExceptionalDistanceNeverBecomeInside(float x)
        =>Assert.Throws<RetailMainApplyFault>(()=>V.Distance(ManagedMembershipFixture.V(x,0,0),default));
    [Fact]
    public void EnterStayLeaveOutsideAndActualSpawnValuesComeFromOwnedRawBytes()
    {
        var r=Complete(ManagedMembershipFixture.Make());var known=r.Known!;
        var spawn=Assert.IsType<V.Spawn>(Assert.Single(known.ProvisionalIntents,p=>p.Meaning.Kind==V.Kind.Spawn).Meaning);
        var stay=Assert.IsType<V.Move>(Assert.Single(known.ProvisionalIntents,p=>p.Meaning.Kind==V.Kind.Move).Meaning);
        var leave=Assert.IsType<V.Delete>(Assert.Single(known.ProvisionalIntents,p=>p.Meaning.Kind==V.Kind.Delete).Meaning);
        Assert.Equal(100u,spawn.RecipientId);Assert.Equal(101u,stay.RecipientId);Assert.Equal(102u,leave.RecipientId);
        Assert.DoesNotContain(known.ProvisionalIntents,p=>p.Meaning.RecipientId==103);
        V.Argument Arg(int n)=>Assert.Single(spawn.Arguments,a=>a.NativeIndex==n);
        Assert.Equal(ManagedMembershipFixture.V(32),Arg(2).Vector);Assert.Equal(212776ul,Arg(4).Scalar);Assert.Equal(555ul,Arg(10).Scalar);
        Assert.Equal("Rig",Arg(13).Text);Assert.Equal("Key",Arg(15).Text);Assert.Equal(50ul,Arg(16).Scalar);Assert.Equal(100ul,Arg(17).Scalar);
        Assert.Equal(35ul,Arg(18).Scalar);Assert.Equal(ManagedMembershipFixture.Bits(2),(uint)Arg(20).Scalar);
        Assert.Equal(6000ul,Arg(30).Scalar);Assert.Equal(111u,BinaryPrimitives.ReadUInt32LittleEndian(Arg(19).Bytes.AsSpan(0,4)));
        Assert.Equal(150,spawn.NativeSourceSize); //122 + UTF16 text12 + one equipment16.
        Assert.Equal(ManagedMembershipFixture.V(31),stay.EffectiveOld);Assert.Equal(ManagedMembershipFixture.V(32),stay.Goal);
        Assert.Equal(2,known.Events.Count(e=>e.Stage=="Slot38Read1c34"));
        Assert.Equal(2,known.Events.Count(e=>e.Stage=="RecipientSlot41Raw1698"));
        Assert.Equal(0u,O.U32(known.Users[0].Image,0x7358));Assert.Contains(known.Events,e=>e.Stage=="PostSeeCAS7358Expected1Desired2");
        Assert.All(known.ProvisionalIntents,p=>{Assert.False(p.EncodedFor48);Assert.False(p.HostEffectsExecuted);});
    }
    [Theory]
    [InlineData(0u)] [InlineData(2u)]
    public void ActualPostSeeZeroOrTwoStateFailsCasWithoutAnInventedPoststate(uint state)
    {
        var i=ManagedMembershipFixture.UserImage(ManagedMembershipFixture.Make(),0,u=>O.Put32(u,0x7358,state));var r=Complete(i);
        Assert.Equal(state,O.U32(r.Known!.Users[0].Image,0x7358));Assert.Null(r.Known.Random);
    }
    [Theory]
    [InlineData(0u,0u)] [InlineData(0u,2u)] [InlineData(0u,255u)]
    [InlineData(1u,0u)] [InlineData(1u,2u)] [InlineData(1u,255u)]
    public void BothCachedTribeBytesShareRawWordAndSkipUnknownMatrix(uint tribe,uint flags)
    {
        var i=ManagedMembershipFixture.Make();i=ManagedMembershipFixture.UserImage(i,0,u=>O.Put32(u,0x1698,tribe));
        i=ManagedMembershipFixture.NpcImage(i,n=>O.Put32(n,0x1c3c,tribe==0?flags<<8:flags<<16));
        var r=Complete(i);var spawn=Assert.IsType<V.Spawn>(Assert.Single(r.Known!.ProvisionalIntents,p=>p.Meaning.Kind==V.Kind.Spawn).Meaning);
        Assert.Equal((ulong)flags,Assert.Single(spawn.Arguments,a=>a.NativeIndex==6).Scalar);
        Assert.DoesNotContain(r.Known.Events,e=>e.Stage=="MatrixRead");
        Assert.Equal((flags&2)==0,r.Known.Events.Any(e=>e.Stage=="PostSeeCAS7358Expected1Desired2"));
    }
    [Fact]
    public void HiddenBitSkipsSeeAndStayBeforeUnreachedSpawnClockOrRelationWork()
    {
        var i=ManagedMembershipFixture.NpcImage(ManagedMembershipFixture.Make(),n=>O.Put32(n,0x1c3c,1));
        i=i with {Clocks=i.Clocks.Where(c=>c.RecipientId==0).ToImmutableArray()};var r=Complete(i);
        Assert.Single(r.Known!.ProvisionalIntents);Assert.IsType<V.Delete>(r.Known.ProvisionalIntents[0].Meaning);
        Assert.DoesNotContain(r.Known.Events,e=>e.Stage=="RecipientSlot41Raw1698" || e.Stage=="Slot38Read1c34");
    }
    [Fact]
    public void SharedRawRngIsConsumedBeforeTheLater7350SkipAndRetainsItsOwnLease()
    {
        var i=ManagedMembershipFixture.UserImage(ManagedMembershipFixture.Make(),0,u=>O.Put32(u,0x7350,71));var rng=ManagedMembershipFixture.Random();
        i=i with {Scene=i.Scene with {Random=rng,Globals=i.Scene.Globals with {PostSee142769c24=2}}};
        var expected=RetailRandomEngine.Draw(rng.State);var r=Complete(i);Assert.Equal(expected.State.Sha256,r.Known!.Random!.State.Sha256);
        int At(string name)=>Array.FindIndex(r.Known.Events.ToArray(),e=>e.Stage==name);
        Assert.True(At("SharedRawRngBeforeAny7350Read")<At("PostSeeRead7350"));Assert.DoesNotContain(r.Known.Events,e=>e.Stage=="PostSeeCAS7358Expected1Desired2");
        Assert.Equal(rng.State.Sha256,r.Candidate!.Value.ExpectedRandomSha256);Assert.Equal(rng.Lease,r.Known.Random.Lease);
    }
    [Theory]
    [InlineData(0)] [InlineData(1)]
    public void UnknownReachedRandomAndLostSharedLeaseNeverProduceACompleteTransaction(int mode)
    {
        var i=ManagedMembershipFixture.Make();O.SharedRandom? random=mode==0?null:ManagedMembershipFixture.Random();
        if(random is not null)random=random with {Lease=random.Lease with {Exclusive=false}};
        i=i with {Scene=i.Scene with {Random=random,Globals=i.Scene.Globals with {PostSee142769c24=2}}};var r=M.Calculate(i);
        Assert.Null(r.Candidate);Assert.Equal(mode==0?O.Status.IncompleteReachedChild:O.Status.InvalidInput,r.Status);
    }
    [Fact]
    public void CasSuccessRetainsChangedPrefixButNoCompleteFinalTransaction()
    {
        var i=ManagedMembershipFixture.UserImage(ManagedMembershipFixture.Make(),0,u=>O.Put32(u,0x7358,1));var r=M.Calculate(i);
        Assert.Equal(O.Status.IncompleteReachedChild,r.Status);Assert.Null(r.Candidate);Assert.NotNull(r.Known);
        Assert.Equal(2u,O.U32(r.Known!.Users[0].Image,0x7358));Assert.Single(r.Known.ProvisionalIntents,p=>p.Meaning.Kind==V.Kind.Spawn);
    }
    [Theory]
    [InlineData(0)] [InlineData(1)]
    public void MatrixCallsPreserveAsymmetricStrideAndBothReverseReadsTwice(int asymmetric)
    {
        var i=ManagedMembershipFixture.Make();i=ManagedMembershipFixture.UserImage(i,0,u=>O.Put32(u,0x1698,2));
        i=ManagedMembershipFixture.NpcImage(i,n=>O.Put32(n,0x1698,asymmetric==0?2u:1u));
        var matrix=new O.Matrix(0xb00000,asymmetric==0?3:2,3,asymmetric==0?[0,1,2,3,4,5,6,0,2]:[0,1,2,3,0,5],"matrix-v1",i.Evidence);
        i=i with {Scene=i.Scene with {Relations=matrix}};var r=Complete(i);var spawn=Assert.IsType<V.Spawn>(Assert.Single(r.Known!.ProvisionalIntents,p=>p.Meaning.Kind==V.Kind.Spawn).Meaning);
        Assert.Equal(asymmetric==0?0x24ul:8ul,Assert.Single(spawn.Arguments,a=>a.NativeIndex==6).Scalar);
        Assert.Equal(asymmetric==0?6:4,r.Known.Events.Count(e=>e.Stage=="MatrixRead"));
        Assert.Equal(asymmetric==0?0:2,r.Known.Events.Count(e=>e.Stage=="MatrixBoundsDefaultMinus1"));
    }
    [Theory]
    [InlineData(0)] [InlineData(1)]
    public void MatrixIndexWrapAndUnboundLiteralTableValuesRemainTypedFrontiers(int wrap)
    {
        var i=ManagedMembershipFixture.Make();uint tribe=wrap==1?49999u:2u;
        i=ManagedMembershipFixture.UserImage(i,0,u=>O.Put32(u,0x1698,tribe));i=ManagedMembershipFixture.NpcImage(i,n=>O.Put32(n,0x1698,tribe));
        var matrix=wrap==1?new O.Matrix(0xb00000,50000,50000,[0],"matrix-v1",i.Evidence):new O.Matrix(0xb00000,3,3,[0,0,0,0,0,0,0,0,15],"matrix-v1",i.Evidence);
        var r=M.Calculate(i with {Scene=i.Scene with {Relations=matrix}});Assert.Equal(O.Status.IncompleteReachedChild,r.Status);Assert.Null(r.Candidate);
        Assert.Equal(wrap==1?"matrix":"table",r.Failure!.Code);
    }
    [Theory]
    [InlineData(0u,true)] [InlineData(27u,true)] [InlineData(28u,false)] [InlineData(uint.MaxValue,false)]
    public void PresentEffectManagerUsesUnsignedPayloadSize(uint bytes,bool complete)
    {
        var i=ManagedMembershipFixture.Make();const ulong manager=0xb00000;
        byte[] raw=new byte[0x4470];ManagedMembershipFixture.W32(raw,0x446c,bytes);
        i=ManagedMembershipFixture.NpcImage(i,n=>O.Replace(n,0x158,BitConverter.GetBytes(manager)));
        i=i with {Scene=i.Scene with {Pointees=[new(manager,"effect-manager","effect-v1",[..raw],i.Evidence)]}};
        var r=M.Calculate(i);Assert.Equal(complete?O.Status.CompleteManagedTransaction:O.Status.IncompleteReachedChild,r.Status);
        Assert.Equal(complete,r.Candidate is not null);Assert.Equal(bytes,Assert.Single(r.Known!.Events,e=>e.Stage=="EffectPayloadUnsignedSize").After);
    }
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void AlternateSelectionAndLateHouseWorkCannotBeReturnedAsSuccess(int mode)
    {
        var i=ManagedMembershipFixture.Make();i=mode switch
        {0=>ManagedMembershipFixture.NpcImage(i,n=>O.Replace(n,0x1c34,[0])),
         1=>ManagedMembershipFixture.NpcImage(i,n=>O.Put32(n,0x728,1)),
         _=>ManagedMembershipFixture.NpcImage(i,n=>O.Put32(n,0x1bb0,1))};
        if(mode==2)i=i with {Npc=i.Npc with {Template=O.Put32(i.Npc.Template,0x43c,0x200)}};
        var r=M.Calculate(i);Assert.Equal(O.Status.IncompleteReachedChild,r.Status);Assert.Null(r.Candidate);
        Assert.Equal(mode==2?1:0,r.Known!.ProvisionalIntents.Count(p=>p.Meaning.Kind==V.Kind.Spawn));
    }
    [Fact]
    public void UnreachedHpAndPendingReadsAreNotInventedAfterFirstAliveByteFails()
    {
        var i=ManagedMembershipFixture.NpcImage(ManagedMembershipFixture.Make(),n=>O.Replace(n,0x1c34,[0]));var r=M.Calculate(i);
        Assert.Equal(O.Status.IncompleteReachedChild,r.Status);Assert.Contains(r.Known!.Events,e=>e.Stage=="Slot38Read1c34");
        Assert.DoesNotContain(r.Known.Events,e=>e.Stage=="Slot38Read778" || e.Stage=="Slot38Read728");Assert.Null(r.Candidate);
    }
    [Fact]
    public void MissingLateSpawnClockPreservesCalculatedMembershipButWithholdsCandidate()
    {
        var i=ManagedMembershipFixture.Make();var r=M.Calculate(i with {Clocks=i.Clocks.Where(c=>c.RecipientId==0).ToImmutableArray()});
        Assert.Equal(O.Status.IncompleteReachedChild,r.Status);Assert.Null(r.Candidate);Assert.Equal("clock",r.Failure!.Code);
        Assert.Equal(ManagedMembershipFixture.V(32),r.Known!.Motion.State.Endpoint120);Assert.Equal(1,Assert.Single(r.Known.Membership,m=>m.Actor.ObjectId==7).Cell);
        Assert.Equal(2,r.Known.ProvisionalIntents.Length);Assert.DoesNotContain(r.Known.ProvisionalIntents,p=>p.Meaning.Kind==V.Kind.Spawn);
    }
}
