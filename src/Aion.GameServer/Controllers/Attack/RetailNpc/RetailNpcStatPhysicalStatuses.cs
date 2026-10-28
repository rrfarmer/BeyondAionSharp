using System.Runtime.CompilerServices;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Retail;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using K = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcStatStatusSources.StatusKind;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>NPC-only read side of successful existing status-owner events.
/// Legacy player/PvP checks still use the same observers and charge ownership.</summary>
internal static class RetailNpcStatPhysicalStatuses
{
    internal interface ICounter
    {
        int Count { get; }
        int Decrement();
        void EndOwner();
        int SkillId { get; }
        int Position { get; }
        string SourceSha256 { get; }
    }
    internal sealed record Snapshot(int Strength,int Count,int? OwnerSkill,int? OwnerPosition,string SourceSha256);
    internal sealed record Consumption(K Kind,Snapshot Before,int CountAfter,bool Decremented,bool Expired,bool RemovalRequested,Snapshot After);
    internal sealed record Resolution(bool Hit,int CachedDefense,bool NaturalDodge,IReadOnlyList<Consumption> Consumptions);
    internal sealed class State
    {
        private sealed record Binding(int Skill,int Position,WeakReference<ICounter> Owner);
        private sealed class Slot { internal int Strength;internal ICounter? Counter;internal Binding? Binding;internal bool Known=true; }
        private readonly object gate=new();
        private readonly Dictionary<K,Slot> slots=new(){{K.Dodge,new()},{K.Block,new()},{K.Parry,new()}};
        private readonly Dictionary<ICounter,(K Kind,bool Known,string Reason)> active=new();
        private readonly Dictionary<object,(bool Known,string Reason)> blinds=new();
        private bool blindActive,blindKnown=true;private int blindChance;
        internal bool HadBlindEvents {get;private set;}
        internal void Begin(K kind,ICounter counter,int strength,bool known,string reason="")
        {
            lock(gate)
            {
                active[counter]=(kind,known,reason);var slot=slots[kind];
                slot.Known=known;slot.Strength=known?strength:0;slot.Counter=known?counter:null;
                slot.Binding=known?new(counter.SkillId,counter.Position,new(counter)):null;
            }
        }
        internal void End(K kind,ICounter counter)
        {
            lock(gate)
            {
                if(!active.Remove(counter,out var entry))return;
                var slot=slots[kind];slot.Strength=0;slot.Counter=null;
                // Native End clears the pair unconditionally, without restoring
                // an older owner. An unmapped event cannot certify another live pair.
                slot.Known=entry.Known||!active.Values.Any(x=>x.Kind==kind&&x.Known);
                // Actor's skill binding is separate; only consumption clears it.
            }
        }
        internal void BeginBlind(object owner,int chance,bool known,string reason="")
        {
            lock(gate){HadBlindEvents=true;blinds[owner]=(known,reason);blindKnown=known;blindActive=true;blindChance=chance;}
        }
        internal void EndBlind(object owner)
        {
            lock(gate)
            {
                if(!blinds.Remove(owner,out var entry))return;
                blindActive=false;blindChance=0;blindKnown=entry.Known||!blinds.Values.Any(x=>x.Known);
            }
        }
        internal (bool Active,int Chance) Blind {get{lock(gate)return(blindActive,blindChance);}}
        internal bool CanRead(out string reason)
        {
            lock(gate)
            {
                var unknown=active.Values.FirstOrDefault(x=>!x.Known);
                if(unknown.Reason is not null){reason=unknown.Reason;return false;}
                var unknownBlind=blinds.Values.FirstOrDefault(x=>!x.Known);
                if(unknownBlind.Reason is not null){reason=unknownBlind.Reason;return false;}
                if(!blindKnown||slots.Values.Any(x=>!x.Known)){reason="unresolved-status-owner-after-unmapped-end";return false;}
                reason="";return true;
            }
        }
        internal Snapshot Read(K kind)
        {
            lock(gate)
            {
                var slot=slots[kind];
                if(!slot.Known||active.Values.Any(x=>x.Kind==kind&&!x.Known))
                    throw new InvalidOperationException("Retail physical status owner unavailable during execution: "+kind);
                return new(slot.Strength,slot.Counter?.Count??0,
                    slot.Binding?.Skill,slot.Binding?.Position,slot.Counter?.SourceSha256??"");
            }
        }
        private Consumption Consume(K kind)
        {
            Snapshot before;int afterCount;bool decremented=false,expired=false;Binding? binding=null;
            lock(gate)
            {
                before=Read(kind);afterCount=before.Count;var slot=slots[kind];
                if(slot.Strength!=0&&slot.Counter is { } counter&&counter.Count!=0)
                {afterCount=counter.Decrement();decremented=true;expired=afterCount<1;}
            }
            // Native decrement releases the manager lock before removal. End
            // callbacks may replace pairs which subsequent families read afresh.
            if(expired){lock(gate)binding=slots[kind].Binding;}
            if(binding is not null)
            {
                if(binding.Owner.TryGetTarget(out var owner))owner.EndOwner();
                lock(gate)slots[kind].Binding=null; // Mandatory outer write even after a replacement callback.
            }
            return new(kind,before,afterCount,decremented,expired,binding is not null,Read(kind));
        }
        internal Resolution Resolve(int force,bool dodgeAllowed=true,bool parryAllowed=true)
        {
            var operations=new List<Consumption>();int cache=0;
            if(dodgeAllowed)
            {
                bool defended=force<Read(K.Dodge).Strength;
                if(defended||force!=0)operations.Add(Consume(K.Dodge));
                if(defended)return new(false,0,false,operations.AsReadOnly());
            }
            bool block=force<Read(K.Block).Strength;
            if(block||force!=0)operations.Add(Consume(K.Block));
            if(block)cache=2;
            if(parryAllowed)
            {
                bool parry=force<Read(K.Parry).Strength;
                if(parry||force!=0)operations.Add(Consume(K.Parry));
                if(parry)cache=1;
            }
            return new(true,cache,force==0,operations.AsReadOnly());
        }
    }

