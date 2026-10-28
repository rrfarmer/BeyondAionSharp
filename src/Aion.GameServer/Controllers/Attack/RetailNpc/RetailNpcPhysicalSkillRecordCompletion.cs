using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using C = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcPhysicalInstantCalculation;
using N = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcImmediateDamageOwner;
using P = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcPhysicalInstantParameters;
using Protection = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcPilotProtectionTransition;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Inactive complete admitted NPC-to-NPC physical Begin/record/retained completion.
/// Original draws/ticks are ordered primitive receipts, never callbacks, HP answers or RNG generation.
/// Every unsupported reached child exposes only an explicitly known prefix; no final actor result.
/// User, successful system-effect activation, active reactive/item and movement children remain gated.</summary>
internal static class RetailNpcPhysicalSkillRecordCompletion
{
    internal const string Proposal = "9a7cc3303b2e8aa9a78c69598e498853fc257c36c6916c6f8d19b18ddb734343";
    internal const string SkillSource = "27cbdaaae9d5ce6b7286ed518385553805338e95c4a4d82b6ee7ba21ffc072e3";
    // IncompleteReachedChild denotes a reached interpreter domain/frontier. Its reason states
    // whether a native child is reached now or remains future/deferred; it never claims that child ran.
    internal enum Completion { RejectedBeforeEvaluation, IncompleteReachedChild, TargetCheckFailed,
        CompleteImmediateInvocation, CompleteInitialEnqueueHpPending, CompleteTimerHandler }
    internal enum Allocation { Unknown, OriginalNull, OriginalFreshDisjoint }
    internal enum CallStage { ShieldDraw, ReflectorDraw, SpinDraw, ConstructionTick, TimerEntry, TimerDue, TimerExit }
    internal sealed record OriginalCall(CallStage Stage, ulong Site, uint Value, int Minimum,
        int Maximum, C.Evidence Evidence);
    internal sealed record RawActor(N.Actor Owner, ulong Address, string StorageIdentity,
        ulong? World118, uint? Z128Bits, int? State19f8, int? Field1a3c, ulong? Pointer1a58,
        int? First1a58, int? Field198At1a58, uint? Template278, ushort? Offense888,
        ushort? Defense824, int? Weapon8ec, ulong? ItemMask6c8, int? ReactiveCounter,
        uint? Post1248, uint? ThreatRateFf8Bits);
    /// <summary>Actual actor guards only. ActionInfo belongs to the newly constructed
    /// completion context and is calculated from owned header40; it is never an input prediction.</summary>
    internal sealed record CoreGuards(uint? Abnormal1250,uint? CurrentCast131c,uint? Systems12cc,
        byte? Byte1425,N.Manager Manager,N.BoundSkill Bound160,N.BoundSkill Bound210,N.BoundSkill Bound168,N.Evidence Evidence);
    internal sealed record OwnerEnvironment(N.Identity Identity, N.World World, CoreGuards Guards,
        N.Attribution Attribution, N.RewardGuard Reward, N.Scheduler Scheduler,
        N.Transport Transport, N.FloatingContract Floating, N.Infrastructure Infrastructure,
        ImmutableArray<N.Tick> OriginalTicks, ulong? WorldPointer118);
    internal sealed record ManagerProgram(N.ManagerPresence Presence, ulong Pointer, string Generation,
        uint? Flags2b18, int? ShieldChance2c88, int? ShieldPerHit2c8c, int? ShieldMode2c90,
        int? Budget2cac, uint? ReflectSkill2c34, int? ReflectPercent2c38, int? ReflectFlat2c3c,
        uint? ReflectMin2c40Bits, uint? ReflectMax2c44Bits, int? ReflectChance2c48,
        C.Evidence Evidence);
    internal sealed record Stagger(uint? Global14276a0cc, uint? Global142766278,
        ulong? SpinObject1428b79a0, ulong? SpinVtable, C.Evidence Evidence);
    internal sealed record Storage(C.Binding WrapperFrameRbp, C.Binding Header,
        C.Binding Records, C.Binding Info, C.Binding Copy, C.Binding CompletionFrameRbp,
        ImmutableArray<byte> HeaderBefore40, ImmutableArray<byte> RecordsBefore896,
        ImmutableArray<byte> InfoBefore948, ImmutableArray<byte> CopyBefore948,
        int ContainerCount, int ContainerCapacity, ImmutableArray<byte> ContainerBytes,
        int PriorMaximum, int PriorMinimum, ImmutableArray<C.Span> OtherOwnedSpans,
        bool? CompleteSourceAndDestinationAliases);
    internal sealed record TimerTuple(uint Due, ulong ObjectAddress, int TimerId, ulong Data, uint Generation);
    internal sealed record TimerQueue(uint Index, uint MinimumWake, ImmutableArray<TimerTuple> Heap);
    internal sealed record DelayedScheduler(uint? Closing, bool? Rotation, uint? TlsIndex,
        uint? QueueCount, uint? GlobalNextWake, uint? SleepMilliseconds,
        ulong? AccumulatedDelay, ulong? EnqueueCount, ImmutableArray<TimerQueue> Queues,
        C.Evidence Evidence);
    internal sealed record AllocationInput(Allocation Outcome, C.Binding AllocationBinding,
        ImmutableArray<byte> OriginalBytes110, uint? Modulus142726bd0, uint? Modulus142726bd4,
        uint? TypeNameInitialized15486eff0, uint? CallerInitialized15486eff8,
        uint? CallerId15486effc, ulong? CallerCounter, DelayedScheduler Scheduler);
    internal sealed record Input(C.Input Calculation, RawActor Attacker, RawActor Defender,
        OwnerEnvironment Primary, OwnerEnvironment Reflection, ManagerProgram Protection,
        Stagger Stagger, Storage Storage, AllocationInput Allocation, ushort? RunInfo78,
        int? RunInfo40, ulong? Proxy50, int? SkillHop364, ulong? SkillHopFunction378,
        int? CurrentSkill4d4, ImmutableArray<OriginalCall> OriginalCalls,
        bool? DiagnosticsDisabled, bool? SerializedNoInlineWriter);
    /// <summary>Ordered semantic event. Site is an exact retained source instruction/function
    /// anchor for the described operation, not an observed program counter or an exhaustive memory trace.</summary>
    internal sealed record Event(int Sequence, string Stage, ulong Site, long? Before = null,
        long? After = null, string? Detail = null);
    internal sealed record NativeSync(ImmutableArray<byte> Bytes, uint RoutingNpcId,
        int Getter19f8, int OldTargetHp, int ApplicationCell, N.TransportEffects Transport);
    internal sealed record StorageResult(ImmutableArray<byte> Header40, ImmutableArray<byte> Records896,
        ImmutableArray<byte> Info948, ImmutableArray<byte> Copy948, ImmutableArray<byte> Container,
        int ContainerCount, int Maximum, int Minimum);
    internal sealed record OwnerBatch(bool Primary,int FirstIndex,int Count,string Operation,string Epoch);
    internal sealed record KnownPrefix(RawActor Attacker, RawActor Defender, ManagerProgram? BeginProtectionManager,
        StorageResult? Storage, C.Result? Calculation, ImmutableArray<Event> Events,
        ImmutableArray<N.OwnerEvent> OwnerEvents, ImmutableArray<OriginalCall> ConsumedCalls,
        N.State? IncompleteOwnerState, int? KnownOwnerApplicationCell, ulong? RaceMaskAfter,
        ImmutableArray<byte> RetainedObjectBytes, DelayedScheduler? CurrentDelayedScheduler,
        ulong? CallerCounterAfter, int? Phase108After, bool? ConstructionReferenceReleased,
        int CurrentDamageCell, int CurrentReflectedCell, int CurrentThreat,ImmutableArray<OwnerBatch> OwnerBatches);
    internal sealed record Result(Completion Status, KnownPrefix? Known,
        Continuation? Retained, NativeSync? Packet, N.Result? Primary, N.Result? Reflection,
        string? Unsupported)
    {
        internal bool ActivationReady => false;
        internal int? FinalHpAfterDeferredLife => null;
        internal bool NativeOracle => false;
        internal bool InvocationComplete => Status is Completion.TargetCheckFailed or Completion.CompleteImmediateInvocation
            or Completion.CompleteInitialEnqueueHpPending or Completion.CompleteTimerHandler;
    }
    /// <summary>Payload is privately produced from the complete initial wrapper. Fresh timer
    /// entry still requires original current actor/manager/dispatch snapshots; enqueue HP is not current HP.</summary>
    internal sealed class Continuation
    {
        internal static Continuation CreateFromReplay(object producer)
        {
            ArgumentNullException.ThrowIfNull(producer);
            if(producer is not Replay replay || replay.PreparedContinuation is not { } value)
                throw new ArgumentException("Only the private live replay can produce a retained continuation.",nameof(producer));
            return new(value.Origin,value.Bytes,value.Storage,value.Scheduler,value.Damage,value.Reflected,value.Threat,value.Due);
        }
        internal Input Origin { get; }
        internal ImmutableArray<byte> ObjectBytes { get; }
        internal StorageResult Storage { get; }
        /// <summary>Historical enqueue result only; it is not the current queue at later dispatch.</summary>
        internal DelayedScheduler Scheduler { get; }
        internal int HpDamage { get; }
        internal int Reflected { get; }
        internal int Threat { get; }
        internal uint Due { get; }
        internal int ObjectReferences { get; }
        internal int PendingTimers { get; }
        internal uint SourceId => Origin.Attacker.Owner.Binding.ObjectId;
        internal uint TargetId => Origin.Defender.Owner.Binding.ObjectId;
        private Continuation(Input origin, ImmutableArray<byte> bytes, StorageResult storage,
            DelayedScheduler scheduler, int hp, int reflected, int threat, uint due)
        { Origin=origin; ObjectBytes=bytes; Storage=storage; Scheduler=scheduler;
          HpDamage=hp; Reflected=reflected; Threat=threat; Due=due; ObjectReferences=1; PendingTimers=1; }
    }
    internal sealed record TimerInput(Continuation Retained, RawActor CurrentAttacker, RawActor CurrentDefender,
        OwnerEnvironment Primary, OwnerEnvironment Reflection, C.Binding CompletionFrameRbp,
        ImmutableArray<byte> OriginalObjectBytes, int? Phase108, int? References8, int? Pending10,
        C.Evidence Dispatch, bool? DispatchOwnsActualRetainedObject, bool? DiagnosticsDisabled,
        bool? SerializedNoInlineWriter, ImmutableArray<C.Span> CurrentOtherOwnedSpans,
        bool? CompleteCurrentOwnerStorageAliases);

