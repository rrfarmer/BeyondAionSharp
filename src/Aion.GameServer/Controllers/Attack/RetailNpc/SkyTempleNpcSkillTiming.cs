using System.Runtime.CompilerServices;
using Aion.GameServer.Ai.Lifecycle;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Retail;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;
using Microsoft.Extensions.Logging;
using TYPE=Aion.GameServer.Network.Aion.ServerPackets.SmAttackStatus.TYPE;
using LOG=Aion.GameServer.Network.Aion.ServerPackets.SmAttackStatus.LOG;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Actual selected Skill lifetime, source animation/projectile delay and one
/// primary HP lease. State/periodic Begin is independent at cast end. This managed
/// scheduler replaces native transport/allocator ownership, not gameplay calculations.</summary>
internal static class SkyTempleNpcSkillTiming
{
    private static readonly ILogger Log=AionLog.For(nameof(SkyTempleNpcSkillTiming));
    internal readonly record struct Motion(ushort FirstHit,uint Recovery,uint Cast,uint Reuse,float Ammo);
    // D source joins/16 original assets: skill-motion59.json1f8ce40e; all59 found,
    // first marker identical in both source loader modes. No fallback300 substituted.
    private static readonly IReadOnlyDictionary<(int Npc,int Skill,int Level),Motion> Motions=
        new Dictionary<(int,int,int),Motion>
    {
        [(212761,16724,21)]=new(1466,2000,3000,0,25f),
        [(212762,16590,21)]=new(400,2000,1500,0,25f),
        [(212762,16724,21)]=new(633,2000,3000,0,25f),
        [(212763,16590,22)]=new(400,2000,1500,0,25f),
        [(212763,16724,22)]=new(633,2000,3000,0,25f),
        [(212764,16724,22)]=new(1000,2000,3000,0,25f),
        [(212764,16593,22)]=new(1000,2000,1500,0,25f),
        [(212764,16699,22)]=new(1000,2000,2500,0,0f),
        [(212765,16561,21)]=new(1200,2000,3000,0,0f),
        [(212766,16836,21)]=new(1200,2566,3000,0,0f),
        [(212766,16561,21)]=new(1200,2200,3000,0,0f),
        [(212767,16836,22)]=new(1200,2566,3000,0,0f),
        [(212767,16561,22)]=new(1200,2200,3000,0,0f),
        [(212768,16561,22)]=new(800,1800,3000,0,0f),
        [(212768,16595,22)]=new(900,1800,2500,0,25f),
        [(212768,16588,22)]=new(933,1800,3000,0,0f),
        [(212769,16725,21)]=new(1266,2400,3000,0,25f),
        [(212770,17039,21)]=new(1366,2400,3500,0,25f),
        [(212770,16725,21)]=new(1366,2400,3000,0,25f),
        [(212771,17039,22)]=new(1366,2400,3500,0,25f),
        [(212771,16725,22)]=new(1366,2400,3000,0,25f),
        [(212772,16725,23)]=new(1366,2400,3000,0,25f),
        [(212772,16594,23)]=new(1366,2400,2500,0,25f),
        [(212772,17039,23)]=new(1366,2400,3500,0,25f),
        [(212773,16723,21)]=new(433,1700,3000,0,0f),
        [(212774,16589,21)]=new(733,1533,2500,0,0f),
        [(212774,16723,21)]=new(733,1533,3000,0,0f),
        [(212775,16589,22)]=new(733,1533,2500,0,0f),
        [(212775,16723,22)]=new(733,1533,3000,0,0f),
        [(212776,16794,21)]=new(933,1666,3500,0,0f),
        [(212776,16677,21)]=new(933,1666,2500,0,0f),
        [(212776,16556,21)]=new(933,1666,500,0,0f),
        [(212777,16723,22)]=new(700,1533,3000,0,0f),
        [(212777,16592,22)]=new(700,1533,2500,0,25f),
        [(212777,16522,22)]=new(533,1000,2500,0,0f),
        [(212778,16723,23)]=new(700,1533,3000,0,0f),
        [(212778,16592,23)]=new(700,1533,2500,0,25f),
        [(212778,16522,23)]=new(533,1000,2500,0,0f),
        [(212779,16576,22)]=new(833,1800,2500,0,25f),
        [(212779,16589,22)]=new(833,1800,2500,0,0f),
        [(212780,16723,23)]=new(666,1533,3000,0,0f),
        [(212780,16592,23)]=new(666,1533,2500,0,25f),
        [(212780,16522,23)]=new(700,1333,2500,0,0f),
        [(212780,16710,23)]=new(933,1533,2500,20000,0f),
        [(212781,16794,23)]=new(933,1666,3500,0,0f),
        [(212781,16677,23)]=new(933,1666,2500,0,0f),
        [(212781,16556,23)]=new(933,1666,500,0,0f),
        [(212782,16576,24)]=new(833,1800,2500,0,25f),
        [(212782,16589,24)]=new(833,1800,2500,0,0f),
        [(212833,16724,22)]=new(1000,2000,3000,0,25f),
        [(212833,16593,22)]=new(1000,2000,1500,0,25f),
        [(212833,16699,22)]=new(1000,2000,2500,0,0f),
        [(212835,16561,23)]=new(800,1800,3000,0,0f),
        [(212835,16595,23)]=new(900,1800,2500,0,25f),
        [(212835,16588,23)]=new(933,1800,3000,0,0f),
        [(212876,16723,24)]=new(666,1533,3000,0,0f),
        [(212876,16592,24)]=new(666,1533,2500,0,25f),
        [(212876,16522,24)]=new(700,1333,2500,0,0f),
        [(212876,16710,24)]=new(933,1533,2500,20000,0f),
    };
    internal static Motion SourceMotion(int npc,int skill,int level)=>Motions.TryGetValue((npc,skill,level),out var row)
        ?row:throw new InvalidOperationException("Selected skill has no source animation binding");
    internal static Motion SourceMotion(SkyTempleNpcProfile profile,int skill,int level)
    {
        if(profile.Cohort is not { } cohort)return SourceMotion(profile.NpcId,skill,level);
        var row=cohort.SkillMotions.SingleOrDefault(x=>x.SkillId==skill&&x.Level==level)
            ??throw new InvalidOperationException("Cohort skill has no source animation binding");
        return new(row.FirstHit,row.Recovery,row.Cast,row.Reuse,row.Ammo);
    }
    private sealed class Cast(Skill skill,Npc owner,Creature first,SkyTempleNpcProfile profile,Motion motion,ushort word)
    {
        internal readonly Skill Skill=skill;
        internal readonly Npc Owner=owner;
        internal readonly Creature First=first;
        internal readonly SkyTempleNpcProfile Profile=profile;
        internal readonly Motion Motion=motion;
        internal readonly ushort Word=word;
        internal readonly long OwnerLife=owner.GetController().RetailLifeEpoch,FirstLife=first.GetController().RetailLifeEpoch;
        internal readonly int Map=owner.GetWorldId(),Instance=owner.GetInstanceId();
        // Prepared0, actual casting1, launched at cast-end2, aborted before launch3.
        // Abort only invalidates casting work; a retained launched projectile is independent.
        internal int Phase;
    }
    private static readonly ConditionalWeakTable<Skill,Cast> Casts=new();
    private static readonly ConditionalWeakTable<Effect,Impact> Impacts=new();
    internal static bool TryPrepare(Skill skill,out int packetHitWord)
    {
        packetHitWord=0;
        if(skill.GetEffector() is not Npc owner||!SkyTempleNpcProfileCatalog.TryGet(owner,out var p))return false;
        if(!p.Skills.Any(x=>x.Id48==skill.GetSkillId()&&x.Level==skill.GetSkillLevel()))
            throw new InvalidOperationException("Skill timing requires an actual selected source binding");
        Creature first=skill.GetFirstTarget() ?? throw new InvalidOperationException("Selected skill has no first target");
        if(!ReferenceEquals(first,owner)&&first is not Player)throw new InvalidOperationException("Unsupported selected skill target");
        var motion=SourceMotion(p,skill.GetSkillId(),skill.GetSkillLevel());var t=skill.GetSkillTemplate();
        if(t.GetDuration()!=motion.Cast||SkyTempleNpcSkillState.ReuseMilliseconds(t.GetCooldown())!=motion.Reuse||t.GetAmmoSpeed()!=motion.Ammo)
            throw new InvalidOperationException("Selected skill current header differs from its source timing");
        ushort word=RetailNpcSkillTimeline.ImpactDelay(new(skill.GetSkillId(),0,motion.Cast,motion.Reuse,0,motion.Ammo),motion.FirstHit,
            new(owner.GetX(),owner.GetY(),owner.GetZ()),new(first.GetX(),first.GetY(),first.GetZ()));
        lock(Casts){Casts.Remove(skill);Casts.Add(skill,new(skill,owner,first,p,motion,word));}
        packetHitWord=word;return true;
    }
    internal static Motion Starting(Skill skill)
    {
        if(!Casts.TryGetValue(skill,out var c)||!ReferenceEquals(c.Owner.GetCastingSkill(),skill)||!Current(c))
            throw new InvalidOperationException("Selected timing has no actual current start");
        if(Interlocked.CompareExchange(ref c.Phase,1,0)!=0)
            throw new InvalidOperationException("Selected skill start was replayed");
        Log.LogInformation("RETAIL_SKYTEMPLE_SKILL_START source={Source} skill={Skill} cast={Cast} impactWord={Impact} recovery={Recovery} life={Life}",
            c.Owner.GetObjectId(),skill.GetSkillId(),c.Motion.Cast,c.Word,c.Motion.Recovery,c.OwnerLife);
        return c.Motion;
    }
    internal static bool IsSelected(Skill skill)=>Casts.TryGetValue(skill,out var c)&&Volatile.Read(ref c.Phase)!=0;
    internal static void Cancel(Skill skill)
    {if(Casts.TryGetValue(skill,out var c))Interlocked.CompareExchange(ref c.Phase,3,1);}
    private static bool Current(Cast c)=>Volatile.Read(ref c.Phase)!=3&&c.Owner.GetController().RetailLifeEpoch==c.OwnerLife&&
        c.First.GetController().RetailLifeEpoch==c.FirstLife&&c.Owner.IsSpawned()&&c.First.IsSpawned()&&
        c.Owner.GetWorldId()==c.Map&&c.First.GetWorldId()==c.Map&&c.Owner.GetInstanceId()==c.Instance&&c.First.GetInstanceId()==c.Instance&&
        SkyTempleNpcProfileCatalog.TryGet(c.Owner,out var p)&&p.Generation==c.Profile.Generation;
    internal static bool CanFinishCast(Skill skill)=>!Casts.TryGetValue(skill,out var c)||
        (Volatile.Read(ref c.Phase)==1&&Current(c)&&ReferenceEquals(c.Owner.GetCastingSkill(),skill));
    internal static void CastEnded(Skill skill)
    {
        if(!Casts.TryGetValue(skill,out var c))return;
        if(Interlocked.CompareExchange(ref c.Phase,2,1)!=1)
            throw new InvalidOperationException("Selected cast completion was replayed or aborted");
        Log.LogInformation("RETAIL_SKYTEMPLE_SKILL_CAST_END source={Source} skill={Skill} impactWord={Impact}",c.Owner.GetObjectId(),skill.GetSkillId(),c.Word);
    }

