using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Stats;
using Aion.GameServer.World.Geo;

namespace Aion.GameServer.Controllers.Attack;

/// <summary>
/// Java parity: controllers/attack/AggroList (ATracer, KKnD).
/// </summary>
public class AggroList
{
    protected readonly Creature Owner;
    private readonly ConcurrentDictionary<int, AggroInfo> _aggroList = new();
    private readonly object _hateReductionLock = new();
    private ScheduledTask? _hateReductionTask;

    public AggroList(Creature owner)
    {
        Owner = owner;
    }

    /// <summary>
    /// Only add damage from enemies. (Verify this includes summons, traps, pets, and excludes fall damage.)
    /// </summary>
    public void AddDamage(Creature attacker, int damage, bool notifyAttack, HopType? hopType)
    {
        if (!IsAware(attacker))
            return;
        // If the incoming damage is higher than the rest life it will decreased to the rest life
        if (damage >= Owner.GetLifeStats().GetCurrentHp())
        {
            damage = Owner.GetLifeStats().GetCurrentHp();
        }
        else if (_hateReductionTask == null)
        {
            StartHateReductionTask();
        }
        int hate = 0;
        if (notifyAttack && hopType == HopType.DAMAGE && damage > 0)
        {
            // damage caused by auto attacks and skills with HopType.DAMAGE is multiplied by 10 and added as hate on retail
            hate = StatFunctions.CalculateHate(attacker, damage * 10);
        }
        AddDamageAndHate(attacker, damage, hate);
    }

    /// <summary>
    /// Hate that is received without dealing damage
    /// </summary>
    public void AddHate(Creature creature, int hate)
    {
        if (ShouldAddHateToMaster(creature))
            creature = creature.GetMaster();
        if (!IsAware(creature))
            return;
        if (hate < 0 && !_aggroList.ContainsKey(creature.ObjectId))
            return;
        AddDamageAndHate(creature, 0, hate);
    }

    private void AddDamageAndHate(Creature creature, int damage, int hate)
    {
        AggroInfo ai = _aggroList.GetOrAdd(creature.ObjectId, _ => new AggroInfo(creature));
        bool isNewInAggroList = ai.GetHate() == 0;
        ai.AddDamage(damage);
        ai.AddHate(hate);
        Owner.GetController().OnAddHate(creature, isNewInAggroList);
    }

    private bool ShouldAddHateToMaster(Creature creature)
    {
        // ice sheet, threatening wave, etc. generate hate for their master. taunting spirit does not!
        return creature is SummonedObject summonedObject && !IsTauntingSpirit(summonedObject);
    }

    private bool IsTauntingSpirit(SummonedObject npc)
    {
        return npc.GetNpcId() switch
        {
            833403 or 833404 or 833478 or 833479 or 833480 or 833481 => true, // spawned by Summon Vexing Energy
            _ => false,
        };
    }

    /// <summary>
    /// @return player with most damage, if no other creatures like NPCs dealt more damage
    /// </summary>
    public Player? GetMostPlayerDamage()
    {
        // Use final damage list to get pet damage as well.
        DamageInfo<Creature>? mostDamage = GetFinalDamageList().GetMostDamage();
        return mostDamage != null && mostDamage.GetAttacker() is Player player ? player : null;
    }

    public void StopHating(VisibleObject creature)
    {
        if (_aggroList.TryGetValue(creature.ObjectId, out AggroInfo? aggroInfo))
            aggroInfo.SetHate(0);
    }

    /// <summary>
    /// Remove creature from aggro list and transfer its damages to the master
    /// </summary>
    public void Remove(Creature creature)
    {
        Remove(creature, true);
    }

    public void Remove(Creature creature, bool transferDamagesToMaster)
    {
        _aggroList.TryRemove(creature.ObjectId, out AggroInfo? aggroInfo);
        if (transferDamagesToMaster && aggroInfo != null)
            TransferDamagesToMaster(aggroInfo);
    }

    private void TransferDamagesToMaster(AggroInfo aggroInfo)
    {
        Creature master = aggroInfo.GetAttacker().GetMaster();
        if (master.Equals(aggroInfo.GetAttacker()) || !IsAware(master))
            return;
        _aggroList.AddOrUpdate(
            master.ObjectId,
            _ =>
            {
                var masterAggroInfo = new AggroInfo(master);
                masterAggroInfo.SetHate(1);
                masterAggroInfo.AddDamage(aggroInfo.GetDamage());
                return masterAggroInfo;
            },
            (_, masterAggroInfo) =>
            {
                masterAggroInfo.AddDamage(aggroInfo.GetDamage());
                return masterAggroInfo;
            });
    }

    public void Clear()
    {
        lock (_hateReductionLock)
        {
            if (_hateReductionTask != null)
            {
                _hateReductionTask.Cancel();
                _hateReductionTask = null;
            }
        }
        _aggroList.Clear();
    }