    /// <summary>Unforgeable by record construction. Stores the actual producer, original
    /// frame/cell/context/header and current pre-owner actor state, not a desired HP result.</summary>
    internal sealed class OwnerCall
    {
        internal static OwnerCall CreateFromReplay(object producer)
        {
            ArgumentNullException.ThrowIfNull(producer);
            if(producer is not Replay replay || replay.PreparedOwner is not { } value)
                throw new ArgumentException("Only the private live replay can produce an original owner call.",nameof(producer));
            return new(value.Attacker,value.Receiver,value.Call,value.FrameRbp,value.HeaderAddress,value.Header,value.Operation,value.Epoch);
        }
        private readonly N.Actor attacker, receiver;
        private readonly N.Invocation call;
        private readonly ulong frameRbp, headerAddress, applicationAddress, contextAddress;
        private readonly ImmutableArray<byte> header;
        private readonly string operation, epoch;
        private OwnerCall(N.Actor a,N.Actor d,N.Invocation invocation,ulong rbp,ulong headerPointer,
            ImmutableArray<byte> headerBytes,string op,string ep)
        { attacker=a;receiver=d;call=invocation;frameRbp=rbp;headerAddress=headerPointer;header=headerBytes;
          applicationAddress=rbp+(invocation.Kind14==2?0x828ul:0x830ul);
          contextAddress=rbp-(invocation.Kind14==2?0xa0ul:0x78ul);operation=op;epoch=ep; }
        internal bool Matches(N.Input input)
        {
            bool primary=call.Kind14==2;
            return input.Attacker==attacker && input.Receiver==receiver && input.Call==call
                && input.Source.Operation==operation && input.Source.Epoch==epoch
                && frameRbp>=0xa0 && applicationAddress==frameRbp+(primary?0x828ul:0x830ul)
                && contextAddress==frameRbp-(primary?0xa0ul:0x78ul)
                && call.Cell.Storage==N.CellStorage.PhysicalSkillCompletionApplicationCell
                && call.Cell.Role==(primary?N.ApplicationCellRole.PrimaryPhysicalSkillCompletionLocal:N.ApplicationCellRole.RetaliationPhysicalSkillCompletionLocal)
                && call.Cell.OwnershipFunction==0x140bb5370 && call.Cell.OwnershipSourceSha256==SkillSource
                && call.Cell.DisjointFromActorOwnedStorage && call.Cell.IsOriginalApplicationCell
                && call.Cell.Operation==operation && call.Cell.Epoch==epoch
                && call.Cell.CellIdentity=="bb5370:"+applicationAddress.ToString("x")
                && call.Context08==0 && call.Context0c==(primary?0:1) && call.Context10==0
                && call.Category==1 && call.Log1c==0xc5 && call.Flags20==0 && call.Critical21==0
                && receiver.State.Pending728 is 0 or 1
                && (primary ? call.Skill18 is 16556 or 16794 && headerAddress!=0 && header.Length==40
                    && input.Guards.ActionInfo.Presence==N.ActionPresence.Present
                    && input.Guards.ActionInfo.Flags==ReadU32(header.AsSpan(),0)
                    : call.Skill18==0 && headerAddress==0 && header.IsEmpty
                    && input.Guards.ActionInfo.Presence==N.ActionPresence.VerifiedNull);
        }
    }

    internal static Result EvaluatePhysicalBegin(Input input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var replay=new Replay(input);
        try{return replay.Begin();}
        catch(Boundary ex){return replay.Stop(ex.Message);}
        catch(ArithmeticException ex){return replay.Stop("Unrepresentable arithmetic: "+ex.Message);}
    }
    internal static Result EvaluateTimerExpired(TimerInput input)
    {
        ArgumentNullException.ThrowIfNull(input);ArgumentNullException.ThrowIfNull(input.Retained);
        var replay=new Replay(input.Retained.Origin,input);
        try{return replay.Timer();}
        catch(Boundary ex){return replay.Stop(ex.Message);}
        catch(ArithmeticException ex){return replay.Stop("Unrepresentable arithmetic: "+ex.Message);}
    }
    /// <summary>Pure complete native33/38/49-byte source-frame encoding. Arguments are raw
    /// primitive fields, not a claim that an HP owner or transport ran. This is not a4.8 client codec.</summary>
    internal static NativeSync EncodeNativeSync(uint source,uint target,int hop,int oldHp,int cell,
        ImmutableArray<byte> header40,int raw19f8,ushort skill,N.TransportEffects transport)
    {
        ArgumentNullException.ThrowIfNull(transport);
        if(header40.IsDefault || header40.Length!=40)throw new ArgumentException("Complete header40 including opaque bytes required.",nameof(header40));
        return Replay.EncodeSync(source,target,hop,oldHp,cell,header40.ToArray(),raw19f8,skill,transport);
    }
    private sealed class Replay
    {
        internal OwnerPreparation? PreparedOwner { get; private set; }
        internal ContinuationPreparation? PreparedContinuation { get; private set; }
        private readonly Input i;
        private readonly TimerInput? timer;
        private RawActor a,d;
        private ManagerProgram manager;
        private OwnerEnvironment primary,reflection;
        private readonly List<Event> events=[];
        private readonly List<N.OwnerEvent> ownerEvents=[];
        private readonly List<OwnerBatch> ownerBatches=[];
        private readonly List<OriginalCall> consumed=[];
        private C.Result? calculation;
        private StorageResult? storage;
        private NativeSync? packet;
        private N.Result? primaryResult,reflectionResult;
        private N.State? ownerPrefix;
        private int? ownerCell;
        private byte[] header=[],records=[];
        private int cursor,damage,reflected,threat;
        private ulong? raceMask;
        private byte[] retainedBytes=[];
        private DelayedScheduler? delayedScheduler;
        private ulong? callerCounter;
        private int? phaseAfter;
        private bool? constructionReleased;
        private bool began;
        internal Replay(Input input,TimerInput? current=null)
        {i=input;timer=current;a=current?.CurrentAttacker??i.Attacker;d=current?.CurrentDefender??i.Defender;
         manager=i.Protection;primary=current?.Primary??i.Primary;reflection=current?.Reflection??i.Reflection;
         raceMask=current is null?i.Calculation.Globals.RaceMask154827468:null;}
        private void Emit(string stage,ulong site,long? before=null,long? after=null,string? detail=null)
            =>events.Add(new(events.Count+1,stage,site,before,after,detail));
        private KnownPrefix Prefix()=>new(a,d,timer is null?manager:null,storage,calculation,[..events],[..ownerEvents],[..consumed],ownerPrefix,ownerCell,
            raceMask,[..retainedBytes],delayedScheduler,callerCounter,phaseAfter,constructionReleased,damage,reflected,threat,[..ownerBatches]);
        internal Result Stop(string reason)=>new(began?Completion.IncompleteReachedChild:Completion.RejectedBeforeEvaluation,
            began?Prefix():null,null,packet,null,null,reason);
        internal Result Begin()
        {
            ValidateBegin();
            header=i.Storage.HeaderBefore40.ToArray();records=i.Storage.RecordsBefore896.ToArray();
            header.AsSpan(0,5).Clear();header.AsSpan(8,12).Clear();header.AsSpan(24,16).Clear();header[24]=1;
            for(int slot=0;slot<16;slot++)records.AsSpan(slot*56,53).Clear();
            // The child receives only this calculated initialization, retaining record padding53..55.
            C.Input raw=i.Calculation with {Storage=i.Calculation.Storage with
            {HeaderBytes=[..header.AsSpan(0,4).ToArray()],RecordBytes=[..records.AsSpan(0,56).ToArray()]}};
            C.Outcome result=C.Evaluate(raw);
            Need(result.Success,"Complete physical child admission rejected: "+result.Diagnostic?.Path+":"+result.Diagnostic?.Message);
            calculation=result.Value??throw new Boundary("Complete calculator reported success without a value.");began=true;
            Emit(calculation.Hit?"TargetCheckAndOriginalBeginChance":"FailedTargetCheckSkipsBegin",calculation.Hit?0x140bbd1feul:0x140bb8d90ul,after:calculation.Hit?1:0);
            if(!calculation.Hit)
            {Need(i.OriginalCalls.IsEmpty,"Unreached wrapper receipts on failed target check.");
             return new(Completion.TargetCheckFailed,Prefix(),null,null,null,null,null);}
            Emit("WrapperHeaderAnd16RecordsInitialized",0x140bb80c0);
            Emit("CompleteCalculatorFromOwnedRawInputs",0x140bb84fa,after:calculation.FinalRecordDamage);
            calculation.HeaderAfter.AsSpan().CopyTo(header);calculation.RecordAfter.AsSpan().CopyTo(records);
            raceMask=calculation.RaceMaskAfter;damage=calculation.FinalRecordDamage!.Value;
            Need(K(i.SkillHop364,"Skill364")==1 && K(i.SkillHopFunction378,"Skill378")==0x140b67a10,
                "Selected source requires actual damage HoP helper, not an omitted optional callback.");
            int rawThreat=unchecked(damage*10+2);Emit("LatchedDamageHoPBeforeProtection",0x140b67a10,damage,rawThreat);
            Protect();
            Write32(records,0,damage);Write32(records,4,0);records[0x33]=0; // both reached NPC slot189 preparation returns0
            bool stop=records[0x30]<10;
            if(ReadI32(calculation.CacheAfter.AsSpan(),4)!=0)header[4]=1;
            uint rate=K(a.ThreatRateFf8Bits,"source ff8");float scaledThreat=Mul(FromInt(rawThreat),Bits(rate));
            Emit("ThreatBinary32Product",0x140bb8671,after:BitConverter.SingleToUInt32Bits(scaledThreat));threat=Cvtt(scaledThreat);
            Emit("ThreatIntFloatRateCvtt",0x140bb867a,rawThreat,threat);
            int targetPercent=Preview(ref d,damage,"Target");int sourcePercent=Preview(ref a,reflected,"Source");
            Stagger(stop,ReadI32(calculation.CacheAfter.AsSpan(),4)!=0);
            storage=ProduceStorage(targetPercent,sourcePercent);
            Need(cursor==i.OriginalCalls.Length || i.Allocation.Outcome==Allocation.OriginalFreshDisjoint,
                "Extra original wrapper draws on immediate fallback.");
            Need(i.Allocation.Outcome is Allocation.OriginalNull or Allocation.OriginalFreshDisjoint,"Unknown allocation outcome.");
            if(i.Allocation.Outcome==Allocation.OriginalNull)
            {Emit("OriginalAllocationReturnedNull",0x140bb8c14);
             Need((K(i.Allocation.TypeNameInitialized15486eff0,"type-name current initialized")&1)!=0,
                "Reached type-name/global registration initialization requires its full child.");CompleteOwner();
             Need(cursor==i.OriginalCalls.Length,"Unconsumed primitive wrapper receipts.");
             return new(Completion.CompleteImmediateInvocation,Prefix(),null,packet,primaryResult,reflectionResult,null);}
            Continuation continuation=Retain();
            Need(cursor==i.OriginalCalls.Length,"Unconsumed primitive wrapper receipts.");
            Need((ReadU32(header,0)&0x10a17)==0,
                "Computed retained header is outside the admitted continuation domain: enqueue prefix is known, future movement/system owner was not evaluated.");
            return new(Completion.CompleteInitialEnqueueHpPending,Prefix(),continuation,null,null,null,null);
        }

