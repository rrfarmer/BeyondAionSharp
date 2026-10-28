using System;
using System.Collections.Generic;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Retail;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Controllers;

/// <summary>
/// Java parity: controllers/ObserveController (ATracer, Cura).
/// </summary>
public class ObserveController
{
    private readonly List<ActionObserver> observers = new();
    private volatile OneTimeBoostSkillAttack? oneTimeBoostSkillAttack;

    // Java CopyOnWriteArrayList → faithful copy-on-write via volatile array swap (lock-free reads).
    private readonly object _attackCalcLock = new();
    private volatile AttackCalcObserver[] attackCalcObservers = Array.Empty<AttackCalcObserver>();
    private long attackCalcVersion;

    /// <summary>
    /// Adds the observer for a single notification; it is automatically removed after notification.
    /// </summary>
    public void Attach(ActionObserver observer)
    {
        observer.MakeOneTimeUse();
        AddObserver(observer);
    }

    public void AddObserver(ActionObserver observer)
    {
        lock (observers)
        {
            observers.Add(observer);
        }
    }

    public void AddAttackCalcObserver(AttackCalcObserver observer)
    {
        lock (_attackCalcLock)
        {
            var copy = new AttackCalcObserver[attackCalcObservers.Length + 1];
            Array.Copy(attackCalcObservers, copy, attackCalcObservers.Length);
            copy[attackCalcObservers.Length] = observer;
            attackCalcObservers = copy;
            attackCalcVersion=unchecked(attackCalcVersion+1);
        }
    }

    public void RemoveObserver(ActionObserver observer)
    {
        bool removed;
        lock (observers)
        {
            removed = observers.Remove(observer);
        }
        if (removed)
            observer.OnRemoved();
    }

    public void RemoveAttackCalcObserver(AttackCalcObserver observer)
    {
        lock (_attackCalcLock)
        {
            int index = Array.IndexOf(attackCalcObservers, observer);
            if (index < 0)
                return;
            var copy = new AttackCalcObserver[attackCalcObservers.Length - 1];
            Array.Copy(attackCalcObservers, 0, copy, 0, index);
            Array.Copy(attackCalcObservers, index + 1, copy, index, attackCalcObservers.Length - index - 1);
            attackCalcObservers = copy;
            attackCalcVersion=unchecked(attackCalcVersion+1);
        }
    }

    public void NotifyObservers(ObserverType type, params object[] @object)
    {
        List<ActionObserver> notifiable = new List<ActionObserver>(0);
        lock (observers)
        {
            if (observers.Count == 0)
                return;
            for (int i = 0; i < observers.Count;)
            {
                ActionObserver observer = observers[i];
                if (observer.Matches(type))
                {
                    if (notifiable.Count == 0)
                        notifiable = new List<ActionObserver>();
                    notifiable.Add(observer);
                    if (observer.IsOneTimeUse())
                    {
                        observers.RemoveAt(i);
                        continue;
                    }
                }
                i++;
            }
        }

        // notify outside of lock
        foreach (ActionObserver observer in notifiable)
        {
            NotifyAction(type, observer, @object);
            if (observer.IsOneTimeUse())
                observer.OnRemoved();
        }
    }

