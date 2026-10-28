using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Aion.GameServer.Commons.Utils.RetailRandom;
using P = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcPhysicalInstantParameters;
using M = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcToUserPhysicalSkillPveModifier;
using A = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcNormalAttackPreparation;
using N = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcUserNormalInputs;
using U = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcUserPhysicalInputs;
using static Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcUserPhysicalInputs;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Inactive complete admitted source preparation/ordinary first normal record.
/// Computes actual type1 User getters and all admitted children. Outer acquisition, range,
/// protection, delay, transport and HP are separate owners, never passed-success booleans.
/// The output is the pre-HP calculation record, before World's later movement resampling
/// and packed record+33 store. Step sites identify the proved instruction or compound
/// program anchor; Steps are a semantic footprint, not a CPU instruction replay.</summary>
internal static class RetailNpcToUserNormalRecordCalculation
{
    internal enum ClockRole { WorldMovement, DodgeCounter, BlockCounter, ParryCounter, ResistCounter }
    internal sealed record Clock(ClockRole Role,ulong CallerSite,ulong TickSite,uint Value,ulong ActorAddress,
        string Operation,string Epoch,string ActorGeneration,string Receipt);
    internal sealed record Floating(bool? NearestEven,bool? MaskedExceptions,bool? FlushToZero,bool? DenormalsAreZero,bool? NormalReturn);
    internal sealed record Infrastructure(bool? SerializedNoOtherWriter,bool? NoInlineGameplayWriter,bool? NormalInfrastructureReturn);
    internal sealed record Limits(int MaxImages,int MaxBytes,int MaxSteps,int MaxRandomOperations,int MaxRawDraws,long MaxRandomLogicalTraceBytes);
    internal sealed record Boundary(ulong UserActor,ulong WorldFrameRbp,string UserLife,string FrameLife,string Receipt);
    internal sealed record Input(A.Context Context,A.SelectedNormal Selected,Memory Memory,Boundary Boundary,
        RetailRandomTransaction Random,ImmutableArray<Clock> Clocks,Floating Floating,Infrastructure Infrastructure,Limits Limits);
    internal sealed record RandomCall(int Sequence,string Stage,ulong CallerSite,int First,int Second,int ComputedReturn,
        ImmutableArray<RetailRandomDrawTrace> RawDraws,ulong NextGeneration);
    internal sealed record Result(int FinalRecordDamage,byte BaseStatus,bool Critical,int Element,uint Header,
        float FinalRecordFloat,Memory CalculatedMemory,ImmutableArray<byte> RecordBytes,ImmutableArray<byte> RecordKnown,
        ImmutableArray<Step> Steps,ImmutableArray<Clock> ConsumedClocks,ImmutableArray<RandomCall> RandomCalls,
        RetailRandomState? RandomStateAfter,ulong RandomGenerationAfter,long RandomLogicalTraceBytes,
        M.Result? Pve,ImmutableDictionary<string,uint> FloatBits,bool Terminal)
    {
        internal bool CurrentActorObserved=>false;
        internal bool HostEffectsExecuted=>false;
        internal bool WholeWorldAttackCompleted=>false;
        internal bool HpApplied=>false;
        internal bool ActivationReady=>false;
        internal bool GlobalNativeRngOrderProved=>false;
    }
    internal sealed record Outcome(Result? Value,Failure? Failure,RetailRandomTransactionResult? DiagnosticRandomPrefix=null)
    {
        [MemberNotNullWhen(true,nameof(Value))]internal bool Success=>Value is not null&&Failure is null;
        internal bool DiagnosticPrefixCommittable=>false;
    }
    internal static Outcome CalculateFirstRecord(Input input)
    {
        ArgumentNullException.ThrowIfNull(input);Replay? replay=null;
        try{replay=new(input);return new(replay.Run(),null);}
        catch(Domain e){return new(null,e.Failure,replay?.RandomFailure);}
        catch(ArithmeticException e){return new(null,new("arithmetic","normal-record",0,e.Message,true));}
    }
    /// <summary>Only the calculated complete World guard producer can select this entry.
    /// The standalone entry and its preparation/clock semantics remain unchanged.</summary>
    internal static Outcome CalculateFromWorld(RetailNpcUserNormalWorldOwner.GuardAdmission admission)
    {
        ArgumentNullException.ThrowIfNull(admission);Replay? replay=null;
        try{replay=new(admission.CalculationInput,admission);return new(replay.Run(),null);}
        catch(Domain e){return new(null,e.Failure,replay?.RandomFailure);}
        catch(ArithmeticException e){return new(null,new("arithmetic","normal-world-record",0,e.Message,true));}
    }
    private sealed class Replay(Input input,RetailNpcUserNormalWorldOwner.GuardAdmission? world=null)
    {
        private Cursor c=null!;
        private int clockIndex,randomOperations,rawDraws;
        private long traceBytes;
        private RetailRandomState? randomState;
        private ulong randomGeneration=input.Random?.InitialGeneration??0;
        private readonly List<Clock> clocks=[];
        private readonly List<RandomCall> draws=[];
        private readonly Dictionary<string,uint> floats=new(StringComparer.Ordinal);
        internal RetailRandomTransactionResult? RandomFailure{get;private set;}
        private ulong Source=>input.Selected.Current.SourceActor;
        private ulong Template=>input.Selected.Current.SourceTemplate;
        private ulong User=>input.Boundary.UserActor;
        private ulong Frame=>input.Boundary.WorldFrameRbp;
        private ulong Header=>Frame+0x18;
        private ulong Record=>Frame+0x80;
        private ulong SharedActive=>Frame+0x58;
        private ulong SharedCritical=>Frame-0x70;
        private ulong FinalFloat=>Frame-0x20;
        private ulong ElementOutput=>Frame+0x50;
        private float F(string name,float value,ulong site=0)
        {c.Keep(name,value,site);floats.Add(name,BitConverter.SingleToUInt32Bits(value));return value;}
        private byte Pose()=> (byte)(c.U32(User+0x13c,0x14036c7d4,"User.pose13c")&15);
        private uint Ratio(ulong actor,ulong site)
        {
            uint v=c.U32(actor+0x1514,site,"actor.stat_ratio1514");
            Need(v is >=1000 and <=65000,"range","stat_ratio",0,"Finite independently constructed ratio1000..65000 required.");return v;
        }
        internal Result Run()
        {
            Validate();
            if(world is null)A.Apply(c,input.Selected);
            else world.RequireInput(input); // Both hand preparation and outer guards already calculated once.
            // This is the proven calculation interval after outer World guards, not proof of those guards.
            uint sourceId=c.U32(Source+0x70,0x14068f52f,"Npc.object_id70");
            uint targetId=c.U32(User+0x70,0x14068f532,"User.object_id70");
            c.W32(Source+0x13d4,sourceId,0x14068f537,"Npc.attack_source13d4");
            c.W32(Source+0x13d8,targetId,0x14068f53d,"Npc.attack_target13d8");
            c.W32(User+0x13d4,sourceId,0x14068f543,"User.attack_source13d4");
            c.W32(User+0x13d8,c.U32(User+0x70,0x14068f54b,"User.object_id70.late"),0x14068f553,"User.attack_target13d8");
            c.Note(0x14068f55e,"ComputedNoOp","source NPC slot189",value:0);
            byte movement=U.Movement(c,User,ClockValue(ClockRole.WorldMovement,0x14068f571,0x140578ec9),"World.User.slot189");
            c.External(0x142766278,4,"normal-world-debug");
            Need((c.U32(0x142766278,0x14068f577,"World.debug_mask")&4)==0,"incomplete-child","World.debug/Hear",0x14068f599,"Active reached observer/debug child.",true);
            InitializeCaller();
            int element=c.I32(Source+0x8f4,0x14036a0a4,"Npc.element8f4");
            bool physical=c.I32(Source+0x8e4,0x14068f69b,"Npc.physical_selector8e4")!=0;
            Need(physical==(element==0),"stale-entry","prepared-selector",0,"Prepared element and physical selector differ.");
            int suppress=SourceCounter(physical);
            Need((c.U32(Source+0x1250,0x14068f6f1,"Npc.abnormal1250")&0x20)==0,
                "incomplete-child","source.blind",0,"Blind consumes its own reached draw/owner.",true);
            PreGate(physical,suppress);
            InitializeOrdinaryOutputs();
            Need(c.U8(Source+0x1687,0x14068f85a,"Npc.main_hit_count1687")==1 && c.U8(Source+0x1688,0x14068f86a,"Npc.off_hit_count1688")==0,
                "incomplete-child","hit-count",0,"Complete first-record domain requires current main1/off0; no multi-hit shortcut.",true);
            c.W32(ElementOutput,unchecked((uint)element),0x14036a0bd,"normal.element_output");
            ushort flatWord=c.U16(Source+0x14d0,0x14036a08d,"Npc.flat14d0");
            uint min=c.U16(Source+0x14dc,0x14036a0ab,"Npc.minimum14dc");
            uint max=c.U16(Source+0x14e0,0x14036a0b4,"Npc.maximum14e0");
            Need((c.U8(Source+0x13c,0x14036a0bf,"Npc.booster13c")&0x80)==0,"incomplete-child","booster",0,"Active booster/inventory owner.",true);
            float additive=c.F32(Source+0x8dc,0x14036a2a5,"Npc.additive8dc");
            int flat=unchecked(flatWord+c.I32(Source+0x14d4,0x14036a2ae,"Npc.flat14d4"));
            float rate=Add(additive,c.F32(Source+0x8d4,0x14036a2b5,"Npc.modifier8d4"));
            float damage=Add(Mul(FromInt(Draw("Base",0x14036a2c4,unchecked((int)min),unchecked((int)max))),rate),FromInt(flat));
            damage=F("base",damage<1f?1f:damage);
            damage=F("movement_attack",Add(Mul(Div(FromInt(Move(0,0)),100f),damage),damage));
            int floor=N.Floor(c,User),denominator=N.DenominatorAdjustment(c,Source,User);
            c.Note(0x14036b890,"ComputedIdentity","actual User skips NPC level damage falloff");
            uint defense=N.Defense(c,User,physical).Bits;
            short resistance=physical?(short)0:N.Elemental(c,User,element-1).Value;
            int adjusted=Cvtt(Add(Mul(Div(FromInt(Move(movement,1)),100f),FromUInt(defense)),FromUInt(defense)));
            damage=Sub(damage,Div(FromInt(adjusted),10f));
            if(physical)damage=Div(damage,Div(FromUInt(Ratio(User,0x14036a995)),1000f));
            if(damage<0f)damage=0f;
            if(!physical)
            {
                int normalized=Cvtt(Div(FromInt(unchecked(resistance+Move(movement,2))),Div(FromUInt(Ratio(Source,0x14036a8c5)),1000f)));
                if(normalized<floor)normalized=floor;
                float factor=Sub(1f,Div(FromInt(normalized),Add(FromInt(denominator),1300f)));
                F("elemental_factor",factor);damage=Mul(factor,damage);
                c.Note(0x14036a8f0,"Computed","elemental.normalized",value:normalized);
            }
            F("after_defense",damage);
            Need((c.U8(User+0x1ca4,0x14036a9eb,"User.debug1ca4")&2)==0,"incomplete-child","User.debug/Hear",0,"Reached gameplay-visible debug owner.",true);
            var active=ActiveDefense(damage,movement,suppress);damage=active.Damage;bool terminal=active.Terminal;
            F("after_active",damage);
            if(terminal)
            {
                c.W32(Record,unchecked((uint)Cvtt(damage)),0x14036abc7,"record.terminal_damage");
                return Finish(element,damage,true,null);
            }
            int bonus=Pose()==5?500:0;
            int criticalSample=Draw("Critical",0x14036abfe,1,1000);
            int critical=c.U16(Source+(physical?0x14bcul:0x14c0ul),0x14036ac5f,"Npc.current_critical");
            int numerator=unchecked(critical+Move(0,6)+bonus);
            int resist=N.Critical(c,User,physical,false),reduction=N.Critical(c,User,physical,true);
            int threshold=System.Math.Min(500,Cvtt(Div(FromInt(unchecked(numerator-resist)),Div(FromUInt(Ratio(User,0x14036ae40)),1000f))));
            c.Note(0x14036ae82,"Computed","critical.threshold",value:threshold);
            if(criticalSample<=threshold)
            {
                c.W32(SharedCritical,1,0x14036ae99,"normal.shared_critical");
                int normalized=Cvtt(Div(FromInt(reduction),Div(FromUInt(Ratio(Source,0x14036ae9c)),1000f)));
                float factor=Sub(physical?1.5f:1f,Div(FromInt(normalized),1000f));if(factor<=1f)factor=1f;
                F("critical_multiplier",factor);damage=Mul(damage,factor);
                c.W8(Record+0x31,12,0x14036b001,"record.critical31");
            }
            F("after_critical",damage);
            M.Result modifier=Pve(element);
            damage=F("after_pve",Mul(damage,BitConverter.UInt32BitsToSingle(modifier.RatioBits)));
            if(damage<1f)damage=1f;
            c.Note(physical?0x1400b47a0ul:0x1400b47b0ul,"ComputedNoOp","actual User fixed damage slot187/188",value:0);
            int id=c.I32(Source+0x100,0x14036b714,"Npc.class_id100");
            ulong selected=ClassLookup(c,id);
            if(selected!=0)
            {
                if(selected!=Template)c.External(selected,0x451,"lookup-class");
                Need(unchecked((sbyte)c.U8(selected+0x450,0x14036b735,"class.exclusive450"))<1,
                    "incomplete-child","exclusive",0,"Current selected class enables an unclosed exclusive-attribute child.",true);
            }
            c.W32(Record,unchecked((uint)Cvtt(damage)),0x14036b7f8,"record.damage0");
            c.W32(FinalFloat,BitConverter.SingleToUInt32Bits(damage),0x14036b801,"normal.per_hand_final_float");
            c.Note(physical?0x1400b47a0ul:0x1400b47b0ul,"ComputedNoOp","actual User fixed slot after successful parent",value:0);
            return Finish(element,damage,false,modifier);
        }
        private void InitializeCaller()
        {
            c.W32(Header,0,0x14068f600,"normal.aggregate_header_zero");
            // Serialized disjoint stack storage: collapse idempotent overlapping native zero
            // stores into their exact53-byte union. Bytes53..55 remain supplied raw/known.
            for(int i=0;i<16;i++)c.Write(Record+(ulong)i*56,new byte[53],0x14068f640,"normal.record_zero0_to52."+i);
        }
        private void InitializeOrdinaryOutputs()
        {
            c.W32(FinalFloat,0,0x14068f82e,"normal.per_hand_float_zero");
            c.W32(SharedActive,0,0x14068f833,"normal.shared_active_zero");
            c.W32(SharedCritical,0,0x14068f83a,"normal.shared_critical_zero");
        }
        private int SourceCounter(bool physical)
        {
            ulong manager=c.U64(Source+0x158,0x14068f6c3,"Npc.manager158");if(manager==0)return 0;
            c.External(manager,0x4404,"manager");
            return c.I32(manager+(physical?0x43d8ul:0x43f8ul),physical?0x14068f6d3ul:0x14068f6e3ul,"Npc.source_suppression_counter");
        }
        private void PreGate(bool physical,int counter)
        {
            Need(counter==0,"incomplete-child","source.suppression_counter",0x140369c90,"Nonzero ordinary pre-gate reaches effect-consumption children.",true);
            if(physical)
            {
                int state=State();if(DodgeRow(state)==0)ForceZero(0x43e0);
                ForceZero(0x43f0);if(ParryRow(State())==0)ForceZero(0x43e8);
            }
            else ForceZero(0x4400);
            c.Note(0x140369c90,"Computed","ordinary.pre_gate_return",value:4);
        }
        private void ForceZero(int offset)
        {
            ulong manager=c.U64(User+0x158,0x140369c90,"User.manager158");if(manager==0)return;
            c.External(manager,0x4404,"manager");
            Need(c.I32(manager+(ulong)offset,0x140369c90,"User.force"+offset.ToString("x"))==0,
                "incomplete-child","target.forced_counter",0x140369c90,"Active forced effect removal has no chosen callback/poststate.",true);
        }
        private (float Damage,bool Terminal) ActiveDefense(float damage,byte movement,int suppress)
        {
            Need(suppress==0,"incomplete-child","suppression",0,"Active suppression not admitted.",true);
            int group=c.I32(Source+0x8ec,0x14036c64d,"Npc.group8ec");
            bool physicalGroup=((1u<<(group&31))&0x3e07f)!=0;
            if(physicalGroup)
            {
                ushort accuracy=c.U16(Source+0x14aa,0x14036c7c4,"Npc.accuracy14aa");
                if(Pose()!=5 && (c.U32(Source+0x1250,0x14036c7e6,"Npc.abnormal_for_defense")&0x649f04c)==0)
                {
                    if(DodgeRow(State())==0)
                    {
                        ushort dodge=Status(c,User,StatusGetter.Dodge).Value;
                        c.Note(0x14036b970,"ComputedIdentity","actual User dodge level helper",value:dodge);
                        int threshold=Chance(dodge,accuracy,Move(movement,5),300);QueryGuard();
                        if(Draw("Dodge",0x14036c96f,1,1000)<=threshold)
                        {StatusStore(0,0x80,1);Counter(ClockRole.DodgeCounter,0x80);return(0f,true);}
                    }
                    if(ShieldEligible(c,User))
                    {
                        ushort block=Status(c,User,StatusGetter.Block).Value;
                        c.Note(0x14036baf0,"ComputedIdentity","actual User block level helper",value:block);
                        int threshold=Chance(block,accuracy,Move(movement,4),500);
                        if(Draw("Block",0x14036cb38,1,1000)<=threshold)
                        {
                            c.W32(SharedActive,3,0x14036cc03,"normal.shared_active");
                            var shield=Shield(c,User,damage);
                            if(shield.Applied){StatusStore(4,0x20,3);Counter(ClockRole.BlockCounter,0x20);return(shield.Damage,false);}
                        }
                    }
                    if(ParryRow(State())==0)
                    {
                        ushort parry=Status(c,User,StatusGetter.Parry).Value;
                        c.Note(0x14036ba30,"ComputedIdentity","actual User parry level helper",value:parry);
                        int threshold=Chance(parry,accuracy,Move(movement,3),400);
                        if(Draw("Parry",0x14036cd53,1,1000)<=threshold)
                        {StatusStore(2,0x40,2);damage=Mul(damage,BitConverter.UInt32BitsToSingle(0x3f19999a));Counter(ClockRole.ParryCounter,0x40);return(damage,false);}
                    }
                }
            }
            else
            {
                float half=Pose()==5?0.5f:1f;
                uint accuracy=c.U16(Source+0x14c4,0x14036cf13,"Npc.magical_accuracy14c4");
                uint resistance=N.MagicalResistance(c,User).Bits;
                int inner=Cvtt(Mul(FromInt(unchecked((int)(resistance-accuracy))),half));
                int probability=Cvtt(Div(FromInt(inner),Div(FromUInt(Ratio(Source,0x14036cf7c)),1000f)));
                QueryGuard();probability=System.Math.Clamp(probability,0,500);
                probability=System.Math.Clamp(unchecked(probability+N.ResistanceLevelBonus(c,Source,User)),0,900);
                c.Note(0x14036d031,"Computed","resist.threshold",value:probability);
                if(Draw("Resist",0x14036d031,1,1000)<=probability)
                {StatusStore(6,0x100,4);Counter(ClockRole.ResistCounter,0x100);return(0f,true);}
            }
            c.W32(SharedActive,0,0x14036d087,"normal.shared_ordinary");
            c.W8(Record+0x30,10,0x14036d096,"record.ordinary30");return(damage,false);
        }
        private void StatusStore(byte status,uint header,int active)
        {
            ulong shared=status switch{0=>0x14036ca3a,2=>0x14036ce22,4=>0x14036cc03,_=>0x14036d04a};
            ulong record=status switch{0=>0x14036ca4c,2=>0x14036ce35,4=>0x14036cc36,_=>0x14036d061};
            ulong aggregate=status switch{0=>0x14036ca50,2=>0x14036ce55,4=>0x14036cc3a,_=>0x14036d065};
            if(status!=4)c.W32(SharedActive,(uint)active,shared,"normal.shared_active");
            c.W8(Record+0x30,status,record,"record.status30");
            c.W32(Header,c.U32(Header,aggregate,"header.old")|header,aggregate,"header.or_status");
        }
        private int Chance(ushort defense,int accuracy,int movement,int cap)
        {int value=Cvtt(Div(FromInt(unchecked(defense+unchecked(movement-accuracy))),Div(FromUInt(Ratio(Source,0x14036c490)),1000f)));
            return System.Math.Clamp(value,0,cap);}
        private void QueryGuard()
        {int target=c.I32(User+0x738,0x1400d1850,"User.query738");int source=c.I32(Source+0x1890,0x1400bc890,"Npc.query1890");
            c.Note(0x1400b47c0,"ComputedGuard","actual NPC classifier0 skips PvP additions",value:source==target?0:1);}
        private int State()
        {int state=c.I32(User+0x13d0,0x14036c7f9,"User.state13d0");Need(state is >=0 and <32,"range","state13d0",0,"Explicit supported state table index0..31 required.");return state;}
        private int DodgeRow(int state)
        {ulong address=0x145a2c630ul+(ulong)state*4;c.External(address,4,"dodge-row");return c.I32(address,0x14036c7f9,"current.dodge_row");}
        private int ParryRow(int state)
        {
            c.External(0x141122d00,128,"parry-table");byte[] raw=c.Peek(0x141122d00,128);
            for(int i=0;i<32;i++)Need(BinaryPrimitives.ReadInt32LittleEndian(raw.AsSpan(i*4,4))==(i==1?1:0),"source-table","parry",0,"Complete static parry table differs from pinned PE.");
            return c.I32(0x141122d00ul+(ulong)state*4,0x14036cc84,"pinned.parry_row");
        }
        private int Move(int row,int column)
        {Need(row is >=0 and <=7 && column is >=0 and <7,"range","movement",0,"Original mask7/seven-column table required.");
            ulong address=0x141069ed0ul+(ulong)row*28;c.External(address,28,"movement-row");return c.I32(address+(ulong)column*4,0x14036a400,"current.movement_column");}
        private void Counter(ClockRole role,uint mask)
        {uint now=ClockValue(role,0x1400b4640,0x1400b4650);ulong offset=mask switch{0x20=>0x13dc,0x40=>0x13e0,0x80=>0x13e4,_=>0x13e8};
            c.W32(User+offset,unchecked(now+5000),0x1400b469a,"User.counter_deadline");}
        private uint ClockValue(ClockRole role,ulong caller,ulong site)
        {
            Need(clockIndex<input.Clocks.Length,"clock",role.ToString(),site,"Missing original per-call clock observation.");var value=input.Clocks[clockIndex++];
            Need(value.Role==role&&value.CallerSite==caller&&value.TickSite==site&&value.ActorAddress==User&&value.Operation==input.Context.Common.Operation&&
                value.Epoch==input.Context.Common.Epoch&&value.ActorGeneration==input.Boundary.UserLife,"clock",role.ToString(),site,"Clock role/site/order/actor/lifetime/epoch differs.");
            Text(value.Receipt);clocks.Add(value);c.Note(site,"OriginalClock",role.ToString(),value:value.Value);return value.Value;
        }
        private int Draw(string stage,ulong site,int first,int second)
        {
            Need(randomOperations<input.Limits.MaxRandomOperations,"budget","rng.operations",site,"Cumulative operation capacity reached.",true);
            var original=input.Random;
            var transaction=original with {InitialGeneration=randomGeneration,InitialStateBytes=randomState?.Bytes??original.InitialStateBytes,
                InitialStateSha256=randomState?.Sha256??original.InitialStateSha256,
                Ownership=original.Ownership is null?null:original.Ownership with{InitialGeneration=randomGeneration},
                Operations=[new(0,input.Context.Common.Operation+":"+input.Context.Common.Epoch+":normal:"+randomOperations.ToString(CultureInfo.InvariantCulture),RetailRandomOperationKind.SignedInclusive,randomGeneration,first,second)],
                Limits=new(){MaxOperations=1,MaxRawDraws=input.Limits.MaxRawDraws-rawDraws,MaxTraceBytes=input.Limits.MaxRandomLogicalTraceBytes-traceBytes}};
            var result=RetailRandomOwnerPlan.Calculate(transaction);
            if(!result.Completed){RandomFailure=result;throw Error("rng-"+result.Status,stage,site,result.Detail,true);}
            var operation=result.CompletedPrefix.Single();Need(operation.SignedValue.HasValue&&result.CompleteNextGeneration.HasValue,"rng",stage,site,"Complete signed return/generation missing.");
            rawDraws=checked(rawDraws+result.Draws.Length);traceBytes=checked(traceBytes+result.LogicalTraceBytesCharged);
            randomGeneration=result.CompleteNextGeneration.Value;if(result.EngineStateAdmitted)randomState=result.CompleteFinalState;
            randomOperations++;int value=operation.SignedValue.Value;c.Note(site,"ComputedRng",stage,value:value);
            draws.Add(new(randomOperations,stage,site,first,second,value,result.Draws,randomGeneration));return value;
        }
        private Result Finish(int element,float amount,bool terminal,M.Result? modifier)
        {
            Need(clockIndex==input.Clocks.Length,"clock","tail",0,"Unused original clock observations.");
            Memory memory=c.Snapshot();var image=memory.Images.Single(x=>x.Binding.Address<=Record&&Record+56<=x.Binding.Address+(ulong)x.Bytes.Length);
            int off=checked((int)(Record-image.Binding.Address));
            return new(Cvtt(amount),c.U8(Record+0x30,0,"result.status"),c.U32(SharedCritical,0,"result.shared_critical")==1,element,c.U32(Header,0,"result.header"),amount,
                memory,c.Raw(Record,56).ToImmutableArray(),image.Known.AsSpan(off,56).ToArray().ToImmutableArray(),c.Steps,[..clocks],[..draws],randomState,randomGeneration,
                traceBytes,modifier,floats.ToImmutableDictionary(StringComparer.Ordinal),terminal);
        }
        private void Validate()
        {
            A.ValidateContext(input.Context);
            Need(input.Selected is not null && input.Selected.Context==input.Context && input.Memory is not null&&input.Boundary is not null&&input.Random is not null&&
                input.Limits is not null&&input.Floating is not null&&input.Infrastructure is not null&&!input.Clocks.IsDefault,"input","required",0,"Missing joined finite input.");
            var l=input.Limits;Need(l.MaxImages is >0 and <=128&&l.MaxBytes is >0 and <=8_000_000&&l.MaxSteps is >0 and <=100_000&&
                l.MaxRandomOperations>=0&&l.MaxRawDraws>=0&&l.MaxRandomLogicalTraceBytes>=0&&input.Memory.Images.Length<=l.MaxImages&&input.Memory.ByteCount<=l.MaxBytes,
                "budget","adapter",0,"Explicit bounded image/read-write/RNG capacities required.");
            c=new(input.Context.Common,input.Memory,l.MaxSteps);
            Need(input.Floating.NearestEven==true&&input.Floating.MaskedExceptions==true&&input.Floating.FlushToZero==false&&input.Floating.DenormalsAreZero==false&&input.Floating.NormalReturn==true,
                "floating","environment",0,"Declared RNE/masked/FTZ-DAZ-off/normal-return required; not observed MXCSR.");
            Need(input.Infrastructure.SerializedNoOtherWriter==true&&input.Infrastructure.NoInlineGameplayWriter==true&&input.Infrastructure.NormalInfrastructureReturn==true,
                "infrastructure","interval",0,"Serialized current owner/no inline gameplay writer/normal infrastructure return required.");
            Text(input.Boundary.UserLife);Text(input.Boundary.FrameLife);Text(input.Boundary.Receipt);
            Need(Frame>=0x80&&Frame<=ulong.MaxValue-0x400,"range","World.frame",0,"Bounded native caller output storage required.");
            A.ProtectSource(c,input.Selected.Current);A.Disjoint(Source,0x2000,User,0x8010);A.Disjoint(Template,0x500,User,0x8010);
            foreach(var owner in new[]{(Source,0x2000),(Template,0x500),(User,0x8010)})A.Disjoint(Frame-0x80,0x480,owner.Item1,owner.Item2);
            c.Protect(User,0x8010);c.Protect(Frame-0x80,0x480);
            Need(c.BindingAt(User,0x8010).Generation==input.Boundary.UserLife&&c.BindingAt(Frame-0x80,0x480).Generation==input.Boundary.FrameLife,
                "generation","User/frame",0,"Current User/output lifetimes differ.");
            Need(c.U64(User,0,"User.vtable")==UserVtable&&c.I32(User+0x144,0,"User.type144")==1,"actor","User",0,"Actual type1 User/vtable required, not an NPC receiver.");
            Need(c.U32(Source+0x1940,0,"Npc.kind1940")==1&&c.U32(Source+0x188c,0,"Npc.master188c")==0&&c.U32(Source+0x1894,0,"Npc.related1894")==0&&
                c.U64(Source+0x1850,0,"Npc.virtual1850")==0&&c.U64(Source+0x1bf8,0,"Npc.difficulty1bf8")==0,
                "source-domain","Npc",0,"Whole modifier child currently admits ordinary kind1 ownerless/unscaled only; kind8 range-only preparation is separate.");
            c.External(0x14275eb74,4,"telemetry");Need(c.I32(0x14275eb74,0x140369e10,"current.telemetry_guard")==0,
                "incomplete-child","telemetry",0,"Active TLS/diagnostic clocks are outside the admitted interval.",true);
            foreach(var image in input.Memory.Images)A.Disjoint(image.Binding.Address,image.Bytes.Length,RetailRandomEngine.NativeEngineAddress,5008);
            Need(input.Random.Sources==new RetailRandomSourceDeclaration(P.MainSha256,RetailRandomEngine.RngEvidenceSha256)&&!input.Random.Operations.IsDefault&&input.Random.Operations.IsEmpty&&
                input.Random.Origin==(input.Context.Common.Origin==Origin.ConstructedFixture?RetailRandomInputOrigin.ConstructedFixture:RetailRandomInputOrigin.DeclaredCurrentMemory),
                "rng-source","context",0,"Same-source/origin ordered shared engine with no caller-supplied operations or rolls required.");
        }
        private M.Result Pve(int element)
        {
            var ctx=new M.Context(P.MainSha256,P.PackageContext,input.Context.Common.Operation,input.Context.Common.Epoch,
                input.Context.Common.Origin==Origin.ConstructedFixture?M.Origin.MathematicalFixture:M.Origin.SourceBoundDeclaration,
                input.Context.Common.Receipt,M.ProofSha256,M.OracleAmendmentSha256,M.ReleaseEvidenceSha256);
            M.Binding Bind(ulong a,int n){var b=c.BindingAt(a,n);return new(a,b.Identity,b.Generation,ctx);}
            var npc=new M.Npc(Bind(Source,0x2000),Bind(Template,0x500),input.Selected.NpcId,new(){Type144=c.Optional<int>(Source+0x144),Vtable=c.Optional<ulong>(Source),
                Kind1940=c.Optional<uint>(Source+0x1940),Master188c=c.Optional<uint>(Source+0x188c),Related1894=c.Optional<uint>(Source+0x1894),Template1848=c.Optional<ulong>(Source+0x1848),
                Virtual1850=c.Optional<ulong>(Source+0x1850),Difficulty1bf8=c.Optional<ulong>(Source+0x1bf8),Current1bc8=c.Optional<int>(Source+0x1bc8),Current1bd8=c.Optional<int>(Source+0x1bd8),
                Template23c=c.Optional<int>(Template+0x23c),Current1bd0=c.Optional<int>(Source+0x1bd0),Current1be0=c.Optional<int>(Source+0x1be0),Template244=c.Optional<int>(Template+0x244),NormalAttackElement8f4=element});
            var user=new M.User(Bind(User,0x8010),new(){Type144=c.Optional<int>(User+0x144),Vtable=c.Optional<ulong>(User),Primary6e50=c.Optional<ulong>(User+0x6e50),Backup6e38=c.Optional<ulong>(User+0x6e38),
                Current166c=c.Optional<int>(User+0x166c),CurrentCf4=c.Optional<int>(User+0xcf4),Current1674=c.Optional<int>(User+0x1674),CurrentD04=c.Optional<int>(User+0xd04),Selector738=c.Optional<int>(User+0x738),
                Maximum774=c.Optional<int>(User+0x774),Current778=c.Optional<int>(User+0x778),Program8008=c.Optional<ulong>(User+0x8008)});
            M.Snapshot? Snapshot(ulong? pointer)
            {
                if(pointer is null or 0)return null;
                N.SnapshotPrefix(c,pointer.Value);if(element!=0)N.SnapshotTail(c,pointer.Value);
                return new(Bind(pointer.Value,element==0?0x1cc:0x218),c.Optional<ushort>(pointer.Value),c.Optional<uint>(pointer.Value+0x1c8),c.Optional<int>(pointer.Value+0x1c4),
                    c.Optional<uint>(pointer.Value+0x214),c.Optional<int>(pointer.Value+0x210));
            }
            var primary=Snapshot(user.Raw.Primary6e50);var backup=primary?.OccupiedWord==0?Snapshot(user.Raw.Backup6e38):null;
            M.ProgramImage? program=null;
            if(user.Raw.Program8008 is >0)
            {
                ulong address=user.Raw.Program8008.Value;ushort tag=BinaryPrimitives.ReadUInt16LittleEndian(c.Peek(address+2,2));int count=tag==0x12?32:4;
                c.External(address,count,"HP2-program");program=new(Bind(address,count),c.Raw(address,count).ToImmutableArray());
            }
            var child=M.Calculate(new(ctx,npc,user,primary,backup,program,c.Optional<int>(0x14275eb74),new(true,true,false,false),true,true,M.NativeEntry.NormalAttack,0,M.ModeSourceProofSha256));
            Need(child.Success,"modifier-"+child.Diagnostic?.Code,"PvE/HP2",0x14036b44f,child.Diagnostic?.Message??"Incomplete normal modifier child.",true);
            var complete=child.Value??throw Error("modifier","PvE",0,"Complete child missing.");
            foreach(var read in complete.Reads)
            {
                if(program is not null&&read.Operand.StartsWith("HP2",StringComparison.Ordinal))
                {
                    int offset=read.Operand switch{"HP2+2"=>2,"HP2+0"=>0,"HP2+6"=>6,"HP2+4"=>4,
                        _ when read.Operand.StartsWith("HP2 row",StringComparison.Ordinal)=>10+(read.Operand[7]-'0')*8,
                        _=>throw Error("modifier-footprint",read.Operand,read.NativeSite,"Unknown reached HP2 field.")};
                    _=c.Read(program.Current.Address+(ulong)offset,offset==0?1:2,read.NativeSite,read.Operand);
                }
                else if(program is not null&&read.Operand.StartsWith("selected HP2 row+",StringComparison.Ordinal))
                {
                    Need(complete.SelectedHp2Row.HasValue,"modifier-footprint",read.Operand,read.NativeSite,"Selected row absent.");
                    int relative=read.Operand[^1]-'0',offset=8+complete.SelectedHp2Row.Value*8+relative;
                    _=c.Read(program.Current.Address+(ulong)offset,relative==0?1:2,read.NativeSite,read.Operand);
                }
                c.Note(read.NativeSite,"ComputedChildRead",read.Operand,value:read.Value);
            }
            c.Note(0x14036beb0,"ComputedChild","normal.PvE_ratio",value:complete.RatioBits);return complete;
        }
    }
}