        internal Result Timer()
        {
            TimerInput t=timer!;ValidateTimer(t);began=true;
            storage=t.Retained.Storage;header=storage.Header40.ToArray();records=storage.Records896.ToArray();
            damage=t.Retained.HpDamage;reflected=t.Retained.Reflected;threat=t.Retained.Threat;
            retainedBytes=t.OriginalObjectBytes.ToArray(); // Handler does not read the current delayed-service heap.
            int phase=K(t.Phase108,"current phase108");
            Write32(retainedBytes,0x108,0);phaseAfter=0;
            Emit("Phase108Exchange",0x140bb5fb1,phase,0);
            Need(phase==0,"Reached nonzero108 prepass/requeue300 and required movement children; HP is not completed.");
            CompleteOwner();
            uint old=K(d.Post1248,"current post-completion1248");
            d=d with {Post1248=0};Emit("Target1248CasClear",0x140bb60e2,old,0);
            if((old&255)!=0 && primary.Guards.Manager.Presence==N.ManagerPresence.Present)
                Need(false,"Reached abnormal-status broadcast child after HP; known prefix retained, no completed result.");
            Emit("TimerHandlerReturnsDeferredReleaseAndDeathPending",0x140bb6118);
            return new(Completion.CompleteTimerHandler,Prefix(),null,packet,primaryResult,reflectionResult,null);
        }

        private void Protect()
        {
            ulong ptr=K(i.Calculation.Defender.Manager.CurrentPointer158,"defender manager158");
            Need(manager.Pointer==ptr && manager.Generation==d.Owner.Binding.ManagerGeneration,
                "Protection manager pointer/generation mismatch.");
            if(ptr==0)
            {Need(manager.Presence==N.ManagerPresence.VerifiedNull,"Null manager has mismatched presence.");Emit("ProtectorNullManagerSkip",0x1400b40f0);return;}
            Need(manager.Presence==N.ManagerPresence.Present,"Unknown protection manager.");
            Evidence(manager.Evidence);uint flags=K(manager.Flags2b18,"protection flags2b18");
            Need(primary.Guards.Manager.Flags2b18==flags && primary.Guards.Manager.Pointer158==ptr,
                "Core/protection manager raw flags must be the same owned current state.");
            Need((flags&0x1000)==0,"Whole-skill reflection child is reached.");
            int latched=damage;
            if((flags&0x40)!=0 && damage>0)
            {
                int draw=Draw(CallStage.ShieldDraw,0x14034cbdf);
                int chance=K(manager.ShieldChance2c88,"Shield chance2c88");
                int perHit=K(manager.ShieldPerHit2c8c,"Shield perHit2c8c"),mode=K(manager.ShieldMode2c90,"Shield mode2c90");
                Need(chance==1000 && perHit is 69 or 71 or 73 && mode==1,"Unverified effective pilot Shield program.");
                if(draw<=chance)
                {
                    int budget=K(manager.Budget2cac,"Shield budget2cac");Need(budget>=0,"Negative Shield budget.");
                    int amount=Protection.ShieldAmount(damage,perHit,mode);Need(amount>=0,"Unsupported negative Shield absorption.");
                    int used=Math.Min(amount,budget);damage=Math.Max(0,unchecked(damage-used));
                    manager=manager with {Budget2cac=budget-used};
                    Emit("ShieldHpAndBudgetWrite",0x14034cc34,latched,damage);
                    Need(manager.Budget2cac>0,"Reached exhausted whole-skill removal and StatUp recompute; no fabricated poststate.");
                    records[0x32]|=2;Emit("OrShieldOutputEvenZeroAbsorption",0x14034cc77);
                }
            }
            Emit("FreshPostShieldFlagsRead",0x14034cc7a,flags,flags);
            Need(damage<=0 || (flags&0x800)==0,"Reached MPShield child.");
            if((flags&0x10)!=0 && K(i.Calculation.Attacker.Raw.Kind1940,"attacker type1940")!=0x40)
            {
                Emit("NpcSlot28OneThenTypeRead",0x14034ce87,after:i.Calculation.Attacker.Raw.Kind1940);
                Need(K(manager.ReflectMin2c40Bits,"Reflect min")==0 && K(manager.ReflectMax2c44Bits,"Reflect max")==0x42800000,
                    "Unverified effective pilot Reflector radii.");
                if(Within(d,a,64f,0x14034ceab) && !Within(d,a,0f,0x14034cec7))
                {
                    int draw=Draw(CallStage.ReflectorDraw,0x14034cedc);
                    Need(K(manager.ReflectChance2c48,"Reflect chance")==1000,"Unverified Reflector chance.");
                    if(draw<=manager.ReflectChance2c48)
                    {
                        Need(K(manager.ReflectPercent2c38,"Reflect percent")==25 && K(manager.ReflectFlat2c3c,"Reflect flat")==60
                            && K(manager.ReflectSkill2c34,"Reflect skill")==16699,"Unverified pilot Reflector values.");
                        reflected=Protection.ReflectionAmount(latched,25,60);
                        if(reflected>0)
                        {Write32(records,0x14,reflected);Write32(records,0x18,16699);records[0x32]|=1;
                         Emit("LatchedReflectionMetadata",0x14034cf23,latched,reflected);
                         Emit("NpcSlot188ReturnsZeroNoSecondQuery",0x14034cf7a,after:0);}
                    }
                }
            }
            if(damage>0)Need((flags&5)==0,"Reached Protect/Convert child at original later flags read.");
        }

        private bool Within(RawActor first,RawActor second,float bound,ulong site)
        {
            ulong w1=K(first.World118,"first world118"),w2=K(second.World118,"second world118");
            if(w1!=w2){Emit("GeometryWorldMismatchSkipsXYZ",site);return false;}
            float dy=Sub(Bits(first.Owner.Telemetry.Position120.Y),Bits(second.Owner.Telemetry.Position120.Y));
            float dx=Sub(Bits(first.Owner.Telemetry.Position120.X),Bits(second.Owner.Telemetry.Position120.X));
            float dz=Sub(Bits(K(first.Z128Bits,"first rawZ")),Bits(K(second.Z128Bits,"second rawZ")));
            float squared=Add(Add(Mul(dy,dy),Mul(dx,dx)),Mul(dz,dz));
            Emit("RawSquaredGeometryMaxThenMin",site,BitConverter.SingleToUInt32Bits(squared),BitConverter.SingleToUInt32Bits(bound));
            return squared<=bound;
        }

