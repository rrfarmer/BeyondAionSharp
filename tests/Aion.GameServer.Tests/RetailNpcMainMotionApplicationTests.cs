using System.Buffers.Binary;
using System.Text.Json;
using Aion.GameServer.GeoEngine.RetailPath;

namespace Aion.GameServer.Tests;

public sealed class RetailNpcMainMotionApplicationTests
{
    public static IEnumerable<object[]> MotionCases()=>MainApplicationFixture.Cases("vectors");
    [Theory,MemberData(nameof(MotionCases))]
    public void IndependentBinary32SourceVectorsAndPreWorldPacketSnapshots(string name,string json)
    {
        using var d=JsonDocument.Parse(json);var input=d.RootElement.GetProperty("input");var expected=d.RootElement.GetProperty("expected_selected_writes");
        var actor=MainApplicationFixture.Actor() with { Heading12c=input.GetProperty("old_heading").GetInt32(),TurnRate7c8=Convert.ToUInt32(input.GetProperty("turn_rate_bits").GetString(),16) };
        byte[] frame=MainApplicationFixture.Frame(MainApplicationFixture.ReadVector(input.GetProperty("requested_endpoint_bits")),
            MainApplicationFixture.ReadVector(input.GetProperty("requested_goal_bits")),input.GetProperty("requested_duration").GetUInt32(),
            input.GetProperty("flags").GetByte(),input.GetProperty("requested_heading").GetByte());
        // This corpus record requests(18,12,5). Bind its post-move material sample
        // to actual reader bytes: (9,6), word160 /32 =5m; other samples stay0.
        var world=name=="three_axis_velocity"
            ?MainApplicationFixture.World(MainApplicationFixture.Material(sampleHeight:(x,y)=>x==9&&y==6?(ushort)160:(ushort)0))
            :MainApplicationFixture.World();
        if(name=="three_axis_velocity")Assert.Equal((ushort)160,BinaryPrimitives.ReadUInt16LittleEndian(world.Material.HeightWords.AsSpan(2*(6*33+9),2)));
        var result=MainApplicationFixture.Run(actor:actor,world:world,frame:frame);
        Assert.True(result.Calculated,name+":"+result.Detail);var final=Assert.IsType<RetailNpcMainActorSnapshot>(result.FinalActor);
        Assert.Equal(MainApplicationFixture.ReadVector(expected.GetProperty("velocity186c_bits")),final.Motion.Velocity186c);
        Assert.Equal(MainApplicationFixture.ReadVector(expected.GetProperty("prior_endpoint1860_bits")),final.Motion.PriorEndpoint1860);
        Assert.Equal(MainApplicationFixture.ReadVector(expected.GetProperty("goal19fc_bits")),final.Motion.ContinuingGoal19fc);
        Assert.Equal(MainApplicationFixture.ReadVector(expected.GetProperty("endpoint120_bits")),final.Motion.Endpoint120);
        Assert.Equal(expected.GetProperty("start185c").GetUInt32(),final.Motion.Start185c);Assert.Equal(expected.GetProperty("due1858").GetUInt32(),final.Motion.Due1858);
        Assert.Equal(expected.GetProperty("heading12c").GetInt32(),final.Heading12c);Assert.Equal(expected.GetProperty("state_flags1c35").GetByte(),final.Flags1c35);
        Assert.Equal(Convert.FromHexString(d.RootElement.GetProperty("client_packet_pre_world_hex").GetString()!),result.ClientPacket.ToArray());
        Assert.Equal(8U,result.FinalGlobals!.TlsDepth60);Assert.Equal((byte)1,final.Material1426);
        Assert.Equal(actor.Counter19f8,final.Counter19f8);Assert.Equal(actor.Motion.CurrentSpeed7c0,final.Motion.CurrentSpeed7c0);
        Assert.Equal(actor.LastMaterial1427,final.LastMaterial1427);Assert.Equal(actor.MaterialTimers,final.MaterialTimers);
        Assert.Equal(4,result.EvaluatedSkips.Count(s=>s.StartsWith("material[",StringComparison.Ordinal)));
        Assert.False(result.CanCommitToHost);Assert.False(result.NativeTargetExecuted);Assert.False(result.CurrentNativeSceneObserved);
    }

