using System.Runtime.CompilerServices;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Retail;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Actual pilot effect owners; no native allocator/TLS simulation. Source16522/16699,
/// native34ca40 order and bb5370/68ed40 primary-before-reflection are the authority.</summary>
internal static class SkyTempleNpcProtection
{
    private sealed class State(Creature owner)
    {
        internal readonly Creature Owner = owner;
        internal readonly object Gate = new();
        internal PilotObserver? Shield, Reflector;
        // Bound skill ownership remains with the actual Effect. End does not forcibly null it.
    }
    private static readonly ConditionalWeakTable<Creature,State> States = new();
    private static readonly ConditionalWeakTable<Effect,PendingReflection> Pending = new();

    internal static bool TryStart(Effect effect,bool reflect)
    {
        if (effect.GetEffector() is not Npc npc || !SkyTempleNpcProfileCatalog.TryGet(npc,out var p)) return false;
        int id = reflect?16699:16522;
        if (effect.GetSkillId()!=id) return false;
        if (!ReferenceEquals(effect.GetEffected(),npc) || !p.Skills.Any(s=>s.Id48==id&&s.Level==effect.GetSkillLevel()))
            throw new InvalidOperationException("Unmatched SkyTemple protection owner/source level");
        var state = States.GetValue(npc,static owner=>new(owner));
        var observer = new PilotObserver(state,effect,reflect,checked(2*effect.GetSkillLevel()+(reflect?16:25)));
        lock(state.Gate)
        {
            if (reflect) state.Reflector=observer; else state.Shield=observer;
            effect.AddObserver(npc,observer);
        }
        return true;
    }

    internal static void End(Effect effect,bool reflect)
    {
        if (!States.TryGetValue(effect.GetEffected(),out var state)) return;
        lock(state.Gate)
        {
            var current=reflect?state.Reflector:state.Shield;
            if (current is not null && ReferenceEquals(current.Effect,effect))
            { current.Active=false; if(!reflect)current.Budget=0; }
        }
    }

    internal static void Preflight(IReadOnlyList<AttackCalcObserver> observers,Creature attacker,bool preparing)
    {
        var pilots=observers.OfType<PilotObserver>().ToArray();
        if(pilots.Length==0&&!preparing)return;
        if(observers.Any(x=>x is AttackShieldObserver)||pilots.Any(x=>!ReferenceEquals(x.State,pilots[0].State)))
            throw new InvalidOperationException("Unsupported coexisting general protection on SkyTemple pilot owner");
        if(!preparing&&pilots.Length!=0&&attacker is not Player)
            throw new InvalidOperationException("Pilot protection requires an actual Player damage source");
    }

    /// <summary>Called with the actual observer snapshot. Preflight unsupported coexistence
    /// BEFORE RNG/budget/effect callbacks, then retain fresh registry reads after removal.</summary>
    internal static bool CheckOrdered(IReadOnlyList<AttackCalcObserver> observers,List<AttackResult> attacks,
        Effect? incoming,Creature attacker)
    {
        var pilots=observers.OfType<PilotObserver>().ToArray();
        if(pilots.Length==0)return false;
        State state=pilots[0].State;
        Preflight(observers,attacker,false);
        lock(state.Gate)
        {
            foreach(var attack in attacks)
            {
                var status=AttackStatusExtensions.GetBaseStatus(attack.GetAttackStatus());
                int original=attack.GetDamage();
                if(original==0||status is AttackStatus.DODGE or AttackStatus.RESIST)continue;
                if(original<0)throw new InvalidOperationException("Negative protection input");
                var shield=state.Shield;
                if(shield is {Active:true})
                {
                    _=SkyTempleNpcCombat.Draw(1,1000); // Even source chance1000 consumes this draw.
                    int absorb=Math.Min(RetailNpcPilotProtectionTransition.ShieldAmount(original,shield.PerHit,1),shield.Budget);
                    shield.Budget-=absorb;
                    attack.SetDamage(Math.Max(0,unchecked(original-absorb)));
                    if(shield.Budget<1)shield.Effect.EndEffect(); // Actual complete synchronous End/recompute owner.
                    attack.SetShieldType(ShieldType.NORMAL.GetId()); // Native sets it even if absorption0.
                    if(absorb>=original)attack.SetLaunchSubEffect(false);
                }
                // Do not cache this across EndEffect. It can remove/replace the actual effect.
                var reflector=state.Reflector;
                if(reflector is not {Active:true})continue;
                Creature defender=state.Owner;
                if(attacker.GetWorldId()!=defender.GetWorldId()||attacker.GetInstanceId()!=defender.GetInstanceId())continue;
                float distance=DistanceSquared(attacker.GetX(),attacker.GetY(),attacker.GetZ(),defender.GetX(),defender.GetY(),defender.GetZ());
                if(distance>64f)continue; // maximum predicate first
                if(distance<=0f)continue; // minimum predicate is reached even at source radius0
                _=SkyTempleNpcCombat.Draw(1,1000);
                int reflected=RetailNpcPilotProtectionTransition.ReflectionAmount(original,25,reflector.PerHit);
                if(reflected<=0)continue;
                attack.SetReflectedDamage(reflected); attack.SetReflectedSkillId(16699);
                attack.SetShieldType(ShieldType.REFLECTOR.GetId());
                // Actual Player slot188 equivalent is zero; no chosen override callback.
            }
        }
        // Preserve real host observer callbacks exactly once. Production non-shield
        // observers inherit a no-op; capture/other observers retain their actual behavior.
        foreach(var observer in observers)if(observer is not PilotObserver)observer.CheckShield(attacks,incoming!,attacker);
        return true;
    }

