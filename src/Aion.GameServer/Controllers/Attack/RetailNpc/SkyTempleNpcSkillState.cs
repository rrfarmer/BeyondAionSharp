using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Manager;
using Aion.GameServer.Controllers.Movement;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Skill;
using Aion.GameServer.Retail;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Properties;
using Aion.GameServer.Utils;
using Microsoft.Extensions.Logging;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Per-instance source-ordered NPC skill rows. Count/rate/delay are not Java
/// probability%, chain count or random milliseconds. One existing attack scheduler owns work.</summary>
internal static class SkyTempleNpcSkillState
{
    private static readonly ILogger ChaseLog=AionLog.For(nameof(SkyTempleNpcSkillState));
    internal enum ChaseDisposition { Continue, Suppressed, Expired, Unsupported, Inactive }
    internal sealed class ChaseDecision
    {
        private readonly Npc owner;
        private readonly State state;
        private readonly Aion.GameServer.Model.GameObjects.VisibleObject? target;
        private readonly long targetLife;
        private readonly long revision;
        private readonly object world;
        private readonly int map,instance;
        private bool claimed;
        internal ChaseDisposition Disposition { get; }
        internal uint Tick { get; }
        internal uint Deadline { get; }
        internal uint NativeSuppressionMask { get; }
        internal bool ApproachFailure { get; }
        internal string? Detail { get; }
        private ChaseDecision(Npc owner,State state,Aion.GameServer.Model.GameObjects.VisibleObject? target,long targetLife,
            ChaseDisposition disposition,uint tick,uint deadline,uint mask,bool failure,string? detail=null)
        {
            this.owner=owner;this.state=state;this.target=target;this.targetLife=targetLife;revision=state.ChaseRevision;
            world=owner.GetPosition().GetWorldMapInstance();map=owner.GetWorldId();instance=owner.GetInstanceId();
            Disposition=disposition;Tick=tick;Deadline=deadline;NativeSuppressionMask=mask;ApproachFailure=failure;Detail=detail;
        }
        internal static bool Evaluate(Npc owner,out ChaseDecision decision)
        {
            decision=null!;
            if(!SkyTempleNpcProfileCatalog.TryGet(owner,out var p))return false;
            var state=Current(owner,p);
            lock(owner.GetMoveController())
            {
                var target=owner.GetTarget();
                long targetLife=target is Creature creature?creature.GetController().RetailLifeEpoch:0;
                uint captured=SystemClock.RetailTick32(); // UpdateAttackState's earlier capture.
                bool failure=owner.GetMoveController().RetailHasRouteFailure;
                lock(state.Gate)
                {
                    // Effective stacked4.8 flags do not reconstruct the native188 mirror.
                    state.UnprovedChaseHistory|=unchecked((uint)((AbnormalState)owner.GetEffectController().GetAbnormals()&UnprovedChaseAliases));
                    uint mask=state.Chase188;bool known=state.UnprovedChaseHistory==0;
                    ChaseDisposition disposition;uint tick=captured;string? detail=null;
                    if(!CurrentLife(owner,state)||owner.GetAi().GetState()!=AIState.FIGHT)
                        disposition=ChaseDisposition.Inactive;
                    else if(!known||!state.ChaseStarted)
                    {
                        detail=!known?$"unproved-abnormal-event-history:0x{state.UnprovedChaseHistory:x8}":"missing-current-encounter-or-action-anchor";
                        if(state.LastChaseIssue!=detail)
                            ChaseLog.LogWarning("SkyTemple chase unsupported npc={Npc} template={Template} detail={Detail}",owner.GetObjectId(),owner.GetNpcId(),detail);
                        state.LastChaseIssue=detail;disposition=ChaseDisposition.Unsupported;
                    }
                    else
                    {
                        state.LastChaseIssue=null;
                        if((mask&0x80000000u)!=0)
                        {
                            // The outer Sanctuary branch precedes CheckStopChase.
                            if(!failure)RefreshChase(state,SystemClock.RetailTick32());
                            disposition=ChaseDisposition.Suppressed;
                        }
                        else
                        {
                            bool suppressed=(mask&0x0749f05cu)!=0;
                            tick=!suppressed&&state.Timers.TemplateTimeout16cc!=0?SystemClock.RetailTick32():captured;
                            disposition=suppressed?ChaseDisposition.Suppressed:
                                state.Timers.TemplateTimeout16cc!=0&&state.Timers.Timeout738<=tick?ChaseDisposition.Expired:ChaseDisposition.Continue;
                        }
                    }
                    // Even a reentrant clock provider can retire/replace the life.
                    // Keep the original state/target binding; dispatch rechecks it.
                    decision=new(owner,state,target,targetLife,disposition,tick,state.Timers.Timeout738,mask,failure,detail);
                    return true;
                }
            }
        }
        internal bool TryClaimExpiry(Npc current)
        {
            if(!ReferenceEquals(current,owner)||!System.Threading.Monitor.IsEntered(owner.GetMoveController()))return false;
            lock(state.Gate)
            {
                if(claimed||Disposition!=ChaseDisposition.Expired||!CurrentLife(owner,state)||owner.GetAi().GetState()!=AIState.FIGHT||
                    !SkyTempleNpcProfileCatalog.TryGet(owner,out var p)||p.Generation!=state.Generation||
                    !States.TryGetValue(owner,out var actual)||!ReferenceEquals(actual,state)||state.ChaseRevision!=revision||
                    !ReferenceEquals(owner.GetTarget(),target)||(target is Creature creature&&creature.GetController().RetailLifeEpoch!=targetLife)||
                    state.Timers.Timeout738!=Deadline||state.Chase188!=NativeSuppressionMask||state.UnprovedChaseHistory!=0||
                    !state.ChaseStarted||(state.Chase188&0x8749f05cu)!=0||state.Timers.TemplateTimeout16cc==0||Deadline>Tick)return false;
                claimed=true;
                return true;
            }
        }
        // After source-owned effect cleanup, clear callbacks may legitimately
        // refresh738. Retain the committed expiry identity, not its old deadline.
        internal bool ClaimedIdentityIsCurrent(Npc current)
        {
            if(!System.Threading.Monitor.IsEntered(owner.GetMoveController()))return false;
            lock(state.Gate)return claimed&&ReferenceEquals(current,owner)&&CurrentLife(owner,state)&&
                SkyTempleNpcProfileCatalog.TryGet(owner,out var p)&&p.Generation==state.Generation&&
                States.TryGetValue(owner,out var actual)&&ReferenceEquals(actual,state)&&
                owner.GetWorldId()==map&&owner.GetInstanceId()==instance&&ReferenceEquals(owner.GetPosition().GetWorldMapInstance(),world)&&
                ReferenceEquals(owner.GetTarget(),target)&&(target is not Creature creature||creature.GetController().RetailLifeEpoch==targetLife);
        }
    }
    internal readonly record struct ChaseSnapshot(uint Deadline,uint TemplateMilliseconds,uint LastRefresh,
        bool Started,long Life,string Generation,uint Native188,uint UnprovedHistory);
    internal sealed class Counts(IReadOnlyList<int> initial)
    {
        private readonly int[] original=initial.ToArray();
        internal readonly int[] Remaining=initial.ToArray();
        internal readonly uint[] LastStart=new uint[initial.Count];
        internal bool Allows(int index,uint now,uint reuse,uint delay) =>
            (original[index]==0||Remaining[index]!=0)&&ReuseAllows(LastStart[index],now,reuse,delay);
        internal void Use(int index,uint now){LastStart[index]=now;if(Remaining[index]!=0)Remaining[index]=unchecked(Remaining[index]-1);}
        internal void Cancel(int index){if(original[index]!=0)Remaining[index]=unchecked((int)Math.Max(unchecked((uint)(Remaining[index]+1)),(uint)original[index]));}
    }
    private sealed class State(Npc owner,SkyTempleNpcProfile profile)
    {
        internal readonly object Gate=new();
        internal readonly long Life=owner.GetController().RetailLifeEpoch;
        internal readonly string Generation=profile.Generation;
        internal readonly Counts Rows=new(profile.Skills.Select(s=>s.Count).ToArray());
        internal int? InFlight;
        internal bool NormalSelected;
        internal Aion.GameServer.Ai.Lifecycle.RetailNpcSkillTimeline.Timers Timers=new(0,0,0,0,checked((uint)profile.MaxChaseMilliseconds));
        internal bool ChaseStarted;
        internal uint LastChaseRefresh;
        internal Creature? ChaseTarget;
        internal string? LastChaseIssue;
        // A new managed NPC life replaces the native Creature constructor/Load's
        // zero188 storage. Only authentic Set/Clear API operations mutate this mirror.
        internal uint Chase188;
        internal uint UnprovedChaseHistory;
        internal long ChaseRevision;
    }
    private static readonly ConditionalWeakTable<Npc,State> States=new();
    private static State Current(Npc owner,SkyTempleNpcProfile p)
    {
        // Goal queries and the chooser are independent callers. Serialize epoch replacement
        // so two first reads after respawn cannot both Remove/Add the same CWT key.
        lock(States)
        {
            var state=States.GetValue(owner,n=>new(n,p));
            if(state.Life==owner.GetController().RetailLifeEpoch && state.Generation==p.Generation)return state;
            States.Remove(owner);state=new(owner,p);States.Add(owner,state);return state;
        }
    }