    [Fact]
    public void ReaderBytesToProducerToApplicationToEffectivePoseHasEveryLogicalWrite()
    {
        var actor=MainApplicationFixture.Actor();var world=MainApplicationFixture.World();var frame=MainApplicationFixture.Frame();
        Assert.Equal(44,frame.Length);Assert.Equal(RetailCanonicalMoveStatus.CanonicalProducerLayout,RetailNpcInterserverMoveCodec.DecodeCanonical(frame).Status);
        var result=MainApplicationFixture.Run(actor,world,frame);Assert.Equal(RetailMainApplyStatus.CalculatedFiniteApplication,result.Status);
        var final=Assert.IsType<RetailNpcMainActorSnapshot>(result.FinalActor);
        Assert.Same(world,result.FinalWorld);Assert.Equal(0x1412712c8UL,final.RefSource28);Assert.Equal(0xebdU,final.RefLine30);Assert.Equal(1,final.RefCount8);
        Assert.Equal(new ulong[]{9,8},result.CalculatedPrefixWrites.Where(w=>w.Field=="TLS+60").Select(w=>w.After).ToArray());
        string[] required=["actor+186c","actor+1870","actor+1874","actor+1858","actor+1860","actor+1864","actor+1868","actor+185c",
            "actor+19fc[0]","actor+19fc[1]","actor+19fc[2]","actor+1c35","actor+8","actor+28","actor+30","actor+118/world",
            "actor+120[0]","actor+120[1]","actor+120[2]","actor+12c","actor+1426","TLS+60"];
        Assert.Equal(required.Order(),result.CalculatedPrefixWrites.Select(w=>w.Field).Distinct().Order());
        Assert.True(Index(result,"actor+1858")<Index(result,"actor+120[0]"));Assert.True(Index(result,"actor+1426")>Index(result,"actor+120[0]"));
        var ctx=new RetailNpcMainMotionContext(RetailNpcMainMotion.MainBinarySha256,RetailNpcMainMotion.GetterEvidenceSha256,RetailNpcMainMotion.NpcVtable,
            "life1","final-snapshot","getter-op",final.SnapshotSha256,true,true,false,false,true,true);
        var pose=RetailNpcMainMotion.Calculate(final.Motion,new(3100,0x140b1802e,"life1","final-snapshot","getter-op",MainApplicationFixture.Hash,true),ctx);
        Assert.True(pose.Calculated);Assert.Equal(MainApplicationFixture.V(12,4,1),pose.Location);Assert.Equal(MainApplicationFixture.V(18,4,1),final.Motion.Endpoint120);
        Assert.Equal(MainApplicationFixture.Bits(6),BinaryPrimitives.ReadUInt32LittleEndian(result.ClientPacket.AsSpan(11..)));
    }
    private static int Index(RetailMainApplyResult r,string field)=>Array.FindIndex(r.CalculatedPrefixWrites.ToArray(),w=>w.Field==field);

