using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Globalization;
using System.Diagnostics.CodeAnalysis;
using Aion.GameServer.Commons.Utils.RetailRandom;
using U=Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcUserPhysicalInputs;
using A=Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcNormalAttackPreparation;
using C=Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcToUserNormalRecordCalculation;
using H=Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcToUserImmediateDamageOwner;
using N=Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcImmediateDamageOwner;
using static Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcUserPhysicalInputs;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Inactive bounded current-state declarations for the complete admitted World
/// normal procedure. No memory, actor, clock, observer or socket is acquired here.</summary>
internal static class RetailNpcUserNormalWorldInputs
{
    internal const string WorldProof="6252bcb481431e3f7eab125b5554f8165fefc65750f71b3425ea64e4709d4793";
    internal const string LeafProof="02b85c435d6f170b048067c8d7df298a9f04a0f616c9ed70021151586d823a24";
    internal enum Phase { Position, Normal, Late, Hp }
    internal sealed record Clock(Phase Phase,string Role,ulong CallerSite,ulong TickSite,ulong Actor,
        string Life,uint Value,string Operation,string Epoch,string Receipt);
    internal sealed record UserRoot(ulong Address,string Life,int CellX,int CellY,int ListOrdinal,
        ulong ConnectionAddress,string ConnectionGeneration,string ConnectionReceipt);
    internal sealed class Scene
    {
        internal ulong WorldAddress{get;} internal string WorldLife{get;}
        internal string Owner{get;} internal string Revision{get;} internal string Receipt{get;}
        internal U.Context Context{get;} internal ImmutableArray<UserRoot> Users{get;}
        private Scene(U.Context context,ulong address,string life,string owner,string revision,string receipt,ImmutableArray<UserRoot> users)
        {Context=context;WorldAddress=address;WorldLife=life;Owner=owner;Revision=revision;Receipt=receipt;Users=users;}
        /// <summary>Declaration of the entire owned current User registry, not a chosen
        /// recipient list. Its source and completeness require a future host acquisition.</summary>
        internal static Scene DeclareComplete(U.Context context,ulong world,string life,string owner,string revision,
            string receipt,IEnumerable<UserRoot> currentUsers,int capacity)
        {
            CheckContext(context);Text(life);Text(owner);Text(revision);Text(receipt);
            Need(currentUsers is not null,"scene","current registry",0,"Complete current collection required.");
            Need(world!=0&&capacity is >0 and <=16_384,"scene","capacity",0,"Explicit bounded whole current registry required.");
            var users=currentUsers.Take(capacity+1).ToImmutableArray();
            Need(users.Length<=capacity&&users.All(x=>x is not null&&x.Address!=0)&&users.Select(x=>x.Address).Distinct().Count()==users.Length,
                "scene","registry",0,"Incomplete/unbounded/duplicate current User registry.");
            foreach(var u in users){Text(u.Life);Text(u.ConnectionGeneration);Text(u.ConnectionReceipt);Need(u.CellX>=0&&u.CellY>=0&&u.ListOrdinal>=0,"scene","cell/list ownership",0,"Explicit nonnegative current cell/list position required.");}
            foreach(var cell in users.GroupBy(x=>(x.CellX,x.CellY)))
                Need(cell.Select(x=>x.ListOrdinal).Order().SequenceEqual(Enumerable.Range(0,cell.Count())),"scene","complete cell list",0,"Current cell list must have complete unique contiguous native traversal ordinals.");
            return new(context,world,life,owner,revision,receipt,users);
        }
        internal bool CurrentWorldObserved=>false;
    }
    internal sealed record Limits(int MaxSteps,int MaxRecipients,int MaxRandomOperations,int MaxRawDraws,long MaxRandomTraceBytes);
    internal sealed class Entry
    {
        internal C.Input Record{get;} internal H.Input HpCurrent{get;} internal H.OwnedGearSnapshot Gear{get;}
        internal Scene World{get;} internal ImmutableArray<Clock> Clocks{get;} internal Limits Budget{get;}
        internal ushort Duration{get;} internal byte CallerByte{get;} internal string Receipt{get;}
        private Entry(C.Input record,H.Input hp,H.OwnedGearSnapshot gear,Scene world,ImmutableArray<Clock> clocks,
            Limits budget,ushort duration,byte caller,string receipt)
        {Record=record;HpCurrent=hp;Gear=gear;World=world;Clocks=clocks;Budget=budget;Duration=duration;CallerByte=caller;Receipt=receipt;}
        internal static Entry Declare(C.Input record,H.Input hp,H.OwnedGearSnapshot gear,Scene world,
            ImmutableArray<Clock> clocks,Limits budget,ushort duration,byte caller,string receipt,
            string proof=WorldProof,string leaf=LeafProof)
        {
            Need(record is not null&&hp is not null&&gear is not null&&world is not null&&budget is not null&&!clocks.IsDefault,
                "input","World",0,"Complete source/current/owned-gear/scene/clock declarations required.");
            Need(record.Selected is not null&&record.Random is not null&&record.Context is not null,"input","normal current",0,"Private selected source and shared RNG declaration required.");
            A.ValidateContext(record.Context);Need(proof==WorldProof&&leaf==LeafProof,"source","World",0,"Exact reviewed native/leaf evidence required.");
            Need(world.Context==record.Context.Common&&budget.MaxSteps is >0 and <=100_000&&budget.MaxRecipients is >0 and <=16_384&&
                budget.MaxRandomOperations>=0&&budget.MaxRawDraws>=0&&budget.MaxRandomTraceBytes>=0,"input","context/budget",0,"Joined bounded current interval required.");
            Need(record.Selected.Ordinal is >=0 and <=255,"range","World selected ordinal byte",0,"Native World consumes an ordinal byte; no silent truncation/reselection.");
            Need(record.Selected.Context==record.Context&&hp.Source.MainSha256==record.Context.Common.Sources.Main&&
                hp.Source.DatasetSha256==N.Dataset&&hp.Source.RegionContext==N.Region,
                "source","HP-program-role",0,"HP current/program declaration retains its historical source role; do not relabel D as A.");
            Need(hp.Source.Operation==record.Context.Common.Operation&&hp.Source.Epoch==record.Context.Common.Epoch&&
                hp.Scope.AttackerNpcTemplateId==record.Selected.NpcId,"context","HP-current",0,"Actual same operation/epoch/source actor template required.");
            Text(receipt);foreach(var clock in clocks)
            {Need(clock is not null,"clock","record",0,"Missing clock declaration.");Need(Enum.IsDefined(clock.Phase),"clock","phase",0,"Undefined original clock phase.");Text(clock.Role);Text(clock.Receipt);}
            return new(record,H.CopyWorldCurrent(hp),gear,world,clocks,budget,duration,caller,receipt);
        }
        internal bool CurrentActorObserved=>false;
    }
    /// <summary>One fresh private calculation cursor and cumulative ordered RNG/clock
    /// owner. A supplied current engine is a declaration, never an observed global draw order.</summary>
    internal sealed class Work
    {
        internal Entry Entry{get;} internal Cursor Cursor{get;private set;}
        internal ImmutableArray<Step> Prior{get;private set;}=[];
        internal List<C.RandomCall> RandomCalls{get;}=[];
        internal List<Clock> Consumed{get;}=[];
        internal int ClockIndex{get;private set;}
        internal ulong RandomGeneration{get;private set;}
        internal RetailRandomState? RandomState{get;private set;}
        internal int RawDraws{get;private set;}
        internal long RandomTraceBytes{get;private set;}
        internal ulong Source=>Entry.Record.Selected.Current.SourceActor;
        internal ulong User=>Entry.Record.Boundary.UserActor;
        internal ulong Template=>Entry.Record.Selected.Current.SourceTemplate;
        internal ulong Frame=>Entry.Record.Boundary.WorldFrameRbp;
        internal ulong Record=>Frame+0x80;
        internal ulong Header=>Frame+0x18;
        internal ulong World=>Entry.World.WorldAddress;
        internal Work(Entry entry,Memory? memory=null)
        {
            Entry=entry;Cursor=new(entry.Record.Context.Common,memory??entry.Record.Memory,entry.Budget.MaxSteps);
            RandomGeneration=entry.Record.Random.InitialGeneration;
        }
        internal ImmutableArray<Step> Steps
        {
            get{Need(Prior.Length+Cursor.Steps.Length<=Entry.Budget.MaxSteps,"budget","whole.trace",0,"Whole read/write/child trace capacity reached.",true);
                return Prior.Concat(Cursor.Steps).Select((x,i)=>x with{Sequence=i+1}).ToImmutableArray();}
        }
        internal uint Tick(Phase phase,string role,ulong caller,ulong tick,ulong actor,string life)
        {
            Need(ClockIndex<Entry.Clocks.Length,"clock",role,tick,"Missing reached original clock.");var v=Entry.Clocks[ClockIndex++];
            Need(v.Phase==phase&&v.Role==role&&v.CallerSite==caller&&v.TickSite==tick&&v.Actor==actor&&v.Life==life&&
                v.Operation==Entry.Record.Context.Common.Operation&&v.Epoch==Entry.Record.Context.Common.Epoch,
                "clock",role,tick,"Original clock role/site/actor/lifetime/order differs.");
            Consumed.Add(v);Cursor.Note(tick,"OriginalClock",role,value:v.Value);return v.Value;
        }
        internal ImmutableArray<C.Clock> NormalClockBlock()
        {
            return Entry.Clocks.Skip(ClockIndex).TakeWhile(x=>x.Phase==Phase.Normal).Select(x=>
            {
                Need(Enum.TryParse<C.ClockRole>(x.Role,false,out var role)&&Enum.IsDefined(role),"clock",x.Role,x.TickSite,"Unknown normal child clock role.");
                return new C.Clock(role,x.CallerSite,x.TickSite,x.Value,x.Actor,x.Operation,x.Epoch,x.Life,x.Receipt);
            }).ToImmutableArray();
        }
        internal void Adopt(C.Result result)
        {
            Need(result.ConsumedClocks.Length==NormalClockBlock().Length,"clock","normal block",0,"Normal child did not consume the full ordered block.");
            foreach(var v in result.ConsumedClocks)
            {var clock=Entry.Clocks[ClockIndex++];Need(clock.Actor==v.ActorAddress&&clock.Life==v.ActorGeneration&&clock.Value==v.Value,"clock","normal join",v.TickSite,"Child clock differs.");Consumed.Add(clock);}
            Prior=Steps.AddRange(result.Steps);Cursor=new(Entry.Record.Context.Common,result.CalculatedMemory,Entry.Budget.MaxSteps);
            A.ProtectSource(Cursor,Entry.Record.Selected.Current);Cursor.Protect(User,Cursor.Available(User));Cursor.Protect(Frame-0x80,0x480);Cursor.Protect(World,0x54);
            RandomCalls.AddRange(result.RandomCalls);RandomState=result.RandomStateAfter;RandomGeneration=result.RandomGenerationAfter;
            RawDraws=result.RandomCalls.Sum(x=>x.RawDraws.Length);RandomTraceBytes=result.RandomLogicalTraceBytes;_=Steps;
        }
        internal int Draw(string name,ulong site,int low,int high)
        {
            Need(RandomCalls.Count<Entry.Budget.MaxRandomOperations,"budget","rng.operations",site,"Whole random operation limit reached.",true);
            var original=Entry.Record.Random;
            var next=original with{InitialGeneration=RandomGeneration,InitialStateBytes=RandomState?.Bytes??original.InitialStateBytes,
                InitialStateSha256=RandomState?.Sha256??original.InitialStateSha256,
                Ownership=original.Ownership is null?null:original.Ownership with{InitialGeneration=RandomGeneration},
                Operations=[new(0,Entry.Record.Context.Common.Operation+":World:"+RandomCalls.Count.ToString(CultureInfo.InvariantCulture),
                    RetailRandomOperationKind.SignedInclusive,RandomGeneration,low,high)],
                Limits=new(){MaxOperations=1,MaxRawDraws=Entry.Budget.MaxRawDraws-RawDraws,MaxTraceBytes=Entry.Budget.MaxRandomTraceBytes-RandomTraceBytes}};
            var result=RetailRandomOwnerPlan.Calculate(next);
            Need(result.Completed&&result.CompleteNextGeneration.HasValue,"rng-"+result.Status,name,site,result.Detail,true);
            var op=result.CompletedPrefix.Single();Need(op.SignedValue.HasValue,"rng",name,site,"Missing computed signed result.");
            RandomGeneration=result.CompleteNextGeneration.Value;if(result.EngineStateAdmitted)RandomState=result.CompleteFinalState;
            RawDraws=checked(RawDraws+result.Draws.Length);RandomTraceBytes=checked(RandomTraceBytes+result.LogicalTraceBytesCharged);
            RandomCalls.Add(new(RandomCalls.Count+1,name,site,low,high,op.SignedValue.Value,result.Draws,RandomGeneration));
            Cursor.Note(site,"ComputedRng",name,value:op.SignedValue.Value);return op.SignedValue.Value;
        }
        internal ImmutableArray<H.OriginalTick> HpClockBlock()
        {
            return Entry.Clocks.Skip(ClockIndex).TakeWhile(x=>x.Phase==Phase.Hp).Select(x=>
            {
                Need(Enum.TryParse<H.OriginalTickRole>(x.Role,false,out var role)&&Enum.IsDefined(role),"clock",x.Role,x.TickSite,"Unknown User HP clock role.");
                Need(x.Actor==User&&x.Life==Entry.Record.Boundary.UserLife&&x.Operation==Entry.Record.Context.Common.Operation&&
                    x.Epoch==Entry.Record.Context.Common.Epoch&&x.CallerSite==0,"clock",x.Role,x.TickSite,"Wrong HP actor/context/clock caller declaration.");
                return new H.OriginalTick(role,x.TickSite,x.Value,x.Operation,x.Epoch,new(N.FactKind.SourceBoundDeclaration,x.Receipt));
            }).ToImmutableArray();
        }
        internal void ConsumeHpClocks(int count)
        {Need(count==HpClockBlock().Length,"clock","HP block",0,"HP child did not consume its full clock block.");for(int i=0;i<count;i++)Consumed.Add(Entry.Clocks[ClockIndex++]);}
        internal void RequireTail()=>Need(ClockIndex==Entry.Clocks.Length,"clock","tail",0,"Unconsumed original clock records.");
        internal string Fingerprint()=>Hash(System.Text.Encoding.UTF8.GetBytes(string.Join(";",Cursor.Snapshot().Images.Select(x=>
            x.Binding.Address.ToString("x",CultureInfo.InvariantCulture)+":"+x.Binding.Identity+":"+x.Binding.Generation+":"+x.BytesSha256+":"+x.KnownSha256))));
        internal void Flush()
        {Prior=Steps;Cursor=new(Entry.Record.Context.Common,Cursor.Snapshot(),Entry.Budget.MaxSteps);}
    }
    internal static void ValidateCurrent(Work w)=>ValidateCurrentCore(w,false);
    internal static void ValidateRetainedCurrent(Work w,RetailNpcUserNormalCompletion.OwnedImpact owned)
    {owned.RequireFrame(w);ValidateCurrentCore(w,true);}
    private static void ValidateCurrentCore(Work w,bool producedPending)
    {
        var c=w.Cursor;var e=w.Entry;var common=e.Record.Context.Common;
        // Adapter role/alias admission precedes the native procedure. These are not
        // manufactured native reads or gameplay success callbacks.
        ulong[] worldTables=[0x141242520,0x141242cc0,0x1412254f0,0x141225c18,0x141227518,
            0x14122bc48,0x14122a610,0x14122b1f0,0x141263180,0x1412521d8];
        Need(worldTables.Contains(e.HpCurrent.World.Vtable),"world","actual class",0,"Current World class lacks the pinned ordinary HP/world programs.");
        A.ProtectSource(c,e.Record.Selected.Current);
        foreach(var owner in new[]{(w.Source,0x2000),(w.Template,0x500),(w.User,c.Available(w.User)),(w.Frame-0x80,0x480)})
            A.Disjoint(w.World,0x54,owner.Item1,owner.Item2);
        A.Disjoint(w.User,c.Available(w.User),w.Source,0x2000);A.Disjoint(w.User,c.Available(w.User),w.Template,0x500);
        A.Disjoint(w.Frame-0x80,0x480,w.User,c.Available(w.User));A.Disjoint(w.Frame-0x80,0x480,w.Source,0x2000);A.Disjoint(w.Frame-0x80,0x480,w.Template,0x500);
        c.Protect(w.User,c.Available(w.User));c.Protect(w.Frame-0x80,0x480);c.Protect(w.World,0x54);
        Need(c.U64(w.World,0,"World.vtable")==e.HpCurrent.World.Vtable&&c.BindingAt(w.World,0x54).Generation==e.World.WorldLife&&
            e.HpCurrent.World.Generation==e.World.WorldLife,"world","current owner",0,"Actual World class/lifetime/current owner differs.");
        Need(c.U32(w.World+0x20,0,"World.map20")==e.HpCurrent.World.MapId20&&c.U32(w.World+0x24,0,"World.id24")==e.HpCurrent.World.NativeId24,
            "world","identity",0,"World identity differs from HP current state.");
        Need(c.U64(w.Source+0x118,0,"Npc.World118")==w.World&&c.U64(w.User+0x118,0,"User.World118")==w.World,
            "world","membership",0,"Managed current invocation must belong to one actual World; not a native CanAttack comparison.");
        Need(c.BindingAt(w.Source,0x2000).Generation==e.Record.Selected.Current.SourceLife&&c.BindingAt(w.User,0x8010).Generation==e.Record.Boundary.UserLife,
            "generation","actors",0,"Actual actor life bindings differ.");
        Need(c.U64(w.Source,0,"Npc.vtable")==NpcVtable&&c.I32(w.Source+0x144,0,"Npc.type")==2&&
            c.U64(w.User,0,"User.vtable")==UserVtable&&c.I32(w.User+0x144,0,"User.type")==1,"actor","classes",0,"Actual NPC and User classes required.");
        Need(c.U32(w.Source+0x70,0,"Npc.id")==e.HpCurrent.Attacker.Binding.ObjectId&&c.U32(w.User+0x70,0,"User.id")==e.HpCurrent.Receiver.Binding.ObjectId,
            "actor","object IDs",0,"Raw and logical actor identities differ.");
        Need(c.I32(w.Source+0x1940,0,"Npc.kind")==1&&c.U32(w.Source+0x188c,0,"Npc.master")==0&&c.U32(w.Source+0x1894,0,"Npc.related")==0,
            "incomplete-child","ordinary owner",0,"Only actual kind1/master0/related0 source is admitted.",true);
        Need(e.Record.Floating==new C.Floating(true,true,false,false,true)&&e.Record.Infrastructure==new C.Infrastructure(true,true,true),
            "environment","World",0,"Explicit admitted floating/serialized normal-return contract required.");
        Need(c.I32(0x14275eb74,0,"current diagnostic guard")==0,"incomplete-child","telemetry",0,"Active native diagnostics add unknown calls.",true);
        Need(e.World.Users.Count(x=>x.Address==w.User)==1,"scene","target membership",0,"Target must occur once in the complete current User registry.");
        foreach(var u in e.World.Users)
        {
            if(u.Address!=w.User)
            {
                foreach(var role in new[]{(w.Source,0x2000),(w.Template,0x500),(w.Frame-0x80,0x480),(w.World,0x54)})A.Disjoint(u.Address,c.Available(u.Address),role.Item1,role.Item2);
                c.Protect(u.Address,c.Available(u.Address));
            }
            Need(c.BindingAt(u.Address,0x148).Generation==u.Life&&c.U64(u.Address,0,"recipient.vtable")==UserVtable&&
                c.I32(u.Address+0x144,0,"recipient.type")==1&&c.U64(u.Address+0x118,0,"recipient.World")==w.World,
                "scene","current User root",0,"Current registry contains stale/incorrect User membership.");
        }
        Text(common.Receipt);JoinHpCurrent(w,e.HpCurrent,producedPending);
    }
    // These reads belong to the reached lethal User tail, not unconditional entry
    // admission. Only the private calculated World HP carrier invokes them.
    internal static H.UserTelemetry JoinReachedUserLog(Work w,H.UserTelemetry declared)
    {
        var c=w.Cursor;
        int field79c=c.I32(w.User+0x79c,0x1405421d8,"telemetry.User.field79c");
        int field584c=c.I32(w.User+0x584c,0x1405421df,"telemetry.User.field584c");
        int character=c.I32(w.User+0x3a70,0x1405421f5,"telemetry.User.character3a70");
        uint logId=c.U32(w.User+0x7254,0x140542205,"telemetry.User.log7254");
        string account=TerminatedUtf16(w,w.User+0x70bc,0x140542212,"telemetry.User.account70bc");
        string label=TerminatedUtf16(w,w.User+0x70da,0x140542221,"telemetry.User.label70da");
        Need(declared.Field79c==field79c&&declared.Field584c==field584c&&declared.Character3a70==character&&
            declared.Log7254==logId&&string.Equals(declared.Account70bc,account,StringComparison.Ordinal)&&
            string.Equals(declared.Label70da,label,StringComparison.Ordinal),
            "join","User lethal telemetry",0x140542352,"Reached structured telemetry must equal the same actual User bytes and UTF16 code units.");
        return new(logId,character,label,account,field584c,field79c,declared.Evidence);
    }
    internal static uint JoinReachedSourceWorld(Work w,H.SourceWorldBinding declared)
    {
        var c=w.Cursor;ulong pointer=c.U64(w.Source+0x118,0x140542655,"telemetry.Npc.World118");
        Need(declared.Presence==(pointer==0?H.Presence.VerifiedNull:H.Presence.Present),
            "join","source World presence",0x140542655,"Reached source-world presence contradicts the actual source pointer.");
        if(pointer==0)return 0; // A native null source world does not read a map word.
        Need(pointer==w.World&&c.BindingAt(pointer,0x24).Generation==w.Entry.World.WorldLife,
            "join","source World lifetime",0x140542655,"Reached source world must name the same current owned World lifetime.");
        uint map=c.U32(pointer+0x20,0x140542663,"telemetry.Npc.World.map20");
        Need(declared.MapId20==map,"join","source World map20",0x140542663,"Reached source map differs from actual current World bytes.");
        return map;
    }
    private static string TerminatedUtf16(Work w,ulong address,ulong site,string operand)
    {
        // An explicit adapter bound, not a claim about native inline-array capacity.
        // Read one little-endian code unit at a time: a known early terminator never
        // requires later bytes. No Unicode normalization, trimming or surrogate decode.
        const int maximumCodeUnits=256;
        int count=System.Math.Min(maximumCodeUnits,w.Cursor.Available(address)/2);
        var characters=new char[count];
        for(int i=0;i<count;i++)
        {
            ushort unit=w.Cursor.U16(address+(ulong)(2*i),site,operand+"["+i+"]");
            if(unit==0)return new string(characters,0,i);
            characters[i]=(char)unit;
        }
        throw Error("capacity",operand,site,"No known UTF16 terminator within the explicit finite image/code-unit bound.");
    }
    internal static void JoinHpCurrent(Work w,H.Input hp,bool allowProducedPending)
    {
        var c=w.Cursor;
        Need(hp.Attacker.Binding.LifeGeneration==w.Entry.Record.Selected.Current.SourceLife&&hp.Receiver.Binding.LifeGeneration==w.Entry.Record.Boundary.UserLife,
            "join","HP life",0,"Logical HP/current raw lifetimes differ.");
        void Eq(ulong address,uint value,string name)=>Need(c.U32(address,0,"join."+name)==value,"join",name,0,"Raw current bank and logical HP state differ.");
        void State(ulong actor,N.OwnerState s)
        {
            foreach(var x in new[]{(0x774,s.MaxHp774),(0x778,s.Hp778),(0x78c,s.MaxMp78c),(0x790,s.Mp790),(0x728,s.Pending728),(0x788,s.Field788),(0x72c,s.Field72c)})Eq(actor+(ulong)x.Item1,unchecked((uint)x.Item2),"life"+x.Item1.ToString("x"));
            Eq(actor+0x770,s.Dirty770,"dirty770");Eq(actor+0x1308,s.LastAttacker1308,"lastAttacker1308");
            // Master/related/virtual are NPC-only offsets. The shared POD's unconsumed
            // User members must never be interpreted as fake NPC fields on actual User.
            if(actor==w.Source)
            {
                Eq(actor+0x188c,s.Master188c,"master188c");Eq(actor+0x1894,s.Related1894,"related1894");
                Need(c.U64(actor+0x1850,0,"join.virtual1850")==s.Pointer1850,"join","virtual1850",0,"Current source virtual pointer differs.");
            }
            ulong killer=c.U64(actor+0x918,0,"join.killer918");
            Need(s.Killer918 is null ? killer==0 : s.Killer918==w.Entry.HpCurrent.Attacker.Binding.ObjectId&&killer==w.Source,
                "join","killer918",0,"Raw killer is an actor pointer; join its managed ID through the actual source root, never cast pointer to object ID.");
        }
        State(w.Source,hp.Attacker.State);State(w.User,hp.Receiver.State);
        Need(allowProducedPending?hp.Receiver.State.Pending728 is 0 or 1:hp.Receiver.State.Pending728==0,"join","pending728",0,"Unproduced pending-death state at entry.");
        foreach(var pair in new[]{(w.Source,hp.Attacker.Telemetry),(w.User,hp.Receiver.Telemetry)})
        {
            ulong actor=pair.Item1;var t=pair.Item2;
            Eq(actor+0x734,unchecked((uint)t.Raw734),"telemetry734");Eq(actor+0x738,unchecked((uint)t.Race738),"telemetry738");
            Need(c.U16(actor+0x1492,0,"join.level1492")==t.Level1492,"join","level1492",0,"Actual current level differs.");
            Eq(actor+0x120,t.Position120.X,"endpointX");Eq(actor+0x124,t.Position120.Y,"endpointY");Eq(actor+0x128,t.Position120.Z,"endpointZ");
        }
        Eq(w.Source+0x100,hp.Attacker.Telemetry.LogId100,"Npc.class/log100");
        Eq(w.Template+0x354,unchecked((uint)hp.Attacker.Template.SourceType354),"NpcClass.type354");
        Need(c.U8(w.Template+0x3ec,0,"join.class3ec")==hp.Attacker.Template.SourceFlag3ec&&
            c.U8(w.Template+0x3ed,0,"join.class3ed")==hp.Attacker.Template.SourceFlag3ed,"join","NpcClass flags",0,"Raw attribution source flags differ.");
        var r=hp.Receiver.Raw;
        void U32(int offset,uint? value,string name){if(value.HasValue)Eq(w.User+(ulong)offset,value.Value,name);}
        void I32(int offset,int? value,string name)=>U32(offset,value.HasValue?unchecked((uint)value.Value):null,name);
        void Byte(int offset,byte? value,string name){if(value.HasValue)Need(c.U8(w.User+(ulong)offset,0,"join."+name)==value,"join",name,0,"Raw User byte differs.");}
        U32(0x13c,r.Pose13c,"pose13c");Byte(0x7319,r.Protection7319,"protection7319");Byte(0x74f7,r.SignedGuard74f7,"signed74f7");
        Byte(0x731b,r.Protection731b,"protection731b");Byte(0x731c,r.Protection731c,"protection731c");Byte(0x731d,r.Protection731d,"protection731d");
        U32(0x43a0,r.World43a0,"world43a0");U32(0x43a4,r.World43a4,"world43a4");I32(0x1318,r.Duel1318,"duel1318");
        Byte(0x1686,r.Connected1686,"connected1686");I32(0x7bf0,r.Spectator7bf0,"spectator7bf0");U32(0x7b80,r.SpectatorWorld7b80,"spectatorWorld7b80");
        if(r.DebugMask14276916c.HasValue)Eq(0x14276916c,r.DebugMask14276916c.Value,"debug global");
        Byte(0x8024,r.Activity8024,"activity8024");U32(0x3b60,r.Gather3b60,"gather3b60");U32(0x5680,r.Combine5680,"combine5680");
        U32(0x832c,r.Combine832c,"combine832c");U32(0x5698,r.CombineDue5698,"combineDue5698");U32(0x41e0,r.UseItem41e0,"useItem41e0");
        U32(0x56b4,r.Gauge56b4,"gauge56b4");if(r.Pet7948.HasValue)Need(c.U64(w.User+0x7948,0,"join.pet7948")==r.Pet7948,"join","pet7948",0,"Pet pointer differs.");
        U32(0x7d98,r.Hotspot7d98,"hotspot7d98");U32(0x141c,r.Invincibility141c,"User141c");U32(0x595c,r.Recall595c,"recall595c");
        I32(0x5a10,r.VerboseHistory5a10,"history5a10");I32(0x3a78,r.NativeClass3a78,"class3a78");U32(0x1698,r.Tribe1698,"tribe1698");
        Eq(w.User+0x42cc,r.ActivityDue42cc,"activity42cc");Eq(w.User+0x5b24,unchecked((uint)r.FirstDeathKind5b24),"first5b24");
        var g=hp.Guards;U32(0x1250,g.Abnormal1250,"abnormal1250");U32(0x131c,g.CurrentCast131c,"cast131c");U32(0x12cc,g.Systems12cc,"systems12cc");Byte(0x1425,g.Byte1425,"byte1425");
        Need(c.U64(w.User+0x158,0,"join.manager158")==g.Manager.Pointer158,"join","manager158",0,"Raw/managed effect manager differs.");
        if(g.Manager.Presence==N.ManagerPresence.Present)
        {
            c.External(g.Manager.Pointer158,0x4404,"manager");
            Need(c.BindingAt(g.Manager.Pointer158,0x4404).Generation==g.Manager.Generation&&g.Manager.Generation==hp.Receiver.Binding.ManagerGeneration,
                "join","manager generation",0,"Raw/HP actor/current manager lifetimes differ.");
            if(g.Manager.Flags2b18.HasValue)Eq(g.Manager.Pointer158+0x2b18,g.Manager.Flags2b18.Value,"manager flags");
        }
        foreach(var bound in new[]{(0x160,g.Bound160),(0x168,g.Bound168),(0x210,g.Bound210)})
            if(bound.Item2.Pointer.HasValue)Need(c.U64(w.User+(ulong)bound.Item1,0,"join.bound")==bound.Item2.Pointer,"join","bound skill",0,"Bound skill pointer differs.");
        ulong received=c.U64(w.User+0x270,0,"join.received270");
        if(r.Received270==H.Presence.VerifiedNull)Need(received==0,"join","received270",0,"Received context must be null.");
        else if(r.Received270==H.Presence.Present)
        {Need(received!=0,"join","received270",0,"Missing received context.");c.External(received,0x14,"received-bookkeeping");if(r.ReceivedCounter0c.HasValue)Eq(received+0xc,r.ReceivedCounter0c.Value,"received0c");if(r.ReceivedCounter10.HasValue)Eq(received+0x10,r.ReceivedCounter10.Value,"received10");}
    }
}