    // Numeric max_chase_time15 in all25 profiles selects CheckStopChase16dc==0.
    // Native738 is an ACTION/hate/abnormal deadline. It is not first path failure,
    // distance from home, time since damage, or FollowState's30second policy.
    internal static void BeginEncounter(Npc owner)
    {
        if(!SkyTempleNpcProfileCatalog.TryGet(owner,out var p)||owner.IsDead()||!owner.IsSpawned())return;
        var state=Current(owner,p);
        lock(owner.GetMoveController())lock(state.Gate)
        {
            if(!CurrentLife(owner,state))return;
            state.ChaseTarget=owner.GetTarget() as Creature;
            RefreshChase(state,SystemClock.RetailTick32());state.LastChaseIssue=null;
        }
    }
    private static bool CurrentLife(Npc owner,State state)=>owner.IsSpawned()&&!owner.IsDead()&&
        owner.GetController().RetailLifeEpoch==state.Life;
    private static void RefreshChase(State state,uint tick)
    {
        state.LastChaseRefresh=tick;state.ChaseStarted=true;
        state.Timers=state.Timers with {Timeout738=unchecked(tick+state.Timers.TemplateTimeout16cc)};
        state.ChaseRevision=unchecked(state.ChaseRevision+1);
    }
    internal static void HateReceived(Npc owner,Creature attacker)
    {
        if(!SkyTempleNpcProfileCatalog.TryGet(owner,out var p)||owner.GetAi().GetState()!=AIState.FIGHT)return;
        // Never acquire nav under SkillState.Gate. A18 prevents103210 refreshing
        // an already failed approach; the managed route owner supplies that fact.
        var state=Current(owner,p);
        lock(owner.GetMoveController())
        {
            bool failure=owner.GetMoveController().RetailHasRouteFailure;
            lock(state.Gate)
            {
                if(!CurrentLife(owner,state)||failure)return;
                if(owner.GetTarget() is Creature current&&!ReferenceEquals(current,owner))state.ChaseTarget=current;
                if(ReferenceEquals(state.ChaseTarget,attacker))RefreshChase(state,SystemClock.RetailTick32());
            }
        }
    }
    /// <summary>Private producer captured before the actual host operation. Its
    /// completion cannot affect a replacement NPC life/profile. In particular an
    /// NPC clear notification is not the Main/4.8 effective stacked-status value.</summary>
    internal sealed class AbnormalChange
    {
        private readonly Npc owner;
        private readonly State state;
        private AbnormalChange(Npc owner,State state){this.owner=owner;this.state=state;}
        internal static AbnormalChange? Capture(Npc owner)
        {
            if(!SkyTempleNpcProfileCatalog.TryGet(owner,out var p))return null;
            return new(owner,Current(owner,p));
        }
        internal void Set(AbnormalState value)=>Apply(value,true);
        internal void Clear(AbnormalState value)=>Apply(value,false);
        private void Apply(AbnormalState value,bool set)
        {
            // Unknown aliases cannot manufacture a native clear snapshot, even
            // after the4.8 bit goes away. A fresh life/profile retires that history.
            uint unproved=unchecked((uint)(value&UnprovedChaseAliases));
            TryChaseFlags(value&~UnprovedChaseAliases,out uint mask);
            lock(owner.GetMoveController())
            {
                bool failure=!set&&(mask&0x0749f05cu)!=0&&owner.GetMoveController().RetailHasRouteFailure;
                lock(state.Gate)
                {
                    if(owner.IsDead()||owner.GetController().RetailLifeEpoch!=state.Life||
                        !SkyTempleNpcProfileCatalog.TryGet(owner,out var p)||p.Generation!=state.Generation||
                        !States.TryGetValue(owner,out var current)||!ReferenceEquals(current,state))return;
                    state.UnprovedChaseHistory|=unproved;
                    if(mask!=0||unproved!=0)state.ChaseRevision=unchecked(state.ChaseRevision+1);
                    if(set)state.Chase188|=mask;
                    else
                    {
                        // MainNpc.Clear forwards MagicID even if the effective
                        // Main bit remains set, or was already clear. NPC157d50
                        // clears188 then always enters110fd0 with the outgoing bit.
                        state.Chase188&=~mask;
                        if((mask&0x0749f05cu)!=0&&!failure)RefreshChase(state,SystemClock.RetailTick32());
                    }
                }
            }
        }
    }
    // Explicit named joins: Main original status-name table141121ee0 -> actual
    // Begin Magic-ID operands -> NPC157d50 dispatch/188 bit writers. In particular
    // native kind25..28 alias other MagicIDs; never cast the whole4.8 enum into188.
    internal static bool TryChaseFlags(AbnormalState current,out uint native)
    {
        native=0;
        if((current&UnprovedChaseAliases)!=0)return false;
        foreach(var pair in ChaseFlagJoins)if((current&pair.Legacy)!=0)native|=pair.Native;
        return true; // Other named4.8 states are outside these consumed chase gates.
    }
    private const AbnormalState UnprovedChaseAliases=AbnormalState.STUNLIKE|AbnormalState.CANT_MOVE_OR_ATTACK|AbnormalState.UNK|AbnormalState.UNK_2;
    private static readonly (AbnormalState Legacy,uint Native)[] ChaseFlagJoins=
    [
        (AbnormalState.PARALYZE,0x4),(AbnormalState.SLEEP,0x8),(AbnormalState.ROOT,0x10),
        (AbnormalState.CHARM,0x40),(AbnormalState.STUN,0x1000),(AbnormalState.PETRIFICATION,0x2000),
        (AbnormalState.STUMBLE,0x4000),(AbnormalState.STAGGER,0x8000),(AbnormalState.OPENAERIAL,0x10000),
        (AbnormalState.SPIN,0x80000),(AbnormalState.PULLED,0x400000),(AbnormalState.SIMPLE_MOVE_BACK,0x1000000),
        (AbnormalState.SANCTUARY,0x80000000)
    ];
    internal static bool TryEvaluateChase(Npc owner,out ChaseDecision decision)
        =>ChaseDecision.Evaluate(owner,out decision);
    internal static ChaseSnapshot GetChaseSnapshot(Npc owner)
    {
        if(!SkyTempleNpcProfileCatalog.TryGet(owner,out var p))throw new InvalidOperationException("No current cohort profile");
        var state=Current(owner,p);lock(state.Gate)return new(state.Timers.Timeout738,state.Timers.TemplateTimeout16cc,
            state.LastChaseRefresh,state.ChaseStarted,state.Life,state.Generation,state.Chase188,state.UnprovedChaseHistory);
    }
    // SkillTemplate cooldown uses100ms wire/content units; native reuse/delay use ms.
    internal static uint ReuseMilliseconds(int cooldown)=>checked((uint)cooldown*100u);
    private static Properties RequiredProperties(Aion.GameServer.SkillEngine.Model.SkillTemplate template)
        => template.GetProperties() ?? throw new InvalidOperationException("Source skill properties are missing");
    internal static bool ReuseAllows(uint last,uint now,uint reuse,uint delay)
        =>unchecked(last+(delay==0?reuse:delay))<now;

