using System.Security.Cryptography;
using Aion.GameServer.GeoEngine.RetailPath;

namespace Aion.GameServer.Tests;

public sealed class RetailNpcMovementOwnerPlanTests
{
    [Fact]
    public void ActualReaderBytesThroughComputedGeometrySearchFollowMotionMembershipAckTimerCancelAndQueuedId1()
    {
        var f=MovementOwnerFixture.Create();
        var result=RetailNpcMovementOwnerPlan.Calculate(f.State,f.Scene,f.Goal(),1000,0,f.Limits);
        Assert.True(result.WholeEffectListApplicable,result.Failure?.ToString());
        Assert.Equal(RetailMovementCalculationStatus.CalculatedApplicationChain,result.Status);Assert.Equal(0u,result.NativeRawResult);
        Assert.Equal(new(32,1,0),result.State.Actor.Position);Assert.Equal(new(31,1,0),result.State.Actor.SamplingPosition9d8);
        Assert.Equal(0u,result.State.Actor.SamplingCount9d4);Assert.Equal(1u,result.State.Globals.MoveNpcCalls174c54720);
        Assert.Equal(0x40001062u,BitConverter.SingleToUInt32Bits(result.State.Actor.DistanceBudget59c));
        Assert.Equal(0x40403133u,BitConverter.SingleToUInt32Bits(result.State.Actor.Velocity.X));
        Assert.Equal(2,result.State.Actor.Progress82c);Assert.Equal(2,result.State.Path!.Count);Assert.Equal(1,result.State.Path.Index);
        Assert.Equal(new[]{new RetailSearchPoint(31,1,0),new RetailSearchPoint(32,1,0)},result.State.Path.Points);
        Assert.Equal(12,result.State.Actor.References8);Assert.Equal(1,result.State.Actor.DelayedReferencesC);Assert.Equal(1,result.State.Actor.PendingIo10);
        Assert.Equal(1,result.State.Actor.InWorldAc);Assert.Equal(0,result.State.Actor.WorldHash62);
        Assert.Empty(result.State.World.Cells.Single(c=>c.Index==0).OrderedObjects);
        Assert.Equal(new uint[]{7},result.State.World.Cells.Single(c=>c.Index==1).OrderedObjects);
        Assert.Equal(1u,result.State.DelayedWrappers.WriteIndex);Assert.Equal("old",Assert.Single(result.State.DelayedWrappers.Slots).Value.WrapperIdentity);
        var move=Assert.Single(result.Effects,e=>e.Kind==RetailNpcMovementEffectKind.NativeInterserverMove);
        Assert.Equal(333u,move.Duration);Assert.Equal(0xe0u,move.Flags);Assert.Equal(0u,move.Heading);
        var timer=Assert.Single(result.State.Timers.Entries);Assert.Equal(1333u,timer.Due);Assert.Equal(1,timer.TimerId);Assert.Equal(7u,timer.Generation64);
        Assert.Equal(1333u,result.State.Motion.StartGate0);Assert.Equal(1333u,result.State.Motion.Deadline4);
        Assert.Contains(result.Children,c=>c.Program=="FindPath computed geometry/policy/search");
        // Explicit progress handshake: standalone follow consumes2->0/index1->2 and raw1, not duplicate-waypoint raw2.
        var nextState=result.State with {ClockReads=[]};
        var reached=RetailNpcMoveToGoalPlan.Plan(nextState,f.Scene,f.Goal(),f.Limits);
        // At exact metadata destination native MoveToGoal early raw2 precedes follow. Use a far target with supplied same metadata below.
        Assert.Equal(2u,reached.NativeRawResult);Assert.Equal(2,reached.State.Actor.Progress82c);
        var p=result.State.Path;var o=result.State.Ownership;
        var follow=RetailNpcPathFollowObservation.Create(p.Points,p.Count,p.Index,p.Destination,new(32,1,0),new(32,1,0),2,0,0,
            new(0x14007dfa0,1,0,2,2,true,true,MovementOwnerFixture.Hash),new(o.ActorIdentity,o.RequestRevision,o.ActorLifetime,o.PathRevision,o.WorldIdentity,o.InstanceIdentity,o.SceneRevision,o.AssetRevision),
            new("consume",RetailNpcPathFollowObservation.ComputeSnapshotSha256(p.Points,p.Count,p.Index,p.Destination,20),MovementOwnerFixture.Hash,"independent producer"),true,20);
        var consumed=RetailNpcMoveByPathPlan.Plan(follow,f.Scene.Collision,new(20,20,20,100,f.Limits.Geometry));
        Assert.Equal(1u,consumed.NativeValue);Assert.Equal(2,consumed.FinalPathIndex);Assert.Equal(0,consumed.FinalProgress);
        var cancelled=RetailNpcMotionPlan.Cancel(result.State with {ClockReads=[]},f.Scene,f.Limits);
        Assert.True(cancelled.WholeEffectListApplicable);Assert.Null(cancelled.State.Path);Assert.Equal(0u,cancelled.State.Motion.Deadline4);
        Assert.Equal(0u,cancelled.State.Motion.Pending8);Assert.Equal(0x10u,cancelled.State.Motion.Flags38);Assert.Equal(default,cancelled.State.Actor.Velocity);
        Assert.Equal(timer,Assert.Single(cancelled.State.Timers.Entries)); // Cancel does not remove queuedID1.
        var callback=RetailNpcMovementOwnerPlan.CalculateQueuedTimer(cancelled.State with {ClockReads=MovementOwnerFixture.CallbackClocks()},f.Scene,timer,f.Limits);
        Assert.True(callback.WholeEffectListApplicable,callback.Failure?.ToString());Assert.Equal(100,callback.State.Actor.SignedHp14c);
        Assert.Equal(0,callback.State.Actor.AiExchange740);Assert.Equal(timer,Assert.Single(callback.State.Timers.Entries));
        Assert.Equal(12,callback.State.Actor.References8);Assert.Equal(1,callback.State.Actor.PendingIo10);
        Assert.Single(callback.Effects,e=>e.Kind==RetailNpcMovementEffectKind.TimerDispatchCompletion);
        Assert.False(callback.RuntimeVerified);Assert.False(callback.ActivationReady);Assert.False(callback.HostEffectsExecuted);
        Assert.All(result.Writes,w=>Assert.NotEqual(0UL,w.NativeAddress));
        int send=result.Effects.Single(e=>e.Kind==RetailNpcMovementEffectKind.NativeInterserverMove).Sequence;
        int pos=result.Writes.Single(w=>w.Target=="actor+90/94/98").Sequence;
        int ack=result.Writes.Single(w=>w.Target=="actor+82c.compareExchange(1,2)").Sequence;
        int registration=result.Effects.Single(e=>e.Kind==RetailNpcMovementEffectKind.TimerRegistration).Sequence;
        Assert.True(send<pos&&pos<ack&&ack<registration);
        Assert.Equal(new(31,1,0),f.State.Actor.Position);Assert.Empty(f.State.Timers.Entries); // Initial graph remains owned/unmodified.
    }

