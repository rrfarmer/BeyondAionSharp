using System;
using System.Collections.Generic;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Controllers;

/// <summary>
/// Java parity: controllers/ObserveController (ATracer, Cura).
/// </summary>
public class ObserveController
{
    private readonly List<ActionObserver> observers = new();

    // Java CopyOnWriteArrayList → faithful copy-on-write via volatile array swap (lock-free reads).
    private readonly object _attackCalcLock = new();
    private volatile AttackCalcObserver[] attackCalcObservers = Array.Empty<AttackCalcObserver>();

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
                if (observer.GetObserverType().MatchesObserver(type))
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

    public void CheckShieldStatus(List<AttackResult> attackList, Effect effect, Creature attacker)
    {
        CheckShieldStatus(attackList, effect, attacker, null);
    }

    public void CheckShieldStatus(List<AttackResult> attackList, Effect effect, Creature attacker, ShieldType? shieldType)
    {
        if (attackCalcObservers.Length > 0)
        {
            foreach (AttackCalcObserver observer in attackCalcObservers)
            {
                if (shieldType == null || (observer is AttackShieldObserver shieldObserver && shieldObserver.GetShieldType() == shieldType))
                    observer.CheckShield(attackList, effect, attacker);
            }
        }
    }

    public float GetBasePhysicalDamageMultiplier(bool isSkill)
    {
        float multiplier = 1;
        if (attackCalcObservers.Length > 0)
        {
            foreach (AttackCalcObserver observer in attackCalcObservers)
            {
                multiplier *= observer.GetBasePhysicalDamageMultiplier(isSkill);
            }
        }
        return multiplier;
    }

    public float GetBaseMagicalDamageMultiplier()
    {
        float multiplier = 1;
        if (attackCalcObservers.Length > 0)
        {
            foreach (AttackCalcObserver observer in attackCalcObservers)
            {
                multiplier *= observer.GetBaseMagicalDamageMultiplier();
            }
        }
        return multiplier;
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
        }
    }
}
