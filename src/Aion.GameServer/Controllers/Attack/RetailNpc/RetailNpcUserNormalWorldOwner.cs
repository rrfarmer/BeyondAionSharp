using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Aion.GameServer.Commons.Utils.RetailRandom;
using Aion.GameServer.GeoEngine.RetailPath;
using W=Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcUserNormalWorldInputs;
using A=Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcNormalAttackPreparation;
using C=Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcToUserNormalRecordCalculation;
using P=Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcUserNormalProtection;
using F=Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcUserNormalCompletion;
using static Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcUserPhysicalInputs;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Complete finite World68f150 invocation. All successful effects are calculated
/// and unexecuted. Preceding Npc::Attack target/heading ownership remains a separate boundary.</summary>
internal static class RetailNpcUserNormalWorldOwner
{
    internal enum Status { RejectedBeforeEvaluation,NativeTargetRejectedPrepared,CalculatedImmediateWorld,CalculatedRetainedImpact,IncompleteAtChild }
    internal readonly record struct Vector(float X,float Y,float Z);
    internal sealed record Recipient(uint ObjectId,string Life,string ConnectionGeneration,string Receipt);
    internal sealed record Packet(ImmutableArray<byte> Bytes,ImmutableArray<Recipient> Recipients);
    internal sealed record Totals(int Hp,int Mp,int ConvertedHp,int ConvertedMp,int Reflected,int Threat,int Classification,uint Header);
    internal sealed record Result(Status Status,Memory? Calculated,Packet? Emission,Totals? Totals,F.Result? Completion,
        F.OwnedImpact? Retained,ImmutableArray<Step> Steps,ImmutableArray<W.Clock> Clocks,ImmutableArray<C.RandomCall> RandomCalls,
        Failure? Failure,RetailRandomState? RandomStateAfter=null,ulong? RandomGenerationAfter=null,long? RandomTraceBytes=null)
    {
        internal bool CurrentActorObserved=>false;internal bool HostEffectsExecuted=>false;internal bool ActivationReady=>false;
        internal int? FinalHpAfterDeferredLife=>null;
    }
    /// <summary>Private calculated preparation and complete World-guard carrier. Its
    /// factory evaluates all gameplay predicates; no caller-supplied guard/result is accepted.</summary>
    internal sealed class GuardAdmission
    {
        internal C.Input CalculationInput{get;} internal W.Work Work{get;}
        internal Vector SourcePosition{get;} internal Vector UserPosition{get;}
        private readonly string memorySeal;private readonly int clockSeal,stepSeal;
        private GuardAdmission(W.Work work,Vector source,Vector target)
        {
            Work=work;SourcePosition=source;UserPosition=target;
            memorySeal=work.Fingerprint();clockSeal=work.ClockIndex;stepSeal=work.Steps.Length;
            CalculationInput=work.Entry.Record with{Memory=work.Cursor.Snapshot(),Clocks=work.NormalClockBlock(),
                Limits=work.Entry.Record.Limits with{MaxSteps=work.Entry.Budget.MaxSteps-work.Steps.Length,
                    MaxRandomOperations=work.Entry.Budget.MaxRandomOperations,MaxRawDraws=work.Entry.Budget.MaxRawDraws,
                    MaxRandomLogicalTraceBytes=work.Entry.Budget.MaxRandomTraceBytes}};
        }
        internal void RequireWork()=>Need(Work.Fingerprint()==memorySeal&&Work.ClockIndex==clockSeal&&Work.Steps.Length==stepSeal&&Work.RandomCalls.Count==0,
            "producer","World guard",0,"Calculated guard state/clock prefix was changed.");
        internal void RequireInput(C.Input input)=>Need(ReferenceEquals(input,CalculationInput),"producer","World core",0,
            "Only the exact immutable calculated World entry may select the prepared core entry.");
        internal static GuardResult Calculate(W.Entry entry)
        {
            W.Work? w=null;
            try
            {
                w=new(entry);W.ValidateCurrent(w);A.Apply(w.Cursor,entry.Record.Selected);
                Vector source=NpcPosition(w),target=UserPositionNow(w);
                if(!Inside(w,source)||!Inside(w,target)||!CanAttack(w)||w.Cursor.I32(w.User+0x778,0x1400b2bd0,"World.targetHP.fresh")<1)
                    return new(null,w,null,true);
                // Actual master0 is independently read, not a supplied range/LOS answer.
                uint guard=w.Cursor.U32(0x14271cd08,0x14068f39d,"World.extra_reach_enabled");
                int master=w.Cursor.I32(w.Source+0x188c,0x14068f3ab,"Npc.master.extra_reach");
                if(guard!=0&&master>0&&(w.Cursor.U8(w.Source+0x1940,0x14068f3c8,"Npc.kind.extra_reach")&6)!=0)
                    throw Error("incomplete-child","World.extra_reach_LOS",0x14068f3ce,"Reached alternate owner/range/LOS child.",true);
                return new(new(w,source,target),w,null,false);
            }
            catch(Domain e){return new(null,w,e.Failure,false);}
            catch(ArithmeticException e){return new(null,w,new("arithmetic","World guards",0,e.Message,true),false);}
        }
    }
    internal sealed record GuardResult(GuardAdmission? Value,W.Work? Work,Failure? Failure,bool NativeRejected);
    internal static Result EvaluateWorld(W.Entry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);var guard=GuardAdmission.Calculate(entry);W.Work? w=guard.Work;
        Result Fail(Failure f)=>new(f.Unsupported||guard.Value is not null?Status.IncompleteAtChild:Status.RejectedBeforeEvaluation,null,null,null,null,null,[],[],[],f);
        if(guard.Failure is not null)return Fail(guard.Failure);
        try
        {
            if(guard.NativeRejected)
            {
                Need(w is not null,"input","native guard work",0,"Calculated guard work missing.");w.RequireTail();
                return new(Status.NativeTargetRejectedPrepared,w.Cursor.Snapshot(),null,null,null,null,w.Steps,[..w.Consumed],[],null);
            }
            var admitted=guard.Value??throw Error("producer","World",0,"Missing calculated guard producer.");
            ImpactAdmission impact=ImpactAdmission.Calculate(admitted);w=impact.Work;
            Totals totals=impact.Totals;Packet packet=impact.Emission;
            F.Result? completion=null;F.OwnedImpact? retained=null;
            if(entry.Duration<=50)
            {
                completion=F.CompleteImmediate(impact);
                Need(completion.Completed,"completion",completion.Failure?.Operand??"Pass2",completion.Failure?.Site??0,
                    completion.Failure?.Detail??"Incomplete whole User HP completion.",true);
            }
            else
            {
                retained=F.OwnedImpact.CreateCalculated(impact);
            }
            // Native World clears after immediate Pass2 or enqueue; delayed Pass2 does not.
            w.Cursor.W64(w.User+0x13d4,0,0x140690166,"User.attack_pair.cleanup");
            w.Cursor.W64(w.Source+0x13d4,0,0x140690171,"Npc.attack_pair.cleanup");
            w.RequireTail();
            return new(retained is null?Status.CalculatedImmediateWorld:Status.CalculatedRetainedImpact,w.Cursor.Snapshot(),packet,totals,completion,retained,
                w.Steps,[..w.Consumed],[..w.RandomCalls],null,w.RandomState,w.RandomGeneration,w.RandomTraceBytes);
        }
        catch(Domain e){return Fail(e.Failure);}
        catch(ArithmeticException e){return Fail(new("arithmetic","World",0,e.Message,true));}
    }
    /// <summary>Unforgeable full calculated World producer. No raw result/totals factory
    /// exists: its factory performs the entire core/protection/preview/encoding procedure.</summary>
    internal sealed class ImpactAdmission
    {
        internal W.Work Work{get;} internal Totals Totals{get;} internal Packet Emission{get;}
        private readonly string memorySeal;private readonly int clockSeal,stepSeal,drawSeal;
        private ImpactAdmission(W.Work work,Totals totals,Packet packet)
        {Work=work;Totals=totals;Emission=packet;memorySeal=work.Fingerprint();clockSeal=work.ClockIndex;stepSeal=work.Steps.Length;drawSeal=work.RandomCalls.Count;}
        internal void RequireWork(W.Work work)=>Need(ReferenceEquals(work,Work)&&work.Fingerprint()==memorySeal&&
            work.ClockIndex==clockSeal&&work.Steps.Length==stepSeal&&work.RandomCalls.Count==drawSeal,
            "producer","World impact",0,"Calculated producer storage/clock/RNG footprint was changed.");
        internal static ImpactAdmission Calculate(GuardAdmission admitted)
        {
            admitted.RequireWork();W.Work w=admitted.Work;var entry=w.Entry;
            var child=C.CalculateFromWorld(admitted);Need(child.Success,"normal-child",child.Failure?.Operand??"normal",child.Failure?.Site??0,
                child.Failure?.Detail??"Incomplete actual User normal calculation.",true);
            C.Result record=child.Value!;w.Adopt(record);var c=w.Cursor;
            int threat=Cvtt(Mul(FromInt(unchecked(record.FinalRecordDamage*10+2)),c.F32(w.Source+0xff8,0x14068fa24,"Npc.threat_multiplier")));
            int classification=c.I32(w.Source+0x8e4,0x14068f69b,"Npc.physical_selector")!=0?1:2;
            P.Result protection=P.Calculate(w,record.FinalRecordDamage,classification);
            c.W32(w.Record,unchecked((uint)protection.Hp),0x14068fb0a,"record.protected_HP");
            c.W32(w.Record+4,unchecked((uint)protection.Mp),0x14068fb10,"record.protected_MP");
            c.W8(w.Record+0x32,protection.Flags,0,"record.protector_flags");
            byte movement=RetailNpcUserPhysicalInputs.Movement(c,w.User,w.Tick(W.Phase.Late,"FinalMovement",0x14068fb1b,0x140578ec9,w.User,entry.Record.Boundary.UserLife),"World.final.User.movement");
            c.Note(0x14068fb2e,"ComputedNoOp","World.final.source_Npc.movement",value:0);
            c.W8(w.Record+0x33,(byte)(movement&15),0x14068fb75,"record.final_movement33");
            uint header=c.U32(w.Header,0x14068fbe1,"World.computed_header");
            Need((header&0x10a0f)==0&&(header&0x10)==0,"incomplete-child","World.MoveBack_or_Spin",0x14068fbf1,
                "Reached computed movement header requires its complete gameplay owner.",true);
            var totals=new Totals(protection.Hp,protection.Mp,0,0,protection.Reflected,threat,classification,header);
            byte targetPercent=Preview(w,w.User,totals.Hp,totals.ConvertedHp,true);
            byte sourcePercent=Preview(w,w.Source,totals.Reflected,0,false);
            uint activity=w.Tick(W.Phase.Late,"WorldActivity",0x14068fd43,0x14068fd43,w.User,entry.Record.Boundary.UserLife);
            c.W32(w.User+0x42cc,unchecked(activity+10000),0x14068fd4f,"User.World.activity_due");
            Need(c.U32(w.User+0x595c,0x1405e6e80,"User.World.recall595c")==0,"incomplete-child","World.CancelRecall",0x14068fd5a,"Active recall cancellation owner.",true);
            byte sequence=unchecked((byte)(c.U8(w.Source+0x1684,0x14068fd5f,"Npc.attack_sequence")+1));
            c.W8(w.Source+0x1684,sequence,0x14068fd65,"Npc.attack_sequence.increment");
            Packet packet=new(Encode(w,sequence,targetPercent,sourcePercent),SelectRecipients(w,admitted.SourcePosition));
            return new(w,totals,packet);
        }
    }
    private static bool Inside(W.Work w,Vector v)
    {
        var c=w.Cursor;
        foreach(var x in new[]{(v.X,0x38ul,0x44ul),(v.Y,0x3cul,0x48ul),(v.Z,0x40ul,0x4cul)})
            if(x.Item1<c.F32(w.World+x.Item2,0,"World.lower")||c.F32(w.World+x.Item3,0,"World.upper")<=x.Item1)return false;
        return true;
    }
    private static bool CanAttack(W.Work w)
    {
        var c=w.Cursor;if(c.U64(w.Source+0x118,0x140369be9,"Npc.world118")==0)return false;
        if(c.U8(w.User+0x731b,0x1400d3860,"User731b")!=0||c.U8(w.User+0x731d,0x1400d3860,"User731d")!=0||
            c.U8(w.User+0x731c,0x1400d3860,"User731c")!=0)return false;
        if(c.U32(w.User+0x43a4,0x1400d3860,"User43a4")!=c.U32(w.User+0x43a0,0x1400d3860,"User43a0")||
            (c.U8(w.User+0x13c,0x1400d3860,"User.pose")&15)==2)return false;
        c.Note(0x1400941d0,"ComputedNoOp","actual User slot39",value:0);
        uint baseDetection=c.U32(w.Template+0x35c,0x1400bc3e0,"NpcClass.detection35c"),currentDetection=c.U32(w.Source+0x1420,0x1400bc3e0,"Npc.detection1420");
        if(c.U32(w.User+0x141c,0x1400bc3e0,"User.invisibility141c")>System.Math.Max(baseDetection,currentDetection))return false;
        if(c.I32(w.User+0x778,0x1400d3860,"User.alive_HP")<=0)return false;
        if((c.U32(w.Source+0x1250,0x140b21f80,"Npc.abnormal1250")&0x649f04c)!=0)return false;
        int state=c.I32(w.Source+0x13d0,0x140b21f80,"Npc.state13d0");
        Need(state is >=0 and <32,"range","Npc.stance",0,"Explicit known stance cell0..31 required.");
        if(c.I32(0x145a2d2c0+(ulong)state*4,0x140b21f80,"current Npc stance cell")!=0)return false;
        uint pose=c.U32(w.Source+0x13c,0x1404bf1a0,"Npc.event17.pose");uint low=pose&15;
        Need(low<15,"range","Object.event17",0,"Only the15 pinned complete static event rows are admitted; row15 is adjacent storage.");
        uint next=low is 1 or 2 or 3 or 4 or 11 or 14?low:0;
        if(next!=0&&(pose&0x310)==0)c.W32(w.Source+0x13c,(pose&0xfffffff0)|next,0x1404bf1a0,"Npc.event17.pose_write");
        c.Note(0x140b22134,"Computed","Npc.CanAttack ignores event17 return",value:1);return true;
    }
    private static Vector NpcPosition(W.Work w)
    {
        var c=w.Cursor;ulong a=w.Source;string life=w.Entry.Record.Selected.Current.SourceLife;
        uint now=w.Tick(W.Phase.Position,"NpcPosition",0x14068f2b6,0x140b1802e,a,life);
        uint start=c.U32(a+0x185c,0x140b18010,"Npc.motion.start");
        uint due=ReadRaw32(c,a+0x1858);
        if(start>=now)ReadVector(c,a+0x1860,"Npc.motion.prior");
        else
        {
            due=c.U32(a+0x1858,0x140b18010,"Npc.motion.due");ReadVector(c,a+0x120,"Npc.motion.endpoint");
            if(due>=now)ReadVector(c,a+0x186c,"Npc.motion.velocity");
            else{ReadVector(c,a+0x19fc,"Npc.motion.continuing_goal");_=c.U32(a+0x7c0,0x140b18010,"Npc.motion.speed");}
        }
        RetailMainVectorWords RawVector(ulong p)=>new(ReadRaw32(c,p),ReadRaw32(c,p+4),ReadRaw32(c,p+8));
        var state=new RetailNpcMainMotionState(RawVector(a+0x120),RawVector(a+0x1860),RawVector(a+0x186c),RawVector(a+0x19fc),start,due,ReadRaw32(c,a+0x7c0));
        string receipt=Hash(Encoding.UTF8.GetBytes(w.Entry.Receipt));bool synthetic=w.Entry.Record.Context.Common.Origin==Origin.ConstructedFixture;
        var result=RetailNpcMainMotion.Calculate(state,new(now,0x140b1802e,life,w.Entry.World.Revision,w.Entry.Record.Context.Common.Operation,receipt,synthetic),
            new(RetailNpcMainMotion.MainBinarySha256,RetailNpcMainMotion.GetterEvidenceSha256,NpcVtable,life,w.Entry.World.Revision,w.Entry.Record.Context.Common.Operation,receipt,true,true,false,false,true,synthetic));
        Need(result.Calculated&&result.Location.HasValue,"position",result.Branch??"Npc.current",0x140b18010,result.Detail??"Incomplete NPC current pose.",true);
        var p=result.Location.Value;return new(Normal(BitConverter.UInt32BitsToSingle(p.X)),Normal(BitConverter.UInt32BitsToSingle(p.Y)),Normal(BitConverter.UInt32BitsToSingle(p.Z)));
    }
    private static Vector UserPositionNow(W.Work w)
    {
        var c=w.Cursor;ulong a=w.User;uint now=w.Tick(W.Phase.Position,"UserPosition",0x14068f2c7,0x1400d1b10,a,w.Entry.Record.Boundary.UserLife);
        float seconds=Mul(FromUInt(unchecked(now-c.U32(a+0x423c,0x1400d1b00,"User.position.previous"))),BitConverter.UInt32BitsToSingle(0x3a83126f));
        float vx=c.F32(a+0x4458,0x1400d1b00,"User.position.vx"),vy=c.F32(a+0x445c,0x1400d1b00,"User.position.vy");
        float x=c.F32(a+0x120,0x1400d1b00,"User.position.x"),y=c.F32(a+0x124,0x1400d1b00,"User.position.y");
        float z=Add(Mul(seconds,c.F32(a+0x4460,0x1400d1b00,"User.position.vz")),c.F32(a+0x128,0x1400d1b00,"User.position.z"));
        return new(Add(Mul(seconds,vx),x),Add(Mul(seconds,vy),y),z);
    }
    private static byte Preview(W.Work w,ulong actor,int damage,int heal,bool user)
    {
        var c=w.Cursor;int max=c.I32(actor+0x774,user?0x14068fc6dul:0x14068fce1ul,"preview.maxHP");
        int hp=c.I32(actor+0x778,user?0x14068fc7cul:0x14068fcf0ul,"preview.currentHP");
        int after=unchecked(hp+heal-damage);long percent=max<1||after<1?0:((long)after*99/max)+1;
        if(percent<1)
        {
            if(user)Need(c.U32(w.Source+0x188c,0x140b22320,"Npc.InDuel.master")==0,"incomplete-child","Npc.InDuel",0,"Active source owner duel program.",true);
            uint old=c.U32(actor+0x728,0,"preview.pending728");if(old==0)c.W32(actor+0x728,1,0x14068fcd0,"preview.pending.CAS0to1");
            return 0;
        }
        return (byte)System.Math.Min(percent,100);
    }
    private static ImmutableArray<byte> Encode(W.Work w,byte sequence,byte targetPercent,byte sourcePercent)
    {
        var c=w.Cursor;using var output=new MemoryStream();using var writer=new BinaryWriter(output);
        writer.Write((ushort)0);writer.Write((ushort)0x1de);writer.Write((byte)0x56);writer.Write((ushort)0xfe21);
        writer.Write(c.U32(w.Source+0x70,0x1409f8ce0,"packet.source"));writer.Write(sequence);writer.Write(w.Entry.Duration);
        writer.Write((byte)w.Entry.Record.Selected.Ordinal);writer.Write(w.Entry.CallerByte);writer.Write(c.U32(w.User+0x70,0x1409f8ce0,"packet.target"));
        writer.Write(targetPercent);writer.Write(sourcePercent);uint header=c.U32(w.Header,0x1409f8ce0,"packet.header");writer.Write(header);
        Need((header&0x10a0f)==0&&(header&0x10010)==0,"incomplete-child","packet.movement_union",0,"Nonordinary calculated native packet header.",true);
        writer.Write((byte)1);writer.Write(c.I32(w.Record,0x1409f8ce0,"packet.record_damage"));
        writer.Write((byte)(c.U8(w.Record+0x31,0,"packet.record_critical")<<4|c.U8(w.Record+0x30,0,"packet.record_status")));
        byte flags=c.U8(w.Record+0x32,0,"packet.record_flags");writer.Write(flags);
        if((flags&0x15)!=0)writer.Write(c.Read(w.Record+8,28,0x1409f8ce0,"packet.protection_union"));
        if((flags&8)!=0)writer.Write(c.Read(w.Record+0x24,12,0x1409f8ce0,"packet.protected_recipient"));
        writer.Write((byte)0);writer.Flush();byte[] bytes=output.ToArray();
        Need(bytes.Length<=0x1fff,"budget","packet",0,"Native packet bound exceeded.");BinaryPrimitives.WriteUInt16LittleEndian(bytes,(ushort)bytes.Length);return [..bytes];
    }
    private static ImmutableArray<Recipient> SelectRecipients(W.Work w,Vector center)
    {
        var c=w.Cursor;int height=c.I32(w.World+0x30,0,"World.cell_y_dimension"),width=c.I32(w.World+0x34,0,"World.cell_x_dimension");
        Need(height is >0 and <=1_000_000&&width is >0 and <=1_000_000,"range","World.cells",0,"Explicit bounded dimensions required.");
        int lowX=System.Math.Clamp(unchecked(Cvtt(center.X)-100),0,width),highX=System.Math.Clamp(unchecked(Cvtt(center.X)+101),0,width);
        int lowY=System.Math.Clamp(unchecked(Cvtt(center.Y)-100),0,height),highY=System.Math.Clamp(unchecked(Cvtt(center.Y)+101),0,height);
        int cellLowX=lowX>>5,cellHighX=(System.Math.Max(highX,lowX)+31)>>5;
        int cellLowY=lowY>>5,cellHighY=(System.Math.Max(highY,lowY)+31)>>5;
        var result=ImmutableArray.CreateBuilder<Recipient>();var ids=new HashSet<uint>();
        foreach(var root in w.Entry.World.Users.OrderBy(x=>x.CellX).ThenBy(x=>x.CellY).ThenBy(x=>x.ListOrdinal))
        {
            Vector endpoint=ReadVector(c,root.Address+0x120,"recipient.endpoint");
            int x=Cvtt(endpoint.X),y=Cvtt(endpoint.Y);
            Need(x>=0&&y>=0&&x<width&&y<height,"scene","recipient.cell",0,"Current registry has invalid endpoint/cell ownership.");
            Need((x>>5)==root.CellX&&(y>>5)==root.CellY,"scene","recipient.cell ownership",0,"Declared complete cell/list ownership differs from actual endpoint.");
            uint id=c.U32(root.Address+0x70,0,"recipient.id");Need(ids.Add(id),"scene","duplicate ID",0,"Current User registry object IDs duplicate.");
            if((x>>5)<cellLowX||(x>>5)>=cellHighX||(y>>5)<cellLowY||(y>>5)>=cellHighY)continue;
            float dx=Sub(endpoint.X,center.X),dy=Sub(endpoint.Y,center.Y),dz=Sub(endpoint.Z,center.Z);
            // DPPS71 reduction is (X*X+Y*Y)+(Z*Z+0), then CVTT signed32.
            int distance=Cvtt(Add(Add(Mul(dx,dx),Mul(dy,dy)),Mul(dz,dz)));
            if(distance>10000)continue;
            // The complete integer sphere predicate runs before User::SendBuf reads
            // its connection pointer. Outside users do not consume that storage.
            ulong connection=c.U64(root.Address+0x3720,0x1400370d8,"recipient.connection3720");
            Need(connection==root.ConnectionAddress,"scene","recipient connection",0,"Current connection root differs from its owned declaration.");
            if(connection==0)continue;
            c.External(connection,1,"connection");
            Need(c.BindingAt(connection,1).Generation==root.ConnectionGeneration,"generation","recipient connection",0,"Connection lifetime differs.");
            Need(result.Count<w.Entry.Budget.MaxRecipients,"budget","recipients",0,"Whole emission recipient capacity reached.",true);
            result.Add(new(id,root.Life,root.ConnectionGeneration,root.ConnectionReceipt));
        }
        return result.ToImmutable();
    }
    internal static Vector ReadVector(Cursor c,ulong address,string name)=>new(c.F32(address,0,name+".x"),c.F32(address+4,0,name+".y"),c.F32(address+8,0,name+".z"));
    private static uint ReadRaw32(Cursor c,ulong address)=>BinaryPrimitives.ReadUInt32LittleEndian(c.Raw(address,4));
}