    [Fact]
    public void ComputedTrianglePolicySearchFollowNpcEmissionComposesWithFiniteMainSameCellApply()
    {
        var fixture=MovementOwnerFixture.Create();
        // 31->30 remains in NPC32m cell0: no Remove/DelayFree timestamp read.
        var npcInput=fixture.State with {ClockReads=MovementOwnerFixture.Clocks("FindBegin","FindSuccessEnd","MoveBegin","MoveEnd","AddTimer")};
        var npc=RetailNpcMovementOwnerPlan.Calculate(npcInput,fixture.Scene,fixture.Goal(30,1),1000,0,fixture.Limits);
        Assert.True(npc.WholeEffectListApplicable,npc.Failure?.ToString());
        var emission=Assert.Single(npc.Effects,e=>e.Kind==RetailNpcMovementEffectKind.NativeInterserverMove);
        Assert.Contains(npc.Children,c=>c.Program=="FindPath computed geometry/policy/search");
        var position=emission.Position!.Value;var goal=emission.Desired!.Value;
        byte[] frame=RetailNpcInterserverMoveCodec.EncodeCanonical(new(fixture.State.Actor.ObjectId,MainApplicationFixture.V(position.X,position.Y,position.Z),
            (byte)emission.Heading!.Value,(byte)emission.Flags!.Value,(byte)fixture.State.Actor.PacketState178,
            MainApplicationFixture.V(goal.X,goal.Y,goal.Z),emission.Duration!.Value)).Bytes.ToArray();
        var main=MainApplicationFixture.Actor();main=main with { ObjectId=fixture.State.Actor.ObjectId,Counter19f8=fixture.State.Actor.PacketState178,
            TurnRate7c8=0,Motion=new(MainApplicationFixture.V(31,1,0),MainApplicationFixture.V(31,1,0),default,
                MainApplicationFixture.V(31,1,0),0,1000,MainApplicationFixture.Bits(3)) };
        var ticks=new[]{MainApplicationFixture.Tick(RetailNpcMainMotionApplication.GetterTickSite,1000),
            MainApplicationFixture.Tick(RetailNpcMainMotionApplication.DueTickSite,1100),MainApplicationFixture.Tick(RetailNpcMainMotionApplication.StartTickSite,1103)};
        var result=MainApplicationFixture.Run(actor:main,frame:frame,ticks:ticks);
        Assert.True(result.Calculated,result.Detail);Assert.Equal(MainApplicationFixture.V(30,1,0),result.FinalActor!.Motion.Endpoint120);
        Assert.Equal(unchecked(1100U+emission.Duration.Value),result.FinalActor.Motion.Due1858);Assert.Equal(1103U,result.FinalActor.Motion.Start185c);
        Assert.Equal(MainApplicationFixture.Bits(31),BinaryPrimitives.ReadUInt32LittleEndian(result.ClientPacket.AsSpan(11..)));
        var final=result.FinalActor;var context=new RetailNpcMainMotionContext(RetailNpcMainMotion.MainBinarySha256,RetailNpcMainMotion.GetterEvidenceSha256,
            RetailNpcMainMotion.NpcVtable,"life1","after-main","pose-query",final.SnapshotSha256,true,true,false,false,true,true);
        var pose=RetailNpcMainMotion.Calculate(final.Motion,new(final.Motion.Due1858,0x140b1802e,"life1","after-main","pose-query",MainApplicationFixture.Hash,true),context);
        Assert.True(pose.Calculated);Assert.Equal(final.Motion.Endpoint120,pose.Location);
        Assert.False(result.ActivationReady);Assert.False(npc.HostEffectsExecuted);
    }

    [Fact]
    public void ExistingComputedCrossCellNpcEmissionExposesTheMandatoryMainExpansionBoundary()
    {
        var f=MovementOwnerFixture.Create();var npc=RetailNpcMovementOwnerPlan.Calculate(f.State,f.Scene,f.Goal(),1000,0,f.Limits);
        Assert.True(npc.WholeEffectListApplicable,npc.Failure?.ToString());var move=Assert.Single(npc.Effects,e=>e.Kind==RetailNpcMovementEffectKind.NativeInterserverMove);
        var main=MainApplicationFixture.Actor();main=main with { ObjectId=7,Counter19f8=0,Motion=new(MainApplicationFixture.V(31,1,0),MainApplicationFixture.V(31,1,0),default,MainApplicationFixture.V(31,1,0),0,1000,MainApplicationFixture.Bits(3)) };
        var p=move.Position!.Value;var g=move.Desired!.Value;
        var frame=RetailNpcInterserverMoveCodec.EncodeCanonical(new(7,MainApplicationFixture.V(p.X,p.Y,p.Z),(byte)move.Heading!.Value,(byte)move.Flags!.Value,0,MainApplicationFixture.V(g.X,g.Y,g.Z),move.Duration!.Value)).Bytes.ToArray();
        var r=MainApplicationFixture.Run(actor:main,frame:frame);
        Assert.Equal(RetailMainApplyStatus.IncompleteAtChild,r.Status);Assert.Equal(0x140d796c0UL,r.IncompleteChild);Assert.Null(r.FinalActor);Assert.Null(r.FinalWorld);Assert.Empty(r.ClientPacket);
    }