    internal static bool TryChoose(NpcAI ai,out NpcSkillEntry? chosen)
    {
        chosen=null;Npc owner=ai.GetOwner();
        if(!SkyTempleNpcProfileCatalog.TryGet(owner,out var p))return false;
        if(owner.GetTarget() is Creature unverified&&SkyTempleNpcCombat.RejectUnverifiedTarget(owner,unverified))return true;
        if(owner.IsDead()||owner.GetLifeStats().IsAboutToDie()||ai.IsInSubState(AISubState.CAST))return true;
        var state=Current(owner,p);uint now=SystemClock.RetailTick32();
        lock(state.Gate)
        {
            state.NormalSelected=false;
            if(state.Timers.Attack728>now)return true;
            // Actual FIFO/aimed pattern owner first. Native HandleAction checks the
            // selected row's count/reuse; Count0 skips only its count predicate.
            var queued=owner.GetNextQueuedSkill();
            if(queued is not null&&!SkillAttackManager.CantUseSkill(queued,owner))
            {
                int index=Array.FindIndex(p.Skills.ToArray(),s=>s.Id48==queued.GetSkillId()&&s.Level==queued.GetSkillLevel());
                if(index<0)throw new InvalidOperationException("Queued SkyTemple skill is outside source binding");
                var descriptor=p.Skills[index];var template=queued.GetSkillTemplate() ?? throw new InvalidOperationException("Source skill template is missing");
                bool reuse=state.Rows.Allows(index,now,ReuseMilliseconds(template.GetCooldown()),(uint)descriptor.Delay);
                Creature? aim=queued is IAimedSkill {Aim:Creature actual}?actual:owner.GetTarget() as Creature;
                if(RequiredProperties(template).GetFirstTarget()==FirstTargetAttribute.ME)aim=owner;
                if(reuse&&aim is not null&&!aim.IsDead()&&(ReferenceEquals(aim,owner)||
                    owner.GetMoveController().RetailArrival(aim,RequiredProperties(template).GetFirstTargetRange())==SkyTempleArrival.Arrived))
                {chosen=queued;return true;}
            }
            if(owner.GetTarget() is not Creature target||target.IsDead())return true;
            var eligible=new List<RetailNpcMinimumReachPool.EligibleAttack>
            {new(0,p.Attacks[0].Range,p.Attacks[0].Weight,p.SourceRecordSha256)};
            for(int i=0;i<p.Skills.Length;i++)
            {
                var s=p.Skills[i];if(s.Rate==0)continue;
                var entry=owner.GetSkillList().GetSkillOnPosition(i);
                if(entry is null||entry.GetSkillId()!=s.Id48||entry.GetSkillLevel()!=s.Level)
                    throw new InvalidOperationException("Source-ordered live skill list differs");
                var template=entry.GetSkillTemplate() ?? throw new InvalidOperationException("Source skill template is missing");
                if(SkillAttackManager.CantUseSkill(entry,owner)||!state.Rows.Allows(i,now,ReuseMilliseconds(template.GetCooldown()),(uint)s.Delay))continue;
                eligible.Add(new(i+1,RequiredProperties(template).GetFirstTargetRange(),s.Rate,p.SourceRecordSha256+"/skill/"+i));
            }
            float targetRadius=target.GetObjectTemplate().GetBoundRadius().GetMaxOfFrontAndSide();
            float squared=SkyTempleNpcProtection.DistanceSquared(owner.GetX(),owner.GetY(),owner.GetZ(),target.GetX(),target.GetY(),target.GetZ());
            var pool=RetailNpcMinimumReachPool.Prepare(SkyTempleCombatInputs.Scope(p.NpcId),RetailNpcMinimumReachPool.NpcServerBinarySha256,
                "live source-ordered SkyTemple selector",new(targetRadius,p.Bounds.HorizontalRadius,squared,false),eligible);
            if(pool.TotalWeight==0)return true;
            int draw=SkyTempleNpcCombat.DrawNpc(1,pool.TotalWeight);
            foreach(var candidate in pool.Entries)
            {
                if(draw>candidate.CumulativeThreshold)continue;
                if(owner.GetMoveController().RetailArrival(target,candidate.EntryRange)!=SkyTempleArrival.Arrived)continue;
                if(candidate.CombinedIndex==0){state.NormalSelected=true;return true;}
                chosen=owner.GetSkillList().GetSkillOnPosition(candidate.CombinedIndex-1);return true;
            }
            return true;
        }
    }

