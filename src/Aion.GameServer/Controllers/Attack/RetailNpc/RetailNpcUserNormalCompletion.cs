using System.Buffers.Binary;
using System.Collections.Immutable;
using Aion.GameServer.Commons.Utils.RetailRandom;
using W=Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcUserNormalWorldInputs;
using O=Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcUserNormalWorldOwner;
using A=Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcNormalAttackPreparation;
using H=Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcToUserImmediateDamageOwner;
using N=Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcImmediateDamageOwner;
using static Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcUserPhysicalInputs;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Whole admitted normal Pass2. Native storage infrastructure is represented by
/// managed ownership and typed unexecuted intents. No future HP/skill/callback result is supplied.</summary>
internal static class RetailNpcUserNormalCompletion
{
    internal enum Status { CalculatedImpactCompleted,AlreadyConsumed,AlreadyClaimed,Cancelled,AdapterInvalidated,AdapterNotDue,IncompleteAtChild }
    internal sealed record Intent(int Sequence,ulong Site,string Kind,ImmutableArray<byte> NativePacket,
        uint ObjectId,int? Current,int? Maximum,string Receipt);
    internal sealed record Result(Status Status,Memory? Calculated,H.OwnedResult? Hp,ImmutableArray<Intent> Outbox,
        ImmutableArray<Step> Steps,ImmutableArray<W.Clock> Clocks,Failure? Failure,RetailRandomState? RandomStateAfter=null,ulong? RandomGenerationAfter=null,long? RandomTraceBytes=null)
    {
        internal bool Completed=>Status==RetailNpcUserNormalCompletion.Status.CalculatedImpactCompleted;
        internal bool HostEffectsExecuted=>false;internal bool CurrentActorObserved=>false;internal bool ActivationReady=>false;
        internal int? FinalHpAfterDeferredLife=>null;
    }
    internal enum CancelReason { Death,Delete,WorldClose,Respawn }
    internal sealed record Cancellation(string Owner,string WorldLife,string SourceLife,string UserLife,CancelReason Reason);
    /// <summary>Private computed immutable payload plus an exclusive calculation token.
    /// The token is local ownership bookkeeping, not executed World/HP/scheduler state.
    /// An incomplete calculation releases its claim; successful dispatch/cancel consumes once.</summary>
    internal sealed class OwnedImpact
    {
        private int state;
        private readonly O.Totals totals;
        private readonly A.SelectedNormal selected;
        private readonly ulong source,user,world;
        private readonly string owner,worldLife,sourceLife,userLife;
        private readonly int producedPending;
        private readonly string? stream,lifetime;
        private readonly ulong minimumRandomGeneration;
        internal uint RelativeDelay{get;} internal uint Due{get;}
        internal ImmutableArray<byte> VisibleRecord{get;}
        internal O.Packet Emission{get;}
        private OwnedImpact(O.ImpactAdmission producer,uint now)
        {
            var w=producer.Work;totals=producer.Totals;selected=w.Entry.Record.Selected;
            source=w.Source;user=w.User;world=w.World;owner=w.Entry.World.Owner;worldLife=w.Entry.World.WorldLife;
            sourceLife=w.Entry.Record.Selected.Current.SourceLife;userLife=w.Entry.Record.Boundary.UserLife;
            producedPending=w.Cursor.I32(user+0x728,0,"retained.produced_pending");
            minimumRandomGeneration=w.RandomGeneration;stream=w.Entry.Record.Random.Ownership?.StreamIdentity;lifetime=w.Entry.Record.Random.Ownership?.LifetimeIdentity;
            RelativeDelay=(uint)(w.Entry.Duration-50);Due=unchecked(now+RelativeDelay);Emission=producer.Emission;
            VisibleRecord=[..w.Cursor.Read(w.Record,53,0x14068c240,"retained.visible_record_union")];
        }
        internal static OwnedImpact CreateCalculated(O.ImpactAdmission producer)
        {
            var w=producer.Work;producer.RequireWork(w);
            Need(w.Entry.Duration>50,"producer","retained",0,"Only a computed delayed World attack may produce a retained impact.");
            uint now=w.Tick(W.Phase.Late,"ManagedImpactSchedule",0,0,w.World,w.Entry.World.WorldLife);
            w.RequireTail();return new(producer,now);
        }
        internal bool TryClaim()=>Interlocked.CompareExchange(ref state,1,0)==0;
        internal void ReleaseIncomplete()=>Interlocked.CompareExchange(ref state,0,1);
        internal void CompleteClaim()=>Interlocked.CompareExchange(ref state,2,1);
        internal Status ExistingStatus=>Volatile.Read(ref state)==1?Status.AlreadyClaimed:Status.AlreadyConsumed;
        internal O.Totals Totals=>totals;
        internal A.SelectedNormal Selected=>selected;
        internal void RequireFrame(W.Work current)
        {
            var e=current.Entry;
            Need(current.Source==source&&current.User==user&&current.World==world&&e.World.Owner==owner&&e.World.WorldLife==worldLife&&
                e.Record.Selected.Current.SourceLife==sourceLife&&e.Record.Boundary.UserLife==userLife,
                "adapter-invalidated","retained actor/world generations",0,"Retained managed ownership/life generation no longer matches.");
            Need(e.Record.Context.DatasetId==selected.Context.DatasetId&&e.Record.Context.ManifestSha256==selected.Context.ManifestSha256&&
                e.Record.Context.Region==selected.Context.Region&&e.Record.Selected.NpcId==selected.NpcId&&e.Record.Selected.Ordinal==selected.Ordinal,
                "source","retained source cohort",0,"Source lineage changed; do not relabel A/D or silently reselect a profile.");
            Need(current.Cursor.I32(user+0x728,0,"retained.current_pending")==producedPending,
                "adapter-invalidated","retained pending lineage",0,"Current pending state differs from the privately produced preview lineage.");
            Need(e.Record.Random.InitialGeneration>=minimumRandomGeneration&&e.Record.Random.Ownership?.StreamIdentity==stream&&
                e.Record.Random.Ownership?.LifetimeIdentity==lifetime,"rng-source","retained current engine",0,
                "Use a fresh declaration of the same shared stream lifetime, never a per-impact cloned RNG.");
        }
        internal bool Cancel(Cancellation c)
        {
            Need(c is not null&&Enum.IsDefined(c.Reason)&&c.Owner==owner&&c.WorldLife==worldLife&&c.SourceLife==sourceLife&&c.UserLife==userLife,
                "adapter-invalidated","cancel generation",0,"Cancellation must name the exact owned actor/world lifetime.");
            return Interlocked.CompareExchange(ref state,3,0)==0;
        }
        internal bool CurrentActorObserved=>false;
    }
    /// <summary>Unforgeable current completion prefix. It is produced only from the full
    /// World program or its private retained payload, and calculates reached state/break guards.</summary>
    internal sealed class CompletionAdmission
    {
        internal W.Work Work{get;} internal O.Totals Totals{get;} internal A.SelectedNormal Selected{get;}
        private readonly string memorySeal;private readonly int clockSeal,stepSeal;
        private CompletionAdmission(W.Work work,O.Totals totals,A.SelectedNormal selected)
        {Work=work;Totals=totals;Selected=selected;memorySeal=work.Fingerprint();clockSeal=work.ClockIndex;stepSeal=work.Steps.Length;}
        internal void RequireCurrent()=>Need(Work.Fingerprint()==memorySeal&&Work.ClockIndex==clockSeal&&Work.Steps.Length==stepSeal,
            "producer","Pass2 prefix",0,"Calculated completion prefix was altered.");
        internal static CompletionAdmission Immediate(O.ImpactAdmission producer)
        {producer.RequireWork(producer.Work);Prefix(producer.Work,producer.Totals);return new(producer.Work,producer.Totals,producer.Work.Entry.Record.Selected);}
        internal static CompletionAdmission Retained(OwnedImpact owner,W.Entry entry)
        {
            var w=new W.Work(entry);W.ValidateRetainedCurrent(w,owner);
            // Managed scheduler admission, not a claimed native GetTickCount in Pass2.
            uint now=w.Tick(W.Phase.Late,"ManagedImpactDispatch",0,0,w.World,w.Entry.World.WorldLife);
            Need(unchecked((int)(now-owner.Due))>=0,"adapter-not-due","retained schedule",0,"The managed ordinary impact is not due; no HP candidate is calculated.");
            w.Cursor.W32(w.Header,owner.Totals.Header,0x14068c750,"retained.copied_header");
            Prefix(w,owner.Totals);return new(w,owner.Totals,owner.Selected);
        }
        private static void Prefix(W.Work w,O.Totals t)
        {
            Need((t.Header&0x10a0f)==0&&(t.Header&0x10)==0,"incomplete-child","Pass2 movement header",0x14068ee7c,"Unclosed moveback/spin branch.",true);
            if(t.Hp!=0||t.Mp!=0)
            {
                byte mode=w.Cursor.U8(w.User+0x1425,0x14037bcc0,"Pass2.target.mode1425");
                if(mode is not (1 or 3))
                {
                    // Actual User slot79 is Bound168 reason10. Pointer0 proves the whole
                    // wrapper's no-removal branch, not a chosen cast/End result.
                    Need(w.Cursor.U64(w.User+0x168,0x1400b2a00,"Pass2.target.bound168")==0,
                        "incomplete-child","Pass2.target.Bound168_End",0x14037bd27,"Active bound state requires its exact whole NoEnd/End owner.",true);
                }
                w.Cursor.Note(0x14068ee9d,"Computed","Pass2.target.slot161",value:mode);
            }
        }
    }
    /// <summary>Private native World context source. The old User entry stays context1,
    /// pending0 and generation-A; this produced entry retains actual generation-D identity.</summary>
    internal sealed class HpAdmission
    {
        internal H.Input Input{get;} internal H.OwnedGearSnapshot Gear{get;} internal A.SelectedNormal Selected{get;}
        private readonly W.Work work;
        private HpAdmission(H.Input input,H.OwnedGearSnapshot gear,A.SelectedNormal selected,W.Work current)
        {Input=input;Gear=gear;Selected=selected;work=current;}
        // A private produced World carrier alone may admit native-valid inline text.
        // The old standalone HP API continues to enforce its original finite Text rule.
        internal H.UserTelemetry NativeLogAtDeathTail(H.Input current)
        {RequireInput(current);return W.JoinReachedUserLog(work,current.Receiver.Log);}
        internal uint NativeSourceWorldAtDeathTail(H.Input current)
        {RequireInput(current);return W.JoinReachedSourceWorld(work,current.SourceWorld);}
        internal static HpAdmission Calculate(CompletionAdmission producer)
        {
            producer.RequireCurrent();var w=producer.Work;var c=w.Cursor;var old=w.Entry.HpCurrent;var t=producer.Totals;
            int hp=unchecked(t.Hp-t.ConvertedHp),mp=unchecked(t.Mp-t.ConvertedMp);
            Need(hp>=0&&mp>=0,"incomplete-child","Pass2.signed_heal",0x140373200,"Negative computed channel requires the actual User heal owner; never clamp.",true);
            var receiver=old.Receiver with{State=ReadState(w,w.User,old.Receiver.State),Raw=old.Receiver.Raw with
                {Pose13c=c.U32(w.User+0x13c,0,"HP.current_pose"),ActivityDue42cc=c.U32(w.User+0x42cc,0,"HP.current_activity"),
                 FirstDeathKind5b24=c.I32(w.User+0x5b24,0,"HP.current_first_kind")}};
            var attacker=old.Attacker with{State=ReadState(w,w.Source,old.Attacker.State)};
            var evidence=new N.Evidence(N.FactKind.SourceBoundDeclaration,"computed World68f150/68ed40 producer; not native execution");
            var call=old.Call with{RequestedHp=hp,RequestedMp=mp,RawThreat=t.Threat,Category=t.Classification,
                Context08=0,Context0c=0,Context10=0,Kind14=2,Skill18=0,Log1c=0xc5,Flags20=0,Critical21=0};
            // Pointer0 is the actual aggregate header, not an absent action object.
            var guards=old.Guards with{ActionInfo=new(N.ActionPresence.Present,t.Header,evidence)};
            H.Input input=old with{Source=new(old.Source.MainSha256,producer.Selected.Context.DatasetId,producer.Selected.Context.Region,
                old.Source.Operation,old.Source.Epoch,evidence),Attacker=attacker,Receiver=receiver,Call=call,Guards=guards,Ticks=w.HpClockBlock()};
            W.JoinHpCurrent(w,input,true);
            if(c.U8(w.User+0x1686,0,"HP client current gate")!=0)
            {
                var root=w.Entry.World.Users.Single(x=>x.Address==w.User);
                ulong connection=c.U64(w.User+0x3720,0,"joined HP client connection root");
                Need(connection!=0&&connection==root.ConnectionAddress&&c.BindingAt(connection,1).Generation==root.ConnectionGeneration&&
                    old.Client.Presence==H.Presence.Present&&old.Client.ConnectionIdentity3720==root.ConnectionReceipt,
                    "join","HP current connection",0,"Owned World and current User HP transport declarations must identify the same current connection lifetime.");
            }
            return new(H.CopyWorldCurrent(input),w.Entry.Gear,producer.Selected,w);
        }
        internal void RequireInput(H.Input input)
        {
            Need(input.Source==Input.Source&&input.Call==Input.Call&&input.Receiver.State==Input.Receiver.State&&input.Receiver.Raw==Input.Receiver.Raw&&
                input.Attacker.Binding==Input.Attacker.Binding&&input.Receiver.Binding==Input.Receiver.Binding&&
                input.Receiver.Log==Input.Receiver.Log&&input.SourceWorld==Input.SourceWorld&&
                input.Call.Context08==0&&input.Call.Context0c==0&&input.Call.Context10==0&&input.Call.Kind14==2&&input.Call.Skill18==0&&
                input.Call.Log1c==0xc5&&input.Call.Flags20==0&&input.Call.Critical21==0&&input.Receiver.State.Pending728 is 0 or 1&&
                input.Source.DatasetSha256==Selected.Context.DatasetId&&input.Source.RegionContext==Selected.Context.Region,
                "producer","User World entry",0,"Only the exact produced World context/source/pending payload can enter User HP.");
        }
    }
    internal static Result CompleteImmediate(O.ImpactAdmission impact)
    {
        try{return Run(CompletionAdmission.Immediate(impact));}
        catch(Domain e){return FailureResult(e.Failure);}
        catch(ArithmeticException e){return FailureResult(new("arithmetic","Pass2",0,e.Message,true));}
    }
    internal static Result DispatchImpact(OwnedImpact impact,W.Entry current)
    {
        ArgumentNullException.ThrowIfNull(impact);ArgumentNullException.ThrowIfNull(current);
        if(!impact.TryClaim())return Empty(impact.ExistingStatus);
        try
        {
            var result=Run(CompletionAdmission.Retained(impact,current));
            if(result.Completed)impact.CompleteClaim();else impact.ReleaseIncomplete();return result;
        }
        catch(Domain e){impact.ReleaseIncomplete();return FailureResult(e.Failure);}
        catch(ArithmeticException e){impact.ReleaseIncomplete();return FailureResult(new("arithmetic","retained Pass2",0,e.Message,true));}
    }
    internal static Result CancelImpact(OwnedImpact impact,Cancellation cancellation)
    {
        try{return Empty(impact.Cancel(cancellation)?Status.Cancelled:impact.ExistingStatus);}
        catch(Domain e){return FailureResult(e.Failure);}
    }
    private static Result Run(CompletionAdmission completion)
    {
        var w=completion.Work;var t=completion.Totals;
        HpAdmission admission=HpAdmission.Calculate(completion);
        int oldHp=w.Cursor.I32(w.User+0x778,0x14068ef7e,"Pass2.original_target_HP");
        H.OwnedResult hp=H.EvaluateFromWorld(admission);
        Need(hp.Owner.Status==H.Completion.CompleteImmediateUserOwner&&hp.Owner.Calculated is not null&&hp.Owner.ApplicationCell.HasValue,
            "User-HP-child",hp.Owner.Failure?.Stage??"HP",hp.Owner.Failure?.NativeSite??0,
            hp.Owner.Failure?.Reason??"Whole owned User HP/gear child is incomplete.",true);
        w.ConsumeHpClocks(hp.Owner.ConsumedTicks.Count);
        foreach(var step in hp.Owner.Events)w.Cursor.Note(step.NativeSite,"CalculatedUserHpChild",step.GetType().Name,value:step.Ordinal);
        var state=hp.Owner.Calculated;
        WriteState(w,w.User,state.Receiver);WriteUser(w,state.User);
        var outbox=ImmutableArray.CreateBuilder<Intent>();
        int applicationCell=hp.Owner.ApplicationCell.Value;
        uint raw4238=w.Cursor.U32(w.User+0x4238,0x1400d44c0,"source_sync.target_slot87_raw4238");
        outbox.Add(new(1,0x140b35120,"SourceNpcSync",SourceSync(w,t.Threat,oldHp,applicationCell,raw4238),
            w.Entry.HpCurrent.Attacker.Binding.ObjectId,applicationCell,null,w.Entry.Receipt));
        SendHp(w,outbox);
        Need(t.Reflected==0,"incomplete-child","Pass2.actual_User_to_Npc_HP",0x14068f003,"Positive actual User reflection requires its real source/receiver owner.",true);
        // Current finite protector computes no redirect metadata. Reaching one never
        // masquerades as a zero recipient through a supplied aggregate poststate.
        Procs(w,t.Classification);
        w.RequireTail();
        return new(Status.CalculatedImpactCompleted,w.Cursor.Snapshot(),hp,outbox.ToImmutable(),w.Steps,[..w.Consumed],null,w.RandomState,w.RandomGeneration,w.RandomTraceBytes);
    }
    private static void Procs(W.Work w,int classification)
    {
        var c=w.Cursor;ulong sourceManager=c.U64(w.Source+0x158,0x14068f01e,"Pass2.source.manager");
        if(sourceManager!=0)Need(c.I32(sourceManager+0x43d0,0x14034e770,"source.reactive_count43d0")==0,
            "incomplete-child","source.reactive_skill",0x14034e770,"Active source reactive entry program.",true);
        Need(c.U32(w.Source+0x6c8,0x140372490,"Npc.item_proc_mask6c8")==0,"incomplete-child","Npc.item_proc",0x140372490,
            "Actual NPC source can have item procs; active current equipment/proc child is unsupported.",true);
        ulong manager=c.U64(w.User+0x158,0x14068f050,"Pass2.target.manager");
        if(classification!=4&&manager!=0)Need(c.I32(manager+0x43c0+(ulong)classification*4,0x14034ebb0,"target.class_reactive_count")==0,
            "incomplete-child","target.reactive_skill",0x14034ebb0,"Active class-specific target reactive entry program.",true);
        ulong template=c.U64(w.Source+0x1848,0x140b1fbe0,"embedded.current_template");
        Need(c.BindingAt(template,0x500).Generation==w.Entry.Record.Selected.Current.TemplateLife&&template==w.Entry.Record.Selected.Current.SourceTemplate,
            "incomplete-child","embedded refreshed template",0,"Fresh late template differs from the explicitly owned source lifetime; no stale source profile fallback.",true);
        ulong start=c.U64(template+0x70,0x140b1fbe0,"embedded.vector_begin"),end=c.U64(template+0x78,0x140b1fbe0,"embedded.vector_end");
        Need(end>=start&&(end-start)%8==0&&(end-start)/8<=65_536,"range","embedded vector",0,"Invalid bounded current late vector.");
        ulong ordinal=(byte)w.Entry.Record.Selected.Ordinal;
        if(ordinal>=(end-start)/8)return;
        Need(c.BindingAt(start+ordinal*8,8).Generation==w.Entry.Record.Selected.Current.VectorLife,
            "generation","embedded vector",0,"Late vector lifetime differs.");
        ulong entry=c.U64(start+ordinal*8,0x140b1fbe0,"embedded.current_entry");
        Need(entry==w.Entry.Record.Selected.EntryAddress&&c.BindingAt(entry,0x30).Generation==w.Entry.Record.Selected.Current.EntryLife,
            "incomplete-child","embedded refreshed entry",0,"Late selected entry/source changed; fresh source admission required.",true);
        if(c.U64(entry+0x28,0x140b1fbe0,"embedded.Skill28")!=0)return;
        ulong skill=c.U64(entry+0x18,0x140b1fbe0,"embedded.skill18");if(skill==0)return;
        byte level=c.U8(entry+0x20,0x140b1fbe0,"embedded.level");if(level==0)return;
        byte chance=c.U8(entry+0x21,0x140b1fbe0,"embedded.chance");if(chance==0)return;
        int sample=w.Draw("EmbeddedNormalSkill",0x140b1fc4a,1,100);
        Need(sample>chance,"incomplete-child","embedded.normal_skill",0x140b1fc65,"Successful embedded skill requires actual definition148 and whole skill owner.",true);
    }
    private static void SendHp(W.Work w,ImmutableArray<Intent>.Builder outbox)
    {
        var c=w.Cursor;if(c.U8(w.User+0x1686,0x1405306f2,"SendHP.connected")==0)return;
        var old=w.Entry.HpCurrent;Need(old.Client.Presence==H.Presence.Present&&!string.IsNullOrWhiteSpace(old.Client.ConnectionIdentity3720),
            "transport","SendHP.connection",0x1400c6350,"Actual current stable self connection required.");
        int hp=c.I32(w.User+0x778,0x1405305b0,"SendHP.current"),max=c.I32(w.User+0x774,0x1405305b0,"SendHP.maximum");
        outbox.Add(new(outbox.Count+1,0x1400c6350,"UserSelfHP",[],old.Receiver.Binding.ObjectId,hp,max,old.Client.ConnectionIdentity3720!));
        Need(c.I32(w.User+0x7bf0,0x1405305b0,"SendHP.spectator")==0,"incomplete-child","SendHP.spectator",0x1405305b0,"Active spectator owner.",true);
        Need(c.U32(w.User+0x7b80,0x1405305b0,"SendHP.spectator_world")!=c.U32(w.World+0x20,0,"World.mapID"),
            "incomplete-child","SendHP.world_spectator",0x1405305b0,"Active World spectator owner.",true);
        uint debug=c.U32(0x14276916c,0x14053ebd2,"SendHP.debug_mask");
        if((debug&2)!=0)Need(old.Receiver.Raw.DebugCategory1.HasValue&&(old.Receiver.Raw.DebugCategory1.Value&1)==0,
            "incomplete-child","SendHP.debug",0x14053ec15,"Reached unknown/active debug category owner.",true);
        outbox.Add(new(outbox.Count+1,0x1400e4190,"UserHPStatSync",[],old.Receiver.Binding.ObjectId,hp,max,w.Entry.Receipt));
        c.W32(w.User+0x770,(c.U32(w.User+0x770,0,"SendHP.dirty")&~1u)|0x800,0x1405305b0,"SendHP.dirty_write");
    }
    private static ImmutableArray<byte> SourceSync(W.Work w,int threat,int oldHp,int cell,uint raw4238)
    {
        using var output=new MemoryStream();using var writer=new BinaryWriter(output);
        writer.Write((ushort)0);writer.Write((ushort)0x12);writer.Write((byte)0xad);writer.Write((ushort)0xffed);
        writer.Write(w.Entry.HpCurrent.Attacker.Binding.ObjectId);writer.Write(w.Entry.HpCurrent.Receiver.Binding.ObjectId);
        // Pass2 EBX=arg16 raw threat (RBP+a8) is forwarded in R8D at68efc5.
        writer.Write(threat);writer.Write(oldHp);writer.Write(cell);
        uint header=w.Cursor.U32(w.Header,0x14007d3a0,"source_sync.header");writer.Write(header);
        Need((header&0x10a0f)==0&&(header&0x10010)==0,"incomplete-child","source_sync movement",0,"Active native source sync movement union.",true);
        writer.Write((ushort)0);writer.Flush();byte[] b=output.ToArray();
        // Caller always evaluates slot87, even though ordinary header leaves it unwritten.
        _=raw4238;BinaryPrimitives.WriteUInt16LittleEndian(b,(ushort)b.Length);return [..b];
    }
    private static N.OwnerState ReadState(W.Work w,ulong a,N.OwnerState prior)
    {
        var c=w.Cursor;ulong killer=c.U64(a+0x918,0,"HP.current_killer_pointer");
        Need(killer==0||killer==w.Source,"incomplete-child","current killer root",0,"Other live killer requires its actual managed actor root.",true);
        return prior with{MaxHp774=c.I32(a+0x774,0,"HP.max"),Hp778=c.I32(a+0x778,0,"HP.current"),
            MaxMp78c=c.I32(a+0x78c,0,"MP.max"),Mp790=c.I32(a+0x790,0,"MP.current"),Pending728=c.I32(a+0x728,0,"HP.pending"),
            Dirty770=c.U32(a+0x770,0,"dirty"),Field788=c.I32(a+0x788,0,"field788"),Field72c=c.I32(a+0x72c,0,"field72c"),
            LastAttacker1308=c.U32(a+0x1308,0,"last_attacker"),Killer918=killer==0?null:w.Entry.HpCurrent.Attacker.Binding.ObjectId};
    }
    private static void WriteState(W.Work w,ulong a,N.OwnerState s)
    {
        var c=w.Cursor;
        foreach(var v in new[]{(0x774,s.MaxHp774),(0x778,s.Hp778),(0x78c,s.MaxMp78c),(0x790,s.Mp790),(0x728,s.Pending728),(0x788,s.Field788),(0x72c,s.Field72c)})
            c.W32(a+(ulong)v.Item1,unchecked((uint)v.Item2),0,"joined HP child scalar");
        c.W32(a+0x770,s.Dirty770,0,"joined HP child dirty");c.W32(a+0x1308,s.LastAttacker1308,0,"joined HP child attacker");
        Need(s.Killer918 is null||s.Killer918==w.Entry.HpCurrent.Attacker.Binding.ObjectId,"join","killer root",0,"Unbound managed killer ID.");
        c.W64(a+0x918,s.Killer918 is null?0:w.Source,0,"joined HP child killer pointer");
    }
    private static void WriteUser(W.Work w,H.RawUser r)
    {
        var c=w.Cursor;c.W32(w.User+0x42cc,r.ActivityDue42cc,0,"joined User activity");c.W32(w.User+0x5b24,unchecked((uint)r.FirstDeathKind5b24),0,"joined User first kind");
        if(r.Hotspot7d98.HasValue)c.W32(w.User+0x7d98,r.Hotspot7d98.Value,0,"joined User hotspot");
        ulong p=c.U64(w.User+0x270,0,"joined User bookkeeping pointer");
        if(r.Received270==H.Presence.Present&&p!=0)
        {if(r.ReceivedCounter0c.HasValue)c.W32(p+0xc,r.ReceivedCounter0c.Value,0,"joined User bookkeeping0c");if(r.ReceivedCounter10.HasValue)c.W32(p+0x10,r.ReceivedCounter10.Value,0,"joined User bookkeeping10");}
    }
    private static Result Empty(Status s)=>new(s,null,null,[],[],[],null);
    private static Result FailureResult(Failure f)=>new(f.Code=="adapter-invalidated"?Status.AdapterInvalidated:f.Code=="adapter-not-due"?Status.AdapterNotDue:Status.IncompleteAtChild,null,null,[],[],[],f);
}