    [Fact]
    public void KnownBoundsRejectionRetainsMotionPrefixAndQueriesTheRetainedEndpoint()
    {
        var result=MainApplicationFixture.Run(frame:MainApplicationFixture.Frame(stop:MainApplicationFixture.V(64,4,1)));
        Assert.Equal(RetailMainApplyStatus.CalculatedFiniteApplication,result.Status);Assert.Equal(RetailMainWorldDisposition.BoundsRejected,result.WorldDisposition);
        Assert.Equal(MainApplicationFixture.V(12,4,1),result.FinalActor!.Motion.Endpoint120);Assert.Equal(MainApplicationFixture.V(29,0,0),result.FinalActor.Motion.Velocity186c);
        Assert.Equal(4100U,result.FinalActor.Motion.Due1858);Assert.Equal(2103U,result.FinalActor.Motion.Start185c);Assert.Equal((byte)1,result.FinalActor.Material1426);
        Assert.DoesNotContain(result.CalculatedPrefixWrites,w=>w.Field is "actor+12c" or "actor+8");Assert.Equal(8U,result.FinalGlobals!.TlsDepth60);
    }

    [Theory,InlineData(0),InlineData(1),InlineData(2)]
    public void SuppressionComesFromOwnedPointerDataAndNeverStopsMotion(int storage)
    {
        var a=MainApplicationFixture.Actor() with { Suppression1a58=storage==0?new(0,null):new(0x7777,storage==1?0U:5U) };
        var result=MainApplicationFixture.Run(actor:a);Assert.True(result.Calculated,result.Detail);
        Assert.Equal(storage==2?0:37,result.ClientPacket.Length);Assert.Equal(MainApplicationFixture.V(18,4,1),result.FinalActor!.Motion.Endpoint120);
        Assert.Equal(8U,result.FinalGlobals!.TlsDepth60);
    }

    public static IEnumerable<object[]> AllFlags()=>Enumerable.Range(0,256).Select(f=>new object[]{f});
    [Theory,MemberData(nameof(AllFlags))]
    public void EveryFlagHasIndependentNativePacketMaskSizeAndGoalRules(int value)
    {
        byte flag=(byte)value;var a=MainApplicationFixture.Actor() with { Flags1c35=0x55 };
        // Nonextended duration0 sets goal=stop. Matching flags55 is avoided through different goal.
        var result=MainApplicationFixture.Run(actor:a,frame:MainApplicationFixture.Frame(flags:flag));Assert.True(result.Calculated,result.Detail);
        if(flag==8){Assert.Empty(result.ClientPacket);return;}
        byte cleaned=(byte)(flag&0xef);bool ext=(cleaned&0xc0)==0xc0;
        Assert.Equal((ext?37:25)+((cleaned&4)!=0?1:0),result.ClientPacket.Length);
        Assert.Equal(cleaned,result.ClientPacket[24]);Assert.Equal((byte)(flag&0xed),result.FinalActor!.Flags1c35);
        Assert.Equal(0x1d9,BinaryPrimitives.ReadUInt16LittleEndian(result.ClientPacket.AsSpan(2..)));
        Assert.Equal((ushort)result.ClientPacket.Length,BinaryPrimitives.ReadUInt16LittleEndian(result.ClientPacket.AsSpan()));
        if(ext)
        {
            var goal=(cleaned&0x20)!=0?MainApplicationFixture.V(30,4,1):default;
            Assert.Equal(goal.X,BinaryPrimitives.ReadUInt32LittleEndian(result.ClientPacket.AsSpan(25..)));
            Assert.Equal(goal.Y,BinaryPrimitives.ReadUInt32LittleEndian(result.ClientPacket.AsSpan(29..)));
            Assert.Equal(goal.Z,BinaryPrimitives.ReadUInt32LittleEndian(result.ClientPacket.AsSpan(33..)));
        }
        if((cleaned&4)!=0)Assert.Equal((byte)0,result.ClientPacket[^1]);
    }