    private void NotifyAction(ObserverType type, ActionObserver observer, params object[] @object)
    {
        switch (type)
        {
            case ObserverType.ATTACK:
                observer.Attack((Creature)@object[0], (int)@object[1]);
                break;
            case ObserverType.ATTACKED:
                observer.Attacked((Creature)@object[0], (int)@object[1]);
                break;
            case ObserverType.DEATH:
                observer.Died((Creature)@object[0]);
                break;
            case ObserverType.EQUIP:
                observer.Equip((Item)@object[0], (Player)@object[1]);
                break;
            case ObserverType.UNEQUIP:
                observer.Unequip((Item)@object[0], (Player)@object[1]);
                break;
            case ObserverType.MOVE:
                observer.Moved();
                break;
            case ObserverType.STARTSKILLCAST:
                observer.StartSkillCast((Skill)@object[0]);
                break;
            case ObserverType.ENDSKILLCAST:
                observer.EndSkillCast((Skill)@object[0]);
                break;
            case ObserverType.BOOSTSKILLCOST:
                observer.BoostSkillCost((Skill)@object[0]);
                break;
            case ObserverType.DOT_ATTACKED:
                observer.Dotattacked((Creature)@object[0], (Effect)@object[1]);
                break;
            case ObserverType.ITEMUSE:
                observer.Itemused((Item)@object[0]);
                break;
            case ObserverType.ABNORMALSETTED:
                observer.Abnormalsetted((AbnormalState)@object[0]);
                break;
            case ObserverType.SUMMONRELEASE:
                observer.Summonrelease();
                break;
            case ObserverType.SIT:
                observer.Sit();
                break;
            case ObserverType.HP_CHANGED:
                observer.HpChanged((int)@object[0]);
                break;
        }
    }

    public void NotifyDeathObservers(Creature lastAttacker)
    {
        NotifyObservers(ObserverType.DEATH, lastAttacker);
    }

    public void NotifyMoveObservers()
    {
        NotifyObservers(ObserverType.MOVE);
    }

    public void NotifySitObservers()
    {
        NotifyObservers(ObserverType.SIT);
    }

    public void NotifyAttackObservers(Creature creature, int skillId)
    {
        NotifyObservers(ObserverType.ATTACK, creature, skillId);
    }

    public void NotifyAttackedObservers(Creature creature, int skillId)
    {
        NotifyObservers(ObserverType.ATTACKED, creature, skillId);
    }

    public void NotifyDotAttackedObservers(Creature creature, Effect effect)
    {
        NotifyObservers(ObserverType.DOT_ATTACKED, creature, effect);
    }

    public void NotifyStartSkillCastObservers(Skill skill)
    {
        NotifyObservers(ObserverType.STARTSKILLCAST, skill);
    }

    public void NotifyEndSkillCastObservers(Skill skill)
    {
        NotifyObservers(ObserverType.ENDSKILLCAST, skill);
    }

    public void NotifyBoostSkillCostObservers(Skill skill)
    {
        NotifyObservers(ObserverType.BOOSTSKILLCOST, skill);
    }

    public void NotifyItemEquip(Item item, Player owner)
    {
        NotifyObservers(ObserverType.EQUIP, item, owner);
    }

    public void NotifyItemUnEquip(Item item, Player owner)
    {
        NotifyObservers(ObserverType.UNEQUIP, item, owner);
    }

    /// <summary>
    /// Aborts every attached <see cref="ItemUseObserver"/>, so each of them cancels its own item use and tells the player about it.
    /// </summary>
    public void AbortItemUseObservers()
    {
        List<ItemUseObserver> itemUseObservers = new List<ItemUseObserver>();
        lock (observers)
        {
            for (int i = observers.Count - 1; i >= 0; i--)
            {
                if (observers[i] is ItemUseObserver itemUseObserver)
                {
                    itemUseObservers.Add(itemUseObserver);
                    observers.RemoveAt(i);
                }
            }
        }

        // Java iterates in insertion order; the reverse collection loop above gathered them backwards
        itemUseObservers.Reverse();
        foreach (ItemUseObserver itemUseObserver in itemUseObservers)
        {
            itemUseObserver.Abort();
            itemUseObserver.OnRemoved();
        }
    }

    public void NotifyItemuseObservers(Item item)
    {
        NotifyObservers(ObserverType.ITEMUSE, item);
    }

    public void NotifyAbnormalSettedObservers(AbnormalState state)
    {
        NotifyObservers(ObserverType.ABNORMALSETTED, state);
    }

    public void NotifySummonReleaseObservers()
    {
        NotifyObservers(ObserverType.SUMMONRELEASE);
    }