        private int Preview(ref RawActor actor,int incoming,string role)
        {
            int predicted=unchecked(actor.Owner.State.Hp778-incoming),maximum=actor.Owner.State.MaxHp774;
            int percent=maximum<1 || predicted<1?0:unchecked((int)((long)predicted*99/maximum)+1);
            percent=percent<1?0:Math.Min(100,percent);
            Emit(role+"HpPreviewOnly",0x140bb86b4,actor.Owner.State.Hp778,percent);
            if(percent==0 && actor.Owner.State.Pending728==0)
            {actor=actor with {Owner=actor.Owner with {State=actor.Owner.State with {Pending728=1}}};
             Emit(role+"Pending728Cas",0x140bb8721,0,1);}
            return percent;
        }

        private void Stagger(bool stop,bool critical)
        {
            if(calculation!.BeginFallback!=0 || damage<=0)
            {Emit("StaggerEarlySkip",0x140bb4e80);return;}
            int kind=i.Calculation.Selected.AdditionalEffect4b0;
            if(kind!=0 && i.Calculation.Skill.Condition480[0x15]!=0 && !StaggerRelation())
            {NaturalStagger(stop,critical);return;}
            if(kind==0){NaturalStagger(stop,critical);return;}
            Need(kind==3 && i.Calculation.Selected.AdditionalEffect4b4==1,"Unadmitted selected additional-effect program.");
            if((K(i.Calculation.Defender.Raw.Abnormal1250,"target abnormal1250")&0x49c040)!=0)
            {Emit("SpinHighLevelAbnormalSkip",0x140bb4e80);return;}
            int raw=K(d.Field1a3c,"target raw1a3c");
            bool active=raw!=0;
            if(!active)
            {
                ulong pointer=K(d.Pointer1a58,"target raw1a58");
                if(pointer!=0)active=K(d.First1a58,"target1a58 first")!=0 || K(d.Field198At1a58,"target1a58 field198")!=0;
            }
            Emit("SpinActualSlot16Calculated",0x140bb4aa6,after:active?1:0);
            if(active)return;
            Evidence(i.Stagger.Evidence);
            Need(K(i.Stagger.SpinObject1428b79a0,"current Spin effect object")!=0 && K(i.Stagger.SpinVtable,"current Spin effect vtable")==0x1412d3aa8,
                "Actual global Spin instance/vtable required; source constructor is not current existence.");
            uint first=K(i.Stagger.Global14276a0cc,"global14276a0cc");
            if(first!=0 && (K(i.Stagger.Global142766278,"global142766278")&4)!=0)
            {Emit("SpinGlobalImmunitySkip",0x140bbbdd0);return;}
            if((K(i.Calculation.Defender.Raw.TemplateFlags3f0,"target template3f0")&8)!=0 || raw!=0)
            {Emit("SpinTemplateOrSlot17ImmunitySkip",0x140bbbdd0);return;}
            if((K(d.Template278,"target template278")&(1u<<19))!=0)
            {Emit("SpinClass19ImmunitySkip",0x140bbbdd0);return;}
            int resistance=K(d.Defense824,"target Spin defense824")-K(a.Offense888,"source Spin offense888");
            float pose=(K(i.Calculation.Defender.Raw.Pose13c,"target pose13c")&15)==5?Bits(0x3e99999a):1f;
            int threshold=Math.Clamp(Cvtt(Mul(FromInt(resistance),pose)),0,1000);
            int draw=Draw(CallStage.SpinDraw,0x140bbbfda);
            Emit("SpinClass19Comparison",0x140bbbe80,threshold,draw);
            if(threshold>=draw)return;
            int before=d.Owner.State.Field72c;
            d=d with {Owner=d.Owner with {State=d.Owner.State with {Field72c=1}}};
            Emit("Spin72cExchangeDistinctFromPreview728",0x140bb4ae9,before,1);
            if(before!=0)return;
            WriteU32(header,0,ReadU32(header,0)|0x10);Write32(header,24,1);Write32(header,28,3);
            Emit("SpinNewHeader10",0x140bb4af7,0,0x10);
            // This selected bit enters the heading-only tail, not the move-back0x10a0f branch.
            if(K(d.World118,"Spin target world118")!=0)
            {Write32(header,20,K(i.Calculation.Attacker.Raw.Heading12c,"Spin source heading12c"));Emit("SpinHeaderHeadingWrite",0x140bb52f8);}
        }
        private void NaturalStagger(bool stop,bool critical)
        {
            if(stop || !critical){Emit("NaturalStaggerStopOrNoncriticalSkip",0x140bb5180);return;}
            int group=K(a.Weapon8ec,"source effective weapon8ec");uint mask=1u<<(group&31);
            Need((mask&0x70)==0 && (mask&(1u<<13))==0,"Reached natural weapon stagger RNG/knockback child; no generated skip.");
            Emit("NaturalStaggerOtherWeaponSkip",0x140bb5180,after:group);
        }
        private bool StaggerRelation()
        {
            ReadOnlySpan<byte> descriptor=i.Calculation.Skill.Condition480.AsSpan();
            Need((descriptor[0x14]&0xfc)==0 && descriptor[..20].ToArray().All(x=>x==0),"Only selected direction-only additional-effect half admitted.");
            C.RawActor target=i.Calculation.Defender.Raw,source=i.Calculation.Attacker.Raw;
            if((K(target.TemplateFlags3f0,"stagger template3f0")&4)!=0)return true;
            int heading=K(target.Heading12c,"stagger target heading12c");Need(heading is >=0 and <120,"Stagger heading outside pinned table.");
            float dx=Sub(Bits(K(target.X120Bits,"stagger target X")),Bits(K(source.X120Bits,"stagger source X")));
            float dy=Sub(Bits(K(target.Y124Bits,"stagger target Y")),Bits(K(source.Y124Bits,"stagger source Y")));
            float dot=Add(Mul(dy,Bits(i.Calculation.Tables.HeadingABits[heading])),Mul(dx,Bits(i.Calculation.Tables.HeadingBBits[heading])));
            byte direction=descriptor[0x14];Emit("AdditionalEffectDirectionDot",0x140370ca0,after:BitConverter.SingleToUInt32Bits(dot));
            if(((direction&1)!=0 && dot>0) || ((direction&2)!=0 && dot<=0))return true;
            ulong mask=K(raceMask,"current additional-effect race mask");
            if(mask==0)
            {
                int limit=K(i.Calculation.Globals.RaceLimit14f523f78,"current additional-effect race limit");
                Need(limit<=63,"Additional-effect lazy race loop limit outside finite domain.");
                for(int bit=0;bit<=limit;bit++)mask|=1ul<<bit;
                if(limit>=0)Emit("AdditionalEffectSinglePublishedRaceStore",0x140370e50,0,unchecked((long)mask));
                raceMask=mask;
            }
            return false;
        }

        private StorageResult ProduceStorage(int targetPercent,int sourcePercent)
        {
            byte[] info=i.Storage.InfoBefore948.ToArray(),copy=i.Storage.CopyBefore948.ToArray();
            WriteU32(info,0,d.Owner.Binding.ObjectId);Write32(info,4,0);records.CopyTo(info,8);
            info[0x388]=1;info[0x389]=unchecked((byte)targetPercent);info[0x38a]=unchecked((byte)sourcePercent);
            header.CopyTo(info,0x38c); // info38b and all header/record padding retain their original owned bytes.
            info.AsSpan(0,0x38b).CopyTo(copy); // helper skips info38b, header5..7.
            info.AsSpan(0x38c,5).CopyTo(copy.AsSpan(0x38c));
            info.AsSpan(0x394,32).CopyTo(copy.AsSpan(0x394));
            Emit("SkillDamageInfoComplete948AndSelectiveCopy",0x140340f90);
            byte[] output=i.Storage.ContainerBytes.ToArray();int count=i.Storage.ContainerCount;
            if(count<i.Storage.ContainerCapacity){copy.CopyTo(output,count*948);count++;Emit("ContainerAppendWhole948",0x140731910,count-1,count);}
            else Emit("ContainerFullSilentSkip",0x140731910,count,count);
            int preliminary=calculation!.PreliminaryReturn!.Value;
            int max=Math.Max(i.Storage.PriorMaximum,preliminary),min=Math.Min(i.Storage.PriorMinimum,preliminary);
            Emit("PreliminaryReturnMetricsDistinctFromProtectedRecord",0x140bb88fa,preliminary,damage);
            return new([..header],[..records],[..info],[..copy],[..output],count,max,min);
        }