    [Theory]
    [InlineData(0x00000000U,true)] [InlineData(0x38d1b716U,true)] [InlineData(0x38d1b717U,false)]
    [InlineData(0x38d1b718U,false)] [InlineData(0xb8d1b716U,true)] [InlineData(0xb8d1b717U,false)] [InlineData(0xb8d1b718U,false)]
    public void StopHelperStrictEpsilonIsComputedFromActualEndpointAndPriorGoal(uint x,bool duplicate)
    {
        var a=MainApplicationFixture.Actor() with { Motion=MainApplicationFixture.Actor().Motion with { ContinuingGoal19fc=default } };
        var result=MainApplicationFixture.Run(actor:a,frame:MainApplicationFixture.Frame(stop:new(x,0,0),flags:0));
        Assert.True(result.Calculated,result.Detail);Assert.Equal(duplicate,result.ClientPacket.IsEmpty);
    }

    [Theory,InlineData(0),InlineData(1),InlineData(2)]
    public void AnyAxisAtEpsilonPreventsStopDuplicateSuppression(int axis)
    {
        uint[] v=[0,0,0];v[axis]=0x38d1b717;
        var a=MainApplicationFixture.Actor() with { Motion=MainApplicationFixture.Actor().Motion with { ContinuingGoal19fc=default } };
        var result=MainApplicationFixture.Run(actor:a,frame:MainApplicationFixture.Frame(stop:new(v[0],v[1],v[2]),flags:0));
        Assert.True(result.Calculated,result.Detail);Assert.NotEmpty(result.ClientPacket);
    }

    [Fact]
    public void PreviousFlag8ForcesStopPacketWithoutConsumingInvalidPriorGoal()
    {
        var a=MainApplicationFixture.Actor();a=a with { Flags1c35=8,Motion=a.Motion with { ContinuingGoal19fc=new(0x7fc00001,0x7f800000,0) } };
        var result=MainApplicationFixture.Run(actor:a,frame:MainApplicationFixture.Frame(flags:0));
        Assert.True(result.Calculated,result.Detail);Assert.Equal(25,result.ClientPacket.Length);
    }

    [Theory,InlineData(0),InlineData(1),InlineData(2),InlineData(3),InlineData(4)]
    public void ReachedUnknownChildDoesNotExposeFinalStateOrAnEmittablePacket(int child)
    {
        var a=MainApplicationFixture.Actor();var w=MainApplicationFixture.World();byte[] frame=MainApplicationFixture.Frame();ulong address=0;
        switch(child)
        {
            case 0:a=a with { TraceOwner1ba0=1 };address=0x140363af0;break;
            case 1:frame=MainApplicationFixture.Frame(stop:MainApplicationFixture.V(32,4,1));address=0x140d796c0;break;
            case 2:w=MainApplicationFixture.World(MainApplicationFixture.Material(height:3));address=0x140d76e00;break;
            case 3:w=MainApplicationFixture.World(MainApplicationFixture.Material(records:MainApplicationFixture.ActiveRecords()));address=0x14037c1fd;break;
            case 4:w=MainApplicationFixture.World(policy4:9999);address=0x140682020;break;
        }
        var result=MainApplicationFixture.Run(a,w,frame);Assert.Equal(RetailMainApplyStatus.IncompleteAtChild,result.Status);Assert.Equal(address,result.IncompleteChild);
        Assert.Null(result.FinalActor);Assert.Null(result.FinalWorld);Assert.Null(result.FinalGlobals);Assert.Empty(result.ClientPacket);Assert.False(result.CanCommitToHost);
    }

