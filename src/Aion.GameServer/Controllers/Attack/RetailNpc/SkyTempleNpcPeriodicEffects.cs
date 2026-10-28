using System.Runtime.CompilerServices;
using Aion.GameServer.Controllers;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Retail;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;
using LOG=Aion.GameServer.Network.Aion.ServerPackets.SmAttackStatus.LOG;
using F=Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcUserMagicalInputs;
using Family=Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcPeriodicSpellParameters.Family;
namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Five selected periodic e2 owners. One actual Effect cancellation lease, one
/// ThreadPoolManager rearm, one controller/HP commit. Native Check bbe370 and Interval
/// bbed40 store base/critical at application but read element/fortitude/PvE at each tick.</summary>
internal static class SkyTempleNpcPeriodicEffects
{
    internal readonly record struct Source(bool Bleed,int Flat,int Interval,SkillElement Element);
    internal static Source Selected(int id)=>id switch
    {
        16576 or 16589=>new(false,15,2000,SkillElement.FIRE),
        16592 or 16710=>new(false,10,2000,SkillElement.FIRE),
        16594=>new(true,49,3000,SkillElement.WIND),
        _=>throw new InvalidOperationException("Not a selected periodic source")
    };
    private sealed class State(Effect effect,AbstractOverTimeEffect template,Source source,int damage,bool critical,Npc npc)
    {
        internal readonly object Gate=new();
        internal readonly Effect Effect=effect;
        internal readonly AbstractOverTimeEffect Template=template;
        internal readonly Source Source=source;
        internal readonly int Damage=damage, CasterId=npc.GetObjectId(), SourceLevel=npc.GetLevel();
        internal readonly long TargetLife=effect.GetEffected().GetController().RetailLifeEpoch;
        internal readonly int Map=effect.GetEffected().GetWorldId(),Instance=effect.GetEffected().GetInstanceId();
        internal readonly bool Critical=critical;
        internal bool Active,Dispatching;
        internal int Remaining;
        internal TickValues? Pending;
    }
    private static readonly ConditionalWeakTable<Effect,State> States=new();
    internal static bool TryCalculate(Effect effect,EffectTemplate template,SpellStatus? spellStatus,out bool success)
    {
        success=false;
        if(template is not SpellAttackEffect && template is not BleedEffect)return false;
        if(effect.GetEffector() is not Npc npc||!SkyTempleNpcProfileCatalog.TryGet(npc,out var profile))return false;
        Source source=Selected(effect.GetSkillId());
        if(template is not AbstractOverTimeEffect periodic||template.GetPosition()!=2||template.GetElement()!=source.Element||
            periodic.checktime!=source.Interval||(template is BleedEffect)!=source.Bleed||effect.GetEffected() is not Player target)
            throw new InvalidOperationException("Selected periodic source/slot/target differs");
        if(!effect.IsInSuccessEffects(1))return true;
        if(!effect.TryGetCalculatedMagicalCritical(out bool critical))throw new InvalidOperationException("Periodic e2 has no actual e1 critical cache");
        effect.ReuseMagicalCritical(2);
        int damage=unchecked(effect.GetSkillLevel()+source.Flat);
        if(!source.Bleed)
        {
            int delta=npc.GetGameStats().GetMBoost().GetCurrent()-target.GetGameStats().GetMBResist().GetCurrent();
            if(delta>=2900)throw new InvalidOperationException("Periodic boost reaches unresolved cap selector");
            damage=RetailNpcPeriodicDamageStages.SkillBoost(Family.SpellAttack,delta,0,1000,0,damage).Damage;
        }
        if(!template.IsNoResist())
        {
            var c=RetailNpcNormalStatusMath.Context.Create(SkyTempleCombatInputs.Scope(profile.NpcId),RetailNpcNormalScope.MainBinarySha256,"actual4.8 periodic Check inputs",false);
            int threshold=RetailNpcNormalStatusMath.CalculateMagicalResistThreshold(c,new(target.GetGameStats().GetMResist().GetCurrent(),
                npc.GetGameStats().GetMAccuracy().GetCurrent(),(uint)profile.StatRatio,profile.Level,target.GetLevel(),SkyTempleCombatInputs.Pose(target)));
            if(target.GetObserveController().CheckAttackStatus(AttackStatus.RESIST)||SkyTempleNpcCombat.Draw(1,1000)<=threshold)return true;
        }
        if(source.Bleed&&!SkyTempleNpcAbnormalEffects.CheckStatus(effect,1,StatEnum.BLEED_RESISTANCE,StatEnum.BLEED_RESISTANCE_PENETRATION,template.IsNoResist()))return true;
        lock(States){States.Remove(effect);States.Add(effect,new(effect,periodic,source,damage,critical,npc));}
        effect.AddSuccessEffect(template);if(spellStatus.HasValue)effect.SetSpellStatus(spellStatus.Value);
        template.CalculateDamage(effect);success=true;return true;
    }
    internal static bool TryStart(Effect effect,AbstractOverTimeEffect template,AbnormalState? abnormal)
    {
        if(!States.TryGetValue(effect,out var state))return false;
        if(!ReferenceEquals(template,state.Template)||!effect.IsInSuccessEffects(2))throw new InvalidOperationException("Periodic lease source differs");
        lock(state.Gate)
        {
            if(state.Active)throw new InvalidOperationException("Periodic lease already started");
            if(!Current(state))return true;
            state.Remaining=effect.GetDuration();state.Active=true;
            if(abnormal.HasValue){effect.SetAbnormal(abnormal.Value);effect.GetEffected().GetEffectController().SetAbnormal(abnormal.Value);}
            effect.SetReserveds(new EffectReserved(2,state.Damage,EffectReserved.ResourceType.HP,true,false),true);
            Schedule(state,state.Source.Interval);
        }
        return true;
    }
    internal static bool Owns(Effect effect)=>States.TryGetValue(effect,out var state)&&state.Active;
    internal static void Retire(Effect effect)
    {if(States.TryGetValue(effect,out var state))lock(state.Gate)state.Active=false;}
    internal static void RetireTarget(Creature target)
    {
        // Use the actual target registry, not a retained caster list or guessed slot.
        var controller=target.GetEffectController();if(controller is null)return;
        foreach(var effect in controller.GetAbnormalEffects())
            if(States.TryGetValue(effect,out var state))
            {lock(state.Gate)state.Active=false;effect.EndEffect();}
    }
    private static bool Current(State s)=>!s.Effect.RetailHasEnded&&s.Effect.GetEffected().IsSpawned()&&
        s.Effect.GetEffected().GetController().RetailLifeEpoch==s.TargetLife&&s.Effect.GetEffected().GetWorldId()==s.Map&&s.Effect.GetEffected().GetInstanceId()==s.Instance;
    private static void Schedule(State s,int delay)
    {
        lock(s.Effect)
        {
            if(!s.Active||!Current(s))return;
            // The existing Effect owns cancellation; replace only its completed selected lease.
            s.Effect.SetPeriodicTask(null!,2);
            var task=ThreadPoolManager.GetInstance().Schedule(_=>{Tick(s);return ValueTask.CompletedTask;},TimeSpan.FromMilliseconds(delay));
            s.Effect.SetPeriodicTask(task,2);
            s.Effect.SetRetailPeriodicDeadline(SystemClock.CurrentMillis()+s.Remaining);
        }
    }
    private static void Tick(State s)
    {
        lock(s.Gate)
        {
            if(!s.Active)return;
            if(!Current(s)){s.Active=false;s.Effect.EndEffect();return;}
            if(s.Remaining<=s.Source.Interval||s.Effect.GetEffected().IsDead())
            {s.Active=false;s.Effect.EndEffect();return;}
            TickDispatch? dispatch=null;
            try
            {
                var target=(Player)s.Effect.GetEffected();
                // Each tick resolves the current registry. A missing caster is genuinely null,
                // never a fabricated replacement actor or a reason to cancel target damage.
                var found=Aion.GameServer.World.World.GetInstance().FindVisibleObject(s.CasterId);
                Creature? caster=found as Creature;
                if(caster is not null && (!caster.IsSpawned()||caster.GetWorldId()!=s.Map||caster.GetInstanceId()!=s.Instance))caster=null;
                if(caster is not null && (caster is not Npc n||!SkyTempleNpcProfileCatalog.TryGet(n,out _)))
                    throw new InvalidOperationException("Periodic current caster replacement is outside the selected ordinary NPC domain");
                int amount=s.Damage;
                var kind=s.Source.Bleed?Family.Bleed:Family.SpellAttack;
                if(!s.Source.Bleed)
                {
                    int sourceLevel=caster?.GetLevel()??target.GetLevel();
                    amount=RetailNpcPeriodicDamageStages.ElementalDefense(kind,SkyTempleCombatInputs.Scope(((Npc)s.Effect.GetEffector()).GetNpcId()),
                        SkyTempleCombatInputs.SignedWord(SkyTempleCombatInputs.ElementalDefense(target,s.Source.Element),"periodic element"),amount,sourceLevel,target.GetLevel()).Damage;
                }
                if(s.Critical)amount=RetailNpcPeriodicDamageStages.NpcCritical(amount,checked((short)s.Effect.GetSkillLevel()),0,0,
                    SkyTempleCombatInputs.Word(SkyTempleCombatInputs.CriticalReduction(target,false),"periodic fortitude"));
                if(caster is not null)
                {var ratios=SkyTempleCombatInputs.Pve(caster,target,false);amount=F.Cvtt(F.Mul(amount,RetailNpcNormalDamageStages.CalculatePveRatio(
                    SkyTempleCombatInputs.Scope(((Npc)caster).GetNpcId()),ratios.Attack,ratios.Defense)));}
                amount=Math.Max(1,amount);
                s.Pending=new(caster,s.Effect,amount,s.Critical,s.Template.Hoptype,s.Source.Bleed?LOG.BLEED:LOG.SPELLATK);
                s.Dispatching=true;dispatch=TickDispatch.Produce(s);s.Dispatching=false;
                target.GetController().OnRetailPeriodicAttack(dispatch);
                target.GetObserveController().NotifyDotAttackedObservers(caster!,s.Effect);
            }
            finally
            {
                s.Dispatching=false;s.Pending=null;
                if(dispatch?.HpEntered==true)
                {
                    // Even if a post-commit observer throws, consume this interval once.
                    s.Remaining-=s.Source.Interval;
                    if(s.Active&&Current(s))Schedule(s,Math.Min(s.Source.Interval,Math.Max(1,s.Remaining)));
                }
                else{s.Active=false;s.Effect.EndEffect();}
            }
        }
    }
    internal sealed record TickValues(Creature? Source,Effect Effect,int Damage,bool Critical,HopType? Hop,LOG Log);
    internal sealed class TickDispatch
    {
        private readonly State owner;
        private readonly TickValues values;
        private int claimed,hpEntered;
        private TickDispatch(State owner,TickValues values){this.owner=owner;this.values=values;}
        internal static TickDispatch Produce(object producer)
        {
            if(producer is not State s||!s.Dispatching||s.Pending is null||!s.Active||!Current(s))
                throw new InvalidOperationException("Only the actual selected periodic owner can produce HP work");
            return new(s,s.Pending);
        }
        internal bool HpEntered=>Volatile.Read(ref hpEntered)!=0;
        private void Validate(Creature target)
        {
            if(!ReferenceEquals(target,values.Effect.GetEffected())||!Current(owner)||!owner.Active)
                throw new InvalidOperationException("Retired or mismatched periodic HP work");
        }
        internal TickValues ClaimFor(Creature target)
        {Validate(target);if(Interlocked.Exchange(ref claimed,1)!=0)throw new InvalidOperationException("Periodic dispatch replay");return values;}
        internal TickValues EnterHp(Creature target)
        {Validate(target);if(Volatile.Read(ref claimed)!=1||Interlocked.Exchange(ref hpEntered,1)!=0)throw new InvalidOperationException("Periodic HP replay");return values;}
    }
}