    public void NotifyHPChangeObservers(int hpValue)
    {
        NotifyObservers(ObserverType.HP_CHANGED, hpValue);
    }

    public bool CheckAttackStatus(AttackStatus status)
    {
        if (attackCalcObservers.Length > 0)
        {
            foreach (AttackCalcObserver observer in attackCalcObservers)
            {
                if (observer.CheckStatus(status))
                {
                    return true;
                }
            }
        }
        return false;
    }

    public bool CheckAttackerStatus(AttackStatus status)
    {
        if (attackCalcObservers.Length > 0)
        {
            foreach (AttackCalcObserver observer in attackCalcObservers)
            {
                if (observer.CheckAttackerStatus(status))
                {
                    return true;
                }
            }
        }
        return false;
    }

    public AttackerCriticalStatus CheckAttackerCriticalStatus(AttackStatus status, bool isSkill)
    {
        if (attackCalcObservers.Length > 0)
        {
            foreach (AttackCalcObserver observer in attackCalcObservers)
            {
                AttackerCriticalStatus acStatus = observer.CheckAttackerCriticalStatus(status, isSkill);
                if (acStatus.IsResult())
                {
                    return acStatus;
                }
            }
        }
        return new AttackerCriticalStatus(false);
    }

    // Read-only preflight: querying a critical observer can consume its owner.
    internal bool HasSkillCriticalObserver => Array.Exists(attackCalcObservers,
        static observer => observer is AttackerCriticalStatusObserver);

    // Drain healing uses the post-protection record. Until each protection
    // owner is mapped to native semantics, do not consume an unproved owner.
    // The observer contract allows shield behavior outside AttackShieldObserver.
    internal bool HasDrainProtectionObserver => Array.Exists(attackCalcObservers,
        static observer => observer.GetType().GetMethod(nameof(AttackCalcObserver.CheckShield))?.DeclaringType
            != typeof(AttackCalcObserver));

    // Recheck under the observer mutation lock at the point where the selected
    // NPC drain records its damage. Do not invoke a newly added unreviewed
    // shield owner after the earlier preflight has already passed.
    internal sealed record PlayerDrainProtectionReceipt(ObserveController Owner,
        Aion.GameServer.Model.Stats.Container.PlayerLifeStats Life,long LifeEpoch,AttackShieldObserver? ReviewedShield=null,
        RetailNpcPlayerShieldCatalog? ShieldSources=null);

    // One source-checked ordinary shield can use the reviewed 5.8 branch.
    // All other shield observers and combinations remain visibly unsupported.
    private bool TrySelectRetailPlayerShield(Player target,RetailNpcPlayerShieldCatalog? catalog,
        out AttackShieldObserver? shield,out RetailNpcPlayerShieldSource? source)
    {
        shield=null;source=null;
        foreach(var observer in attackCalcObservers)
        {
            if(observer.GetType().GetMethod(nameof(AttackCalcObserver.CheckShield))?.DeclaringType==typeof(AttackCalcObserver))continue;
            if(shield is not null ||observer is not AttackShieldObserver candidate
                ||catalog?.Find(candidate.RetailShieldSkillId) is not { } selected
                ||!candidate.MatchesRetailPlayerShield(target,selected))return false;
            shield=candidate;source=selected;
        }
        return true;
    }

    internal bool CanCalculateRetailPlayerDrain(Player target)
    {
        if(!ReferenceEquals(target.GetObserveController(),this))return false;
        lock(_attackCalcLock)return TrySelectRetailPlayerShield(target,RetailNpcStatCatalog.PlayerShieldSources,out _,out _);
    }