    [Theory,InlineData(0),InlineData(1),InlineData(2),InlineData(3),InlineData(4),InlineData(5),InlineData(6),InlineData(7)]
    public void StageOwnershipAndProvenanceCannotBeLaunderedThroughDeclarations(int invalid)
    {
        var a=MainApplicationFixture.Actor();var w=MainApplicationFixture.World();var g=MainApplicationFixture.Globals();var f=MainApplicationFixture.Frame();
        var c=MainApplicationFixture.Context(a,w,g,f);var ticks=MainApplicationFixture.Ticks();
        switch(invalid)
        {
            case 0:c=c with { Origin=(RetailMainApplicationOrigin)99 };break;
            case 1:c=c with { Origin=RetailMainApplicationOrigin.DeclaredHistoricalObservation };break;
            case 2:c=c with { CanonicalRecordSha256=MainApplicationFixture.Hash };break;
            case 3:c=c with { ActorSnapshotSha256=MainApplicationFixture.Hash };break;
            case 4:c=c with { WorldSnapshotSha256=MainApplicationFixture.Hash };break;
            case 5:c=c with { StopEvidenceSha256=MainApplicationFixture.Hash };break;
            case 6:ticks[1]=ticks[1] with { OperationRevision="unrelated" };break;
            case 7:ticks[2]=ticks[2] with { CallSite=ticks[1].CallSite };break;
        }
        var r=MainApplicationFixture.Run(a,w,f,g,ticks,c);Assert.False(r.Calculated);Assert.Null(r.FinalActor);Assert.Empty(r.ClientPacket);
    }

    [Fact]
    public void SequenceMismatchHasOneReleaseAndNoClockOrWorldApply()
    {
        var r=MainApplicationFixture.Run(frame:MainApplicationFixture.Frame(sequence:8),ticks:[]);
        Assert.Equal(RetailMainApplyStatus.CalculatedNativeEarlyReturn,r.Status);Assert.Equal(9U,r.FinalGlobals!.TlsDepth60);
        Assert.Equal("TLS+60",Assert.Single(r.CalculatedPrefixWrites).Field);Assert.Empty(r.ClientPacket);
        var unused=MainApplicationFixture.Run(frame:MainApplicationFixture.Frame(sequence:8));Assert.Equal(RetailMainApplyStatus.InvalidOwnership,unused.Status);Assert.Null(unused.FinalActor);
    }

    [Fact]
    public void NullWorldEarlyReturnDoesNotInventLockClockOrSceneAccess()
    {
        var a=MainApplicationFixture.Actor() with { WorldIdentity=null,LockBypass700=0 };var g=MainApplicationFixture.Globals();var f=MainApplicationFixture.Frame();
        var c=MainApplicationFixture.Context(a,null,g,f) with { NearestEven=false };
        var r=RetailNpcMainMotionApplication.Calculate(f,a,null,g,[],c);
        Assert.Equal(RetailMainApplyStatus.CalculatedNativeEarlyReturn,r.Status);Assert.Equal(a,r.FinalActor);Assert.Equal(g,r.FinalGlobals);Assert.Empty(r.CalculatedPrefixWrites);
    }

    [Fact]
    public void WrappedTimestampAndDisabledDebugTlsWritesArePreservedLiterally()
    {
        var g=MainApplicationFixture.Globals() with { TlsDepth60=0 };
        var ticks=MainApplicationFixture.Ticks();ticks[1]=ticks[1] with { Value=uint.MaxValue-99 };ticks[2]=ticks[2] with { Value=uint.MaxValue-95 };
        var r=MainApplicationFixture.Run(globals:g,ticks:ticks);Assert.True(r.Calculated,r.Detail);
        Assert.Equal(1900U,r.FinalActor!.Motion.Due1858);Assert.Equal(uint.MaxValue-95,r.FinalActor.Motion.Start185c);Assert.Equal(uint.MaxValue-1,r.FinalGlobals!.TlsDepth60);
        var a=r.FinalActor;var c=new RetailNpcMainMotionContext(RetailNpcMainMotion.MainBinarySha256,RetailNpcMainMotion.GetterEvidenceSha256,RetailNpcMainMotion.NpcVtable,
            "life1","wrapped","getter",a.SnapshotSha256,true,true,false,false,true,true);
        var pose=RetailNpcMainMotion.Calculate(a.Motion,new(2000,0x140b1802e,"life1","wrapped","getter",MainApplicationFixture.Hash,true),c);
        Assert.Equal("before-or-at-start",pose.Branch);Assert.Equal(a.Motion.PriorEndpoint1860,pose.Location);
    }