    /// <summary>Current action-derived approach, queried before taking the navigation lock.
    /// Range is a source skill property, not a chosen arrival result. This query consumes
    /// no weighted draw and never dequeues or decrements the actual skill owner.</summary>
    internal static bool TryGetApproachGoal(Npc owner,out SkyTempleNpcApproachGoal goal)
    {
        goal=null!;
        if(!SkyTempleNpcProfileCatalog.TryGet(owner,out var p)||!owner.IsSpawned()||owner.IsDead()||owner.GetLifeStats().IsAboutToDie())return false;
        var state=Current(owner,p);uint now=SystemClock.RetailTick32();
        lock(state.Gate)
        {
            static bool CurrentTarget(Npc source,[NotNullWhen(true)] Creature? target)=>target is not null&&target.IsSpawned()&&!target.IsDead()&&
                !target.GetLifeStats().IsAboutToDie()&&target.GetWorldId()==source.GetWorldId()&&target.GetInstanceId()==source.GetInstanceId();
            SkyTempleNpcApproachGoal Bind(Creature target,float range,int? skill)
            {
                if(!float.IsFinite(range)||range<0)throw new InvalidOperationException("Invalid source SkyTemple approach range");
                return new(target,range,skill,state.Life,target.GetController().RetailLifeEpoch,p.Generation);
            }
            var queued=owner.GetNextQueuedSkill();
            if(queued is not null&&!SkillAttackManager.CantUseSkill(queued,owner))
            {
                int index=Array.FindIndex(p.Skills.ToArray(),s=>s.Id48==queued.GetSkillId()&&s.Level==queued.GetSkillLevel());
                if(index<0)throw new InvalidOperationException("Queued SkyTemple approach has no source binding");
                var template=queued.GetSkillTemplate() ?? throw new InvalidOperationException("Source skill template is missing");var row=p.Skills[index];
                Creature? aim=queued is IAimedSkill {Aim:Creature actual}?actual:owner.GetTarget() as Creature;
                if(RequiredProperties(template).GetFirstTarget()==FirstTargetAttribute.ME)aim=owner;
                // Direct/queued HandleAction keeps finite counts as well as source reuse.
                if(CurrentTarget(owner,aim)&&state.Rows.Allows(index,now,ReuseMilliseconds(template.GetCooldown()),(uint)row.Delay))
                {goal=Bind(aim!,RequiredProperties(template).GetFirstTargetRange(),queued.GetSkillId());return true;}
            }
            if(owner.GetTarget() is not Creature target||!CurrentTarget(owner,target))return false;
            float range=p.Attacks[0].Range;int? skillId=null;
            for(int i=0;i<p.Skills.Length;i++)
            {
                var row=p.Skills[i];if(row.Rate==0)continue;
                var entry=owner.GetSkillList().GetSkillOnPosition(i);
                if(entry is null||entry.GetSkillId()!=row.Id48||entry.GetSkillLevel()!=row.Level)
                    throw new InvalidOperationException("Source-ordered SkyTemple approach skill list differs");
                var template=entry.GetSkillTemplate() ?? throw new InvalidOperationException("Source skill template is missing");
                if(SkillAttackManager.CantUseSkill(entry,owner)||!state.Rows.Allows(i,now,ReuseMilliseconds(template.GetCooldown()),(uint)row.Delay))continue;
                // An automatic self buff does not enlarge enemy attack reach. The chooser
                // remains the sole owner of actual action selection.
                if(RequiredProperties(template).GetFirstTarget()==FirstTargetAttribute.ME)continue;
                float candidate=RequiredProperties(template).GetFirstTargetRange();
                if(!float.IsFinite(candidate)||candidate<0)throw new InvalidOperationException("Invalid source SkyTemple skill range");
                if(candidate>range){range=candidate;skillId=row.Id48;}
            }
            goal=Bind(target,range,skillId);return true;
        }
    }