    [Theory]
    [InlineData("script")] [InlineData("observer")] [InlineData("coverage")] [InlineData("defer")]
    public void UnknownActiveChildrenCannotBeCompletedByAnArbitraryPoststate(string active)
    {
        var f=MovementOwnerFixture.Create();var input=f.State;
        if(active=="script")input=input with {Actor=input.Actor with {ScriptSee8d8=1},World=input.World with {Cells=input.World.Cells.Select(c=>c.Index==0?c with {ScriptSeeCount=1}:c).ToArray()}};
        if(active=="observer")input=input with {World=input.World with {Cells=input.World.Cells.Select(c=>c.Index==3?c with {OrderedUsers=new uint[]{99}}:c).ToArray()}};
        if(active=="coverage")input=input with {World=input.World with {Cells=input.World.Cells.Take(3).ToArray()}};
        var r=RetailNpcMovementOwnerPlan.Calculate(input,f.Scene,f.Goal(),1000,0,f.Limits);
        if(active=="defer")
        {
            var timer=Assert.Single(r.State.Timers.Entries);var cancelled=RetailNpcMotionPlan.Cancel(r.State with {ClockReads=[]},f.Scene,f.Limits);
            r=RetailNpcMovementOwnerPlan.CalculateQueuedTimer(cancelled.State with {Actor=cancelled.State.Actor with {AiDefer724=1000},ClockReads=MovementOwnerFixture.CallbackClocks()},f.Scene,timer,f.Limits);
            Assert.Equal(0u,r.State.Actor.AiDefer724);
        }
        Assert.Equal(RetailMovementCalculationStatus.IncompleteAtChild,r.Status);Assert.False(r.WholeEffectListApplicable);Assert.Null(r.NativeRawResult);
        if(active!="defer"){Assert.Equal(new(32,1,0),r.State.Actor.Position);Assert.Equal(2,r.State.Actor.Progress82c);Assert.Empty(r.State.Timers.Entries);}
    }
    [Theory]
    [InlineData("lifetime")] [InlineData("generation")] [InlineData("absent")]
    public void StaleQueuedEntryDoesNotRunAiOrReleaseReferences(string kind)
    {
        var f=MovementOwnerFixture.Create();var t=new RetailNpcMovementTimerEntry(1333,7,1,0,7,"life1","timer1");
        var state=f.State with {Timers=f.State.Timers with {Entries=new[]{t}},ClockReads=[]};
        var foreign=kind=="lifetime"?t with {ActorLifetime="old"}:kind=="generation"?t with {Generation64=6}:t with {TimerIdentity="missing"};
        var r=RetailNpcMovementOwnerPlan.CalculateQueuedTimer(state,f.Scene,foreign,f.Limits);
        Assert.Equal(RetailMovementCalculationStatus.StaleOwnership,r.Status);Assert.Equal(10,r.State.Actor.References8);Assert.Empty(r.Effects);
    }
    [Theory]
    [InlineData("busy")] [InlineData("dead")] [InlineData("deleted")] [InlineData("timeout")]
    public void ReachedLifecycleOwnersRemainIncomplete(string kind)
    {
        var f=MovementOwnerFixture.Create();var t=new RetailNpcMovementTimerEntry(1333,7,1,0,7,"life1","timer1");
        var state=f.State with {Timers=f.State.Timers with {Entries=new[]{t}},ClockReads=MovementOwnerFixture.CallbackClocks()};
        state=kind switch {"busy"=>state with {Actor=state.Actor with {AiExchange740=1}},"dead"=>state with {Actor=state.Actor with {SignedHp14c=0}},"deleted"=>state with {World=state.World with {Deleting149b8=1}},_=>state with {Actor=state.Actor with {Timeout8d4=1}}};
        var r=RetailNpcMovementOwnerPlan.CalculateQueuedTimer(state,f.Scene,t,f.Limits);
        Assert.Equal(RetailMovementCalculationStatus.IncompleteAtChild,r.Status);Assert.False(r.WholeEffectListApplicable);Assert.Equal(100, f.State.Actor.SignedHp14c);
        Assert.DoesNotContain(r.Effects,e=>e.Kind==RetailNpcMovementEffectKind.TimerDispatchCompletion);
    }
    [Fact]
    public void ExtraClockReadsAndChangedOwnershipCannotClaimCompletion()
    {
        var f=MovementOwnerFixture.Create();var r=RetailNpcMotionPlan.Cancel(f.State,f.Scene,f.Limits);
        Assert.Equal("unused-clock-tail",r.Failure!.Boundary);Assert.False(r.WholeEffectListApplicable);
        r=RetailNpcMotionPlan.Cancel(f.State with {Ownership=f.State.Ownership with {RequestRevision="changed"},ClockReads=[]},f.Scene,f.Limits);
        Assert.Equal(RetailMovementCalculationStatus.StaleOwnership,r.Status);
    }
    [Fact]
    public void UndefinedOriginIsNotACurrentOwnerDeclaration()
    {
        var f=MovementOwnerFixture.Create();var o=f.State.Ownership with {Origin=(RetailMovementInputOrigin)99};
        var scene=new RetailGroundMovementScene(f.Scene.Sources,f.Scene.Collision,o,new(),0,true,f.Scene.FloatingEnvironment);
        var r=RetailNpcMotionPlan.Cancel(f.State with {Ownership=o,ClockReads=[]},scene,f.Limits);
        Assert.Equal(RetailMovementCalculationStatus.InvalidInput,r.Status);Assert.Equal("ownership-origin",r.Failure!.Boundary);Assert.False(r.WholeEffectListApplicable);
    }
}