    private sealed class Impact(Cast cast,Effect effect,DamageEffect template)
    {
        internal readonly Cast Cast=cast;
        internal readonly Effect Effect=effect;
        internal readonly DamageEffect Template=template;
        internal readonly Creature Target=effect.GetEffected();
        internal readonly long TargetLife=ReferenceEquals(effect.GetEffected(),cast.First)?cast.FirstLife:effect.GetEffected().GetController().RetailLifeEpoch;
        internal readonly int Damage=effect.GetReserveds(template.GetPosition()).GetValue();
        internal bool Producing,HpEntered,ChildApplied,Prepassed,RunEntered;
        internal readonly object Gate=new();
        // The launched source reference is retained, as in DelayedSkillDamage. A later
        // cast abort/death does not retroactively cancel its independent target work.
        internal bool Current()=>Volatile.Read(ref Cast.Phase)==2&&Target.IsSpawned()&&!Target.IsDead()&&
            Target.GetController().RetailLifeEpoch==TargetLife&&Target.GetWorldId()==Cast.Map&&Target.GetInstanceId()==Cast.Instance;
        internal void ApplyChild()
        {
            if(ChildApplied)return;ChildApplied=true;
            var child=Effect.GetSubEffect();
            if(child is not null&&!Target.IsDead())child.ApplyEffect();
        }
        internal void Run()
        {
            lock(Gate)
            {
                if(RunEntered||!Current())return;RunEntered=true;
                Producing=true;var dispatch=ImpactDispatch.Produce(this);Producing=false;
                try {Target.GetController().OnRetailSkillAttack(dispatch);}
                finally
                {
                    // HP entered once even when a real post-commit observer faults. Do not
                    // replay HP or silently lose the independent reached secondary owner.
                    try {if(HpEntered)Cast.Owner.GetObserveController().NotifyAttackObservers(Target,Effect.GetSkillId());}
                    finally
                    {
                        if(HpEntered&&Current())ApplyChild();
                        Log.LogInformation("RETAIL_SKYTEMPLE_SKILL_IMPACT source={Source} target={Target} skill={Skill} requested={Damage} committed={Committed}",
                            Cast.Owner.GetObjectId(),Target.GetObjectId(),Effect.GetSkillId(),Damage,HpEntered);
                    }
                }
            }
        }
    }
    internal static int PrimaryDelay(ushort word)=>Math.Max(100,(int)word-50);
    internal static bool TrySchedulePrimary(Effect effect,DamageEffect template)
    {
        var skill=effect.GetSkill();if(skill is null||!Casts.TryGetValue(skill,out var c))return false;
        if(Volatile.Read(ref c.Phase)!=2)return true;
        if(template.GetPosition()!=1||!ReferenceEquals(effect.GetEffector(),c.Owner)||effect.GetEffected() is not Player)
            throw new InvalidOperationException("Selected primary delay owner/source differs");
        Impact impact;
        lock(Impacts)
        {
            if(Impacts.TryGetValue(effect,out var existing))
            {
                if(!ReferenceEquals(existing.Template,template))throw new InvalidOperationException("Selected primary template changed");
                return true; // Repeated Apply cannot enqueue another HP/child lease.
            }
            impact=new Impact(c,effect,template);Impacts.Add(effect,impact);
        }
        int delay=PrimaryDelay(c.Word);
        bool stagger=effect.GetSubEffect() is { } child&&SkyTempleNpcSkillCombat.HasOwnedChild(child,17039,8217)&&child.GetSuccessEffects().Any();
        // Native ordinary User long-delay Stagger runs the movement prepass300ms
        // before phase0 HP. Short delays set400 and complete both in phase0.
        if(stagger&&delay>=311)
        {
            ThreadPoolManager.GetInstance().Schedule(ct=>
            {
                lock(impact.Gate)
                {
                    if(impact.Prepassed||!impact.Current())return ValueTask.CompletedTask;
                    impact.Prepassed=true;
                    var prepassChild=effect.GetSubEffect();
                    if(prepassChild is not null)Aion.GameServer.GeoEngine.RetailPath.SkyTempleNpcForcedMove.TryApplyPrepass(prepassChild,out _);
                    // Native requeues300 from actual prepass delivery, not another
                    // independently armed absolute HP timer/catch-up event.
                    ThreadPoolManager.GetInstance().Schedule(_=>{impact.Run();return ValueTask.CompletedTask;},TimeSpan.FromMilliseconds(300));
                }
                return ValueTask.CompletedTask;
            },TimeSpan.FromMilliseconds(delay-300));
        }
        else ThreadPoolManager.GetInstance().Schedule(_=>{impact.Run();return ValueTask.CompletedTask;},TimeSpan.FromMilliseconds(delay));
        return true;
    }
    internal static bool DeferChild(Effect effect)=>Impacts.TryGetValue(effect,out _);

    internal sealed record Values(Npc Source,Effect Effect,int Damage,HopType? Hop);
    internal sealed class ImpactDispatch
    {
        private readonly Impact owner;
        private int claimed,hpEntered;
        private ImpactDispatch(Impact owner){this.owner=owner;}
        internal static ImpactDispatch Produce(object producer)=>producer is Impact i&&i.Producing&&i.Current()
            ?new(i):throw new InvalidOperationException("Only selected primary owner can produce HP work");
        private Values Read(Creature target)
        {
            if(!ReferenceEquals(target,owner.Target)||!owner.Current())throw new InvalidOperationException("Retired selected skill HP work");
            return new(owner.Cast.Owner,owner.Effect,owner.Damage,owner.Template.Hoptype);
        }
        internal Values ClaimFor(Creature target)
        {var v=Read(target);if(Interlocked.Exchange(ref claimed,1)!=0)throw new InvalidOperationException("Selected skill dispatch replay");return v;}
        internal Values EnterHp(Creature target)
        {
            var v=Read(target);if(Volatile.Read(ref claimed)!=1||Interlocked.Exchange(ref hpEntered,1)!=0)
                throw new InvalidOperationException("Selected skill HP replay");
            owner.HpEntered=true;return v;
        }
    }
}