    internal static float DistanceSquared(float ax,float ay,float az,float bx,float by,float bz)
    {
        var f=RetailNpcUserMagicalInputs.Sub(ay,by);var x=RetailNpcUserMagicalInputs.Sub(ax,bx);
        var z=RetailNpcUserMagicalInputs.Sub(az,bz);
        return RetailNpcUserMagicalInputs.Add(RetailNpcUserMagicalInputs.Add(
            RetailNpcUserMagicalInputs.Mul(f,f),RetailNpcUserMagicalInputs.Mul(x,x)),RetailNpcUserMagicalInputs.Mul(z,z));
    }

    private sealed class PilotObserver(State state,Effect effect,bool reflect,int hit):AttackCalcObserver
    {
        internal State State {get;}=state;
        internal Effect Effect {get;}=effect;
        internal bool Reflect {get;}=reflect;
        internal int PerHit {get;}=hit;
        internal int Budget=20000;
        internal bool Active=true;
        public override void CheckShield(List<AttackResult> attacks,Effect effect,Creature attacker)
            =>throw new InvalidOperationException("Pilot protection requires ordered dispatcher");
    }

    internal static void AttachEffectReflection(Effect effect,AttackResult result)
    {
        if(result.GetReflectedSkillId()!=16699 || result.GetReflectedDamage()==0 ||
            effect.GetEffected() is not Npc npc || !SkyTempleNpcProfileCatalog.TryGet(npc,out _))return;
        Pending.Remove(effect);
        Pending.Add(effect,new(effect.GetEffector(),effect.GetEffected(),result.GetReflectedDamage()));
    }
    internal static PendingReflection? NormalReflection(Creature source,Creature target,IReadOnlyList<AttackResult> results)
    {
        if(target is not Npc npc||!SkyTempleNpcProfileCatalog.TryGet(npc,out _))return null;
        int amount=0;
        foreach(var r in results)if(r.GetReflectedSkillId()==16699)amount=checked(amount+r.GetReflectedDamage());
        return amount==0?null:new(source,target,amount);
    }
    internal static void CompleteEffectReflection(Effect? effect)
    {
        if(effect is null||!Pending.TryGetValue(effect,out var pending))return;
        Pending.Remove(effect);pending.Complete();
    }
    internal sealed class PendingReflection(Creature receiver,Creature source,int amount)
    {
        private int applied;
        internal void Complete()
        {
            if(Interlocked.Exchange(ref applied,1)!=0)return;
            // Existing HP owner directly; no pre-emission protection recursion. Native
            // reflection metadata16699 is deliberately distinct from HP context skill0.
            receiver.GetController().OnAttack(source,null!,SmAttackStatus.TYPE.REGULAR,amount,false,
                SmAttackStatus.LOG.REGULAR,null,null);
        }
    }
}