    [Fact]
    public void PastDueStopReplacesTheOutgoingPoseWhileRetainingPriorRawEndpoint()
    {
        var a=MainApplicationFixture.Actor();a=a with { Motion=a.Motion with { Endpoint120=MainApplicationFixture.V(12,4,1),ContinuingGoal19fc=MainApplicationFixture.V(12,4,1),Start185c=0,Due1858=1000 } };
        var r=MainApplicationFixture.Run(actor:a,frame:MainApplicationFixture.Frame(stop:MainApplicationFixture.V(18,4,1),duration:0));
        Assert.True(r.Calculated,r.Detail);Assert.Equal(MainApplicationFixture.Bits(18),BinaryPrimitives.ReadUInt32LittleEndian(r.ClientPacket.AsSpan(11..)));
        Assert.Equal(MainApplicationFixture.V(12,4,1),r.FinalActor!.Motion.PriorEndpoint1860);Assert.Equal(MainApplicationFixture.V(18,4,1),r.FinalActor.Motion.ContinuingGoal19fc);
        Assert.Equal(2100U,r.FinalActor.Motion.Due1858);Assert.Equal(2103U,r.FinalActor.Motion.Start185c);
    }

    [Theory,InlineData(0),InlineData(1),InlineData(2),InlineData(3)]
    public void CurrentClassAndHostGuardFactsMustActuallyBeAdmitted(int invalid)
    {
        var a=MainApplicationFixture.Actor();var g=MainApplicationFixture.Globals();
        if(invalid==0)a=a with { Vtable=0x1234 };
        if(invalid==1)a=a with { LockBypass700=0 };
        if(invalid==2)g=g with { SerializationDebug142767930=1 };
        if(invalid==3)g=g with { Telemetry14275eb74=1 };
        var r=MainApplicationFixture.Run(actor:a,globals:g);Assert.False(r.Calculated);Assert.Null(r.FinalActor);Assert.Empty(r.ClientPacket);
    }

    [Fact]
    public void HighCounterBytesDoNotSubstituteForOrInvalidateActorLifetime()
    {
        var a=MainApplicationFixture.Actor() with { Counter19f8=0x10007 };var r=MainApplicationFixture.Run(actor:a);
        Assert.True(r.Calculated,r.Detail);Assert.Equal(0x10007U,r.FinalActor!.Counter19f8);
    }

    [Theory,InlineData(120),InlineData(128),InlineData(255)]
    public void RejectedHeadingDoesNotEnterMoveNpcLockBookkeeping(int heading)
    {
        var r=MainApplicationFixture.Run(frame:MainApplicationFixture.Frame(heading:(byte)heading),ticks:[]);
        Assert.Equal(RetailMainApplyStatus.CalculatedNativeEarlyReturn,r.Status);Assert.Equal(10U,r.FinalGlobals!.TlsDepth60);Assert.Empty(r.CalculatedPrefixWrites);
    }

