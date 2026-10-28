using System.Collections.Immutable;
using O = Aion.GameServer.GeoEngine.RetailPath.RetailNpcManagedMembershipObservation;
using V = Aion.GameServer.GeoEngine.RetailPath.RetailNpcManagedVisibility;

namespace Aion.GameServer.GeoEngine.RetailPath;

/// <summary>One whole inactive Main MoveNpc→managed membership→ordinary visibility→material invocation.</summary>
internal static class RetailNpcManagedMembershipMove
{
    internal sealed record Motion(RetailNpcMainMotionState State,int Heading,byte Flags,byte Material);
    internal sealed record Prefix(Motion Motion,O.Allocation Image,ImmutableArray<O.Member> Membership,
        ImmutableArray<O.User> Users,O.SharedRandom? Random,ImmutableArray<V.Intent> ProvisionalIntents,ImmutableArray<O.Event> Events)
    {
        // Complete owned bytes carry source baggage; only the enumerated gameplay stores are calculated.
        // Native wrapper/ref/TLS cells are intentionally not a claimed native-memory poststate.
        internal bool NativeMemoryPoststate=>false;
    }
    internal sealed record CandidateData(Prefix State,string ExpectedWorldGeneration,string SourceActorLifetime,string? ExpectedRandomSha256,
        string SourceSnapshotSha256,string Operation,string Epoch,bool WorldBoundsRejected);
    internal enum OnceStatus { ComputedFirstApplication, AlreadyComputed, StaleOwner }
    /// <summary>One invocation's immutable managed owner ledger. Not an executed host or a new native-memory capture.</summary>
    internal sealed class Owner
    {
        private Owner(string generation,string actorLifetime,string operation,string epoch,string hash,bool consumed,ulong revision,Prefix? calculated)
        {Generation=generation;ActorLifetime=actorLifetime;Operation=operation;Epoch=epoch;SourceSnapshotSha256=hash;Consumed=consumed;Revision=revision;Calculated=calculated;}
        internal string Generation { get; }
        internal string ActorLifetime { get; }
        internal string Operation { get; }
        internal string Epoch { get; }
        internal string SourceSnapshotSha256 { get; }
        internal bool Consumed { get; }
        internal ulong Revision { get; }
        internal Prefix? Calculated { get; }
        internal static Owner FromDeclaredInput(O.Input input)
        {ArgumentNullException.ThrowIfNull(input);O.Validate(input);return new(input.Scene.Lease.Generation,input.Npc.Key.Lifetime,input.Evidence.Operation,input.Evidence.Epoch,SnapshotHash(input),false,0,null);}
        internal (OnceStatus Status,Owner State) CalculateOnce(Candidate candidate)
        {
            ArgumentNullException.ThrowIfNull(candidate);CandidateData v=candidate.Value;
            if(v.ExpectedWorldGeneration!=Generation || v.SourceActorLifetime!=ActorLifetime || v.Operation!=Operation || v.Epoch!=Epoch || v.SourceSnapshotSha256!=SourceSnapshotSha256)
                return (OnceStatus.StaleOwner,this);
            if(Consumed)return (OnceStatus.AlreadyComputed,this);
            return (OnceStatus.ComputedFirstApplication,new(Generation,ActorLifetime,Operation,Epoch,SourceSnapshotSha256,true,checked(Revision+1),v.State));
        }
    }
    private static string SnapshotHash(O.Input input)=>RetailMainApplyEvidence.HashObject(new object?[] {
        input.CanonicalMove,input.Npc.Key,input.Npc.Image,input.Npc.Template,input.Npc.MotionSource.SnapshotSha256,input.Scene.Geometry.SnapshotSha256,
        input.Scene.Members,input.Scene.Users,input.Scene.Pointees,input.Scene.Relations,
        input.Scene.Random is { } random?new object[]{random.Address,random.Generation,random.State.Sha256,random.Lease,random.Evidence}:null,input.Scene.Globals,
        input.Scene.Lease,input.Clocks,input.Evidence,input.NearestEven,input.ExceptionsMasked,input.Sse2,input.FlushToZero,input.DenormalsAreZero});
    internal sealed class Candidate
    {
        private Candidate(CandidateData value)=>Value=value;
        internal CandidateData Value { get; }
        internal bool HostEffectsExecuted=>false;
        internal bool EncodedFor48=>false;
        internal bool ActivationReady=>false;
        internal static Candidate FromCompletedReplay(object producer)
        {
            ArgumentNullException.ThrowIfNull(producer);
            if(producer is not Replay replay || replay.Prepared is not { } value)
                throw new ArgumentException("Only the completed private replay produces a candidate.",nameof(producer));
            return new(value);
        }
    }
    internal sealed record Result(O.Status Status,Candidate? Candidate,Prefix? Known,O.Failure? Failure)
    { internal bool ActivationReady=>false; internal bool NativeOracle=>false; internal bool CanApplyToHost=>false; }
    internal static Result Calculate(O.Input input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if(input.Npc is null || input.Scene is null || input.Npc.Key is null || input.Npc.MotionSource is null || input.Npc.Image is null || input.Npc.Template is null
            || input.Npc.MotionSource.Motion is null || input.Npc.MotionSource.Template1848 is null || input.Npc.MotionSource.Suppression1a58 is null
            || input.Scene.Geometry is null || input.Scene.Globals is null || input.Scene.Lease is null || input.Evidence is null)
            return new(O.Status.InvalidInput,null,null,new("unknown","Missing required owned root.",null));
        var replay=new Replay(input);
        try{return replay.Calculate();}
        catch(O.Fault error){return replay.Fail(error.Code,error.Message,error.Child);}
        catch(RetailMainApplyFault error){return replay.Fail("main-child",error.Message,error.Child);}
        catch(ArithmeticException error){return replay.Fail("arithmetic",error.Message,null);}
    }
    private sealed class Replay(O.Input i)
    {
        internal CandidateData? Prepared { get; private set; }
        private readonly List<O.Event> events=[];
        private readonly HashSet<(ulong,uint)> consumed=[];
        private O.Allocation image=i.Npc.Image;
        private RetailNpcMainActorSnapshot actor=i.Npc.MotionSource;
        private ImmutableArray<O.Member> members=i.Scene.Members;
        private V.Run? visibility;
        private V.Result? visible;
        private V.Move? moveMeaning;
        private bool started,boundsRejected;
        private void Emit(string stage,ulong site,long? before=null,long? after=null)=>events.Add(new(events.Count+1,stage,site,0,before,after));
        private Prefix Prefix()=>new(new(actor.Motion,actor.Heading12c,actor.Flags1c35,actor.Material1426),image,members,
            visible?.Users??visibility?.CurrentUsers??i.Scene.Users,visible?.Random??visibility?.CurrentRandom??i.Scene.Random,
            visible?.Intents??visibility?.CurrentIntents??[],[..events]);
        internal Result Fail(string code,string detail,ulong? child)=>new(started?O.Status.IncompleteReachedChild:O.Status.InvalidInput,null,started?Prefix():null,new(code,detail,child));
        private uint Tick(ulong site)
        {
            var matches=i.Clocks.Where(c=>c.Site==site && c.RecipientId==0).ToArray();
            O.Need(matches.Length==1 && consumed.Add((site,0)),"clock","Missing/repeated original NPC clock.");
            Emit("OriginalNpcClock",site,after:matches[0].Value);return matches[0].Value;
        }
        private Result Complete(O.Status status)
        {
            O.Need(consumed.Count==i.Clocks.Length,"clock","Unreached original clock receipts are not consumed.");
            Prepared=new(Prefix(),i.Scene.Lease.Generation,i.Npc.Key.Lifetime,i.Scene.Random?.State.Sha256,SnapshotHash(i),i.Evidence.Operation,i.Evidence.Epoch,boundsRejected);
            return new(status,Candidate.FromCompletedReplay(this),Prefix(),null);
        }
        internal Result Calculate()
        {
            O.Validate(i);
            var decoded=RetailNpcInterserverMoveCodec.DecodeCanonical(i.CanonicalMove.AsSpan());
            O.Need(decoded.Record is not null,"record","Only the complete pinned canonical producer record is admitted.");
            var move=decoded.Record!;O.Need(move.ObjectId==i.Npc.Key.ObjectId,"record","Selected actor/producer object ID mismatch.");
            if(move.Heading>=120)return Complete(O.Status.NativeEarlyReturn);
            Emit("ManagedSerializedActorWorldLease",0x140b1cd40);started=true;
            if(unchecked((byte)actor.Counter19f8)!=move.Sequence)return Complete(O.Status.NativeEarlyReturn);
            O.Need(actor.Heading12c is >=0 and <=119,"heading","Current heading outside the admitted native table domain.");
            uint getterTick=Tick(RetailNpcMainMotionApplication.GetterTickSite);
            var context=new RetailNpcMainMotionContext(RetailNpcMainMotion.MainBinarySha256,RetailNpcMainMotion.GetterEvidenceSha256,O.NpcVtable,
                i.Npc.Key.Lifetime,i.Evidence.Epoch,i.Evidence.Operation,actor.SnapshotSha256,
                i.NearestEven,i.ExceptionsMasked,i.FlushToZero,i.DenormalsAreZero,i.Sse2,i.Evidence.Origin==O.Origin.ConstructedSourceFixture);
            var receipt=new RetailNpcMainGetterTick(getterTick,RetailNpcMainMotionApplication.GetterTickSite,i.Npc.Key.Lifetime,
                i.Evidence.Epoch,i.Evidence.Operation,actor.Source.Sha256,i.Evidence.Origin==O.Origin.ConstructedSourceFixture);
            var location=RetailNpcMainMotion.Calculate(actor.Motion,receipt,context);
            O.Need(location.Calculated && location.Location is not null,"getter","Complete old effective-location calculation failed: "+location.Detail);
            var oldEffective=location.Location!.Value;
            var goalResult=RetailNpcInterserverMoveCodec.ResolveContinuingGoal(move,actor.Motion.ContinuingGoal19fc);
            O.Need(goalResult.Available && goalResult.Goal is not null && goalResult.Duration is not null,"goal","Actual continuing goal/duration producer missing.");
            var goal=goalResult.Goal!.Value;uint duration=goalResult.Duration!.Value;uint[] delta=new uint[3],velocity=new uint[3];long turn=0;
            if(RetailMainApplyMath.Equal(oldEffective,move.Position) || duration==0)
            {duration=0;goal=move.Position;if(location.PastDue!=0)oldEffective=move.Position;}
            else
            {
                var stop=RetailMainApplyMath.Components(move.Position);var current=RetailMainApplyMath.Components(oldEffective);
                uint durationFloat=RetailMainApplyMath.Integer(duration);
                delta[0]=RetailMainApplyMath.Sub(stop[0],current[0]);delta[1]=RetailMainApplyMath.Sub(stop[1],current[1]);
                uint seconds=RetailMainApplyMath.Div(durationFloat,0x447a0000);delta[2]=RetailMainApplyMath.Sub(stop[2],current[2]);
                if(RetailMainApplyMath.Float(seconds)!=0)for(int n=0;n<3;n++)velocity[n]=RetailMainApplyMath.Div(delta[n],seconds);
                if((move.Flags&2)!=0 && TurnActive(actor.TurnRate7c8))
                {
                    int difference=System.Math.Abs(actor.Heading12c-move.Heading);
                    uint delay=RetailMainApplyMath.Div(RetailMainApplyMath.Mul(RetailMainApplyMath.Mul(RetailMainApplyMath.SignedInteger(difference),actor.TurnRate7c8),0x447a0000),0x42f00000);
                    turn=RetailMainApplyMath.Truncate64(delay);
                    seconds=RetailMainApplyMath.Div(RetailMainApplyMath.Integer(unchecked(duration-(uint)turn)),0x447a0000);
                    if(RetailMainApplyMath.Float(seconds)==0)Array.Clear(velocity);
                    else for(int n=0;n<3;n++)velocity[n]=RetailMainApplyMath.Div(delta[n],seconds);
                }
            }
            // bd120 normal-return lock infrastructure is replaced by the exclusive generation lease.
            ulong suppression=O.U64(image,0x1a58);
            uint suppressed=suppression==0?0:System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(O.Resolve(i.Scene,suppression,4));
            Emit("CalculatedSuppressionPointee",0x1400bd120,after:suppressed);
            if(suppressed==0)
            {
                bool duplicate=move.Flags==8 || move.Flags==0 && (actor.Flags1c35&8)==0 && RetailMainApplyMath.Near(move.Position,actor.Motion.ContinuingGoal19fc)
                    || move.Flags!=0 && move.Flags!=8 && move.Flags==actor.Flags1c35 && RetailMainApplyMath.Near(goal,actor.Motion.ContinuingGoal19fc);
                if(!duplicate)
                {
                    byte flags=unchecked((byte)(move.Flags&0xef));bool extended=(flags&0xc0)==0xc0;
                    moveMeaning=new(0,i.Npc.Key.ObjectId,oldEffective,move.Heading,flags,extended&&(flags&0x20)!=0?goal:default,0);
                    Emit("MoveSnapshotBeforeWorldPublication",0x140b1cd40);
                }
            }
            if(O.U32(image,0x1ba0)!=0)O.Child(0x140363af0,"Active trace-owner gameplay is not calculated.");
            uint due=unchecked(Tick(RetailNpcMainMotionApplication.DueTickSite)+duration);
            var oldMotion=actor.Motion;
            // Native order: velocityXY, due, priorXY, velocityZ, priorZ, start; then possible turn correction.
            image=O.Put32(image,0x186c,velocity[0]);image=O.Put32(image,0x1870,velocity[1]);Emit("VelocityXY",0x140b1d51f);
            image=O.Put32(image,0x1858,due);Emit("Due1858",0x140b1d527,oldMotion.Due1858,due);
            image=O.Put32(image,0x1860,oldMotion.Endpoint120.X);image=O.Put32(image,0x1864,oldMotion.Endpoint120.Y);Emit("PriorEndpointXY",0x140b1d534);
            image=O.Put32(image,0x1874,velocity[2]);image=O.Put32(image,0x1868,oldMotion.Endpoint120.Z);Emit("VelocityZThenPriorZ",0x140b1d545);
            uint start=Tick(RetailNpcMainMotionApplication.StartTickSite);image=O.Put32(image,0x185c,start);Emit("Start185c",0x140b1d573,oldMotion.Start185c,start);
            if((move.Flags&2)!=0 && TurnActive(actor.TurnRate7c8))
            {uint before=start;start=unchecked(start+(uint)turn);due=unchecked(start+duration);image=O.Put32(image,0x185c,start);image=O.Put32(image,0x1858,due);Emit("LateTurnStartThenDue",0x140b1d5c0,before,start);}
            image=O.PutVector(image,0x19fc,goal);image=O.Replace(image,0x1c35,[(byte)(move.Flags&0xed)]);Emit("ContinuingGoalThenFlags",0x140b1d601);
            actor=actor with {Motion=oldMotion with {Velocity186c=RetailMainApplyMath.Vector(velocity),Due1858=due,PriorEndpoint1860=oldMotion.Endpoint120,Start185c=start,ContinuingGoal19fc=goal},Flags1c35=(byte)(move.Flags&0xed)};
            ApplyWorld(move.Position,move.Heading);
            var (material,_)=RetailNpcMainMaterialQuery.Calculate(i.Scene.Geometry,actor.Motion.Endpoint120);
            image=O.Replace(image,0x1426,[material]);actor=actor with {Material1426=material};Emit("CalculatedMaterial1426",0x140b1d651,after:material);
            var effects=RetailNpcMainMaterialQuery.EvaluateEffects(i.Scene.Geometry,actor,material);
            if(!effects.Computed)O.Child(effects.IncompleteChild??0x14037bf80,effects.Detail??"Reached material gameplay child.");
            foreach(string skip in effects.EvaluatedSkips)Emit("MaterialSkip:"+skip,0x14037bf80);
            Emit("ManagedInvocationCompleteNoNativeTlsOrSocketMutation",0x140b1d6a1);
            return Complete(O.Status.CompleteManagedTransaction);
        }
        private static bool TurnActive(uint word)
        {float rate=RetailMainApplyMath.Float(word);return rate>=RetailMainApplyMath.Float(0x38d1b717)||rate<=RetailMainApplyMath.Float(0xb8d1b717);}
        private void ApplyWorld(RetailMainVectorWords endpoint,int heading)
        {
            var w=i.Scene.Geometry;var old=actor.Motion.Endpoint120;
            if(!RetailNpcMainWorldMotion.InBounds(w,old) || !RetailNpcMainWorldMotion.InBounds(w,endpoint))
            {boundsRejected=true;Emit("KnownWorldBoundsFailureNoRollbackOfMotionStores",0x140681427);return;}
            int oldCell=RetailNpcMainWorldMotion.Cell(w,old),newCell=RetailNpcMainWorldMotion.Cell(w,endpoint);
            if(oldCell!=newCell)
            {
                int Region(RetailMainVectorWords point)=>checked((RetailMainApplyMath.Truncate32(point.Y)>>6)*w.RcuStride5c+(RetailMainApplyMath.Truncate32(point.X)>>6));
                int oldRegion=Region(old),newRegion=Region(endpoint);
                Emit("ManagedSerializationCoversNativeRegionMin",0x140680d00,after:System.Math.Min(oldRegion,newRegion));
                if(oldRegion!=newRegion)Emit("ManagedSerializationCoversNativeRegionMax",0x140680d00,after:System.Math.Max(oldRegion,newRegion));
                O.Member own=members.Single(m=>m.Actor==i.Npc.Key);
                var list=members.Where(m=>m.Actor!=i.Npc.Key).ToList();Emit("RetireOldManagedMembershipLease",0x140d796c0,oldCell,newCell);
                O.Member fresh=own with {Cell=newCell,LeaseIdentity=i.Scene.Lease.Generation+"/"+i.Evidence.Operation+"/new-membership"};
                int index=list.FindIndex(m=>m.Cell==newCell && m.Kind==RetailMainWrapKind.Object);if(index<0)list.Add(fresh);else list.Insert(index,fresh);
                members=[..list];Emit("InsertNewManagedMembershipLeaseBeforeEndpoint",0x140680e33,oldCell,newCell);
                image=O.PutVector(image,0x120,endpoint);image=O.Put32(image,0x12c,unchecked((uint)heading));
                actor=actor with {Motion=actor.Motion with {Endpoint120=endpoint},Heading12c=heading};Emit("EndpointWorldHeadingPublished",0x140680e5d);
                Emit("ManagedDependencyNativeOldRegionReleaseFirst",0x140680ae0,after:oldRegion);
                if(oldRegion!=newRegion)Emit("ManagedDependencyNativeNewRegionReleaseSecond",0x140680ae0,after:newRegion);
            }
            else
            {image=O.PutVector(image,0x120,endpoint);image=O.Put32(image,0x12c,unchecked((uint)heading));actor=actor with {Motion=actor.Motion with {Endpoint120=endpoint},Heading12c=heading};Emit("SameCellEndpointWorldHeadingPublished",0x140680cdc);}
            // Full post-World gameplay follows actual endpoint publication; it is never elided after cross-cell handling.
            if(O.I32(i.Npc.Template,0x354)==4 && O.I32(image,0x188c)>0)O.Child(0x140d7cf70,"Active region/system-skill owner is unsupported.");
            Emit("PostMoveRegionOwnerGuard",0x140680f30);
            if(O.I32(image,0x1a50)==1)O.Child(0x140681640,"Special world-wide NPC broadcast requires its own recipient owner.");
            if(w.Policy14ac0!=100 || w.Policy14ac4!=10000)O.Child(0x1406826c0,"Alternate world visibility dispatcher is outside this finite domain.");
            visibility=new(i,events,consumed);visible=visibility.Calculate(i.Npc,image,old,endpoint,moveMeaning,members);
        }
    }
}