    public bool IsHating(Creature creature)
    {
        return _aggroList.TryGetValue(creature.ObjectId, out AggroInfo? aggroInfo) && aggroInfo.GetHate() > 0;
    }

    public int GetHate(Creature creature)
    {
        return _aggroList.TryGetValue(creature.ObjectId, out AggroInfo? aggroInfo) ? aggroInfo.GetHate() : 0;
    }

    public IEnumerable<AggroInfo> Stream()
    {
        return _aggroList.Values;
    }

    /// <summary>
    /// @return a living creature with no obstacle between it and the owner
    /// </summary>
    public Creature? GetTarget(AggroTarget targetType)
    {
        return GetTarget(targetType, int.MaxValue);
    }

    /// <summary>
    /// @return a living creature that is within the given range with no obstacle between it and the owner
    /// </summary>
    public Creature? GetTarget(AggroTarget targetType, float range)
    {
        IEnumerable<AggroInfo> stream = StreamValidTargetInfo(range);
        return targetType switch
        {
            AggroTarget.RANDOM => Rnd.Get(stream.Select(ai => ai.GetAttacker()).ToList()),
            AggroTarget.RANDOM_EXCEPT_CURRENT_TARGET => Rnd.Get(stream.Select(ai => ai.GetAttacker()).Where(c => !c.Equals(Owner.GetTarget())).ToList()),
            AggroTarget.MOST_HATED => stream.MaxBy(ai => ai.GetHate())?.GetAttacker(),
            AggroTarget.SECOND_MOST_HATED => stream.OrderByDescending(ai => ai.GetHate()).Take(2).LastOrDefault()?.GetAttacker(),
            AggroTarget.THIRD_MOST_HATED => stream.OrderByDescending(ai => ai.GetHate()).Take(3).LastOrDefault()?.GetAttacker(),
            AggroTarget.LOWEST_HP => stream.MinBy(ai => HpFraction(ai.GetAttacker()))?.GetAttacker(),
            AggroTarget.MOST_HP => stream.MaxBy(ai => HpFraction(ai.GetAttacker()))?.GetAttacker(),
            _ => null,
        };
    }

    /// <summary>Health as a fraction of maximum, which is how retail's HP indicators rank.</summary>
    /// <remarks>
    /// Absolute HP would make a boss reaching for the one most nearly dead pick whichever class has
    /// the smallest pool instead, every time, regardless of how hurt anyone is.
    /// </remarks>
    private static float HpFraction(Creature creature)
    {
        int max = creature.GetLifeStats().GetMaxHp();
        return max <= 0 ? 0f : creature.GetLifeStats().GetCurrentHp() / (float)max;
    }

    /// <summary>
    /// @return living creatures that are within the given range with no obstacle between them and the owner
    /// </summary>
    public IEnumerable<Creature> StreamValidTargets(float range)
    {
        return StreamValidTargetInfo(range).Select(ai => ai.GetAttacker());
    }

    private IEnumerable<AggroInfo> StreamValidTargetInfo(float range)
    {
        return Stream()
            .Where(ai => ai.GetHate() > 0 && !ai.GetAttacker().IsDead() && !ai.GetAttacker().GetLifeStats().IsAboutToDie()
                && Owner.GetKnownList().Sees(ai.GetAttacker())
                && (range == int.MaxValue || PositionUtil.IsInRange(Owner, ai.GetAttacker(), range, false))
                && GeoService.GetInstance().CanSee(Owner, ai.GetAttacker()));
    }

    /// <summary>
    /// Damages of summons and summoned objects are added to those of their masters.
    /// </summary>
    /// <returns>list of DamageInfo with npc and player damages</returns>
    public DamageList GetFinalDamageList()
    {
        return new DamageList(_aggroList.Values, Owner);
    }

    protected virtual bool IsAware(Creature creature)
    {
        return creature != null && Owner.GetKnownList().Knows(creature) && !Owner.GetEffectController().IsAbnormalSet(AbnormalState.SANCTUARY)
            && (_aggroList.ContainsKey(creature.ObjectId) || creature.IsEnemy(Owner) || DataManager.TRIBE_RELATIONS_DATA.IsHostileRelation(Owner.GetTribe(), creature.GetTribe()));
    }

    private void StartHateReductionTask()
    {
        lock (_hateReductionLock)
        {
            if (_hateReductionTask == null)
            {
                _hateReductionTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
                {
                    foreach (AggroInfo info in _aggroList.Values)
                    {
                        if (info.GetLastInteractionTime() != 0 && DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - info.GetLastInteractionTime() > 5000)
                        {
                            info.ReduceHate();
                        }
                    }
                    return ValueTask.CompletedTask;
                }, TimeSpan.FromMilliseconds(10000), TimeSpan.FromMilliseconds(10000)); // every 10 sec reduce hate of not attacking creatures
            }
        }
    }
}