    // Retail calculates this shield before scheduling delayed damage. Its
    // post-hit protector branches use different flags, so a matching shield
    // added afterwards must not cancel or absorb the recorded hit. Continue
    // rejecting other protection owners whose delivery semantics are unknown.
    private bool HasOtherDrainProtection(PlayerDrainProtectionReceipt receipt,Player target)
    {
        int reviewedCount=0;
        foreach(var observer in attackCalcObservers)
        {
            if(observer.GetType().GetMethod(nameof(AttackCalcObserver.CheckShield))?.DeclaringType==typeof(AttackCalcObserver))continue;
            if(!ReferenceEquals(observer,receipt.ReviewedShield)
                && !(observer is AttackShieldObserver shield && receipt.ShieldSources is { } catalog
                    && ReferenceEquals(RetailNpcStatCatalog.PlayerShieldSources,catalog)
                    && catalog.Find(shield.RetailShieldSkillId) is { } source
                    && shield.MatchesRetailPlayerShield(target,source)))return true;
            if(++reviewedCount>1)return true;
        }
        return false;
    }

    internal bool TryCalculatePlayerRetailNpcDrain(Effect effect,Player target,int damage,AttackStatus status,
        out PlayerDrainProtectionReceipt? receipt)
    {
        receipt=null;
        if(!ReferenceEquals(target.GetObserveController(),this)||!ReferenceEquals(effect.GetEffected(),target)
            ||effect.GetEffector() is not Npc||damage<0)
            throw new ArgumentException("Invalid player-target retail NPC drain inputs");
        var life=target.GetLifeStats();
        var catalog=RetailNpcStatCatalog.PlayerShieldSources;
        Effect? depleted=null;
        lock(life) lock(_attackCalcLock)
        {
            if(!ReferenceEquals(life,target.GetLifeStats())||!target.IsSpawned()||target.IsDead()||life.GetCurrentHp()<=0
                ||!TrySelectRetailPlayerShield(target,catalog,out var shield,out var source))return false;
            if(shield is not null)
            {
                if(source is null ||!shield.MatchesRetailPlayerShield(target,source))return false;
                int reduced=shield.ApplyRetailPlayerShield(damage,source,out depleted);
                AttackUtil.CalculateEffectResult(effect,target,reduced,status,HitType.MAHIT,true,1,true);
                if(damage>0)effect.SetShieldDefense(ShieldType.NORMAL.GetId());
            }
            else AttackUtil.CalculateEffectResult(effect,target,damage,status,HitType.MAHIT,false,1,true);
            receipt=new(this,life,target.GetController().RetailLifeEpoch,shield,catalog);
        }
        depleted?.EndEffect();
        return true;
    }

    // Prepared NPC drain fixtures may only calculate a hit while there is no
    // unreviewed shield owner. Observer additions, removals and Clear use the
    // same lock, so one cannot appear between this check and the hit record.
    // This does not reserve actor life or protection through later HP dispatch.
    internal bool TryCalculateUnprotectedNpcDrainForOfflineFixture(Effect effect,Npc target,int damage,int position)
        => TryCalculateUnprotectedNpcDrainForOfflineFixture(effect,target,damage,position,out _);

    internal bool TryCalculateUnprotectedNpcDrainForOfflineFixture(Effect effect,Npc target,int damage,int position,
        out NpcDrainTickProtectionReceipt? receipt)
    {
        receipt=null;
        if(!ReferenceEquals(target.GetObserveController(),this)||!ReferenceEquals(effect.GetEffected(),target)
            ||effect.GetEffector() is not Npc ||damage<0)throw new ArgumentException("Invalid NPC drain hit inputs");
        lock(_attackCalcLock)
        {
            if(HasDrainProtectionObserver)return false;
            AttackUtil.CalculateEffectResult(effect,target,damage,AttackStatus.NORMALHIT,
                HitType.MAHIT,false,position,true);
            receipt=new(this,attackCalcVersion);
            return true;
        }
    }