    [Fact]
    public void ResetCopiesRawEndpointWithTwoOriginalTicksAndDoesNotSampleAnotherPose()
    {
        var a=MainApplicationFixture.Actor();var g=MainApplicationFixture.Globals();var c=MainApplicationFixture.Context(a,null,g,[]);
        var r=RetailNpcMainMotionApplication.Reset(a,g,[MainApplicationFixture.Tick(RetailNpcMainMotionApplication.ResetDueTickSite,55),
            MainApplicationFixture.Tick(RetailNpcMainMotionApplication.ResetStartTickSite,57)],c with { NearestEven=false,Sse2=false });
        Assert.True(r.Calculated,r.Detail);Assert.Equal(default(RetailMainVectorWords),r.FinalActor!.Motion.Velocity186c);Assert.Equal(a.Motion.Endpoint120,r.FinalActor.Motion.PriorEndpoint1860);
        Assert.Equal(a.Motion.Endpoint120,r.FinalActor.Motion.ContinuingGoal19fc);Assert.Equal(55U,r.FinalActor.Motion.Due1858);Assert.Equal(57U,r.FinalActor.Motion.Start185c);
        Assert.Equal((byte)0,r.FinalActor.Flags1c35);Assert.Equal(9U,r.FinalGlobals!.TlsDepth60);Assert.Null(r.FinalWorld);Assert.Empty(r.ClientPacket);
    }

    [Fact]
    public void ResetRetainsUnconsumedExceptionalRawEndpointWords()
    {
        var a=MainApplicationFixture.Actor();a=a with { Motion=a.Motion with { Endpoint120=new(0x7fc00001,0x80000000,0x00000001) } };
        var g=MainApplicationFixture.Globals();var c=MainApplicationFixture.Context(a,null,g,[]);
        var r=RetailNpcMainMotionApplication.Reset(a,g,[MainApplicationFixture.Tick(RetailNpcMainMotionApplication.ResetDueTickSite,0xffffffff),MainApplicationFixture.Tick(RetailNpcMainMotionApplication.ResetStartTickSite,0)],c);
        Assert.True(r.Calculated,r.Detail);Assert.Equal(a.Motion.Endpoint120,r.FinalActor!.Motion.ContinuingGoal19fc);Assert.Equal(a.Motion.Endpoint120,r.FinalActor.Motion.PriorEndpoint1860);
    }

    [Theory,InlineData(0),InlineData(1),InlineData(2)]
    public void ResetRejectsDuplicateMissingAndStaleClockReceipts(int invalid)
    {
        var a=MainApplicationFixture.Actor();var g=MainApplicationFixture.Globals();var c=MainApplicationFixture.Context(a,null,g,[]);
        var first=MainApplicationFixture.Tick(RetailNpcMainMotionApplication.ResetDueTickSite,1);var second=MainApplicationFixture.Tick(RetailNpcMainMotionApplication.ResetStartTickSite,2);
        RetailMainTickReceipt[] ticks=invalid==0?[first]:invalid==1?[first,first]:[first,second with { SnapshotRevision="stale" }];
        var r=RetailNpcMainMotionApplication.Reset(a,g,ticks,c);Assert.False(r.Calculated);Assert.Null(r.FinalActor);
    }

    [Theory,InlineData(0),InlineData(1),InlineData(2),InlineData(3)]
    public void FloatingDomainFailureAndMissingClockHaveNoCommittableResult(int invalid)
    {
        var a=MainApplicationFixture.Actor();var w=MainApplicationFixture.World();var g=MainApplicationFixture.Globals();var f=MainApplicationFixture.Frame();var c=MainApplicationFixture.Context(a,w,g,f);
        var ticks=MainApplicationFixture.Ticks();
        if(invalid==0)c=c with { FlushToZero=true };
        if(invalid==1)c=c with { NearestEven=false };
        if(invalid==2){f=MainApplicationFixture.Frame(stop:new(0x7f800000,MainApplicationFixture.Bits(4),MainApplicationFixture.Bits(1)));c=MainApplicationFixture.Context(a,w,g,f);}
        if(invalid==3)ticks=ticks[..2];
        var r=MainApplicationFixture.Run(a,w,f,g,ticks,c);Assert.False(r.Calculated);Assert.Null(r.FinalActor);Assert.Empty(r.ClientPacket);
    }
}