    private static readonly ConditionalWeakTable<Creature,State> states=new();
    internal static State For(Creature actor)=>states.GetValue(actor,_=>new());
    internal sealed class DefenseObserver : AttackStatusObserver,ICounter
    {
        private readonly Effect effect;internal readonly K Kind;internal readonly bool Recorded;
        private readonly string sourceSha;
        internal DefenseObserver(Effect effect,EffectTemplate template,K kind)
            :base(template.Value,ToStatus(kind))
        {
            this.effect=effect;Kind=kind;Position=template.GetPosition();sourceSha="";
            if(effect.GetEffected() is not Player||!RetailNpcStatCatalog.Enabled)return;
            Recorded=true;string reason="missing-retail-status-context";RetailNpcStatStatusSources.Part? part=null;
            bool known=RetailNpcStatCatalog.StatusSources?.TryGet(effect,template,kind,out part!,out reason)==true;
            sourceSha=part?.SourceSha256??"";
            For(effect.GetEffected()).Begin(kind,this,part?.Strength??0,known,reason);
        }
        public int Count=>Value;
        public int Decrement()=>unchecked(--Value);
        public void EndOwner()=>effect.EndEffect();
        public int SkillId=>effect.GetSkillId();
        public int Position {get;}
        public string SourceSha256=>sourceSha;
        public override bool CheckStatus(AttackStatus status)
        {
            if(status!=Status)return false;
            if(--Value<=0)effect.EndEffect();
            return true;
        }
    }
    private static AttackStatus ToStatus(K kind)=>kind switch
    {K.Dodge=>AttackStatus.DODGE,K.Block=>AttackStatus.BLOCK,K.Parry=>AttackStatus.PARRY,_=>throw new ArgumentOutOfRangeException(nameof(kind))};
    internal static void EndDefense(Effect effect,EffectTemplate template,K kind)
    {
        if(!states.TryGetValue(effect.GetEffected(),out var state))return;
        // Observer removal occurs before template End; retain the exact owner by Effect/slot.
        if(owners.TryGetValue(effect,out var map)&&map.TryGetValue(template.GetPosition(),out var observer))
            state.End(kind,observer);
    }
    private static readonly ConditionalWeakTable<Effect,Dictionary<int,DefenseObserver>> owners=new();
    internal static DefenseObserver BeginDefense(Effect effect,EffectTemplate template,K kind)
    {
        var observer=new DefenseObserver(effect,template,kind);
        owners.GetValue(effect,_=>new())[template.GetPosition()]=observer;
        return observer;
    }
    internal static bool BeginBlind(Effect effect,EffectTemplate template)
    {
        if(effect.GetEffected() is not Npc||!RetailNpcStatCatalog.Enabled)return false;
        string reason="missing-retail-status-context";RetailNpcStatStatusSources.Part? part=null;
        bool known=RetailNpcStatCatalog.StatusSources?.TryGet(effect,template,K.Blind,out part!,out reason)==true
            &&effect.GetSkillLevel() is >=0 and <=short.MaxValue;
        if(!known&&reason.Length==0)reason="unreviewed-status-skill-level";
        For(effect.GetEffected()).BeginBlind(effect,known?part!.BlindChance(effect.GetSkillLevel()):0,known,reason);
        return true;
    }
    internal static void EndBlind(Effect effect)
    {if(states.TryGetValue(effect.GetEffected(),out var state))state.EndBlind(effect);}

    internal static bool Preflight(Npc source,Player target,out string reason)
    {
        if(target.GetTransformModel().IsActive()){reason="unreviewed-transformed-player-defense-tables";return false;}
        if(target.GetObserveController().HasUnmappedPhysicalDefenseObserver)
        {reason="unmapped-physical-defense-observer";return false;}
        if(source.GetObserveController().HasUnmappedPhysicalAttackerObserver)
        {reason="unmapped-physical-attacker-observer";return false;}
        if(!For(target).CanRead(out reason)||!For(source).CanRead(out reason))return false;
        if(source.GetEffectController().IsInAnyAbnormalState(AbnormalState.BLIND)&&!For(source).HadBlindEvents)
        {reason="unmapped-current-native-blind-state";return false;}
        reason="";return true;
    }
}