        private Continuation Retain()
        {
            AllocationInput x=i.Allocation;Bind(x.AllocationBinding);
            Need(!x.OriginalBytes110.IsDefault && x.OriginalBytes110.Length==0x110,"Complete fresh allocation image required including opaque padding.");
            ulong address=x.AllocationBinding.Address;
            Need(address<=ulong.MaxValue-0x110,"Allocation address overflow.");
            foreach(C.Span span in i.Calculation.Storage.Spans)Disjoint(address,0x110,span.Address,(ulong)span.Bytes);
            Disjoint(address,0x110,i.Storage.WrapperFrameRbp.Address,0xb94);FrameDisjoint(i.Storage.CompletionFrameRbp,address,0x110);
            foreach(C.Span span in i.Storage.OtherOwnedSpans)Disjoint(address,0x110,span.Address,(ulong)span.Bytes);
            retainedBytes=x.OriginalBytes110.ToArray();constructionReleased=false;
            Write64(retainedBytes,0,0x141130d20);Write32(retainedBytes,8,1);
            uint construction=Tick(CallStage.ConstructionTick,0x14002aa0a);WriteU32(retainedBytes,0x68,construction);
            Write64(retainedBytes,0x48,0x140d02fc0);
            Write64(retainedBytes,0xc,0);Write32(retainedBytes,0x14,0);Write64(retainedBytes,0x50,0);
            Write64(retainedBytes,0x18,0);Write32(retainedBytes,0x20,0);
            uint modulus1=K(x.Modulus142726bd0,"CIO modulus142726bd0");Need(modulus1!=0,"CIO division by zero unsupported.");
            Write64(retainedBytes,0x28,0);
            uint narrowed=unchecked((uint)(address>>5));
            BinaryPrimitives.WriteUInt16LittleEndian(retainedBytes.AsSpan(0x62,2),unchecked((ushort)(narrowed%modulus1)));
            Write32(retainedBytes,0x30,0);
            uint modulus2=K(x.Modulus142726bd4,"CIO modulus142726bd4");Need(modulus2!=0,"CIO division by zero unsupported.");
            Write64(retainedBytes,0x38,0);Write32(retainedBytes,0x40,0);Write64(retainedBytes,0x58,0);
            BinaryPrimitives.WriteUInt16LittleEndian(retainedBytes.AsSpan(0x60,2),0);
            BinaryPrimitives.WriteUInt16LittleEndian(retainedBytes.AsSpan(0x64,2),unchecked((ushort)(narrowed%modulus2)));
            Write64(retainedBytes,0,0x1412eddb8);
            retainedBytes.AsSpan(0x88,5).Clear();retainedBytes.AsSpan(0x90,12).Clear();Write64(retainedBytes,0xa0,1);Write64(retainedBytes,0xa8,0);
            Write64(retainedBytes,0xe8,0);BinaryPrimitives.WriteUInt16LittleEndian(retainedBytes.AsSpan(0xf0,2),0);
            Write32(retainedBytes,0xf4,K(i.RunInfo40,"RunInfo40"));Write64(retainedBytes,0xf8,0);Write64(retainedBytes,0x100,0);
            RetainActor(ref a,"DelayedSource",0x140bb5a59);Write64(retainedBytes,0x70,a.Address);
            RetainActor(ref d,"DelayedTarget",0x140bb5a7f);Write64(retainedBytes,0x78,d.Address);Write64(retainedBytes,0x80,0);
            header.CopyTo(retainedBytes,0x88);
            Write32(retainedBytes,0xb0,1);Write32(retainedBytes,0xb4,damage);Write32(retainedBytes,0xb8,0);Write32(retainedBytes,0xbc,threat);
            Write32(retainedBytes,0xc0,i.Calculation.Selected.Id);
            foreach(int offset in new[]{0xc4,0xc8,0xcc})Write32(retainedBytes,offset,0);
            Write32(retainedBytes,0xd0,reflected);Write32(retainedBytes,0xd4,reflected==0?0:16699);
            foreach(int offset in new[]{0xd8,0xdc,0xe0})Write32(retainedBytes,offset,0);
            ulong itemMask=K(a.ItemMask6c8,"retained source item mask6c8");Write64(retainedBytes,0xf8,itemMask);Write64(retainedBytes,0x100,0);
            Need(itemMask==0,"Reached retained equipped-item snapshot child.");
            uint flags=ReadU32(header,0);int phase=(flags&0x10a0f)!=0 && (flags&0x400)==0?1:0;
            Write32(retainedBytes,0x108,phase);phaseAfter=phase;
            Need((K(x.TypeNameInitialized15486eff0,"type-name current initialized")&1)!=0,
                "Reached type-name/global registration initialization requires its full child.");
            int delay=Math.Max(100,K(i.RunInfo78,"RunInfo78")-50);
            if(phase!=0)delay-=300;
            Need(phase==0,"Reached retained initial movement prepass, outside complete finite ordinary header domain.");
            Need((K(x.CallerInitialized15486eff8,"caller initialized")&1)!=0,"Reached caller registration initialization.");
            uint id=K(x.CallerId15486effc,"actual caller ID");
            if(id is >=1 and <=512){ulong counter=K(x.CallerCounter,"current caller counter");callerCounter=unchecked(counter+1);Emit("CallerCounterIncrement",0x140bb8bc8,unchecked((long)counter),unchecked((long)callerCounter.Value));}
            DelayedScheduler q=x.Scheduler;Evidence(q.Evidence);delayedScheduler=q;
            uint entry=Tick(CallStage.TimerEntry,0x140d01b02);
            Need(K(q.Closing,"timer closing")==0,"Reached closed timer service; no enqueue or completed owner.");
            Write32(retainedBytes,0x10,1);Write32(retainedBytes,8,2);Emit("TimerPendingAndObjectRetain",0x140d01c26,1,2);
            uint due=unchecked(Tick(CallStage.TimerDue,0x140d01c29)+(uint)delay);
            uint queueCount=K(q.QueueCount,"timer queue count");Need(queueCount is >0 and <=1024 && !q.Queues.IsDefault && q.Queues.Length==queueCount,"Complete bounded timer queue set required.");
            uint index=K(q.Rotation,"timer rotation")?unchecked(K(q.TlsIndex,"timer TLS index")+1)%queueCount:0;
            Need(q.Queues.Select(t=>t.Index).Distinct().Count()==q.Queues.Length && q.Queues.All(t=>t.Index<queueCount),"Duplicate/missing timer queue indices.");
            foreach(TimerQueue queue in q.Queues)
            {Need(!queue.Heap.IsDefault,"Unknown current heap.");for(int n=1;n<queue.Heap.Length;n++)Need(unchecked((int)(queue.Heap[(n-1)/2].Due-queue.Heap[n].Due))<=0,"Malformed wrapped timer heap.");}
            uint sleep=K(q.SleepMilliseconds,"timer sleep");delayedScheduler=q with {TlsIndex=index,SleepMilliseconds=0};
            Need(sleep==0,"Reached scheduler Sleep; its host timing is outside this raw fixture mode.");
            var chosen=q.Queues.Single(t=>t.Index==index);var heap=chosen.Heap.ToList();
            var item=new TimerTuple(due,address,0,0,construction);heap.Add(item);int child=heap.Count-1;
            while(child>0)
            {int parent=(child-1)/2;if(unchecked((int)(heap[parent].Due-item.Due))<=0)break;
             heap[child]=heap[parent];child=parent;}
            heap[child]=item;
            TimerQueue updated=chosen with {MinimumWake=Math.Min(chosen.MinimumWake,due),Heap=[..heap]};
            delayedScheduler=delayedScheduler with {Queues=q.Queues.Select(t=>t.Index==index?updated:t).ToImmutableArray(),
                AccumulatedDelay=unchecked(K(q.AccumulatedDelay,"timer accumulated delay")+(ulong)delay),EnqueueCount=unchecked(K(q.EnqueueCount,"timer enqueue count")+1)};
            Emit("OriginalHeapInsertStrictWrappedTies",0x140d0f110,delay,due);
            if(due<K(q.GlobalNextWake,"timer global next wake"))Emit("TimerWakeRequest",0x140d01d9c,after:due);
            uint last=Tick(CallStage.TimerExit,0x140d01da2);
            if(unchecked(entry+1000)<last)Emit("TimerSlowLogNormalReturnDescriptor",0x140d01dbc,entry,last);
            Write32(retainedBytes,8,1);constructionReleased=true;Emit("WrapperConstructionReferenceRelease",0x140bb8c08,2,1);
            PreparedContinuation=new(i,[..retainedBytes],storage!,delayedScheduler,damage,reflected,threat,due);
            return Continuation.CreateFromReplay(this);
        }
        private void RetainActor(ref RawActor actor,string reason,ulong site)
        {
            N.References refs=actor.Owner.References;Need(refs.Count8>0 && refs.Count8<int.MaxValue && refs.History14==0,"Actor retain/history/destruction frontier.");
            actor=actor with {Owner=actor.Owner with {References=refs with {Count8=refs.Count8+1}}};
            Emit(reason+"ReferenceRetain",site,refs.Count8,refs.Count8+1);
        }

