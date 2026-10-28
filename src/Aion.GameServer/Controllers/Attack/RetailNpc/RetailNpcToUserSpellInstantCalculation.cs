using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Aion.GameServer.Commons.Utils.RetailRandom;
using U = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcUserMagicalInputs;
using static Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcUserMagicalInputs;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Inactive finite ordinary NPC to actual User magic Check, calculator and Begin
/// chance. Completes every admitted gameplay child up to the exact outer dispatch;
/// it never calls the outer wrapper, supplies its return, applies HP or routes a skill.</summary>
internal static class RetailNpcToUserSpellInstantCalculation
{
    internal enum ClockRole { UserMovement }
    internal sealed record Clock(ClockRole Role, ulong Caller, ulong Site, uint Tick,
        ulong Actor, string Life, string Operation, string Epoch, string Receipt);
    internal sealed record Floating(bool? NearestEven, bool? ExceptionsMasked, bool? FlushToZero,
        bool? DenormalsAreZero, bool? NormalReturn);
    internal sealed record Infrastructure(bool? ExclusiveSerializedLease, bool? NoInlineGameplayWriter,
        bool? NormalReturn, bool? FormattingScratchExcluded);
    internal sealed record Limits(int MaxSteps, int MaxRandomOperations, int MaxRawDraws,
        long MaxRandomLogicalTraceBytes);
    internal sealed record Boundary(ulong Source, ulong User, ulong Template, ulong Skill, ulong RunInfo,
        ulong CheckRsp, ulong CheckRegisters, ulong Window88, string SourceLife, string UserLife, string SkillGeneration,
        string TemplateGeneration, string CallerGeneration, string CallerProducerReceipt, string RandomLife, string RandomOrder);
    internal sealed record Input(Context Context, SelectedUserSpell Selected, Memory Memory, Boundary Boundary,
        RetailRandomTransaction Random, ImmutableArray<Clock> Clocks, Floating Floating,
        Infrastructure Infrastructure, Limits Limits);
    internal sealed record RandomCall(string Stage, ulong Site, int First, int Second, int Value,
        ImmutableArray<RetailRandomDrawTrace> Draws, ulong NextGeneration);
    internal sealed record Counters(int ResistThreshold, int? BoostIntegerQuotient, int? Armor,
        int? CriticalThreshold, bool? Critical, int? Hp2Delta, int? PveDefense);
    private static readonly object DispatchToken = new();
    internal sealed class BeginDispatch
    {
        internal ulong RunInfo { get; }
        internal int EffectIndex => 0;
        internal int ArgumentR8 => 0;
        internal int ArgumentR9 => 0;
        internal int ChanceFallback { get; }
        internal int Stack28 => 0;
        internal ulong CallerSite => 0x140bc10ee;
        internal ulong Target => 0x140bb8e50;
        internal Context Context { get; }
        internal SelectedUserSpell Source { get; }
        internal Memory CalculatedMemory { get; }
        internal RetailRandomState? RandomState { get; }
        internal ulong RandomGeneration { get; }
        internal Boundary Boundary { get; }
        private BeginDispatch(ulong runInfo, int fallback, Context context, SelectedUserSpell source,Memory memory,RetailRandomState? randomState,ulong generation,Boundary boundary)
        { RunInfo = runInfo; ChanceFallback = fallback; Context = context; Source = source;CalculatedMemory=memory;RandomState=randomState;RandomGeneration=generation;Boundary=boundary; }
        internal static BeginDispatch Create(object token, ulong runInfo, int fallback, Context context, SelectedUserSpell source,Memory memory,RetailRandomState? randomState,ulong generation,Boundary boundary)
        { Need(ReferenceEquals(token, DispatchToken), "capability", "Begin", 0, "Only the complete replay may issue dispatch."); return new(runInfo, fallback, context, source,memory,randomState,generation,boundary); }
    }
    internal sealed record Result(bool CheckPassed, int? PreliminaryReturn, int? FinalRecordAmount,
        BeginDispatch? Dispatch, Memory CalculatedMemory, ImmutableArray<Step> Steps,
        ImmutableArray<Clock> ConsumedClocks, ImmutableArray<RandomCall> RandomCalls,
        RetailRandomState? RandomState, ulong RandomGeneration, long RandomTraceBytes,
        ImmutableDictionary<string, uint> FloatWords, Counters Counters)
    {
        internal int? OuterReturn => null;
        internal int? HpResult => null;
        internal bool ActivationReady => false;
        internal bool CurrentActorObserved => false;
    }
    internal sealed record Prefix(Memory? Memory, ImmutableArray<Step> Steps,
        ImmutableArray<Clock> Clocks, ImmutableArray<RandomCall> RandomCalls,
        RetailRandomState? RandomState, ulong RandomGeneration, RetailRandomTransactionResult? RandomFailure)
    { internal bool Committable => false; }
    internal sealed record Outcome(Result? Value, Failure? Failure, Prefix? KnownPrefix)
    {
        [MemberNotNullWhen(true, nameof(Value))] internal bool Complete => Value is not null && Failure is null;
        internal bool PrefixCommittable => false;
    }
    internal static Outcome Calculate(Input input)
    {
        ArgumentNullException.ThrowIfNull(input);
        Replay? replay = null;
        try { replay = new(input); return new(replay.Run(), null, null); }
        catch (Domain e) { return new(null, e.Failure, replay?.Prefix()); }
        catch (ArithmeticException e) { return new(null, new("arithmetic", "calculation", 0, e.Message, true), replay?.Prefix()); }
    }