    // Inspect capability without querying an observer or consuming a charge.
    internal bool HasUnmappedPhysicalDefenseObserver=>Array.Exists(attackCalcObservers,static observer=>
        observer is Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcStatPhysicalStatuses.DefenseObserver native
            ?!native.Recorded
            :!AlwaysResistEffect.IsResistOnlyObserver(observer)
                &&observer.GetType().GetMethod(nameof(AttackCalcObserver.CheckStatus))?.DeclaringType!=typeof(AttackCalcObserver));
    internal bool HasUnmappedPhysicalAttackerObserver=>Array.Exists(attackCalcObservers,static observer=>
        !BlindEffect.IsNativeRecordedObserver(observer)
            &&observer.GetType().GetMethod(nameof(AttackCalcObserver.CheckAttackerStatus))?.DeclaringType!=typeof(AttackCalcObserver));

    internal void PreflightSkyTempleProtection(Creature attacker,bool preparing=false)
        => Aion.GameServer.Controllers.Attack.RetailNpc.SkyTempleNpcProtection.Preflight(attackCalcObservers,attacker,preparing);

    internal void CheckStatisticsPeriodicProtection(List<AttackResult> attacks,Effect effect,Creature? attacker)
    {
        if(attacker is not null){CheckShieldStatus(attacks,effect,attacker);return;}
        // A missing registry caster is a real native periodic branch. Normal/MP
        // shields need no attacker; reflection has no current recipient. Reject
        // unreviewed null-source protect/convert owners before consuming any one.
        var snapshot=attackCalcObservers;
        if(snapshot.OfType<AttackShieldObserver>().Any(x=>x.GetShieldType() is not
            (ShieldType.NORMAL or ShieldType.MPSHIELD or ShieldType.REFLECTOR or ShieldType.SKILL_REFLECTOR)))
            throw new InvalidOperationException("Unreviewed null-caster periodic protection owner");
        foreach(var observer in snapshot)
        {
            if(observer is AttackShieldObserver shield&&shield.GetShieldType() is ShieldType.REFLECTOR or ShieldType.SKILL_REFLECTOR)continue;
            observer.CheckShield(attacks,effect,null!);
        }
    }

    // The same no-unreviewed-shield guarantee for a prepared NPC drain tick.
    // The lock ends before HP dispatch and does not reserve either actor.
    internal bool TryCheckUnprotectedNpcDrainTickForOfflineFixture(List<AttackResult> attacks,Effect effect,Creature? attacker)
        => TryCheckUnprotectedNpcDrainTickForOfflineFixture(attacks,effect,attacker,out _);

    internal sealed record NpcDrainTickProtectionReceipt(ObserveController Owner,long Version);

    internal bool TryCheckUnprotectedNpcDrainTickForOfflineFixture(List<AttackResult> attacks,Effect effect,Creature? attacker,
        out NpcDrainTickProtectionReceipt? receipt)
    {
        receipt=null;
        if(effect.GetEffected() is not Npc target ||!ReferenceEquals(target.GetObserveController(),this)
            ||effect.GetEffector() is not Npc ||attacks.Count!=1 ||attacker is not (null or Npc))
            throw new ArgumentException("Invalid NPC drain tick inputs");
        lock(_attackCalcLock)
        {
            if(HasDrainProtectionObserver)return false;
            CheckStatisticsPeriodicProtection(attacks,effect,attacker);
            receipt=new(this,attackCalcVersion);
            return true;
        }
    }

    // Entered only while the target HP owner is locked; the caller releases
    // this permit before packets, effect callbacks or AI notifications.
    internal sealed class NpcDrainProtectionChangedException()
        : InvalidOperationException("NPC drain protection changed before HP commit");

    internal IDisposable EnterNpcDrainTickHpForOfflineFixture(NpcDrainTickProtectionReceipt receipt,Npc target)
    {
        if(!ReferenceEquals(receipt.Owner,this)||!ReferenceEquals(target.GetObserveController(),this)
            ||!System.Threading.Monitor.IsEntered(target.GetLifeStats()))
            throw new InvalidOperationException("Invalid NPC drain protection receipt or HP owner");
        System.Threading.Monitor.Enter(_attackCalcLock);
        try
        {
            if(attackCalcVersion!=receipt.Version||HasDrainProtectionObserver)
                throw new NpcDrainProtectionChangedException();
            return new NpcDrainTickHpPermit(_attackCalcLock);
        }
        catch { System.Threading.Monitor.Exit(_attackCalcLock);throw; }
    }

