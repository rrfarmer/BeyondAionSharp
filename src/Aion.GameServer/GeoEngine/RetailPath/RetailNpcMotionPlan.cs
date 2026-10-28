using static Aion.GameServer.GeoEngine.RetailPath.RetailMovementArithmetic;

namespace Aion.GameServer.GeoEngine.RetailPath;

/// <summary>Inactive source-bound pose/speed/motion/timer reducer. No scheduler or packet adapter.</summary>
public static class RetailNpcMotionPlan
{
    public static RetailNpcMovementCalculation Update(RetailNpcMovementSnapshot input, RetailGroundMovementScene scene,
        uint updateTick, ulong timerParameter, RetailMovementLimits limits) =>
        RetailNpcMovementOwnerPlan.Evaluate(input,scene,limits,r=> { Common(r,updateTick,timerParameter); return 0; },hasNativeUIntResult:false);
    public static RetailNpcMovementCalculation Cancel(RetailNpcMovementSnapshot input, RetailGroundMovementScene scene,
        RetailMovementLimits limits) => RetailNpcMovementOwnerPlan.Evaluate(input,scene,limits,r=> { Cancel(r); return 0; },hasNativeUIntResult:false);

    internal static bool V18(RetailNpcMovementActor a) => (a.Pose5a0&15) is 3 or 13 or 14 || a.Kind32c==0x10;
    internal static bool V19(RetailNpcMovementActor a) => (a.Pose5a0&15) is 5 or 6;
    internal static void Ordinary(RetailNpcMovementReduction r,ulong address)
    { if(r.Actor.Cutscene1f8!=0)r.Incomplete(address,"cutscene-slot14","Non-null cutscene owner requires its actual +dc and complete cutscene program."); }