    private sealed class Replay(Input input)
    {
        private Cursor? cursor;
        private Cursor C => cursor ?? throw Error("input", "cursor", 0, "Missing admitted cursor.");
        private Boundary B => input.Boundary;
        private SelectedUserSpell S => input.Selected;
        private ulong A => B.Source;
        private ulong D => B.User;
        private ulong H => B.Window88;
        private readonly List<Clock> clocks = [];
        private readonly List<RandomCall> draws = [];
        private readonly Dictionary<string,uint> floats = new(StringComparer.Ordinal);
        private int clockIndex, rawDrawCount;
        private long randomTraceBytes;
        private RetailRandomState? randomState;
        private ulong randomGeneration = input.Random?.InitialGeneration ?? 0;
        private RetailRandomTransactionResult? randomFailure;
        private int resistThreshold;
        private int? boostQuotient, armor, criticalThreshold, hp2Delta, pveDefense;
        private bool? critical;
        private bool movementTableAdmitted;
        internal Prefix Prefix() => new(cursor?.Snapshot(), cursor?.Steps ?? [], [..clocks], [..draws], randomState, randomGeneration, randomFailure);
        private float F(string name,float value,ulong site=0)
        { C.Keep(name,value,site); floats.Add(name,BitConverter.SingleToUInt32Bits(value)); return value; }
        private int Word(int offset,string key,ulong site)
        {
            int value=C.I32(B.Skill+(ulong)offset,site,"Skill."+key);
            Need(S.Spell.Constructed.TryGetValue(key,out long expected)&&value==unchecked((int)expected),
                "stale-skill",key,site,"Current source constructor operand differs from the selected complete recipe."); return value;
        }
        private byte Byte(int offset,string key,ulong site)
        {
            byte value=C.U8(B.Skill+(ulong)offset,site,"Skill."+key);
            Need(S.Spell.Constructed.TryGetValue(key,out long expected)&&value==expected,"stale-skill",key,site,"Current selected byte differs."); return value;
        }
        private float BitsWord(int offset,string key,ulong site)=>Normal(BitConverter.Int32BitsToSingle(Word(offset,key,site)));
        private uint Ratio(ulong actor,ulong site)
        { uint value=C.U32(actor+0x1514,site,"actor.stat_ratio1514");Need(value is >=1000 and <=65000,"range","stat_ratio",site,"Explicit supported construction ratio required.");return value; }
        private int Level(ulong actor,ulong site)
        {int value=C.U16(actor+0x1492,site,actor==A?"Npc.level1492":"User.level1492");Need(value is >=1 and <=100,"range","level",site,"Finite ordinary level/table construction range1..100 required.");return value;}
        private byte Pose(ulong site)=>(byte)(C.U32(D+0x13c,site,"User.pose13c")&15);