    private sealed class NpcDrainTickHpPermit(object gate):IDisposable
    {
        public void Dispose()=>System.Threading.Monitor.Exit(gate);
    }

    internal sealed class PlayerDrainProtectionChangedException()
        : InvalidOperationException("Player drain protection or life changed before HP commit");

    // The target HP lock is held first, matching life retirement and the
    // existing NPC drain owner. Release this before packet/AI callbacks.
    internal IDisposable EnterPlayerDrainHp(PlayerDrainProtectionReceipt receipt,Player target)
    {
        if(!ReferenceEquals(receipt.Owner,this)||!ReferenceEquals(target.GetObserveController(),this)
            ||!ReferenceEquals(target.GetLifeStats(),receipt.Life)||!System.Threading.Monitor.IsEntered(receipt.Life))
            throw new PlayerDrainProtectionChangedException();
        System.Threading.Monitor.Enter(_attackCalcLock);
        try
        {
            if(!ReferenceEquals(target.GetLifeStats(),receipt.Life)||!target.IsSpawned()||target.IsDead()||receipt.Life.GetCurrentHp()<=0
                ||target.GetController().RetailLifeEpoch!=receipt.LifeEpoch||HasOtherDrainProtection(receipt,target))
                throw new PlayerDrainProtectionChangedException();
            return new NpcDrainTickHpPermit(_attackCalcLock);
        }
        catch { System.Threading.Monitor.Exit(_attackCalcLock);throw; }
    }

    public void CheckShieldStatus(List<AttackResult> attackList, Effect effect, Creature attacker)
    {
        CheckShieldStatus(attackList, effect, attacker, null);
    }

    public void CheckShieldStatus(List<AttackResult> attackList, Effect effect, Creature attacker, ShieldType? shieldType)
    {
        if (shieldType == null && Aion.GameServer.Controllers.Attack.RetailNpc.SkyTempleNpcProtection.CheckOrdered(
            attackCalcObservers, attackList, effect, attacker)) return;
        if (attackCalcObservers.Length > 0)
        {
            foreach (AttackCalcObserver observer in attackCalcObservers)
            {
                if (shieldType == null || (observer is AttackShieldObserver shieldObserver && shieldObserver.GetShieldType() == shieldType))
                    observer.CheckShield(attackList, effect, attacker);
            }
        }
    }

    public void SetOneTimeBoostSkillAttack(OneTimeBoostSkillAttack boost) => oneTimeBoostSkillAttack = boost;

    public void RemoveOneTimeBoostSkillAttack(Effect effect)
    {
        OneTimeBoostSkillAttack? boost = oneTimeBoostSkillAttack;
        if (boost != null && ReferenceEquals(boost.GetEffect(), effect))
            oneTimeBoostSkillAttack = null;
    }

    public OneTimeBoostSkillAttack? GetOneTimeBoostSkillAttack(SkillType attackType)
    {
        OneTimeBoostSkillAttack? boost = oneTimeBoostSkillAttack;
        return boost != null && boost.Applies(attackType) ? boost : null;
    }

    public void Clear()
    {
        List<ActionObserver> removed;
        lock (observers)
        {
            removed = new List<ActionObserver>(observers);
            observers.Clear();
        }
        removed.ForEach(o => o.OnRemoved());
        lock (_attackCalcLock)
        {
            attackCalcObservers = Array.Empty<AttackCalcObserver>();
            attackCalcVersion=unchecked(attackCalcVersion+1);
        }
        oneTimeBoostSkillAttack = null;
    }
}