    internal static void BeforeNormalMotion(Npc owner,SkyTempleNpcProfile p)
    {
        var state=Current(owner,p);
        lock(state.Gate)
        {
            if(state.NormalSelected){state.NormalSelected=false;return;}
            // Direct ordinary normal entry: singleton1000 still invokes the weighted draw.
            _=SkyTempleNpcCombat.DrawNpc(1,p.Attacks[0].Weight);
        }
    }
    internal static bool CanStartSkill(Npc owner,Aion.GameServer.SkillEngine.Model.Skill actual)
    {
        if(!SkyTempleNpcProfileCatalog.TryGet(owner,out var p))return true;
        if(!ReferenceEquals(actual.GetEffector(),owner))throw new InvalidOperationException("NPC skill start owner differs");
        int index=Array.FindIndex(p.Skills.ToArray(),x=>x.Id48==actual.GetSkillId()&&x.Level==actual.GetSkillLevel());
        if(index<0)throw new InvalidOperationException("Actual NPC start has no selected source binding");
        var state=Current(owner,p);var row=p.Skills[index];
        uint now=SystemClock.RetailTick32();
        lock(state.Gate)return state.Timers.Movement72c<=now&&state.Timers.NextUpdate754<=now&&
            state.Rows.Allows(index,now,ReuseMilliseconds(actual.GetSkillTemplate().GetCooldown()),(uint)row.Delay);
    }
    internal static void StartingSkill(Npc owner,Aion.GameServer.SkillEngine.Model.Skill actual)
    {
        if(!SkyTempleNpcProfileCatalog.TryGet(owner,out var p))return;
        if(!ReferenceEquals(actual.GetEffector(),owner)||!ReferenceEquals(owner.GetCastingSkill(),actual)||
            !p.Skills.Any(x=>x.Id48==actual.GetSkillId()&&x.Level==actual.GetSkillLevel()))
            throw new InvalidOperationException("Unbound actual selected skill start");
        var state=Current(owner,p);uint now=SystemClock.RetailTick32();
        var motion=SkyTempleNpcSkillTiming.Starting(actual);
        uint castEnd=unchecked(now+motion.Cast);
        int attackDelay=owner.GetGameStats().GetAttackSpeed().GetCurrent();
        if(attackDelay<0)throw new InvalidOperationException("Negative selected recovery delay");
        lock(owner.GetMoveController())lock(state.Gate)
        {
            for(int i=0;i<p.Skills.Length;i++)if(p.Skills[i].Id48==actual.GetSkillId())state.Rows.Use(i,castEnd);
            uint attack=unchecked(castEnd+Math.Max((uint)attackDelay,motion.Recovery));
            state.Timers=Aion.GameServer.Ai.Lifecycle.RetailNpcSkillTimeline.Extend(state.Timers,
                attack,unchecked(castEnd+motion.Recovery),now);
            state.ChaseStarted=true;
            state.ChaseRevision=unchecked(state.ChaseRevision+1);
            state.Timers=state.Timers with {NextUpdate754=Math.Max(attack,state.Timers.NextUpdate754)};
            state.InFlight=actual.GetSkillId();
        }
    }