        internal static NativeSync EncodeSync(uint source,uint target,int hop,int oldHp,int cell,byte[] action,int state,ushort skill,N.TransportEffects transport)
        {
            uint flags=ReadU32(action,0);int size=(flags&0x10a0f)!=0?49:(flags&0x10010)!=0?38:33;
            byte[] bytes=new byte[size];BinaryPrimitives.WriteUInt16LittleEndian(bytes,checked((ushort)size));
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2),0x12);bytes[4]=0xad;BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(5),0xffed);
            WriteU32(bytes,7,source);WriteU32(bytes,11,target);Write32(bytes,15,hop);Write32(bytes,19,oldHp);Write32(bytes,23,cell);WriteU32(bytes,27,flags);
            int offset=31;
            if(size==49){action.AsSpan(8,12).CopyTo(bytes.AsSpan(31));Write32(bytes,43,state);offset=47;}
            else if(size==38){bytes[31]=action[20];Write32(bytes,32,state);offset=36;}
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset),skill);
            return new([..bytes],source,state,oldHp,cell,transport);
        }

        private void Evidence(C.Evidence evidence)
        {
            Need(evidence is not null && evidence.MainSha256==P.MainSha256 && evidence.PackageContext==P.PackageContext
                && evidence.Operation==i.Calculation.Context.Operation && evidence.Epoch==i.Calculation.Context.Epoch
                && evidence.Origin is C.Origin.MathematicalFixture or C.Origin.SourceBoundDeclaration or C.Origin.ObservedOriginal
                && !string.IsNullOrWhiteSpace(evidence.Receipt),"Unknown/mismatched current provenance.");
        }
        private void Bind(C.Binding binding){Need(binding is not null && binding.Address!=0 && !string.IsNullOrWhiteSpace(binding.Identity),"Missing storage binding.");Evidence(binding.Evidence);}
        private int Draw(CallStage stage,ulong site)
        {
            Need(cursor<i.OriginalCalls.Length,"Missing reached original draw.");OriginalCall call=i.OriginalCalls[cursor++];Evidence(call.Evidence);
            Need(call.Stage==stage && call.Site==site && call.Minimum==1 && call.Maximum==1000 && call.Value is >=1 and <=1000,
                "Original draw stage/site/range/order mismatch.");consumed.Add(call);Emit("OriginalDraw:"+stage,site,after:call.Value);return (int)call.Value;
        }
        private uint Tick(CallStage stage,ulong site)
        {
            Need(cursor<i.OriginalCalls.Length,"Missing original tick.");OriginalCall call=i.OriginalCalls[cursor++];Evidence(call.Evidence);
            Need(call.Stage==stage && call.Site==site && call.Minimum==0 && call.Maximum==0,"Wrong original tick stage/site/order.");
            consumed.Add(call);Emit("OriginalTick:"+stage,site,after:call.Value);return call.Value;
        }
        private void ValidateBegin()
        {
            Need(i.DiagnosticsDisabled==true && i.SerializedNoInlineWriter==true,"Diagnostic-off serialized no-inline-writer interval required.");
            Need(i.Calculation.Entry==C.Entry.TargetCheckThenBeginAndCalculate,"Actual selected target-check/Begin producer required.");
            Need(K(i.RunInfo40,"RunInfo40") is 0 or 2 && K(i.Proxy50,"proxy50")==0 && K(i.CurrentSkill4d4,"Skill4d4")==0,
                "Only actual mode0/2, null proxy and ordinary separate effect-level word1 are admitted.");
            Need(!i.OriginalCalls.IsDefault,"Unknown primitive receipt list.");
            Storage s=i.Storage;Bind(s.WrapperFrameRbp);Bind(s.Header);Bind(s.Records);Bind(s.Info);Bind(s.Copy);Bind(s.CompletionFrameRbp);
            ulong rbp=s.WrapperFrameRbp.Address;
            Need(rbp<=ulong.MaxValue-0xb94 && s.Header.Address==rbp && s.Records.Address==rbp+0xa0
                && s.Info.Address==rbp+0x420 && s.Copy.Address==rbp+0x7e0 && s.CompleteSourceAndDestinationAliases==true,
                "Original wrapper header/records/info/copy producer layout must agree.");
            Need(!s.HeaderBefore40.IsDefault && s.HeaderBefore40.Length==40 && !s.RecordsBefore896.IsDefault && s.RecordsBefore896.Length==896
                && !s.InfoBefore948.IsDefault && s.InfoBefore948.Length==948 && !s.CopyBefore948.IsDefault && s.CopyBefore948.Length==948,
                "All original initialized and opaque source/destination bytes required.");
            Need(s.ContainerCount>=0 && s.ContainerCapacity>=s.ContainerCount && s.ContainerCapacity<=1024
                && !s.ContainerBytes.IsDefault && s.ContainerBytes.Length==s.ContainerCapacity*948,"Owned complete bounded output container required.");
            Need(i.Calculation.Storage.Header.Address==rbp && i.Calculation.Storage.Record.Address==s.Records.Address,
                "Calculator views must be actual wrapper header4/record0 subviews.");
            Need(!i.Calculation.Storage.Spans.IsDefault,"Unknown complete calculator-owned spans.");
            FrameDisjoint(s.CompletionFrameRbp,rbp,0xb94);
            foreach(N.Manager current in new[]{primary.Guards.Manager,reflection.Guards.Manager})
                if(current.Presence==N.ManagerPresence.Present)
                {Disjoint(rbp,0xb94,current.Pointer158,0x5000);FrameDisjoint(s.CompletionFrameRbp,current.Pointer158,0x5000);}
            foreach(C.Span span in i.Calculation.Storage.Spans)
                if(span.Role is not(C.SpanRole.Header or C.SpanRole.Record or C.SpanRole.Condition))
                {Disjoint(rbp,0xb94,span.Address,(ulong)span.Bytes);FrameDisjoint(s.CompletionFrameRbp,span.Address,(ulong)span.Bytes);}
            Need(!s.OtherOwnedSpans.IsDefault,"Unknown other effect/ref/timer allocations.");
            foreach(C.Span span in s.OtherOwnedSpans)
            {Evidence(span.Evidence);Need(span.Bytes>0,"Malformed additional owned storage.");Disjoint(rbp,0xb94,span.Address,(ulong)span.Bytes);
             FrameDisjoint(s.CompletionFrameRbp,span.Address,(ulong)span.Bytes);}
            ActorJoin(a,i.Calculation.Attacker,primary.Identity);ActorJoin(d,i.Calculation.Defender,primary.Identity);
            Need(a.Owner.State.Hp778>0 && d.Owner.State.Hp778>0 && a.Owner.State.Pending728 is 0 or 1 && d.Owner.State.Pending728 is 0 or 1,
                "Actual alive source/receiver and supported current preview cells required.");
            Need(primary.Identity.Operation==i.Calculation.Context.Operation && primary.Identity.Epoch==i.Calculation.Context.Epoch,
                "Current HP owner interval does not join the calculator interval.");
            Need(primary.Guards.Abnormal1250==i.Calculation.Defender.Raw.Abnormal1250
                && reflection.Guards.Abnormal1250==i.Calculation.Attacker.Raw.Abnormal1250,
                "Numeric and HP-owner raw abnormal1250 images must agree for each actor.");
            Need(reflection.Identity==primary.Identity,"Both completion contexts share original operation/epoch identity.");
            Need(primary.Guards.Manager.Generation==d.Owner.Binding.ManagerGeneration && reflection.Guards.Manager.Generation==a.Owner.Binding.ManagerGeneration,
                "Actual per-actor manager generations required.");
            ManagerJoin(primary.Guards.Manager,K(i.Calculation.Defender.Manager.CurrentPointer158,"defender current manager158"));
            ManagerJoin(reflection.Guards.Manager,K(i.Calculation.Attacker.Manager.CurrentPointer158,"source current manager158"));
        }
        private static void ManagerJoin(N.Manager manager,ulong pointer)=>Need(manager.Pointer158==pointer
            && manager.Presence==(pointer==0?N.ManagerPresence.VerifiedNull:N.ManagerPresence.Present),
            "Numeric and HP-owner current manager158/presence images disagree.");
        private void ActorJoin(RawActor actor,C.Actor numeric,N.Identity identity)
        {
            Need(actor.Owner is not null && actor.Address==numeric.Current.Address && actor.StorageIdentity==numeric.Current.Identity
                && actor.Owner.Binding.TemplateId==numeric.NpcId && actor.Owner.Binding.LifeGeneration==numeric.LifeGeneration
                && actor.Owner.Binding.Type144==2 && actor.Owner.Binding.Vtable==N.NpcVtable
                && actor.Owner.Binding.Operation==identity.Operation && actor.Owner.Binding.Epoch==identity.Epoch,
                "Numeric/HP actor pointer/source/generation/operation join mismatch.");
            Need(actor.Owner.Telemetry.Position120.X==numeric.Raw.X120Bits && actor.Owner.Telemetry.Position120.Y==numeric.Raw.Y124Bits
                && actor.Owner.Telemetry.Level1492==numeric.Raw.Level1492,"Original scalar/HP raw coordinate and level images disagree.");
            Need(actor.Z128Bits is null || actor.Z128Bits==actor.Owner.Telemetry.Position120.Z,"Known raw geometry Z disagrees with the same owned HP actor image.");
            Need(actor.Owner.State.Hp778>0 && actor.Owner.State.Hp778<=actor.Owner.State.MaxHp774 && actor.Owner.State.Mp790>=0 && actor.Owner.State.MaxMp78c>=0,
                "Ordinary current actor life image outside supported domain.");
            Need(actor.Owner.State.Master188c==0 && actor.Owner.State.Related1894==0 && actor.Owner.State.Pointer1850==0,
                "Independent ordinary ownerless HP actor domain.");
        }
        private static void Disjoint(ulong x,ulong nx,ulong y,ulong ny)
        {Need(x!=0 && y!=0 && x<=ulong.MaxValue-nx && y<=ulong.MaxValue-ny && (x+nx<=y || y+ny<=x),"Overlapping/overflow owned native storage.");}
        private static void FrameDisjoint(C.Binding frame,ulong address,ulong length)
        {Need(frame.Address>=0xa0 && frame.Address<=ulong.MaxValue-0x838,"Completion frame address outside supported range.");Disjoint(frame.Address-0xa0,0x8d8,address,length);}
        private void ValidateTimer(TimerInput t)
        {
            Need(t.DiagnosticsDisabled==true && t.SerializedNoInlineWriter==true && t.DispatchOwnsActualRetainedObject==true,
                "Original dispatch/current serialized interval required; enqueue is not a dispatch receipt.");
            Need(t.Dispatch is not null && t.Dispatch.MainSha256==P.MainSha256 && t.Dispatch.PackageContext==P.PackageContext
                && t.Dispatch.Operation==i.Calculation.Context.Operation && !string.IsNullOrWhiteSpace(t.Dispatch.Epoch)
                && t.Dispatch.Origin is C.Origin.MathematicalFixture or C.Origin.SourceBoundDeclaration or C.Origin.ObservedOriginal,
                "Original current dispatch identity/provenance missing.");
            Need(a.Address==i.Attacker.Address && d.Address==i.Defender.Address && a.Owner.Binding.ObjectId==t.Retained.SourceId
                && d.Owner.Binding.ObjectId==t.Retained.TargetId && a.Owner.Binding.LifeGeneration==i.Attacker.Owner.Binding.LifeGeneration
                && d.Owner.Binding.LifeGeneration==i.Defender.Owner.Binding.LifeGeneration,
                "Retained objects and fresh actor life generations must agree.");
            foreach(RawActor current in new[]{a,d})
                Need(current.Owner.Binding.Type144==2 && current.Owner.Binding.Vtable==N.NpcVtable
                    && current.Owner.State.Master188c==0 && current.Owner.State.Related1894==0 && current.Owner.State.Pointer1850==0,
                    "Fresh timer receiver/source must remain actual ordinary ownerless NPC objects.");
            Need(primary.Identity.Operation==t.Dispatch.Operation && primary.Identity.Epoch==t.Dispatch.Epoch
                && reflection.Identity==primary.Identity && a.Owner.Binding.Operation==primary.Identity.Operation
                && d.Owner.Binding.Operation==primary.Identity.Operation && a.Owner.Binding.Epoch==primary.Identity.Epoch
                && d.Owner.Binding.Epoch==primary.Identity.Epoch,"Fresh timer actor/HP interval must join dispatch.");
            Need(primary.Guards.Manager.Generation==d.Owner.Binding.ManagerGeneration
                && reflection.Guards.Manager.Generation==a.Owner.Binding.ManagerGeneration,
                "Fresh timer manager generations must bind the actual current receiver/source.");
            Need(!t.OriginalObjectBytes.IsDefault && t.OriginalObjectBytes.Length==0x110
                && t.OriginalObjectBytes.AsSpan(0x70,0x98).SequenceEqual(t.Retained.ObjectBytes.AsSpan(0x70,0x98)),
                "Current retained payload must bind all original pointers/header/scalars, not a new damage tape.");
            Need(ReadI32(t.OriginalObjectBytes.AsSpan(),0x108)==K(t.Phase108,"phase108")
                && ReadI32(t.OriginalObjectBytes.AsSpan(),8)==K(t.References8,"references8")
                && ReadI32(t.OriginalObjectBytes.AsSpan(),0x10)==K(t.Pending10,"pending10")
                && t.References8>0 && t.Pending10>=0,"Actual original dispatch reference and phase cells required.");
            Need(t.CompletionFrameRbp.Address>=0xa0 && t.CompletionFrameRbp.Address<=ulong.MaxValue-0x838,
                "Invalid fresh completion frame.");
            Need(t.CompletionFrameRbp.Evidence.Operation==primary.Identity.Operation && t.CompletionFrameRbp.Evidence.Epoch==primary.Identity.Epoch,
                "Fresh completion frame/dispatch interval mismatch.");
            FrameDisjoint(t.CompletionFrameRbp,a.Address,0x2000);FrameDisjoint(t.CompletionFrameRbp,d.Address,0x2000);
            FrameDisjoint(t.CompletionFrameRbp,i.Allocation.AllocationBinding.Address,0x110);
            Need(t.CompleteCurrentOwnerStorageAliases==true && !t.CurrentOtherOwnedSpans.IsDefault,"Unknown complete fresh frame/manager/effect/ref/timer aliases.");
            foreach(N.Manager current in new[]{primary.Guards.Manager,reflection.Guards.Manager})
                if(current.Presence==N.ManagerPresence.Present)FrameDisjoint(t.CompletionFrameRbp,current.Pointer158,0x5000);
            foreach(C.Span span in t.CurrentOtherOwnedSpans)
            {Need(span.Bytes>0 && span.Evidence.Operation==primary.Identity.Operation && span.Evidence.Epoch==primary.Identity.Epoch,
                "Malformed/stale fresh owned storage.");FrameDisjoint(t.CompletionFrameRbp,span.Address,(ulong)span.Bytes);}
        }

        private void CompleteOwner()
        {
            uint flags=ReadU32(header,0);
            if(damage!=0)PreAction();
            Need((flags&0x10a17)==0,"Reached completion movement/system-effect child.");
            int oldHp=d.Owner.State.Hp778;Emit("OldTargetHpReadBeforeApplication",0x140bb54f2,after:oldHp);
            Emit("PrimaryHpOwnerCallBegin",0x140bb5535);
            N.SkillEvaluation first=Owner(primary,a.Owner,d.Owner,damage,0,threat,true);
            if(first.Complete is null)
            {AddOwnerEvents(first.KnownEvents,true);ownerPrefix=first.KnownPrefixState;ownerCell=first.KnownApplicationCell;
             Need(false,"Primary HP owner reached unsupported child: "+first.Unsupported?.Stage+":"+first.Unsupported?.Reason);}
            primaryResult=first.Complete;AddOwnerEvents(primaryResult.Events,true);
            N.State state=primaryResult.Calculated!;
            d=d with {Owner=d.Owner with {State=state.Receiver,References=state.ReceiverReferences}};
            a=a with {Owner=a.Owner with {References=state.AttackerReferences}};
            primary=primary with {Attribution=state.Attribution,Scheduler=state.Scheduler};
            Emit("PrimaryRealSkillHpOwnerReturned",0x140bb5535,damage,primaryResult.ApplicationCell);
            int getter=K(d.State19f8,"post-primary actual slot87/raw19f8");
            packet=EncodeSync(a.Owner.Binding.ObjectId,d.Owner.Binding.ObjectId,threat,oldHp,primaryResult.ApplicationCell!.Value,
                header,getter,unchecked((ushort)i.Calculation.Selected.Id),Send(primary.Transport,a.Owner.Binding.ObjectId));
            Emit("SourceAttackSyncAfterPrimaryCellRewrite",0x140bb5562,after:getter);
            if(reflected!=0)
            {
                reflection=reflection with {Scheduler=state.Scheduler};
                Emit("ReflectionHpOwnerCallBegin",0x140bb55ca);
                N.SkillEvaluation second=Owner(reflection,d.Owner,a.Owner,reflected,0,unchecked(reflected*10+2),false);
                if(second.Complete is null)
                {AddOwnerEvents(second.KnownEvents,false);ownerPrefix=second.KnownPrefixState;ownerCell=second.KnownApplicationCell;
                 Need(false,"Reflected HP owner reached unsupported child: "+second.Unsupported?.Stage+":"+second.Unsupported?.Reason);}
                reflectionResult=second.Complete;AddOwnerEvents(reflectionResult.Events,false);
                N.State reversed=reflectionResult.Calculated!;
                a=a with {Owner=a.Owner with {State=reversed.Receiver,References=reversed.ReceiverReferences}};
                d=d with {Owner=d.Owner with {References=reversed.AttackerReferences}};
                reflection=reflection with {Attribution=reversed.Attribution,Scheduler=reversed.Scheduler};
                ExplicitSourceSendHp();Emit("ReflectedActualHpThenExplicitSendHp",0x140bb55d7,reflected,reflectionResult.ApplicationCell);
            }
            Emit("GiveProtectedDamageComputedZeroIdSkip",0x1403734f0,after:0);
            if(reflection.Guards.Manager.Presence==N.ManagerPresence.Present)
                Need(K(a.ReactiveCounter,"source manager43d0")==0,"Reached nonempty source reactive manager34e770.");
            else Need(reflection.Guards.Manager.Presence==N.ManagerPresence.VerifiedNull,"Unknown source manager presence.");
            Emit("SourceReactiveCurrentCounterSkip",0x14034e770);
            Need(K(a.ItemMask6c8,"current source item mask6c8")==0,"Reached source item-proc/enchant program.");
            Emit("ItemProcActualMaskZeroSkip",0x140372490);
            if(primary.Guards.Manager.Presence==N.ManagerPresence.Present)
                Need(K(d.ReactiveCounter,"target manager class1 counter43c4")==0,"Reached nonempty target reactive manager34ebb0.");
            else Need(primary.Guards.Manager.Presence==N.ManagerPresence.VerifiedNull,"Unknown target manager presence.");
            Emit("TargetReactiveCurrentCounterSkip",0x14034ebb0);
        }
        private void AddOwnerEvents(IEnumerable<N.OwnerEvent> values,bool first)
        {
            N.OwnerEvent[] copy=values.ToArray();ownerBatches.Add(new(first,ownerEvents.Count,copy.Length,primary.Identity.Operation,primary.Identity.Epoch));
            ownerEvents.AddRange(copy);
        }
        private N.SkillEvaluation Owner(OwnerEnvironment env,N.Actor attacker,N.Actor receiver,int hp,int mp,int hop,bool first)
        {
            ulong world=K(first?d.World118:a.World118,"reached HP-owner world118");
            Need(world!=0 && env.WorldPointer118==world,"Reached NPC HP world must bind the actual nonnull receiver118 and vtable/map image.");
            C.Binding frame=timer?.CompletionFrameRbp??i.Storage.CompletionFrameRbp;
            ulong headerAddress=first?(timer is null?i.Storage.Header.Address:i.Allocation.AllocationBinding.Address+0x88):0;
            var evidence=new N.Evidence(N.FactKind.SourceBoundDeclaration,"private bb5370 producer bound to "+Proposal);
            var cell=new N.ApplicationCellBinding(N.CellStorage.PhysicalSkillCompletionApplicationCell,
                first?i.Storage.Records.Identity:"retained-reflection-aggregate", "bb5370:"+(frame.Address+(first?0x828ul:0x830ul)).ToString("x"),
                env.Identity.Operation,env.Identity.Epoch,true,true,0x140bb5370,SkillSource,
                first?N.ApplicationCellRole.PrimaryPhysicalSkillCompletionLocal:N.ApplicationCellRole.RetaliationPhysicalSkillCompletionLocal,evidence);
            var invocation=new N.Invocation(hp,mp,hop,1,0,first?0:1,0,first?2:9,first?unchecked((uint)i.Calculation.Selected.Id):0,0xc5,0,0,cell);
            PreparedOwner=new(attacker,receiver,invocation,frame.Address,headerAddress,first?[..header]:[],env.Identity.Operation,env.Identity.Epoch);
            var proof=OwnerCall.CreateFromReplay(this);
            var scope=RetailNpcNormalScope.Create(P.MainSha256,RetailNpcNormalActorKind.OrdinaryOwnerlessNpc,
                RetailNpcNormalActorKind.OrdinaryOwnerlessNpc,attacker.Binding.TemplateId,receiver.Binding.TemplateId);
            Need(!env.OriginalTicks.IsDefault,"Unknown reached HP-owner tick receipts.");
            var action=new N.ActionInfo(first?N.ActionPresence.Present:N.ActionPresence.VerifiedNull,first?ReadU32(header,0):null,
                new(N.FactKind.SourceBoundDeclaration,"actual private bb5370 header0 aliases owned header40; reflection0 is null"));
            CoreGuards sourceGuards=env.Guards;
            var guards=new N.Guards(sourceGuards.Abnormal1250,sourceGuards.CurrentCast131c,sourceGuards.Systems12cc,sourceGuards.Byte1425,
                sourceGuards.Manager,sourceGuards.Bound160,sourceGuards.Bound210,sourceGuards.Bound168,action,sourceGuards.Evidence);
            return N.EvaluatePhysicalSkill(new(scope,env.Identity,attacker,receiver,env.World,invocation,guards,env.Attribution,
                env.Reward,env.Scheduler,env.Transport,env.Floating,env.Infrastructure,env.OriginalTicks),proof);
        }
        private void PreAction()
        {
            byte mode=K(primary.Guards.Byte1425,"pre-action byte1425");Emit("PreActionActualSlot161",0x14037bcc0,after:mode);
            if(mode is 1 or 3)return;
            N.BoundSkill bound=primary.Guards.Bound168;ulong pointer=K(bound.Pointer,"pre-action bound168");
            int group=K(bound.Group,"pre-action bound126c");N.Manager m=primary.Guards.Manager;
            Need(m.Presence is N.ManagerPresence.VerifiedNull or N.ManagerPresence.Present,"Unknown pre-action manager.");
            if(pointer!=0 && m.Presence==N.ManagerPresence.Present)
            {
                Need(group is >=0 and <8,"Unsupported pre-action group index.");
                var groups=m.Groups.Where(x=>x.Index==group).ToArray();Need(groups.Length==1 && groups[0].Complete,"Incomplete pre-action group.");
                N.EffectRecord? first=groups[0].Records.FirstOrDefault(x=>x.SkillPointer==pointer);
                Need(first is null || first.Level4c==0,"Reached pre-action active End/removal before HP.");
            }
            Emit("PreActionNoEndLookup",0x1400b2a00,after:group);
        }
        private N.TransportEffects Send(N.Transport transport,uint routingId)
        {
            Need(transport.Evidence.Kind is N.FactKind.SourceBoundDeclaration or N.FactKind.ObservedOriginal,"Unknown sync transport interval.");
            int slot=(int)(routingId&7);var sockets=transport.Sockets.Where(x=>x.Slot==slot).ToArray();Need(sockets.Length==1,"Missing/ambiguous original sync socket.");
            N.NpcSocket socket=sockets[0];Need(socket.Presence is N.SocketPresence.VerifiedNull or N.SocketPresence.Present,"Unknown socket presence.");
            if(socket.Presence==N.SocketPresence.VerifiedNull)return new(slot,0,N.TransportDisposition.NoSocketReleased,true,0,0,null);
            int refs=K(socket.ReferenceCount8,"socket reference8");Need(refs>1 && refs<int.MaxValue && socket.History14==0,"Socket reference/destruction/history frontier.");
            int status=K(socket.State414,"socket state414");
            if(status==2)return new(slot,1,N.TransportDisposition.ClosedReleased,true,1,1,refs);
            Need(socket.OpenOutcome is N.OpenSendOutcome.NormalQueuedOrSent or N.OpenSendOutcome.NormalSendError,"Unknown original send normal-return disposition.");
            return new(slot,1,socket.OpenOutcome==N.OpenSendOutcome.NormalSendError?N.TransportDisposition.SendError:N.TransportDisposition.QueuedOrSent,null,1,1,null);
        }
        private void ExplicitSourceSendHp()
        {
            if(a.Owner.State.Pending728==0){_ = Send(reflection.Transport,a.Owner.Binding.ObjectId);Emit("ExplicitSourceHpSync",0x140b22aa0,after:a.Owner.State.Hp778);}
            a=a with {Owner=a.Owner with {State=a.Owner.State with {Dirty770=a.Owner.State.Dirty770&~1u}}};
            Need(a.Owner.State.Master188c==0,"Reached source master HP forwarding.");Emit("ExplicitSourceHpDirtyClear",0x140b22c53);
        }
    }
    // These immutable value records are not producer capabilities. A factory accepts only
    // the inaccessible sealed Replay instance, never one of these records or caller fields.
    internal sealed record OwnerPreparation(N.Actor Attacker,N.Actor Receiver,N.Invocation Call,ulong FrameRbp,
        ulong HeaderAddress,ImmutableArray<byte> Header,string Operation,string Epoch);
    internal sealed record ContinuationPreparation(Input Origin,ImmutableArray<byte> Bytes,StorageResult Storage,
        DelayedScheduler Scheduler,int Damage,int Reflected,int Threat,uint Due);
    private sealed class Boundary(string message):Exception(message);
    private static void Need([DoesNotReturnIf(false)] bool value,string why){if(!value)throw new Boundary(why);}
    private static T K<T>(T? value,string name) where T:struct
    {Need(value.HasValue,"Unknown reached raw input: "+name);return value.GetValueOrDefault();}
    private static uint ReadU32(ReadOnlySpan<byte> bytes,int offset)=>BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset,4));
    private static int ReadI32(ReadOnlySpan<byte> bytes,int offset)=>BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset,4));
    private static void Write32(Span<byte> bytes,int offset,int value)=>BinaryPrimitives.WriteInt32LittleEndian(bytes.Slice(offset,4),value);
    private static void WriteU32(Span<byte> bytes,int offset,uint value)=>BinaryPrimitives.WriteUInt32LittleEndian(bytes.Slice(offset,4),value);
    private static void Write64(Span<byte> bytes,int offset,ulong value)=>BinaryPrimitives.WriteUInt64LittleEndian(bytes.Slice(offset,8),value);
    private static float Bits(uint value)=>Normal(BitConverter.UInt32BitsToSingle(value));
    private static float Normal(float value)
    {uint bits=BitConverter.SingleToUInt32Bits(value)&0x7fffffff;Need(bits<0x7f800000 && (bits==0 || bits>=0x00800000),"Nonfinite/subnormal float input/intermediate.");return value;}
    private static int Cvtt(float value){Normal(value);Need(value>=-2147483648f && value<2147483648f,"CVTT32 outside representable range.");return (int)value;}
    [MethodImpl(MethodImplOptions.NoInlining)]private static float FromInt(int value)=>Normal(value);
    [MethodImpl(MethodImplOptions.NoInlining)]private static float Add(float a,float b)=>Normal(a+b);
    [MethodImpl(MethodImplOptions.NoInlining)]private static float Sub(float a,float b)=>Normal(a-b);
    [MethodImpl(MethodImplOptions.NoInlining)]private static float Mul(float a,float b)=>Normal(a*b);
    [MethodImpl(MethodImplOptions.NoInlining)]private static float Div(float a,float b)=>Normal(a/b);
}