        internal Result Run()
        {
            Validate(); Telemetry(0x140bc0a63);
            C.W8(B.CheckRsp+0xfc,0,0x140bc0a74,"Check.inactive_local_telemetry");
            int element=Word(0x458,"element458",0x140bc0bca);
            int elemental=Element(element);
            int accuracyBase=Word(0x43c,"accuracy43c",0x140bc0bdb);
            int additionalBase=Word(0x45c,"additional45c",0x140bc0be3);
            C.W32(B.CheckRsp+0xec,unchecked((uint)elemental),0x140bc0bf0,"Check.element_local");
            int rawR1=Word(0x2d8,"raw_R1_2d8",0x140bc0bf7);
            int accuracy=unchecked(accuracyBase*S.Spell.Binding.SkillLevel+Word(0x440,"accuracy440",0x140bc0c06));
            int additional=unchecked(additionalBase*S.Spell.Binding.SkillLevel+Word(0x460,"additional460",0x140bc0c0e));
            int r2=unchecked(rawR1*S.Spell.Binding.SkillLevel+Word(0x2dc,"raw_R2_2dc",0x140bc0c19));
            _=Byte(0x464,"percent_flag464",0x140bc0c21);
            C.W32(B.CheckRsp+0x100,unchecked((uint)r2),0x140bc0c2a,"Check.R2_local");
            Need(additional==0,"incomplete-child","additional",0x140bc0ccc,"Selected zero additional descriptor required.",true);
            C.W32(B.CheckRsp+0xf0,0,0x140bc0ca6,"Check.additional_raw_local");C.W32(B.CheckRsp+0xe8,0,0x140bc0cad,"Check.additional_zero_local");
            C.W32(B.CheckRsp+0xe4,0,0x140bc0cba,"Check.additional_percent_local");C.W32(B.CheckRsp+0xe0,0,0x140bc0cc5,"Check.additional_flat_local");
            byte[] condition=C.Read(B.Skill+0x468,16,0x140bc0ccc,"Skill.condition468");
            byte[] second=C.Read(B.Skill+0x478,16,0x140bc0cd5,"Skill.condition478");
            byte sharingFlag=Byte(0x4cc,"sharing4cc",0x140bc0cde);
            byte mask=Byte(0xd72,"mask_d72",0x140bc0cea);
            C.Write(B.CheckRsp+0x110,condition,0x140bc0cfc,"Check.condition_copy468");
            C.Write(B.CheckRsp+0x120,second,0x140bc0d04,"Check.condition_copy478");
            // CMOVNZ's memory source is read even when the selected flag is zero.
            ushort sharingCandidate=C.U16(B.CheckRsp+0x1d8,0x140bc0d0c,"Check.original_sharing_candidate");
            ushort sharing=sharingFlag==0?(ushort)1:sharingCandidate;
            byte[] third=C.Read(B.Skill+0x488,16,0x140bc0d15,"Skill.condition488");
            float criticalAddProduct=Mul(FromInt(S.Spell.Binding.SkillLevel),BitsWord(0x444,"critical_damage444_bits",0x140bc0d24));
            C.Write(B.CheckRsp+0x130,third,0x140bc0d2e,"Check.condition_copy488");
            float criticalProbabilityProduct=Mul(FromInt(S.Spell.Binding.SkillLevel),BitsWord(0x44c,"critical_probability44c_bits",0x140bc0d36));
            float criticalAdd=Add(criticalAddProduct,BitsWord(0x448,"critical_damage448_bits",0x140bc0d40));
            float criticalProbability=Add(criticalProbabilityProduct,BitsWord(0x450,"critical_probability450_bits",0x140bc0d4a));
            byte movement=Movement(); C.Note(0x140bc0d6b,"ComputedGetter","Npc movement constant0",value:0);
            Need(Word(0x2ec,"raw_R6_2ec",0x140bc0d74)==1&&mask==0&&sharing==1,"source-domain","selected calculation flags",0x140bc0daa,"Complete selected NPC-base/mask31/nonsharing recipe required.");
            ulong opaque=C.U64(B.CheckRsp+0x1e0,0x140bc0d94,"Check.opaque_extra_argument");
            int r5=Word(0x2e8,"raw_R5_2e8",0x140bc0daa);C.W32(B.CheckRsp+0xd8,unchecked((uint)r5),0x140bc0db2,"Calc.arg.R5");
            byte limit=Byte(0x434,"limit_attribute434",0x140bc0db9);C.W8(B.CheckRsp+0xd0,limit,0x140bc0dc2,"Calc.arg.limit");
            C.W64(B.CheckRsp+0xc8,opaque,0x140bc0dc9,"Calc.arg.opaque");
            int randomFlags=Word(0x4c8,"amplitude_flags4c8",0x140bc0dd1);
            C.Write(B.CheckRsp+0xc0,BitConverter.GetBytes(sharing),0x140bc0dd9,"Calc.arg.sharing");
            C.W8(B.CheckRsp+0xb8,movement,0x140bc0de1,"Calc.arg.User_movement");C.W8(B.CheckRsp+0xb0,0,0x140bc0de9,"Calc.arg.Npc_movement");
            C.W64(B.CheckRsp+0xa8,H+0x4c,0x140bc0df1,"Calc.arg.cache_alias");
            C.W32(B.CheckRsp+0xa0,unchecked((uint)randomFlags),0x140bc0e01,"Calc.arg.amplitude_flags");
            int randomType=Word(0x4c4,"amplitude4c4",0x140bc0e08);
            C.W32(B.CheckRsp+0x98,unchecked((uint)randomType),0x140bc0e16,"Calc.arg.amplitude");
            Need(randomType==0&&randomFlags==1,"source-domain","amplitude",0x140bc0e08,"Selected no-amplitude constructor required.");
            C.W32(B.CheckRsp+0x90,100,0x140bc0e24,"Calc.arg.additional_probability");C.W32(B.CheckRsp+0x88,0,0x140bc0e2f,"Calc.arg.additional_flat");C.W32(B.CheckRsp+0x80,0,0x140bc0e3d,"Calc.arg.additional_percent");
            C.W64(B.CheckRsp+0x78,B.CheckRsp+0x110,0x140bc0e4c,"Calc.arg.condition_pointer");C.W32(B.CheckRsp+0x70,1,0x140bc0e58,"Calc.arg.Npc_base");C.W32(B.CheckRsp+0x68,unchecked((uint)r2),0x140bc0e5d,"Calc.arg.R2");
            C.W32(B.CheckRsp+0x60,unchecked((uint)accuracy),0x140bc0e68,"Calc.arg.accuracy");C.W32(B.CheckRsp+0x58,unchecked((uint)elemental),0x140bc0e6d,"Calc.arg.elemental_value");
            int repeatedElement=Word(0x458,"element458",0x140bc0e71);C.W32(B.CheckRsp+0x50,unchecked((uint)repeatedElement),0x140bc0e79,"Calc.arg.element_kind");
            int skill16c=Word(0x16c,"skill16c",0x140bc0e7d);
            C.W32(B.CheckRsp+0x48,BitConverter.SingleToUInt32Bits(criticalAdd),0x140bc0e83,"Calc.arg.critical_add");C.W32(B.CheckRsp+0x40,BitConverter.SingleToUInt32Bits(criticalProbability),0x140bc0e89,"Calc.arg.critical_probability");C.W32(B.CheckRsp+0x38,unchecked((uint)skill16c),0x140bc0e8f,"Calc.arg.critical_enabled");
            int noresist=Word(0x438,"noresist438",0x140bc0e93);C.W32(B.CheckRsp+0x30,unchecked((uint)noresist),0x140bc0e9b,"Calc.arg.no_resist");
            int force=C.I32(B.CheckRsp+0x1b8,0x140bc0e9f,"Check.force_input");C.W32(B.CheckRsp+0x28,unchecked((uint)force),0x140bc0ea6,"Calc.arg.force");C.W32(B.CheckRsp+0x20,31,0x140bc0eaa,"Calc.arg.mask31");
            Need(noresist==0&&force==0&&skill16c==1,"incomplete-child","force/noresist/critical",0x140bc0eaf,"Selected ordinary check/critical program required.",true);
            Telemetry(0x14036f888);
            if(!Resist(accuracy))
            {
                // Exact ordinary tail615..62d returns false. The counter helper at290
                // belongs only to excluded immunity/positive-force consumption paths.
                C.Note(0x140370c5c,"ComputedReturn","CalcMagic failed ordinary resistance returns known0",value:0);
                C.Note(0x140bc0ee6,"ComputedReturn","Check false leaves header unchanged",value:0);
                return Finish(false,0,null,null);
            }
            C.W8(H+0x34,10,0x14036fa1e,"record.base_status");
            int minimum=C.U16(A+0x14dc,0x14036fa52,"Npc.effective_minimum14dc");
            int maximum=C.U16(A+0x14e0,0x14036fa5a,"Npc.effective_maximum14e0");
            Need(Word(0x168,"skill168",0x14036fa71)==1,"source-domain","boost",0x14036fa71,"Selected boost participation required.");
            int suppression=SnapshotScalar(0x10,0x1dc,0x1504,true,0x14036fa87,"suppression");
            int boost=unchecked(unchecked((short)C.U16(A+0x1502,0x14036fad2,"Npc.boost1502"))-suppression);
            C.External(0x145a43940,4,"boost-cap");
            int cap=C.U32(0x145a43940,0x14036fada,"current.cap_selector")==0?3400:2900;
            boost=System.Math.Clamp(boost,0,cap);
            boost=System.Math.Clamp(Cvtt(F("boost_normalized",Div(FromInt(boost),Div(FromUInt(Ratio(D,0x14036fb06)),1000f)))),0,cap);
            boostQuotient=boost/1000;
            int sample=Draw("CalcMagic.NpcBase",0x14036fb66,minimum,maximum);
            float coefficient=F("source_R2_coefficient",Add(Div(FromInt(r2),100f),1f));
            int amount=System.Math.Max(0,Cvtt(F("npc_base",Add(Mul(FromInt(sample),Mul(coefficient,Add(FromInt(boostQuotient.Value),1f))),0f))));
            amount=Cvtt(F("ordinary_pct_flat",Add(Mul(Div(FromInt(unchecked(C.I32(A+0x11c4,0x14036fefd,"Npc.percent11c4")+100)),100f),FromInt(amount)),FromInt(C.I32(A+0x11c8,0x14036ff04,"Npc.flat11c8")))));
            OneTime(false,false);
            Need(C.U64(B.Skill+0xe10,0x140370047,"Skill.attenuationE10")==0x140b60ec0,"incomplete-child","attenuation",0x140370047,"Only the complete selected None attenuation leaf is admitted.",true);
            C.Note(0x140b60ec0,"ComputedIdentity","None attenuation",value:amount);
            amount=Cvtt(Add(Mul(Div(FromInt(Move(0,0)),100f),FromInt(amount)),FromInt(amount)));
            Debug(0x14037010b);
            int preliminary=System.Math.Max(1,Cvtt(F("level_User_identity",FromInt(amount))));
            int defense=SnapshotScalar(2,0x1d0,0x1498,false,0x140370167,"mdef");
            int adjusted=Cvtt(F("mdef_movement",Add(Mul(Div(FromInt(Move(movement,1)),100f),FromInt(defense)),FromInt(defense))));
            armor=Cvtt(F("armor_tenths",Div(FromInt(adjusted),10f)));
            int after=System.Math.Max(0,unchecked(preliminary-armor.Value));
            int userLevel=Level(D,0x1403701f2); Need(userLevel is >=1 and <=100,"range","User.level",0x1403701f2,"Supported current floor-table index required.");
            ulong floorAddress=0x145a28930ul+(ulong)userLevel*4; C.External(floorAddress,4,"element-floor");
            int floor=C.I32(floorAddress,0x140370200,"current.User_element_floor");
            int extra=ElementLevel();
            int er=unchecked(elemental+Move(movement,2));
            int normalized=Cvtt(F("ER_normalized",Div(FromInt(er),Div(FromUInt(Ratio(A,0x140370233)),1000f))));
            float factor=F("element_factor",Sub(1f,Div(FromInt(System.Math.Max(floor,normalized)),Add(FromInt(extra),1300f))));
            int damage=Cvtt(F("after_element",Mul(factor,FromInt(after))));
            bool isCritical;
            if(C.I32(H+0x54,0x14037029a,"cache.present")!=0)
                isCritical=C.I32(H+0x50,0x1403702a0,"cache.decision")!=0;
            else
            {
                int poseBonus=Pose(0x1403702a8)==5?500:0;
                int criticalDraw=Draw("CalcMagic.uncachedCritical",0x1403702cc,1,1000);
                int attack=Cvtt(Mul(FromInt(C.U16(A+0x14c0,0x14037031e,"Npc.critical14c0")),criticalProbability));
                int numerator=unchecked(attack+poseBonus);
                OneTime(true,false);
                int reduce=SnapshotScalar(0x400,0x1f4,0x14c8,false,0x1403703fa,"critical_reduction",true);
                numerator=unchecked(numerator-reduce);
                criticalThreshold=System.Math.Min(500,Cvtt(F("critical_threshold",Div(FromInt(numerator),Div(FromUInt(Ratio(D,0x140370431)),1000f)))));
                isCritical=criticalDraw<criticalThreshold.Value;
                C.W32(H+0x54,1,0x140370493,"cache.present_before_decision");
                C.W32(H+0x50,isCritical?1u:0u,0x140370497,"cache.decision");
            }
            critical=isCritical;
            float multiplier=1f;
            if(isCritical)
            {
                int fort=SnapshotScalar(0x800,0x1f8,0x14cc,false,0x1403704b2,"fortitude",true);
                int reduced=Cvtt(F("fortitude_normalized",Div(FromInt(fort),Div(FromUInt(Ratio(A,0x1403704f3)),1000f))));
                multiplier=System.Math.Max(1f,Sub(Add(criticalAdd,1f),Div(FromInt(reduced),1000f)));
                C.W8(H+0x35,12,0x1403705bc,"record.critical_status");
            }
            damage=Cvtt(F("after_critical",Mul(FromInt(damage),multiplier)));
            Need(NpcClassifier(0x140370611)==0,"source-domain","classifier",0x140370611,"Ordinary NPC classifier changed.");
            C.Note(0x140370876,"ComputedClassifier","actual User returns1",value:1);
            damage=System.Math.Max(1,Cvtt(F("after_pve_and_floor",Mul(FromInt(damage),Pve()))));
            Debug(0x140370aba); C.Note(0x1400b47b0,"ComputedGetter","actual User fixed magic0",value:0);
            sbyte exclusive=unchecked((sbyte)Byte(0xec4,"skill_exclusive_ec4",0x140370b0e));
            Need(exclusive<=0,"incomplete-child","Skill.exclusive",0x140370b19,"Active exclusive owner.",true);
            ulong selectedClass=ClassLookup(C.I32(A+0x100,0x140370b57,"Npc.source100"));
            if(selectedClass!=0)
            {
                if(selectedClass!=B.Template)C.External(selectedClass,0x451,"lookup-template");
                Need(unchecked((sbyte)C.U8(selectedClass+0x450,0x140370b6c,"class.exclusive450"))<=0,"incomplete-child","class.exclusive",0x140370b7b,"Active class exclusive owner.",true);
            }
            C.W32(H+4,unchecked((uint)damage),0x140370c47,"calculator.final_record");
            C.W32(H,unchecked((uint)preliminary),0x140bc0ee2,"Check.positive_preliminary_header");
            // Begin reads its own live RunInfo/Skill fields after Check returns.
            Telemetry(0x140bc0f5c);
            Need(C.U64(B.RunInfo,0x140bc1071,"Begin.RunInfo.Skill")==B.Skill,"stale-caller","Begin.Skill",0x140bc1071,"Current RunInfo changed.");
            int beginLevel=unchecked((short)C.U16(B.RunInfo+0x38,0x140bc1074,"Begin.signed_level"));
            int a=C.I32(B.Skill+0x4a0,0x140bc108c,"Skill.chance17"), b=C.I32(B.Skill+0x4a4,0x140bc1094,"Skill.chance18");
            Need(a==S.Chance17&&b==S.Chance18,"stale-skill","chance",0x140bc1094,"Current chance constructor differs.");
            int chance=System.Math.Min(100,unchecked(beginLevel*a+b));
            int beginDraw=Draw("Begin.always_1_100",0x140bc10a2,1,100);
            int fallback=unchecked((uint)beginDraw)>unchecked((uint)chance)?1:0;
            C.Note(0x140bc10ee,"DispatchBoundary","SkillMagicalAttack not executed",value:fallback);
            return Finish(true,preliminary,damage,fallback);
        }

