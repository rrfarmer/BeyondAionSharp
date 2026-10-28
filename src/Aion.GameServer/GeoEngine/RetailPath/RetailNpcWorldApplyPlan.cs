using static Aion.GameServer.GeoEngine.RetailPath.RetailMovementArithmetic;

namespace Aion.GameServer.GeoEngine.RetailPath;

/// <summary>
/// Calculated NPCServer immediate endpoint/membership application, not MainServer interpolation.
/// Serialized owned lists/ref state are required. OS locks, RCU, allocator and network effects are unexecuted.
/// </summary>
public static class RetailNpcWorldApplyPlan
{
    public static RetailNpcMovementCalculation Plan(RetailNpcMovementSnapshot input,RetailGroundMovementScene scene,
        RetailSearchPoint endpoint,RetailMovementLimits limits) =>
        RetailNpcMovementOwnerPlan.Evaluate(input,scene,limits,r=>Move(r,endpoint),0x1402364c0);

    internal static uint Move(RetailNpcMovementReduction r,RetailSearchPoint endpoint)
    {
        const ulong entry=0x140237690;
        if(r.Actor.WorldPointerIdentity is null)Stop(RetailMovementCalculationStatus.UnsupportedConsumedDomain,entry,"move-world-pointer","WorldMoveNPC has no admitted null-world call; no inner MoveObject false result is fabricated.");
        if(r.Actor.Kind88!=2)Stop(RetailMovementCalculationStatus.UnsupportedConsumedDomain,entry,"object-kind","This application unit admits actual NPC kind2; user/item membership is a separate owner.");
        Point(endpoint);
        uint calls=unchecked(r.Globals.MoveNpcCalls174c54720+1);
        r.Write(entry,"global174c54720",r.Globals.MoveNpcCalls174c54720,calls);r.Globals=r.Globals with {MoveNpcCalls174c54720=calls};
        uint start=r.Tick("MoveBegin",0x140237813);
        // This finite application domain closes sampling mode0; it is not a default for an omitted global.
        if(r.Globals.Sampling14176cb40!=0)r.Incomplete(entry,"nonzero-sampling-mode","Current odd/even sampling owner is outside the admitted mode0 transaction.");
        r.Write(entry,"actor+9d8/9dc/9e0",r.Actor.SamplingPosition9d8,r.Actor.Position);
        r.Actor=r.Actor with {SamplingPosition9d8=r.Actor.Position};
        uint moved=MoveObject(r,endpoint);
        if(moved==0)return 0; // Native skips ACK, callbacks and MoveEnd tick on this branch.
        int old=r.Actor.Progress82c,progress=old==1?2:old;
        r.Write(entry,"actor+82c.compareExchange(1,2)",old,progress);r.Actor=r.Actor with {Progress82c=progress};
        if(r.Actor.State598==2)return 1;
        bool see=WatchesSee(r),move=WatchesMove(r);
        // Complete ownership facts, rather than absent XML, prove that no other user/NPC callback is reached.
        if(r.World.Cells.Count!=r.World.CellCount32||r.World.Cells.Select(x=>x.Index).Distinct().Count()!=r.World.CellCount32||r.World.Cells.Any(c=>c.Index<0||c.Index>=r.World.CellCount32))
            r.Incomplete(entry,"observer-membership-coverage","Complete current region coverage is required to prove no observer groups.");
        foreach(var cell in r.World.Cells)
        {
            CurrentCell(r,cell);
            if(cell.ScriptSeeCount<0||cell.ScriptMoveCount<0)Stop(RetailMovementCalculationStatus.InvalidInput,entry,"watcher-count","Negative current watcher count.");
            if(cell.OrderedUsers.Count!=0||cell.OrderedObjects.Any(id=>id!=r.Actor.ObjectId)||cell.ScriptSeeCount!=0||cell.ScriptMoveCount!=0)
                r.Incomplete(entry,"active-observer-group","Reached observer/perception group requires its independently evaluated object and callback programs.");
        }
        if(see||move)r.Incomplete(entry,"actor-perception-script","Own watcher predicate is active; no arbitrary callback mutation tape is admitted.");
        r.Child(entry,"WorldMoveNPC observation groups","all complete current users/other objects/watch counts zero; own watcher helpers false","raw750, actual script-interface8d8/8dc, AI-template119/11a, NPC-template324; actor excluded by identity");
        uint end=r.Tick("MoveEnd",0x140239332);
        if(unchecked(end-start)>20)r.Effect(new(0,RetailNpcMovementEffectKind.DiagnosticLog,entry,"MoveNPC elapsed>20ms; calculated zero observation groups."));
        if(unchecked(r.World.WorldId+0x80000000u)<0x40000000u&&r.Actor.Master55c!=0)
            r.Incomplete(entry,"master-pet-nearby-child","Reached additional master/pet nearby-object consumer.");
        return 1;
    }
    private static bool WatchesSee(RetailNpcMovementReduction r)
    {var a=r.Actor;if(a.Raw750!=0)return true;if(a.AiPointer200 is null)r.Incomplete(0x140229dc0,"script-interface-owner","Actual AI/script-interface pointer owner is missing.");return a.ScriptSee8d8!=0||a.Template119!=0;}
    private static bool WatchesMove(RetailNpcMovementReduction r)
    {var a=r.Actor;if(a.Raw750!=0)return true;if(a.AiPointer200 is null)r.Incomplete(0x140229e50,"script-interface-owner","Actual AI/script-interface pointer owner is missing.");if(a.ScriptMove8dc!=0||a.Template11a!=0)return true;if(a.TemplatePointer1e8 is null)r.Incomplete(0x14022a3b0,"NPC-template-owner","Actual NPC template+324 pointer is missing.");return a.Template324>=1;}
    private static uint MoveObject(RetailNpcMovementReduction r,RetailSearchPoint p)
    {
        const ulong e=0x1402364c0;var a=r.Actor;var w=r.World;
        // Native half-open old and requested bounds are checked before temporary AddRef.
        if(!r.Scene.Collision.Bounds.Contains(a.Position)||!r.Scene.Collision.Bounds.Contains(p))return 0;
        Reference(r,1,e);
        int oldCell=Index(a.Position,w.Columns32,5),newCell=Index(p,w.Columns32,5);
        int oldLock=Index(a.Position,w.Columns64,6),newLock=Index(p,w.Columns64,6);
        if(oldLock<0||newLock<0||oldLock>=w.LockCount64||newLock>=w.LockCount64)
            Stop(RetailMovementCalculationStatus.UnsupportedConsumedDomain,e,"lock-index","Native lock pointer would fall outside explicitly owned lock storage.");
        uint result;
        if(oldCell==newCell&&oldLock==newLock)
        {r.Write(e,"actor+90/94/98",a.Position,p);r.Actor=r.Actor with {Position=p};result=1;}
        else
        {
            r.Child(e,"RCU lock order",oldLock==newLock?$"one index{oldLock}":$"ascending {System.Math.Min(oldLock,newLock)},{System.Math.Max(oldLock,newLock)}","serialized model ownership; actual lock/TLS primitives are host obligations");
            if(!Remove(r,oldCell))result=0;
            else
            {
                // Required quirk: this write happens before PutObject's range check/failure.
                r.Write(e,"actor+90/94/98",r.Actor.Position,p);r.Actor=r.Actor with {Position=p};result=Put(r,newCell);
            }
        }
        Reference(r,-1,e);return result;
    }
    private static int Index(RetailSearchPoint p,int columns,int shift)
    {
        int x=Int(p.X,"membership-x"),y=Int(p.Y,"membership-y");long value=(long)(y>>shift)*columns+(x>>shift);
        if(value<int.MinValue||value>int.MaxValue)Stop(RetailMovementCalculationStatus.UnsupportedConsumedDomain,0x1402364c0,"membership-index","Native signed index overflow is outside the admitted domain.");return (int)value;
    }
    private static RetailNpcMembershipCell Cell(RetailNpcMovementReduction r,int index)
    {
        var found=r.World.Cells.Where(c=>c.Index==index).ToArray();
        if(found.Length!=1)r.Incomplete(0x1402364c0,"membership-cell","Exactly one current owned cell is required at the consumed index.");
        CurrentCell(r,found[0]);return found[0];
    }
    private static void CurrentCell(RetailNpcMovementReduction r,RetailNpcMembershipCell c)
    {
        if(!c.CompleteCurrentOwnership||c.Revision!=r.Value.Ownership.MembershipRevision||!Digest(c.EvidenceSha256))
            Stop(RetailMovementCalculationStatus.StaleOwnership,0x140237690,"membership-revision","Incomplete/stale current membership coverage cannot be assumed empty.");
    }
    private static void SetCell(RetailNpcMovementReduction r,RetailNpcMembershipCell c) =>
        r.World=r.World with {Cells=Array.AsReadOnly(r.World.Cells.Select(x=>x.Index==c.Index?c:x).ToArray())};
    private static bool Remove(RetailNpcMovementReduction r,int index)
    {
        const ulong e=0x1402ee910;var a=r.Actor;
        if(index<0||index>=r.World.CellCount32)return false;
        var c=Cell(r,index);var wrapper=r.World.Wrappers.SingleOrDefault(x=>x.Identity==a.WrapperIdentity80);
        if(wrapper is null||wrapper.ParentCell!=index||wrapper.ActorId!=a.ObjectId)return false;
        var ids=c.OrderedObjects.ToList();int position=ids.IndexOf(a.ObjectId);
        if(position<0||ids.LastIndexOf(a.ObjectId)!=position)return false;
        var live=r.World.Wrappers.Where(x=>x.ParentCell==index&&x.DelayFreed3f==0).ToDictionary(x=>x.ActorId);
        string? prev=position==0?null:live.GetValueOrDefault(ids[position-1])?.Identity;
        string? next=position==ids.Count-1?null:live.GetValueOrDefault(ids[position+1])?.Identity;
        if(wrapper.Previous!=prev||wrapper.Next!=next)return false;
        if(position>0&&prev is null||position<ids.Count-1&&next is null)r.Incomplete(e,"linked-membership-neighbor","Missing current neighbor wrapper.");
        ids.RemoveAt(position);
        r.Write(e,$"cell[{index}].object-list.unlink",a.ObjectId,string.Join(",",ids));
        if(next is not null)r.Write(e,$"wrapper[{next}].prev",wrapper.Identity,prev);
        if(prev is not null)r.Write(e,$"wrapper[{prev}].next",wrapper.Identity,next);
        var changed=r.World.Wrappers.Select(x=>x.Identity==prev?x with {Next=next}:x.Identity==next?x with {Previous=prev}:x).ToArray();
        r.World=r.World with {Wrappers=Array.AsReadOnly(changed)};
        // Native head branch alone updates watcher counters; the nonhead omission is preserved.
        c=c with {OrderedObjects=Array.AsReadOnly(ids.ToArray())};SetCell(r,c);
        if(position==0)
        {
            if(WatchesSee(r)){int see=unchecked(c.ScriptSeeCount-1);r.Write(e,$"cell[{index}].see",c.ScriptSeeCount,see);c=c with {ScriptSeeCount=see};SetCell(r,c);}
            if(WatchesMove(r)){int move=unchecked(c.ScriptMoveCount-1);r.Write(e,$"cell[{index}].move",c.ScriptMoveCount,move);c=c with {ScriptMoveCount=move};SetCell(r,c);}
        }
        r.Write(e,"actor+80",a.WrapperIdentity80,null);r.Actor=r.Actor with {WrapperIdentity80=null};
        Delay(r,wrapper);
        r.Write(0x140239850,"actor+ac.exchange",r.Actor.InWorldAc,0);r.Actor=r.Actor with {InWorldAc=0};Reference(r,-1,0x140239850);return true;
    }
    private static void Delay(RetailNpcMovementReduction r,RetailNpcMovementWrapper wrapper)
    {
        const ulong e=0x1402ee100;var d=r.Delayed;
        if(wrapper.DelayFreed3f!=0)r.Incomplete(e,"already-delayed-wrapper-log","Native already-delayFreed FileLog child is reached before ring work.");
        if(!d.CompleteCurrentOwnership||d.Capacity!=16384||d.Mask!=0x3fff||d.ThreadIndex<0||d.ThreadIndex>212)
            Stop(RetailMovementCalculationStatus.UnsupportedConsumedDomain,e,"delay-ring-owner","Exact current native16384-slot ring,mask/TLS ownership is required.");
        if(d.ReadIndex!=d.WriteIndex||d.Slots.Count!=0)
            r.Incomplete(e,"delay-ring-trim-or-victim","Nonempty/retained old wrappers require complete cleanup/destructor children; no empty-scene inference.");
        if(r.Actor.DelayedReferencesC==int.MaxValue||d.NpcCount146955148==int.MaxValue)
            Stop(RetailMovementCalculationStatus.UnsupportedConsumedDomain,e,"delay-reference-overflow","Native signed delay count overflow outside admitted domain.");
        int slot=(int)(d.WriteIndex&d.Mask);
        r.Write(e,"delay-ring.write",d.WriteIndex,unchecked(d.WriteIndex+1));r.Delayed=d with {WriteIndex=unchecked(d.WriteIndex+1)};
        r.Write(e,"global146955148",d.NpcCount146955148,d.NpcCount146955148+1);
        r.Delayed=r.Delayed with {NpcCount146955148=d.NpcCount146955148+1};
        r.Write(e,"actor+c.increment",r.Actor.DelayedReferencesC,r.Actor.DelayedReferencesC+1);r.Actor=r.Actor with {DelayedReferencesC=r.Actor.DelayedReferencesC+1};
        var slots=d.Slots.ToDictionary(x=>x.Key,x=>x.Value);slots.Add(slot,new(wrapper.Identity,r.Actor.ObjectId,null,1));
        r.Write(e,$"delay-ring[{slot}]","null",wrapper.Identity);
        r.Delayed=r.Delayed with {Slots=new System.Collections.ObjectModel.ReadOnlyDictionary<int,RetailNpcDelayedWrapper>(slots)};
        r.Write(e,$"wrapper[{wrapper.Identity}]+3f",wrapper.DelayFreed3f,1);
        r.World=r.World with {Wrappers=Array.AsReadOnly(r.World.Wrappers.Select(x=>x.Identity==wrapper.Identity?x with {DelayFreed3f=1}:x).ToArray())};
        uint timestamp=r.Tick("DelayTimestamp",0x1402ee457);r.Write(e,$"delay-ring[{slot}].timestamp","unconsumed previous timestamp",timestamp);
        slots[slot]=slots[slot] with {Timestamp=timestamp};r.Delayed=r.Delayed with {Slots=new System.Collections.ObjectModel.ReadOnlyDictionary<int,RetailNpcDelayedWrapper>(slots)};
        r.Child(e,"DelayFree","complete empty current ring; no old/victim child","retains old wrapper reference, increments delayed count, marks/stores wrapper; actual RCU release deferred");
    }
    private static uint Put(RetailNpcMovementReduction r,int index)
    {
        const ulong e=0x140236030;
        if(index<0||index>=r.World.CellCount32)
        {r.Effect(new(0,RetailNpcMovementEffectKind.DiagnosticLog,e,"PutObject index out of native range; XYZ already changed and ACK not performed."));return 0;}
        var c=Cell(r,index);var a=r.Actor;
        if(c.OrderedObjects.Contains(a.ObjectId))
        {r.Effect(new(0,RetailNpcMovementEffectKind.DiagnosticLog,e,"PutObject duplicate pointer identity; native returns1 without successful new insertion certificate."));return 1;}
        r.Write(e,"actor+ac.exchange",a.InWorldAc,1);r.Actor=a with {InWorldAc=1};
        if(a.WrapperIdentity80 is null)
        {
            Reference(r,1,0x1402eddd0);
            if(r.Storage.UsedWrapperSlots>=r.Storage.AvailableWrapperSlots)r.Incomplete(0x1402eddd0,"wrapper-allocation","No reserved model wrapper slot; native allocator result cannot be guessed.");
            string name=$"{r.Storage.ReservationIdentity}/wrapper/{r.Storage.UsedWrapperSlots}";
            if(r.World.Wrappers.Any(x=>x.Identity==name))Stop(RetailMovementCalculationStatus.InvalidInput,e,"wrapper-identity","Reserved model wrapper identity collides.");
            r.Storage=r.Storage with {UsedWrapperSlots=r.Storage.UsedWrapperSlots+1};
            r.Write(0x1402eddd0,$"wrapper[{name}]+3f","uninitialized",0);r.Write(0x1402eddd0,$"wrapper[{name}]+10","uninitialized",a.ObjectId);
            r.Write(0x1402eddd0,"actor+80",r.Actor.WrapperIdentity80,name);r.Actor=r.Actor with {WrapperIdentity80=name};
            r.Write(0x1402eddd0,$"wrapper[{name}]+3c","uninitialized",0xbb5a);r.Write(0x1402eddd0,$"wrapper[{name}]+3e","uninitialized",0xa5);r.Write(0x1402eddd0,$"wrapper[{name}]+40","uninitialized",index);
            r.World=r.World with {Wrappers=Array.AsReadOnly(r.World.Wrappers.Append(new RetailNpcMovementWrapper(name,a.ObjectId,index,null,null,0,0xbb5a,0xa5)).ToArray())};
            r.Effect(new(0,RetailNpcMovementEffectKind.ModelAllocation,0x1402eddd0,"Reserved logical wrapper allocation; native allocator unexecuted."));
            if((r.Globals.ObjectWrapperRtti174c5e224&1)==0)
            {
                uint old=r.Globals.ObjectWrapperRtti174c5e224;r.Write(0x1402eddd0,"global174c5e224",old,old|1);r.Globals=r.Globals with {ObjectWrapperRtti174c5e224=old|1};
                r.Incomplete(0x1402eddd0,"wrapper-first-RTTI","Native sets first-use bit before active Name_base/32cbc0 child; allocation/actor pointer prefix retained.");
            }
            string? head=c.OrderedObjects.Count==0?null:r.World.Wrappers.SingleOrDefault(x=>x.ActorId==c.OrderedObjects[0]&&x.ParentCell==index&&x.DelayFreed3f==0)?.Identity;
            if(c.OrderedObjects.Count!=0&&head is null)r.Incomplete(e,"list-head-owner","Current nonempty head wrapper missing.");
            r.Write(0x1402eddd0,$"wrapper[{name}].prev","uninitialized",null);r.Write(0x1402eddd0,$"wrapper[{name}].next","uninitialized",head);
            if(head is not null)r.Write(0x1402eddd0,$"wrapper[{head}].prev",null,name);
            var wrappers=r.World.Wrappers.Select(x=>x.Identity==head?x with {Previous=name}:x.Identity==name?x with {Next=head,Previous=null}:x).ToArray();
            r.World=r.World with {Wrappers=Array.AsReadOnly(wrappers)};
            r.Write(0x1402eddd0,$"cell[{index}].object-head",head,name);
            c=c with {OrderedObjects=Array.AsReadOnly(c.OrderedObjects.Prepend(a.ObjectId).ToArray())};SetCell(r,c);
            if(WatchesSee(r)){int see=unchecked(c.ScriptSeeCount+1);r.Write(e,$"cell[{index}].see",c.ScriptSeeCount,see);c=c with {ScriptSeeCount=see};SetCell(r,c);}
            if(WatchesMove(r)){int move=unchecked(c.ScriptMoveCount+1);r.Write(e,$"cell[{index}].move",c.ScriptMoveCount,move);c=c with {ScriptMoveCount=move};SetCell(r,c);}
        }
        else r.Effect(new(0,RetailNpcMovementEffectKind.DiagnosticLog,0x1402eddd0,"Non-null wrapper: native void PutMovingObject logs/skips; outer PutObject still continues."));
        uint divisor=r.Globals.WorldHashDivisor14176f0a8;
        if(divisor==0)Stop(RetailMovementCalculationStatus.UnsupportedConsumedDomain,e,"world-hash-divisor","Native unsigned modulo by zero is outside admitted domain.");
        ushort hash=unchecked((ushort)(unchecked(r.World.WorldId/10000+r.World.WorldId)%divisor));
        r.Write(e,"actor+62",r.Actor.WorldHash62,hash);r.Actor=r.Actor with {WorldHash62=hash};Reference(r,1,e);return 1;
    }
    internal static void Reference(RetailNpcMovementReduction r,int change,ulong caller)
    {
        var a=r.Actor;
        ulong entry=change>0?0x14029aba0UL:0x14029acf0UL;
        if(a.References8<1||change is not(1 or -1)||change==1&&a.References8==int.MaxValue)
            Stop(RetailMovementCalculationStatus.UnsupportedConsumedDomain,entry,"reference-counter-domain","Invalid/nonpositive/overflowing signed reference count is outside the admitted normal domain.");
        int next=a.References8+change;r.Write(caller,"actor+8.atomic",a.References8,next);r.Actor=a with {References8=next};
        if(change>0&&a.DebugSource28==0)r.Incomplete(entry,"reference-first-debug-source","Native increments before recording the first caller file-pointer/line; those raw metadata arguments are not fabricated.");
        if(a.ReferenceHistory14!=0)r.Incomplete(entry,"reference-history-child","Native reference counter changed before the active history program.");
        if(change<0&&a.References8==1)r.Incomplete(entry,"reference-destructor-child","Native decremented to0 before delayed-reference diagnostics/destructor; no lifetime cleanup is guessed.");
    }
}