    internal static void Cancel(Npc owner,int skill)
    {
        if(!SkyTempleNpcProfileCatalog.TryGet(owner,out var p)||!States.TryGetValue(owner,out var state))return;
        lock(owner.GetMoveController())lock(state.Gate)
        {
            if(state.InFlight!=skill)return;
            for(int i=0;i<p.Skills.Length;i++)if(p.Skills[i].Id48==skill)state.Rows.Cancel(i);
            uint next=unchecked(SystemClock.RetailTick32()+100u);
            state.Timers=state.Timers with {Attack728=next,Movement72c=next,NextUpdate754=next,
                Timeout738=unchecked(next+state.Timers.TemplateTimeout16cc)};
            state.ChaseStarted=true;
            state.ChaseRevision=unchecked(state.ChaseRevision+1);
            state.InFlight=null;
        }
    }
    internal static void NormalEmitted(Npc owner,uint capturedAttackTick)
    {
        if(!SkyTempleNpcProfileCatalog.TryGet(owner,out var p))return;
        int effective=owner.GetGameStats().GetAttackSpeed().GetCurrent();
        if(effective is <500 or >10000)throw new InvalidOperationException("Unsupported effective normal cadence");
        var words=SkyTempleNpcCombat.NormalMotion(p);
        float recovery=Math.Max(words.First,words.Second)*(effective/(float)p.AttackDelay);
        uint movement=checked((uint)(int)recovery);
        var state=Current(owner,p);lock(owner.GetMoveController())lock(state.Gate)
        {
            state.Timers=Aion.GameServer.Ai.Lifecycle.RetailNpcSkillTimeline.Extend(state.Timers,
                unchecked(capturedAttackTick+(uint)effective),unchecked(capturedAttackTick+movement),SystemClock.RetailTick32());
            state.ChaseStarted=true;
            state.ChaseRevision=unchecked(state.ChaseRevision+1);
        }
    }
    internal readonly record struct RowSnapshot(int SkillId,int Level,int Remaining,uint ReuseAnchor);
    internal static RowSnapshot GetRowSnapshot(Npc owner,int ordinal)
    {
        if(!SkyTempleNpcProfileCatalog.TryGet(owner,out var p)||ordinal<0||ordinal>=p.Skills.Length)
            throw new InvalidOperationException("No selected current skill row");
        var state=Current(owner,p);lock(state.Gate)return new(p.Skills[ordinal].Id48,p.Skills[ordinal].Level,
            state.Rows.Remaining[ordinal],state.Rows.LastStart[ordinal]);
    }
    internal static int GetNextActionInterval(Npc owner)=>RecoveryInterval(owner,false);
    internal static int GetNextMovementInterval(Npc owner)=>RecoveryInterval(owner,true);
    internal static uint GetReturnMovementEndTick(Npc owner)
    {
        if(!SkyTempleNpcProfileCatalog.TryGet(owner,out var p))return 0;
        var state=Current(owner,p);lock(state.Gate)return state.Timers.NextUpdate754;
    }
    private static int RecoveryInterval(Npc owner,bool movement)
    {
        if(!SkyTempleNpcProfileCatalog.TryGet(owner,out var p))return 0;
        var state=Current(owner,p);uint now=SystemClock.RetailTick32();
        lock(state.Gate)
        {
            uint deadline=movement?state.Timers.Movement72c:state.Timers.Attack728;
            if(deadline<=now)return 0;
            uint remaining=deadline-now;
            if(remaining>int.MaxValue)throw new InvalidOperationException("Native unsigned recovery exceeds managed scheduler domain");
            return (int)remaining;
        }
    }
    internal static void Completed(Npc owner)
    {if(States.TryGetValue(owner,out var state))lock(state.Gate)state.InFlight=null;}
}

/// <summary>Managed lifetime identity of the current source-derived approach.</summary>
internal sealed record SkyTempleNpcApproachGoal(Creature Target,float Range,int? SkillId,
    long OwnerLifeEpoch,long TargetLifeEpoch,string Generation);