        private bool Resist(int accuracy)
        {
            C.Note(0x1400b2ce0,"ComputedGetter","actual User immunity0",value:0);
            ulong manager=C.U64(D+0x158,0x14036f262,"User.manager158");
            int forced=0;
            if(manager!=0){C.External(manager,0x4460,"User-manager");forced=C.I32(manager+0x4400,0x14036f271,"User.forced_counter4400");}
            Need(forced<=0,"incomplete-child","forced-resist-owner",0x14036f283,"Positive force counter requires its actual effect consumer.",true);
            float pose=Pose(0x14036f2c1)==5?0.5f:1f;
            int attack=C.U16(A+0x14c4,0x14036f350,"Npc.accuracy14c4");
            // Native ordinary NPC ignores wrapper accuracy addition here.
            C.Note(0x14036f350,"ComputedBranch","wrapper accuracy addition skipped for NPC",value:accuracy);
            int mres=SnapshotScalar(4,0x1d4,0x149c,false,0x14036f37c,"mres",true);
            OneTime(false,true);
            int subtype=Word(0x154,"subtype154",0x14036f4d1);Need(subtype is >=0 and <64,"range","subtype",0x14036f4d1,"Bounded selected subtype index required.");
            int penetration=C.I32(A+0xfc4+(ulong)subtype*4,0x14036f4d8,"Npc.subtype_penetration");
            int difference=unchecked(unchecked(mres-penetration)-attack);
            int posed=Cvtt(F("resist_pose",Mul(FromInt(difference),pose)));
            int normalized=Cvtt(F("resist_normalized",Div(FromInt(posed),Div(FromUInt(Ratio(A,0x14036f4fb)),1000f))));
            // Slot400 query values are read before source classifier short-circuits PvP.
            int targetQuery=C.I32(D+0x738,0x14036f51d,"User.query738"),sourceQuery=C.I32(A+0x1890,0x14036f52b,"Npc.query1890");
            if(sourceQuery!=targetQuery)Need(NpcClassifier(0x14036f543)==0,"source-domain","classifier",0x14036f543,"Source classification changed.");
            int diff=unchecked(Level(D,0x14036bc9e)-Level(A,0x14036bcac));
            int level=diff>=5?unchecked((diff-4)*100):diff<=-5?unchecked((diff+4)*100):0;
            resistThreshold=System.Math.Clamp(unchecked(System.Math.Clamp(normalized,0,500)+level),0,900);
            C.Note(0x14036f5b1,"Computed","resist.threshold",value:resistThreshold);
            return Draw("CheckResist.ordinary",0x14036f615,1,1000)>resistThreshold;
        }