/// <summary>Actual synthetic reader bytes and complete constructed current graph, never pilot current-world evidence.</summary>
internal sealed record MovementOwnerFixture(RetailGroundMovementScene Scene,RetailNpcMovementSnapshot State,RetailMovementLimits Limits)
{
    internal const string Hash="aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    internal RetailNpcMoveToGoalRequest Goal(float x=32,float y=1)=>new(new(x,y,0),0,0,null,0,false,0,RetailGroundQueueKind.LinkedList,null);
    internal static IReadOnlyList<RetailNpcMovementClockRead> Clocks(params string[] roles)=>roles.Select(role=>new RetailNpcMovementClockRead(role,role switch {
        "FindBegin"=>0x140123491UL,"FindSuccessEnd"=>0x1401236c5UL,"FindFailureEnd"=>0x140123923UL,"MoveBegin"=>0x140237813UL,"DelayTimestamp"=>0x1402ee457UL,"MoveEnd"=>0x140239332UL,_=>0x140299fa5UL},1000,"request1","life1")).ToArray();
    internal static IReadOnlyList<RetailNpcMovementClockRead> CallbackClocks()=>[new("UpdateInfo",0x140101cd2,1333,"request1","life1"),new("AiTick",0x14012685c,1333,"request1","life1")];
    internal static MovementOwnerFixture Create(IReadOnlyList<CollisionFace>? faces=null,byte selector=14,Func<int,int,uint>? edgeMask=null,int size=64)
    {
        IReadOnlyList<CollisionFace> actualFaces=faces??[new(new(0,0,0),new(size,0,0),new(0,size,0),0),new(new(size,0,0),new(size,size,0),new(0,size,0),0)];
        // The segment consumer prunes references by two-metre height bands before intersecting triangles.
        // Keep the synthetic broad XY candidate lists, but cover each face's actual Z span.
        SortedDictionary<short,List<uint>> candidates=[];
        for(int i=0;i<actualFaces.Count;i++)
        {
            var face=actualFaces[i];
            int low=(int)System.MathF.Min(face.A.Z,System.MathF.Min(face.B.Z,face.C.Z))/2*2;
            int high=(int)System.MathF.Max(face.A.Z,System.MathF.Max(face.B.Z,face.C.Z))/2*2;
            for(int z=low;z<=high;z+=2)
            {
                short height=checked((short)z);
                if(!candidates.TryGetValue(height,out var ids))candidates.Add(height,ids=[]);
                ids.Add((uint)i);
            }
        }
        CollisionBand[] bands=candidates.Select(p=>new CollisionBand(p.Key,p.Value.ToArray())).ToArray();
        var raw=CollisionFixture.Read(CollisionFixture.Mesh(actualFaces),CollisionFixture.Collision((x,y)=>bands,width:size,height:size),size,size);
        var prepared=CollisionFixture.Prepared(raw);var terrain=CollisionFixture.Terrain((x,y)=>selector,width:size,height:size);var collision=CollisionFixture.Scene(prepared,terrain);
        using var memory=new MemoryStream();using(var writer=new BinaryWriter(memory,System.Text.Encoding.UTF8,true))
        {
            byte[] header=new byte[128];CollisionFixture.Guid.ToByteArray().CopyTo(header,0);header[16]=5;header[18]=6;writer.Write(header);
            writer.Write(4);writer.Write(0);writer.Write(0);
            for(int sy=0;sy<size/16;sy++)for(int sx=0;sx<size/16;sx++)
            {
                writer.Write((byte)1);writer.Write((byte)0);writer.Write((byte)15);
                for(int d=0;d<4;d++)
                {
                    bool outerEdge=d switch {0=>sx==size/16-1,1=>sy==size/16-1,2=>sx==0,3=>sy==0,_=>false};
                    writer.Write(edgeMask?.Invoke(sy*(size/16)+sx,d)??(outerEdge?uint.MaxValue:0u));
                }
                writer.Write(0);
            }
        }
        var read=RetailPathDataReader.Read(memory.ToArray(),new(size,size));Assert.True(read.Success,read.Error?.ToString());var path=read.Data!;
        var sources=new RetailSearchSourceReceipts(path,new(new("actual-synthetic-path",Convert.ToHexString(SHA256.HashData(path.RawFile.Span)).ToLowerInvariant(),path.RawFile.Length),path.BuildGuid,size,size),terrain,
            new(new("actual-synthetic40",Convert.ToHexString(SHA256.HashData(terrain.RawFile.Span)).ToLowerInvariant(),terrain.RawFile.Length),terrain.BuildGuid,size,size),
            new("pinned-NPC",RetailGroundPathSearch.SupportedNpcBinarySha256,RetailGroundPathSearch.SupportedNpcBinaryBytes),new("actual-synthetic-collision",raw.CollisionSha256,raw.CollisionBytes.Length),new("actor7","current-constructed-empty-field-tree","current-triangle-scene",Hash));
        var ownership=new RetailMovementOwnership("actor7","life1","request1","path1","synthetic-world","instance1","scene1","asset1","membership1","script1","timer1",Hash,RetailMovementInputOrigin.ConstructedSyntheticWorld);
        var scene=new RetailGroundMovementScene(sources,collision,ownership,new(),0,true,RetailNpcNativeHeadingTests.Environment);
        var a=new RetailNpcMovementActor {
            ObjectId=7,Vtable=RetailNpcMovementOwnerPlan.BaseNpcVtable,VtableSha256=RetailNpcMovementOwnerPlan.BaseNpcVtableSha256,EvidenceSha256=Hash,WorldPointerIdentity="synthetic-world",
            Position=new(31,1,0),Velocity=default,HeadingB0=0,PacketState178=0,TemplatePointer1e8="constructed-template",TemplateTurnTimeE0=0,Kind88=2,Kind32c=0,State598=0,State180=0,Pose5a0=1,Effects188=0,Cutscene1f8=0,SignedHp14c=100,
            RadiusBc=.5f,FrontC0=1,SideC4=1,UpperC8=2,Speed5e0=3,Speed5e4=3,Speed5e8=3,SpeedAdjustment5a8=0,SelectedSpeed5a4=3,DistanceBudget59c=2.001f,SpeedStatePointer638="constructed-speed-object",SpeedStateObjectPlus8=1,
            AiPointer200="constructed-AI",AiState=1,Crowd310=0,Crowd314=0,TemplateIdA8=1,Master55c=0,ScriptInterfaceIdentity="constructed-script-interface",AiTemplateIdentity="constructed-AI-template",ScriptSee8d8=0,ScriptMove8dc=0,Template119=0,Template11a=0,Raw750=0,Template324=0,
            DeletingB8=0,Timeout8d4=0,TimeoutDue8d0=0,TimeoutState8cc=0,DebugMode8b0=0,Motion890=0,AiDefer724=10000,AiExchange740=0,Progress82c=0,PathLock290=0,LastFailedGoalA1c=default,SamplingCount9d4=0,SamplingPosition9d8=new(31,1,0),
            References8=10,DelayedReferencesC=0,PendingIo10=0,ReferenceHistory14=0,DebugSource28=1,TimerGeneration64=7,WorldHash62=0,InWorldAc=1,WrapperIdentity80="old"};
        var m=new RetailNpcOwnedMotion {StartGate0=0,Deadline4=0,Pending8=0,DesiredC=default,Next18=default,Heading24=0,Duration28=0,PreviousHeading2c=0,PreviousValid30=0,Waypoint34=0,Flags38=0,Direction3c=0,Direction40=0,Action44=0,Action48=0,C4=0,C8=0,Cc=0,D0=0,D4=0};
        var cells=Enumerable.Range(0,(size/32)*(size/32)).Select(i=>new RetailNpcMembershipCell(i,true,"membership1",Hash,[],i==0?new uint[]{7}:[],0,0)).ToArray();
        var w=new RetailNpcOwnedWorld(900010000,size,size,size/32,cells.Length,size/64,(size/64)*(size/64),0,cells,[new("old",7,0,null,null,0,0xbb5a,0xa5)]);
        var globals=new RetailNpcMovementGlobals(0,0,0,0,0,0,0,1,1,1,200,1,0,0,0);
        var state=new RetailNpcMovementSnapshot(ownership,a,null,m,w,globals,new(0,0x3fff,16384,0,0,new Dictionary<int,RetailNpcDelayedWrapper>(),0,true),new([],4,0,0,1,uint.MaxValue,0,0,0,0,"constructed-stripe0-queue"),new(20000,4,0,0,"reservation",true,true),Clocks("FindBegin","FindSuccessEnd","MoveBegin","DelayTimestamp","MoveEnd","AddTimer"));
        return new(scene,state,new());
    }
}