    internal static bool Admit(RetailNpcMovementReduction r)
    {
        var a=r.Actor;
        if(V19(a)&&a.State598!=2)
        {r.Write(0x1401b2850,"motion+cc",r.Motion.Cc,1);r.Motion=r.Motion with {Cc=1};}
        // CanMove evaluates slot22, slot19, both low-32 effect masks, and speed in this order.
        if(a.State598==2||V19(a)||(unchecked((uint)a.Effects188)&0x0749f05c)!=0||(unchecked((uint)a.Effects188)&0x0649f04c)!=0||!(F(a.Speed5e0)>0))return false;
        uint pose=a.Pose5a0&15;
        if(pose>14)Stop(RetailMovementCalculationStatus.UnsupportedConsumedDomain,0x140117640,"action0-row","Adjacent pose15 action row is not an admitted table row.");
        // Actual five words of action0 rows at140406950, stride57*20. Word2 is forbidden bits, not bits to clear.
        uint replacement=pose switch {1=>1,2=>2,3=>3,4=>4,12=>1,14=>14,_=>0};
        uint forbidden=pose switch {1 or 3 or 4 or 14=>272,2 or 12=>784,_=>0};
        r.Child(0x140117640,"DoChangeState(action0,send0,selector-1)",$"replacement={replacement}; forbidden={forbidden:x}; old&forbidden={a.Pose5a0&forbidden:x}","native action0 table words; required/clear/set words all zero");
        if(replacement==0||(a.Pose5a0&forbidden)!=0)return false;
        uint next=(a.Pose5a0&0xfffffff0)|replacement;
        r.Write(0x140117640,"actor+5a0",a.Pose5a0,next);r.Actor=a with {Pose5a0=next};
        Speed(r);return true;
    }
    internal static void Speed(RetailNpcMovementReduction r)
    {
        var a=r.Actor;uint mode=a.Pose5a0&0x60;
        float speed=F(mode==0?a.Speed5e0:mode==0x40?a.Speed5e4:a.Speed5e8);
        if(V18(a)&&speed>.0001f)speed=Add(speed,3);
        r.Write(0x14010a7b0,"actor+5a4",a.SelectedSpeed5a4,speed);r.Actor=a with {SelectedSpeed5a4=speed};
        int state=a.SpeedStatePointer638 is null?0:a.SpeedStateObjectPlus8;
        if(state is 0 or 4)r.Incomplete(0x14010a7b0,"party-speed-owner","Reached actual v38/GetPartyMoveSpeed owner; a null party cannot be inferred from absent XML.");
        if(state is 2 or 6)r.Incomplete(0x14010a7b0,"template-speed-factor","Reached raw template+16e0/16d0 multiplier requires its current independently bound data.");
        float effective=Add(speed,F(a.SpeedAdjustment5a8));if(effective<0)effective=0;
        float budget=Mul(effective,.667f);
        r.Child(0x14010a7b0,"CalcMoveSpeed","speed-object state is neither0/2/4/6","raw separate +638 object+8; ordinary adjustment branch");
        r.Write(0x14010a7b0,"actor+59c",a.DistanceBudget59c,budget);r.Actor=r.Actor with {DistanceBudget59c=budget};
    }
    internal static void Stage(RetailNpcMovementReduction r,RetailSearchPoint desired,RetailSearchPoint next,uint heading,uint flags)
    {
        if(r.Actor.State598==2)return;Ordinary(r,0x1401b2510);
        if(Near(desired,r.Actor.Position))return;
        var m=r.Motion;
        r.Write(0x1401b2510,"motion+c",m.DesiredC,desired);r.Write(0x1401b2510,"motion+18",m.Next18,next);
        r.Write(0x1401b2510,"motion+28",m.Duration28,0);r.Write(0x1401b2510,"motion+24",m.Heading24,heading);
        r.Write(0x1401b2510,"motion+34",m.Waypoint34,flags);r.Write(0x1401b2510,"motion+8.exchange",m.Pending8,1);
        r.Motion=m with {DesiredC=desired,Next18=next,Duration28=0,Heading24=heading,Waypoint34=flags,Pending8=1};
    }
    internal static void Common(RetailNpcMovementReduction r,uint tick,ulong timerParameter)
    {
        Ordinary(r,0x1401b1b20);var m=r.Motion;
        if(m.Pending8!=0&&m.StartGate0<=tick)
        { ExchangeAction(r);Start(r,timerParameter);return; }
        if(m.Deadline4!=0&&m.Deadline4<=tick&&m.Waypoint34==0)StopPacket(r);
        if(r.Motion.Deadline4!=0)return;
        m=r.Motion;
        if(m.Direction3c!=0)r.Incomplete(0x1401b1b20,"change-direction-packet","Reached SendChangeDir native host/protocol child outside the finite move-record chain.");
        if(m.D4!=0||m.D0!=0)
        {r.Write(0x1401b1b20,"motion+48",m.Action48,25);r.Write(0x1401b1b20,"motion+44",m.Action44,1);r.Motion=m with {Action48=25,Action44=1};}
        if(!V19(r.Actor))ExchangeAction(r);
        m=r.Motion;
        if(m.C4!=0||m.C8!=0||m.Cc!=0||m.D4!=0||m.D0!=0)
            r.Incomplete(0x1401b1b20,"active-pose-child","Reached action13/14/3/5/4/2 child requires its complete independently calculated action table/send program.");
        if(!V19(r.Actor))ExchangeAction(r);
        r.Child(0x1401b1b20,"CommonUpdate","ordinary; no reached direction/action/pose child","evaluated current pending/deadline/gate/3c/44/c4/c8/cc/d0/d4 and raw slot19");
    }
    private static void ExchangeAction(RetailNpcMovementReduction r)
    {
        var m=r.Motion;r.Write(0x1401b1b20,"motion+44.exchange",m.Action44,0);r.Motion=m with {Action44=0};
        if(m.Action44!=0)r.Incomplete(0x1401b1b20,"SendActionPacket","Action exchange occurred before the unimplemented packet/action child.");
    }
    internal static void Start(RetailNpcMovementReduction r,ulong timerParameter)
    {
        const ulong entry=0x1401b2e60;var m=r.Motion;var a=r.Actor;
        uint oldFlags=m.Flags38, durationOverride=m.Duration28;
        r.Write(entry,"motion+38.exchange",m.Flags38,0);r.Write(entry,"motion+8.exchange",m.Pending8,0);r.Write(entry,"motion+3c.exchange",m.Direction3c,0);
        r.Motion=m with {Flags38=0,Pending8=0,Direction3c=0};Ordinary(r,entry);
        r.Write(entry,"motion+28.exchange",durationOverride,0);r.Motion=r.Motion with {Duration28=0};
        uint flags=oldFlags|(durationOverride!=0?0xe4u:0xe0u);uint duration;
        float speed=Add(F(a.SelectedSpeed5a4),F(a.SpeedAdjustment5a8));
        if(speed<=0)duration=5000;
        else
        {
            duration=durationOverride!=0?durationOverride:unchecked((uint)Int(Div(Mul(Length(a.Position,m.Next18),1000),speed),"move-duration"));
            if((int)duration<100)duration=100;
            F(a.TemplateTurnTimeE0);
            if(((a.Pose5a0&0x20)==0||(a.TemplatePointer1e8 is not null&&a.TemplateTurnTimeE0>0))&&
                (flags&4)==0&&(a.Kind32c&6)==0&&(!V18(a)||System.MathF.Abs(Sub(a.Position.Z,m.Next18.Z))<=.5f))
            {
                int turn=TurnDistance(a.HeadingB0,m.Heading24);
                if(turn>4){flags|=2;duration=unchecked(duration+300);}
            }
        }
        float seconds=Div((int)duration,1000);
        var delta=Minus(m.Next18,a.Position);
        var velocity=seconds==0?default:new RetailSearchPoint(Div(delta.X,seconds),Div(delta.Y,seconds),Div(delta.Z,seconds));
        r.Write(entry,"actor+9c/a0/a4",a.Velocity,velocity);r.Actor=a with {Velocity=velocity};
        // Native computes velocity before this turn-time replacement. Do not rescale it afterwards.
        if((flags&2)!=0&&a.TemplatePointer1e8 is not null&&a.TemplateTurnTimeE0>0)
            r.Incomplete(0x14010aa80,"positive-template-turn-time","Reached turn-time helper is outside the admitted zero/nonpositive template domain; calculated velocity prefix retained.");
        uint packetHeading=durationOverride!=0?m.PreviousHeading2c:m.Heading24;
        uint packetFlags=(byte)flags;if(m.Waypoint34!=0)packetFlags|=8;
        var desired=r.Globals.SendNextAsDesired14176cb6c!=0&&(a.Pose5a0&0x20)!=0?m.Next18:m.DesiredC;
        r.Effect(new(0,RetailNpcMovementEffectKind.NativeInterserverMove,entry,$"Native move record; actor178 low byte={(byte)a.PacketState178}; no4.8 packet translation or IO executed.",m.Next18,desired,packetHeading,packetFlags,duration));
        r.Write(entry,"actor+b0.exchange",r.Actor.HeadingB0,m.Heading24);r.Actor=r.Actor with {HeadingB0=m.Heading24};
        if(r.Actor.WorldPointerIdentity is null)Stop(RetailMovementCalculationStatus.UnsupportedConsumedDomain,entry,"move-world-argument","Native reads world+20 after emission and heading store; null world is not a fabricated failed move/timer success.");
        RetailNpcWorldApplyPlan.Move(r,m.Next18);
        uint due=AddTimer(r,duration,1,timerParameter);
        m=r.Motion;r.Write(entry,"motion+4.exchange",m.Deadline4,due);r.Write(entry,"motion+2c",m.PreviousHeading2c,m.Heading24);r.Write(entry,"motion+30",m.PreviousValid30,1);
        uint gate=due<=m.StartGate0?m.StartGate0:due;r.Write(entry,"motion+0",m.StartGate0,gate);
        r.Motion=m with {Deadline4=due,PreviousHeading2c=m.Heading24,PreviousValid30=1,StartGate0=gate};
    }
    private static int TurnDistance(uint old,uint next)
    {
        // Admitted native heading words only; rejecting corrupt large values avoids signed abs overflow.
        if(old>120||next>120)Stop(RetailMovementCalculationStatus.UnsupportedConsumedDomain,0x1401b2e60,"heading-word","Native heading arithmetic requires admitted0..120 words.");
        int x=System.Math.Abs((int)old-(int)next);return x>60?120-x:x;
    }
    internal static void StopPacket(RetailNpcMovementReduction r)
    {
        const ulong e=0x1401b2c60;var m=r.Motion;
        r.Write(e,"motion+4.exchange",m.Deadline4,0);r.Motion=m with {Deadline4=0};if(m.Deadline4==0)return;
        r.Write(e,"actor+9c/a0/a4",r.Actor.Velocity,default(RetailSearchPoint));r.Actor=r.Actor with {Velocity=default};
        r.Write(e,"motion+38.exchange",m.Flags38,0);r.Motion=r.Motion with {Flags38=0};
        r.Effect(new(0,RetailNpcMovementEffectKind.NativeInterserverStop,e,$"Native stop record actor178 low byte={(byte)r.Actor.PacketState178}; IO unexecuted.",r.Actor.Position,Heading:r.Actor.HeadingB0,Flags:m.Flags38,Duration:0));
        r.Write(e,"motion+30",m.PreviousValid30,1);r.Write(e,"motion+2c",m.PreviousHeading2c,r.Actor.HeadingB0);
        r.Motion=r.Motion with {PreviousValid30=1,PreviousHeading2c=r.Actor.HeadingB0};
    }
    internal static void Cancel(RetailNpcMovementReduction r)
    {
        const ulong e=0x1401b2c20;r.Write(e,"motion+4.exchange",r.Motion.Deadline4,0);r.Motion=r.Motion with {Deadline4=0};
        r.Write(e,"actor+9c/a0/a4",r.Actor.Velocity,default(RetailSearchPoint));r.Actor=r.Actor with {Velocity=default};
        r.InvalidatePath();r.Write(e,"motion+8.exchange",r.Motion.Pending8,0);r.Write(e,"motion+38.exchange",r.Motion.Flags38,0x10);
        r.Motion=r.Motion with {Pending8=0,Flags38=0x10};
        r.Child(e,"CancelMove","no queue removal","deadline/velocity/path/pending cleared; queued timers and progress preserved");
    }
    internal static uint AddTimer(RetailNpcMovementReduction r,uint delay,int id,ulong parameter)
    {
        const ulong e=0x140299e80;
        if(r.Globals.ClosingIo171d640ec!=0)
        {r.Effect(new(0,RetailNpcMovementEffectKind.DiagnosticLog,e,"AddTimer called after closingIO; native returns0."));return 0;}
        if(r.Actor.PendingIo10==int.MaxValue)Stop(RetailMovementCalculationStatus.UnsupportedConsumedDomain,e,"IO-overflow","Pending IO count overflow.");
        r.Write(e,"actor+10.increment",r.Actor.PendingIo10,r.Actor.PendingIo10+1);r.Actor=r.Actor with {PendingIo10=r.Actor.PendingIo10+1};
        RetailNpcWorldApplyPlan.Reference(r,1,e);
        uint tick=r.Tick("AddTimer",0x140299fa5),due=unchecked(tick+delay);
        if((int)delay<0)r.Incomplete(e,"negative-delay-diagnostic","Native error child occurs after IO/ref increments and tick read.");
        var t=r.Timers;uint stripe;
        if(t.StripeSelectorEnabled146950684==0)stripe=0;
        else {if(t.StripeCount1417719c8==0)Stop(RetailMovementCalculationStatus.UnsupportedConsumedDomain,e,"timer-stripe-divisor","Zero native divisor.");stripe=unchecked(t.ThreadStripe+1)%t.StripeCount1417719c8;}
        r.Write(e,"TLS+24",t.ThreadStripe,stripe);r.Timers=t=t with {ThreadStripe=stripe};
        if(stripe!=t.QueueStripeIndex)Stop(RetailMovementCalculationStatus.StaleOwnership,e,"timer-queue-stripe","The supplied queue storage belongs to a different stripe; computed TLS selector cannot retarget it silently.");
        if(t.Entries.Count>=r.Limits.MaxTimerEntries)Stop(RetailMovementCalculationStatus.DefensiveLimit,e,"timer-budget","Owned timer queue limit.");
        if(t.Entries.Count>=t.Capacity)r.Incomplete(0x1402a2590,"timer-vector-allocation","Reserved nonalias timer capacity exhausted; allocator is not guessed.");
        ulong sum=unchecked(t.SumDelays146952ea8+delay),added=unchecked(t.Added1469536a8+1);
        r.Write(e,"global146952ea8",t.SumDelays146952ea8,sum);r.Write(e,"global1469536a8",t.Added1469536a8,added);
        var item=new RetailNpcMovementTimerEntry(due,r.Actor.ObjectId,id,parameter,r.Actor.TimerGeneration64,r.Value.Ownership.ActorLifetime,$"{r.Value.Ownership.TimerRevision}/{added}");
        var entries=t.Entries.Append(item).ToArray();r.Write(0x1402a2590,"timer-vector.end",t.Entries.Count,entries.Length);
        r.Timers=t=t with {SumDelays146952ea8=sum,Added1469536a8=added,Entries=Array.AsReadOnly(entries)};
        // Complete checked leaf has more heap branches; first entry's zero parent-index is an exact NOP guard.
        if(entries.Length>1)r.Incomplete(0x1402a4f40,"nonzero-timer-heap-index","Reached nonzero push_heap child is outside the finite first-entry domain.");
        r.Child(0x1402a4f40,"timer push_heap","new index0","no comparison/swap child reached");
        if(due<t.StripeNextWake){r.Write(e,"stripe.nextWake",t.StripeNextWake,due);r.Timers=t with {StripeNextWake=due};}
        if(due<t.GlobalNextWake14176f9b8)r.Effect(new(0,RetailNpcMovementEffectKind.SetEvent,e,"Timer wake event requested, not executed."));
        r.Effect(new(0,RetailNpcMovementEffectKind.TimerRegistration,e,"Calculated logical40-byte timer fields; no queue dispatch/native padding/OS operation executed.",Timer:item));return due;
    }
}