        private int SnapshotScalar(uint mask,int snapshotOffset,int liveOffset,bool signedLive16,ulong site,string name,bool liveUnsigned16=false)
        {
            ulong snapshot=Snapshot(mask,site,name,false);
            if(snapshot!=0)return C.I32(snapshot+(ulong)snapshotOffset,site,"snapshot."+name);
            if(signedLive16)return unchecked((short)C.U16(D+(ulong)liveOffset,site,"live."+name));
            return liveUnsigned16?C.U16(D+(ulong)liveOffset,site,"live."+name):C.I32(D+(ulong)liveOffset,site,"live."+name);
        }
        private ulong Snapshot(uint mask,ulong site,string name,bool elemental)
        {
            ulong primary=C.U64(D+0x6e50,site,"User.primary6e50:"+name); C.External(primary,0x218,"snapshot");
            ulong selected=primary;
            if(C.U16(primary,site,"primary.occupied:"+name)==0)
            {
                selected=C.U64(D+0x6e38,site,"User.backup6e38:"+name); C.External(selected,0x218,"snapshot");
                if(C.U16(selected,site,"backup.occupied:"+name)==0)return 0;
            }
            uint flags=elemental?C.U16(selected+0xbc,site,"snapshot.flagsBC:"+name):mask<256?C.U8(selected+0x214,site,"snapshot.flags214:"+name):C.U32(selected+0x214,site,"snapshot.flags214:"+name);
            return(flags&mask)!=0?selected:0;
        }
        private int Element(int element)
        {
            Need(element is >=0 and <=3,"range","element",0x140bc0bd5,"Reviewed selected element required.");
            ulong snapshot=Snapshot(1u<<(element+6),0x1400de340,"element",true);
            return unchecked((short)C.U16((snapshot==0?D+0x764:snapshot+0xb0)+(ulong)element*2,0x1400de379,"User.element.signed16_after_CWDE"));
        }
        private byte Movement()
        {
            uint tick=ClockValue(ClockRole.UserMovement,0x140bc0d54,0x140578ec9);
            uint old=C.U32(D+0x4094,0x140578ed6,"User.movement_lock"); C.W32(D+0x4094,1,0x140578ed6,"User.movement_acquire");
            Need(old==0,"incomplete-child","movement.contention",0x140578ee7,"Contended lock owner unsupported.",true);
            byte[] movement=C.Read(D+0x4084,16,0x140578eec,"User.inline_movement16");C.W32(D+0x4094,0,0x140578ef5,"User.movement_release");
            byte flags=0;
            if(movement[0]!=0&&unchecked(tick-BinaryPrimitives.ReadUInt32LittleEndian(movement.AsSpan(4)))<1332)flags|=movement[0];
            if(movement[8]!=0&&unchecked(tick-BinaryPrimitives.ReadUInt32LittleEndian(movement.AsSpan(12)))>666)flags|=movement[8];
            byte value=(byte)(flags&7);C.Note(0x140578f4a,"ComputedGetter","User.movement",value:value);return value;
        }
        private void OneTime(bool isCritical,bool resist)
        {
            ulong site=isCritical?0x140370342ul:resist?0x14036f3a7ul:0x14036ff65ul;
            if(C.U64(A+(isCritical?0x208ul:0x200ul),site,"Npc.one_time_pointer")==0)return;
            ulong manager=C.U64(A+0x158,site,"Npc.manager158");
            if(manager==0)
            {
                // Native inactive flag1 is an instruction-derived local default,
                // not a supplied flag or a guessed initialization of unused scratch.
                // Resist451/45d, damagefff0/fff4, critical3b9/3bd all skip adjustment.
                ulong branch=isCritical?0x1403703bdul:resist?0x14036f45dul:0x14036fff4ul;
                C.Note(branch,"ComputedInactiveDefault",isCritical?"one_time.critical.null_manager_flag1":resist?"one_time.resist.null_manager_flag1":"one_time.damage.null_manager_flag1",value:1);
                return;
            }
            C.External(manager,0x4460,"Npc-manager");
            uint lockMode=C.U32(manager+0x88,site,"Npc.manager_lock_mode");
            C.Note(site,"ManagedInfrastructure","serialized manager read interval; lock mode preserved",value:lockMode);
            int flags;
            if(isCritical){flags=C.I32(manager+0x4454,0x14037038d,"one_time.critical_flags");_=C.I32(manager+0x4450,0x140370393,"one_time.critical_threshold");}
            else if(resist){_=C.I32(manager+0x443c,0x14036f3fa,"one_time.resist_percent");_=C.I32(manager+0x4440,0x14036f400,"one_time.resist_flat");flags=C.I32(manager+0x4444,0x14036f406,"one_time.flags");}
            else{_=C.I32(manager+0x4438,0x14036ffb5,"one_time.damage_flat");flags=C.I32(manager+0x4444,0x14036ffbb,"one_time.flags");_=C.I32(manager+0x4434,0x14036ffc2,"one_time.damage_percent");}
            if(!isCritical)_=C.U32(manager+0x88,site,"Npc.manager_lock_mode_after");
            else _=C.U32(manager+0x88,0x14037039a,"Npc.manager_lock_mode_after");
            Need((flags&2)==0,"incomplete-child","one-time.active_mask2",site,"Active original one-time effect consumption and subsequent stat changes require their complete owner.",true);
        }
        private int NpcClassifier(ulong caller)
        {
            uint kind=C.U32(A+0x1940,0x140b221f0,"Npc.classifier_kind1940");int result=0;
            if((kind&0x25f6)!=0)result=unchecked((int)C.U32(A+0x188c,0x140b221fc,"Npc.classifier_master188c"))>0?1:0;
            C.Note(caller,"ComputedClassifier","Npc slot3f8",value:result);return result;
        }
        private int ElementLevel()
        {
            if(Level(A,0x14036bbc3)<=50)return 0;
            if(Level(D,0x14036bbd4)<=50)return 0;
            int target=Level(D,0x14036bbea),source=Level(A,0x14036bbf8);
            int chosen=target>source?Level(A,0x14036bc0d):Level(D,0x14036bc2c);
            return unchecked((chosen-50)*10);
        }
        private int Move(int row,int column)
        {
            Need(row is >=0 and <=7&&column is >=0 and <7,"range","movement.row",0,"Native movement row out of range.");
            if(!movementTableAdmitted)
            {
                C.External(0x141069ed0,224,"movement-table");
                Need(Hash(C.Peek(0x141069ed0,224))=="a6f00c22fbed0ed08350a29539b1a3ff6f9870bf0039e49bbff4964a56d82917","source-table","movement",0,"Complete immutable file-backed movement words differ from the pinned PE source.");
                C.Note(0,"SourceTableAdmission","224 immutable movement bytes");movementTableAdmitted=true;
            }
            ulong address=0x141069ed0ul+(ulong)row*28;
            return C.I32(address+(ulong)column*4,0x140370216,"file-backed movement row");
        }
        private float Pve()
        {
            int value=SnapshotScalar(0x20000,0x210,0x1674,false,0x140370894,"magical_pve");
            // Live getter includes the raw current 7c bonus; cached getter does not.
            // SnapshotScalar records selection; reproduce that branch without extra reads.
            Step? selected=C.Steps.LastOrDefault(x=>x.Kind=="Read"&&(x.Operand=="snapshot.magical_pve"||x.Operand=="live.magical_pve"));
            if(selected?.Operand=="live.magical_pve")value=unchecked(value+C.I32(D+0xd04,0x1400b1980,"User.live_bonus7c_D04"));
            int query=C.I32(D+0x738,0x1403708b8,"User.query738");
            int source;
            if(query==0)
            {
                source=C.I32(A+0x1bd0,0x14036c204,"Npc.query_magic1bd0");
                Need(C.U64(A+0x1848,0x14036c20b,"Npc.query_template1848")==B.Template,"stale-template","slot190",0x14036c20b,"Current template changed.");
            }
            else
            {
                Need(C.U64(A+0x1848,query==1?0x14036c24ful:0x14036c20bul,"Npc.query_template1848")==B.Template,"stale-template","slot190",0x14036c24f,"Current template changed.");
                source=query==1?C.I32(A+0x1be0,0x14036c256,"Npc.query_magic1be0"):0;
            }
            int template=C.I32(B.Template+0x244,query==1?0x14036c25dul:0x14036c212ul,"template.magic244");
            value=unchecked(value+unchecked(source-template));
            int maximum=C.I32(D+0x774,0x1403708e6,"User.maximumHP774"),current=C.I32(D+0x778,0x1403708f4,"User.currentHP778");
            hp2Delta=Hp2(current,maximum,value);value=unchecked(value+hp2Delta.Value);pveDefense=value;
            int bounded=System.Math.Clamp(value,-10000,10000);
            return F("pve_ratio",System.Math.Max(0.1f,System.Math.Min(12f,Sub(Add(1f,0f),Div(FromInt(bounded),1000f)))));
        }
        private int Hp2(int current,int maximum,int baseDefense)
        {
            Telemetry(0x140618c95);
            ulong pointer=C.U64(D+0x8008,0x140618da0,"User.HP2_program8008");if(pointer==0)return 0;
            C.External(pointer,4,"HP2-program-header");
            if(C.U16(pointer+2,0x140618da9,"HP2.tag")!=0x12)return 0;
            // Header and body are one role; add only the nonoverlapping remaining range.
            C.External(pointer+4,28,"HP2-program-body");int row=-1;
            for(int i=0;i<3;i++)if(C.U16(pointer+10+(ulong)i*8,0x140618e10,"HP2.row"+i+".attribute")==0x78){row=i;break;}
            if(row<0)return 0;
            float hp=F("hp2.hp",FromInt(current));
            byte mode=C.U8(pointer,0x140618e5f,"HP2.header_mode");
            if(mode==0){Need(maximum>0,"incomplete-child","HP2.maximum",0x140618e5f,"Finite percentage mode needs positive maximum.",true);hp=F("hp2.percent",Mul(F("hp2.fraction",Div(hp,FromInt(maximum))),100f));}
            int high=C.U16(pointer+6,0x140618e74,"HP2.high");float factor=0f;
            if(hp<=high)
            {
                int low=C.U16(pointer+4,0x140618e85,"HP2.low");
                if(hp<low)factor=1f;
                else{Need(high!=low,"incomplete-child","HP2.equal_bounds",0x140618e85,"Exceptional equal-window arithmetic unsupported.",true);factor=Sub(1f,F("hp2.window_quotient",Div(F("hp2.window_numerator",Sub(hp,FromInt(low))),FromInt(high-low))));if(factor>1f)factor=1f;}
            }
            F("hp2.factor",factor);
            ulong selected=pointer+8+(ulong)row*8;
            int rowLow=C.U16(selected+4,0x140618ed2,"HP2.row_low"),rowHigh=C.U16(selected+6,0x140618ed6,"HP2.row_high");
            byte rowMode=C.U8(selected,0x140618edc,"HP2.row_mode");
            float product=F(rowMode!=0?"hp2.absolute_product":"hp2.percent_product",Mul(FromInt(rowHigh-rowLow),factor));
            if(rowMode!=0)return unchecked(Cvtt(product)+rowLow);
            return Cvtt(F("hp2.percent_base_product",Mul(F("hp2.percent_divided",Div(F("hp2.percent_added",Add(product,FromInt(rowLow))),100f)),FromInt(baseDefense))));
        }
        private void Debug(ulong site)
        {
            _=C.I32(A+0x8f4,site,"Npc.weapon_type_format_argument");C.Note(site,"ManagedInfrastructure","formatter normal return/no gameplay writer");
            C.External(0x14276916c,4,"debug-mask");
            if((C.U32(0x14276916c,0x14053ebd2,"debug.mask")&1)!=0)
            {C.External(0x142769b90,4,"debug-category");Need((C.U32(0x142769b90,0x14053ebdd,"debug.category")&0x100)==0,"incomplete-child","User.Hear",0x14053ebfb,"Active gameplay-visible debug child unsupported.",true);}
        }
        private void Telemetry(ulong site)
        {C.External(0x14275eb74,4,"telemetry");Need(C.I32(0x14275eb74,site,"current.telemetry_guard")==0,"incomplete-child","telemetry",site,"Active instrumentation needs its original clocks/owner.",true);}
        private ulong ClassLookup(int id)
        {
            int bucket=unchecked((uint)(id-200000))<100000?0:unchecked((uint)(id-600000))<300000?1:-1;if(bucket<0)return 0;
            int index=unchecked(id-(bucket==0?200000:600000));ulong countAddress=0x14f398f40ul+(ulong)bucket*4,tableAddress=0x14f398f30ul+(ulong)bucket*8;
            C.External(countAddress,4,"class-count");C.External(tableAddress,8,"class-table");
            int count=C.I32(countAddress,0x1400a9c44,"lookup.count");ulong table=C.U64(tableAddress,0x1400a9c49,"lookup.table");
            if(table==0||index<0)return 0;
            Need(table<=ulong.MaxValue-(ulong)index*8,"range","lookup.entry",0x1400a9c59,"Wrapped entry pointer.");
            ulong address=table+(ulong)index*8;C.External(address,8,"class-entry");
            ulong entry=C.U64(address,0x1400a9c59,"lookup.entry_before_count_compare");return entry!=0&&index<count?entry:0;
        }
        private uint ClockValue(ClockRole role,ulong caller,ulong site)
        {
            Need(clockIndex<input.Clocks.Length,"clock",role.ToString(),site,"Missing reached original clock.");Clock value=input.Clocks[clockIndex++];
            Need(value is not null,"clock",role.ToString(),site,"Reached original clock member is absent.");
            Need(value.Role==role&&value.Caller==caller&&value.Site==site&&value.Actor==D&&value.Life==B.UserLife&&value.Operation==input.Context.Operation&&value.Epoch==input.Context.Epoch,
                "clock",role.ToString(),site,"Original role/order/site/actor/lifetime/epoch mismatch.");Text(value.Receipt);clocks.Add(value);C.Note(site,"OriginalClock",role.ToString(),value:value.Tick);return value.Tick;
        }
        private int Draw(string stage,ulong site,int first,int second)
        {
            Need(draws.Count<input.Limits.MaxRandomOperations,"budget","random.operations",site,"Cumulative operation capacity reached.",true);
            var source=input.Random;
            var transaction=source with {InitialGeneration=randomGeneration,InitialStateBytes=randomState?.Bytes??source.InitialStateBytes,
                InitialStateSha256=randomState?.Sha256??source.InitialStateSha256,
                Ownership=source.Ownership is null?null:source.Ownership with {InitialGeneration=randomGeneration},
                Operations=[new(0,input.Context.Operation+":"+input.Context.Epoch+":magic:"+draws.Count.ToString(CultureInfo.InvariantCulture),RetailRandomOperationKind.SignedInclusive,randomGeneration,first,second)],
                Limits=new(){MaxOperations=1,MaxRawDraws=input.Limits.MaxRawDraws-rawDrawCount,MaxTraceBytes=input.Limits.MaxRandomLogicalTraceBytes-randomTraceBytes}};
            var result=RetailRandomOwnerPlan.Calculate(transaction);
            if(!result.Completed){randomFailure=result;throw Error("rng-"+result.Status,stage,site,result.Detail,true);}
            var operation=result.CompletedPrefix.Single();Need(operation.SignedValue.HasValue&&result.CompleteNextGeneration.HasValue,"rng",stage,site,"Missing completed operation result.");
            rawDrawCount=checked(rawDrawCount+result.Draws.Length);randomTraceBytes=checked(randomTraceBytes+result.LogicalTraceBytesCharged);
            randomGeneration=result.CompleteNextGeneration.Value;if(result.EngineStateAdmitted)randomState=result.CompleteFinalState;
            int value=operation.SignedValue.Value;draws.Add(new(stage,site,first,second,value,result.Draws,randomGeneration));C.Note(site,"ComputedRng",stage,value:value);return value;
        }
        private Result Finish(bool passed,int? preliminary,int? amount,int? fallback)
        {
            Need(clockIndex==input.Clocks.Length,"clock","unused tail",0,"Unconsumed supplied clocks are not part of this original branch.");
            Need(!passed||fallback.HasValue,"input","dispatch fallback",0,"Complete Begin calculation required before capability construction.");
            Memory memory=C.Snapshot();
            BeginDispatch? dispatch=passed?BeginDispatch.Create(DispatchToken,B.RunInfo,fallback.GetValueOrDefault(),input.Context,S,memory,randomState,randomGeneration,B):null;
            return new(passed,preliminary,amount,dispatch,memory,C.Steps,[..clocks],[..draws],randomState,randomGeneration,randomTraceBytes,
                floats.ToImmutableDictionary(StringComparer.Ordinal),new(resistThreshold,boostQuotient,armor,criticalThreshold,critical,hp2Delta,pveDefense));
        }
        private void Validate()
        {
            CheckContext(input.Context);
            Need(input.Selected is not null&&input.Memory is not null&&input.Boundary is not null&&input.Random is not null&&input.Floating is not null&&input.Infrastructure is not null&&input.Limits is not null&&!input.Clocks.IsDefault,
                "input","required",0,"Explicit complete immutable inputs required.");
            Need(S.Sources==input.Context.Sources,"source","capability",0,"Selected source and current operation source contexts differ.");
            Need(input.Limits.MaxSteps is >0 and <=100000&&input.Limits.MaxRandomOperations is >=0 and <=4096&&input.Limits.MaxRawDraws is >=0 and <=1000000&&input.Limits.MaxRandomLogicalTraceBytes is >=0 and <=40000000,"budget","limits",0,"Explicit bounded capacities required.");
            cursor=new(input.Context,input.Memory,input.Limits.MaxSteps);
            Need(input.Floating.NearestEven==true&&input.Floating.ExceptionsMasked==true&&input.Floating.FlushToZero==false&&input.Floating.DenormalsAreZero==false&&input.Floating.NormalReturn==true,
                "floating","environment",0,"Explicit RNE/masked/FTZ-DAZ-off normal-return contract required, not a claimed observed MXCSR.");
            Need(input.Infrastructure.ExclusiveSerializedLease==true&&input.Infrastructure.NoInlineGameplayWriter==true&&input.Infrastructure.NormalReturn==true&&input.Infrastructure.FormattingScratchExcluded==true,
                "infrastructure","lease",0,"Exact current serialized/no-gameplay-writer/normal-return managed infrastructure interval required.");
            foreach(string value in new[]{B.SourceLife,B.UserLife,B.SkillGeneration,B.TemplateGeneration,B.CallerGeneration,B.CallerProducerReceipt,B.RandomLife,B.RandomOrder})Text(value);
            var core=new[]{(A,0x2000),(D,0x8010),(B.Template,0x500),(B.Skill,0x1000),(B.RunInfo,0x70),(B.CheckRsp,0x1e8),(B.CheckRegisters,20),(H,88)};
            for(int i=0;i<core.Length;i++){_=C.BindingAt(core[i].Item1,core[i].Item2);C.Protect(core[i].Item1,core[i].Item2);for(int j=0;j<i;j++)Disjoint(core[i],core[j]);}
            Need(C.BindingAt(A,0x2000).Generation==B.SourceLife&&C.BindingAt(D,0x8010).Generation==B.UserLife&&C.BindingAt(B.Template,0x500).Generation==B.TemplateGeneration&&C.BindingAt(B.Skill,0x1000).Generation==B.SkillGeneration&&C.BindingAt(B.RunInfo,0x70).Generation==B.CallerGeneration&&C.BindingAt(B.CheckRsp,0x1e8).Generation==B.CallerGeneration&&C.BindingAt(B.CheckRegisters,20).Generation==B.CallerGeneration&&C.BindingAt(H,88).Generation==B.CallerGeneration,
                "generation","owners",0,"Every source/current/caller generation must match independently.");
            Need(C.U64(A,0,"Npc.vtable")==NpcVtable&&C.U64(D,0,"User.vtable")==UserVtable&&C.I32(A+0x144,0,"Npc.type")==2&&C.I32(D+0x144,0,"User.type")==1,
                "actor","actual type",0,"Distinct actual NPC and User classes required.");
            Need(C.I32(A+0x100,0,"Npc.id")==S.Npc.Id&&C.U32(A+0x1940,0,"Npc.kind")==1&&C.U32(A+0x188c,0,"Npc.master")==0&&C.U32(A+0x1894,0,"Npc.related")==0&&C.U64(A+0x1850,0,"Npc.virtual")==0&&C.U64(A+0x1bf8,0,"Npc.difficulty")==0&&C.U64(A+0x1848,0,"Npc.template")==B.Template&&C.I32(A+0x914,0,"Npc.current914")==0&&(C.U32(B.Template+0x434,0,"template434")&0x200000)==0,
                "source-domain","ordinary NPC",0,"Independent actual master/related/virtual/difficulty/current dual and template flags required; family labels cannot substitute.");
            Need(C.I32(B.Skill+0x148,0,"Skill.id")==S.Spell.Id&&C.I32(B.Skill+0x2c8,0,"Skill.effect_class")==0,
                "stale-skill","identity",0,"Current Skill identity/effect index differs from source capability.");
            Need(C.U64(B.RunInfo,0,"RunInfo.Skill")==B.Skill&&C.U64(B.RunInfo+0x48,0,"RunInfo.source")==A&&C.U64(B.RunInfo+0x50,0,"RunInfo.User")==D&&unchecked((short)C.U16(B.RunInfo+0x38,0,"RunInfo.level"))==S.Spell.Binding.SkillLevel,
                "stale-caller","RunInfo",0,"Actual signed level and actor/source caller binding differ.");
            Need(C.U64(B.CheckRegisters,0x140bc0a71,"Check.original_RDX")==B.Skill&&unchecked((short)C.U16(B.CheckRegisters+8,0x140bc0a6d,"Check.original_R8W"))==S.Spell.Binding.SkillLevel&&C.I32(B.CheckRegisters+16,0x140bc0a6a,"Check.original_R9D")==0,
                "stale-caller","original registers",0,"Original register widths/Skill/signed source level/effect index must bind this invocation independently of RunInfo.");
            Need(C.U64(B.CheckRsp+0x1b0,0,"Check.source")==A&&C.U64(B.CheckRsp+0x1c0,0,"Check.User")==D&&C.U64(B.CheckRsp+0x1c8,0,"Check.window")==H,
                "stale-caller","original frame",0,"Original retained caller frame must bind source/User/header, not guessed cache offsets.");
            Need(input.Random.Sources==new RetailRandomSourceDeclaration(RetailNpcSpellInstantParameters.MainSha256,RetailRandomEngine.RngEvidenceSha256)&&!input.Random.Operations.IsDefault&&input.Random.Operations.IsEmpty&&input.Random.Origin==(input.Context.Origin==Origin.ConstructedFixture?RetailRandomInputOrigin.ConstructedFixture:RetailRandomInputOrigin.DeclaredCurrentMemory),
                "rng-source","context",0,"One source-bound same-origin random engine with no supplied operations/answers required.");
            Need(input.Random.Ownership is not null&&input.Random.Ownership.LifetimeIdentity==B.RandomLife&&input.Random.Ownership.OrderIdentity==B.RandomOrder&&input.Random.Ownership.InitialGeneration==input.Random.InitialGeneration,
                "rng-source","lifetime/order",0,"Original serialized random lifetime/order must bind this invocation independently.");
            foreach(var image in input.Memory.Images)Disjoint((image.Binding.Address,image.Bytes.Length),(RetailRandomEngine.NativeEngineAddress,5008));
        }
        private static void Disjoint((ulong Address,int Bytes) a,(ulong Address,int Bytes) b)
        {Need(a.Address<=ulong.MaxValue-(ulong)a.Bytes&&b.Address<=ulong.MaxValue-(ulong)b.Bytes&&(a.Address+(ulong)a.Bytes<=b.Address||b.Address+(ulong)b.Bytes<=a.Address),"alias","roles",0,"Unsupported overlap of actor/template/Skill/caller/output/random roles.");}
    }
}
