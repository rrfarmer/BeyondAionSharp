using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.condition;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Stats;
using Aion.GameServer.World.Geo;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TYPE = Aion.GameServer.Network.Aion.ServerPackets.SmAttackStatus.TYPE;
using LOG = Aion.GameServer.Network.Aion.ServerPackets.SmAttackStatus.LOG;

namespace Aion.GameServer.Controllers;

/// <summary>
/// Controls Creatures [npcs, players etc]. Java parity: controllers/CreatureController&lt;T extends Creature&gt;
/// (@author -Nemesiss-, ATracer, Sarynth, Wakizashi). Non-generic base + generic shim (no C# wildcard generics).
/// </summary>
public abstract class CreatureController : VisibleObjectController
{
    private static readonly ILogger log = NullLogger.Instance;
    private volatile Aion.GameServer.Controllers.Observer.TerrainZoneCollisionMaterialActor actor;
    private readonly ConcurrentDictionary<int, ScheduledTask> tasks = new ConcurrentDictionary<int, ScheduledTask>();

    private static int JRound(float a) => (int)Math.Floor(a + 0.5f);

    protected Creature GetOwner() => (Creature)GetOwnerObject();

    public override void NotSee(VisibleObject obj, Aion.GameServer.Model.Animations.ObjectDeleteAnimation animation)
    {
        base.NotSee(obj, animation);
        if (obj.Equals(GetOwner().GetTarget()) && GetOwner().GetAi().GetSubState() != Aion.GameServer.Ai.AiSubState.TargetLost)
            GetOwner().SetTarget(null);
    }

    public override void NotKnow(VisibleObject obj)
    {
        base.NotKnow(obj);
        if (obj is Creature creature)
            GetOwner().GetAggroList().Remove(creature);
    }

    /// <summary>Removes owner from the visualObjects lists of all known objects who can't see him anymore.</summary>
    public virtual void OnHide()
    {
        GetOwner().GetKnownList().ForEachObject(other => other.GetKnownList().UpdateVisibleObject(GetOwner()));
    }

    /// <summary>Re-adds owner to the visualObjects lists of all known objects.</summary>
    public virtual void OnHideEnd()
    {
        GetOwner().GetKnownList().ForEachObject(other => other.GetKnownList().UpdateVisibleObject(GetOwner()));
    }

    /// <summary>Perform tasks on Creature starting to move.</summary>
    public virtual void OnStartMove()
    {
        GetOwner().GetMoveController().SetInMove(true);
        GetOwner().GetObserveController().NotifyMoveObservers();
        NotifyAIOnMove();
    }

    /// <summary>Perform tasks on Creature move in progress.</summary>
    public virtual void OnMove()
    {
        GetOwner().GetObserveController().NotifyMoveObservers();
        NotifyAIOnMove();
        UpdateZone();
    }

    /// <summary>Perform tasks on Creature stop move.</summary>
    public virtual void OnStopMove()
    {
        GetOwner().GetMoveController().SetInMove(false);
        GetOwner().GetObserveController().NotifyMoveObservers();
        NotifyAIOnMove();
    }

    /// <summary>Notify everyone in knownlist about move event.</summary>
    protected virtual void NotifyAIOnMove()
    {
        Aion.GameServer.Taskmanager.Tasks.MovementNotifyTask.GetInstance().Add(GetOwner());
    }

    /// <summary>Zone update mask management.</summary>
    public void UpdateZone()
    {
        Aion.GameServer.World.Zone.ZoneUpdateService.GetInstance().Add(GetOwner());
    }

    /// <summary>Called by ZoneManager when creature enters specific zone.</summary>
    public virtual void OnEnterZone(Aion.GameServer.World.Zone.ZoneInstance zoneInstance)
    {
    }

    /// <summary>Called by ZoneManager when player leaves specific zone.</summary>
    public virtual void OnLeaveZone(Aion.GameServer.World.Zone.ZoneInstance zoneInstance)
    {
    }

    /// <summary>Perform tasks on Creature death. Should ONLY be called from CreatureLifeStats to avoid duplicate death events.</summary>
    public virtual void OnDie(Creature lastAttacker)
    {
        GetOwner().GetMoveController().AbortMove();
        GetOwner().SetCasting(null);
        GetOwner().GetEffectController().RemoveAllEffects();
        if (GetOwner() is Player.Player && ((Player.Player)GetOwner()).GetIsFlyingBeforeDeath())
        {
            GetOwner().UnsetState(CreatureState.Active);
            GetOwner().SetState(CreatureState.FloatingCorpse);
        }
        else
            GetOwner().SetState(CreatureState.Dead);
        GetOwner().GetObserveController().NotifyDeathObservers(lastAttacker);
        PacketSendUtility.BroadcastPacketAndReceive(GetOwner(),
            new SM_EMOTION(GetOwner(), EmotionType.Die, 0, GetOwner().Equals(lastAttacker) ? 0 : lastAttacker.GetObjectId()));
        GetOwner().GetKnownList().ForEachObject(o =>
        {
            if (o is Creature creature)
                creature.GetAggroList().StopHating(GetOwner());
        });
    }

    /// <summary>Called when the creature gains or loses hate towards the attacker.</summary>
    public virtual void OnAddHate(Creature attacker, bool isNewInAggroList)
    {
        GetOwner().GetAi().OnCreatureEvent(Aion.GameServer.Ai.Event.AiEventType.Attack, attacker);
    }

    /// <summary>Perform tasks when Creature was attacked.</summary>
    public void OnAttack(Creature creature, int damage, AttackStatus attackStatus)
    {
        OnAttack(creature, null, TYPE.REGULAR, damage, true, LOG.REGULAR, attackStatus, HopType.DAMAGE);
    }

    public void OnAttack(Creature creature, int damage, AttackStatus attackStatus, Effect criticalEffect)
    {
        OnAttack(creature, null, TYPE.REGULAR, damage, true, LOG.REGULAR, attackStatus, HopType.DAMAGE, criticalEffect);
    }

    public void OnAttack(Effect effect, TYPE type, int damage, bool notifyAttack, LOG logId, HopType hopType)
    {
        OnAttack(effect.GetEffector(), effect, type, damage, notifyAttack, logId, effect.GetAttackStatus(), hopType, null);
    }

    public virtual void OnAttack(Creature attacker, Effect effect, TYPE type, int damage, bool notifyAttack, LOG logId, AttackStatus status, HopType hopType)
    {
        OnAttack(attacker, effect, type, damage, notifyAttack, logId, status, hopType, null);
    }

    /// <summary>Perform tasks when Creature was attacked.</summary>
    private void OnAttack(Creature attacker, Effect effect, TYPE type, int damage, bool notifyAttack, LOG logId, AttackStatus status, HopType hopType, Effect criticalEffect)
    {
        if (!GetOwner().IsSpawned())
            return;
        if (damage != 0 && notifyAttack)
        {
            Skill skill = GetOwner().GetCastingSkill();
            if (skill != null)
            {
                if (skill.GetSkillMethod() == Skill.SkillMethod.ITEM)
                {
                    CancelCurrentSkill(attacker);
                }
                else
                {
                    int cancelRate = skill.GetSkillTemplate().GetCancelRate();
                    if (cancelRate >= 99999)
                    {
                        CancelCurrentSkill(attacker);
                    }
                    else if (cancelRate > 0 && !(GetOwner() is Npc && ((Npc)GetOwner()).IsBoss()))
                    {
                        int conc = GetOwner().GetGameStats().GetStat(StatEnum.CONCENTRATION, 0).GetCurrent();
                        float maxHp = GetOwner().GetGameStats().GetMaxHp().GetCurrent();
                        int cancel = JRound(((7f * (damage / maxHp) * 100f) - conc / 2f) * (cancelRate / 100f));
                        if (Rnd.Chance() < cancel)
                            CancelCurrentSkill(attacker);
                    }
                }
            }
            GetOwner().GetObserveController().NotifyAttackedObservers(attacker, effect == null ? 0 : effect.GetSkillId());
        }

        GetOwner().GetAggroList().AddDamage(attacker, damage, notifyAttack, hopType);

        // notify all NPC's around that creature is attacking me
        GetOwner().GetKnownList().ForEachNpc(npc => npc.GetAi().OnCreatureEvent(Aion.GameServer.Ai.Event.AiEventType.CreatureNeedsSupport, GetOwner()));
        GetOwner().GetLifeStats().ReduceHp(type, damage, effect == null ? 0 : effect.GetSkillId(), logId, attacker);
        GetOwner().IncrementAttackedCount();

        if (!GetOwner().IsDead() && attacker is Player.Player player)
        {
            if (criticalEffect != null)
            {
                criticalEffect.ApplyEffect();
            }
            if ((effect == null || effect.TryActivateGodstone()) && status != AttackStatus.DODGE && status != AttackStatus.RESIST)
                CalculateGodStoneEffects(player);
        }
        if (effect != null && type == TYPE.DELAYDAMAGE)
            effect.BroadcastHate();
    }

    private void CalculateGodStoneEffects(Player.Player attacker)
    {
        ApplyGodStoneEffect(attacker, attacker.GetEquipment().GetMainHandWeapon(), true);
        ApplyGodStoneEffect(attacker, attacker.GetEquipment().GetOffHandWeapon(), false);
    }

    private void ApplyGodStoneEffect(Player.Player attacker, Item weapon, bool isMainHandWeapon)
    {
        if (weapon == null || !weapon.HasGodStone())
            return;
        Aion.GameServer.Model.Items.GodStone godStone = weapon.GetGodStone();
        if (!godStone.TryActivate(isMainHandWeapon, GetOwner()))
            return;

        Aion.GameServer.Model.Templates.Items.GodstoneInfo godstoneInfo = godStone.GetGodstoneInfo();
        Aion.GameServer.Model.Templates.Items.ItemTemplate template = DataManager.ITEM_DATA.GetItemTemplate(godStone.GetItemId());
        Skill skill = Aion.GameServer.SkillEngine.SkillEngine.GetInstance().GetSkill(attacker, godstoneInfo.GetSkillId(), godstoneInfo.GetSkillLevel(), GetOwner(), template);
        skill.SetFirstTargetRangeCheck(false);
        if (!skill.CanUseSkill(Aion.GameServer.SkillEngine.properties.Properties.CastState.CAST_START))
            return;
        Effect effect = new Effect(skill, GetOwner());
        effect.Initialize();
        effect.ApplyEffect();
        PacketSendUtility.SendPacket(attacker, SM_SYSTEM_MESSAGE.STR_SKILL_PROC_EFFECT_OCCURRED(skill.GetSkillTemplate().GetL10n()));
        // Illusion Godstones
        if (godstoneInfo.GetBreakProb() > 0)
        {
            godStone.IncreaseActivatedCount();
            if (godStone.GetActivatedCount() > godstoneInfo.GetNonBreakCount() && Rnd.Get(1, 1000) <= godstoneInfo.GetBreakProb())
            {
                weapon.SetGodStone(null);
                PacketSendUtility.SendPacket(attacker,
                    SM_SYSTEM_MESSAGE.STR_MSG_BREAK_PROC(weapon.GetL10n(), DataManager.ITEM_DATA.GetItemTemplate(godStone.GetItemId()).GetL10n()));
                Aion.GameServer.Services.Items.ItemPacketService.UpdateItemAfterInfoChange(attacker, weapon);
            }
        }
    }

    /// <summary>Perform reward operation.</summary>
    public virtual void DoReward()
    {
    }

    public virtual void OnDialogRequest(Player.Player player)
    {
    }

    public virtual void AttackTarget(Creature target, int time, bool skipChecks)
    {
        bool addAttackObservers = true;
        if (!skipChecks
            && (target == null || GetOwner().IsDead() || GetOwner().GetLifeStats().IsAboutToDie() || !GetOwner().CanAttack() || !GetOwner().IsSpawned()))
        {
            return;
        }

        // Calculate and apply damage
        Aion.GameServer.Model.Animations.AttackHandAnimation attackHandAnimation = Aion.GameServer.Model.Animations.AttackHandAnimation.MAIN_HAND;
        Aion.GameServer.Model.Animations.AttackTypeAnimation attackTypeAnimation = Aion.GameServer.Model.Animations.AttackTypeAnimation.MELEE;
        List<AttackResult> attackResult;

        CalculationType[] calculationTypes = new CalculationType[] { CalculationType.APPLY_POWER_SHARD_DAMAGE, CalculationType.REMOVE_POWER_SHARD };
        if (GetOwner() is Player.Player p && p.GetEquipment().IsDualWeaponEquipped())
            calculationTypes = ArrAdd(calculationTypes, CalculationType.DUAL_WIELD);
        if (GetOwner().GetAttackType() == Aion.GameServer.Model.Templates.Items.ItemAttackType.PHYSICAL)
            attackResult = AttackUtil.CalculatePhysAttackResult(GetOwner(), target, calculationTypes);
        else
        {
            attackResult = AttackUtil.CalculateMagAttackResult(GetOwner(), target, GetOwner().GetAttackType().GetMagicalElement(), calculationTypes);
            attackHandAnimation = Aion.GameServer.Model.Animations.AttackHandAnimation.OFF_HAND;
        }
        if (GetOwner() is Npc)
        {
            attackHandAnimation = GetOwner().GetAi().ModifyAttackHandAnimation(attackHandAnimation);
            attackTypeAnimation = GetOwner().GetAi().GetAttackTypeAnimation(target);
        }

        int damage = 0;
        foreach (AttackResult result in attackResult)
        {
            if (result.GetAttackStatus() == AttackStatus.RESIST || result.GetAttackStatus() == AttackStatus.DODGE)
                addAttackObservers = false;
            damage += result.GetDamage();
        }

        AttackStatus firstAttackStatus = AttackStatusExtensions.GetBaseStatus(attackResult[0].GetAttackStatus());
        Effect criticalEffect = null;
        if (GetOwner() is Player.Player player && firstAttackStatus == AttackStatus.CRITICAL && Rnd.Chance() < 10)
        {
            criticalEffect = Aion.GameServer.SkillEngine.SkillEngine.GetInstance().CreateCriticalEffect(player, target, 0);
            if (criticalEffect != null && (criticalEffect.GetEffectResult() == EffectResult.DODGE || criticalEffect.GetEffectResult() == EffectResult.RESIST))
                criticalEffect = null;
        }
        PacketSendUtility.BroadcastPacketAndReceive(GetOwner(),
            new SM_ATTACK(GetOwner(), target, GetOwner().GetGameStats().GetAttackCounter(), time, attackTypeAnimation, attackHandAnimation, attackResult, criticalEffect),
            Aion.GameServer.Ai.Event.AiEventType.CreatureNeedsHelp);

        GetOwner().GetGameStats().IncreaseAttackCounter();
        if (addAttackObservers)
        {
            GetOwner().GetObserveController().NotifyAttackObservers(target, 0);
        }

        if (time == 0)
            target.GetController().OnAttack(GetOwner(), damage, firstAttackStatus, criticalEffect);
        else
            ThreadPoolManager.GetInstance().Schedule(_ => { new DelayedOnAttack(target, GetOwner(), damage, firstAttackStatus, criticalEffect).Run(); return System.Threading.Tasks.ValueTask.CompletedTask; }, TimeSpan.FromMilliseconds(time));
    }

    /// <summary>Handle dialog select: GetOwner() is the target/dialog sender; the given player clicked the dialog.</summary>
    public virtual void OnDialogSelect(int dialogActionId, int prevDialogId, Player.Player player, int questId, int extendedRewardIndex)
    {
    }

    public bool HasTask(Aion.GameServer.Model.TaskId taskId)
    {
        return tasks.ContainsKey((int)taskId);
    }

    public bool HasScheduledTask(Aion.GameServer.Model.TaskId taskId)
    {
        return tasks.TryGetValue((int)taskId, out ScheduledTask task) && task != null && !task.Completion.IsCompleted;
    }

    public ScheduledTask GetAndRemoveTask(Aion.GameServer.Model.TaskId taskId)
    {
        tasks.TryRemove((int)taskId, out ScheduledTask task);
        return task;
    }

    public ScheduledTask CancelTask(Aion.GameServer.Model.TaskId taskId)
    {
        ScheduledTask task = GetAndRemoveTask(taskId);
        if (task != null)
        {
            task.Cancel();
        }
        return task;
    }

    public bool CancelTaskIfPresent(Aion.GameServer.Model.TaskId taskId, ScheduledTask task)
    {
        if (tasks.TryRemove(new KeyValuePair<int, ScheduledTask>((int)taskId, task)))
        {
            task.Cancel();
            return true;
        }
        return false;
    }

    /// <summary>If task already exists - it will be canceled.</summary>
    public void AddTask(Aion.GameServer.Model.TaskId taskId, ScheduledTask task)
    {
        tasks.AddOrUpdate((int)taskId, task, (k, oldTask) =>
        {
            if (oldTask != null)
            {
                oldTask.Cancel();
                if (taskId == Aion.GameServer.Model.TaskId.DESPAWN)
                {
                    log.LogWarning("Despawn task for " + GetOwner() + " was cancelled and replaced with another one, possibly delaying the intended despawn time.");
                }
            }
            return task;
        });
    }

    /// <summary>Cancel all tasks associated with this controller.</summary>
    public void CancelAllTasks()
    {
        foreach (KeyValuePair<int, ScheduledTask> e in tasks)
        {
            ScheduledTask task = e.Value;
            if (task != null)
                task.Cancel();
        }
        tasks.Clear();
    }

    public override void OnDelete()
    {
        CancelAllTasks();
        base.OnDelete();
    }

    /// <summary>Die by reducing HP to 0.</summary>
    public bool Die()
    {
        return Die(null, null, GetOwner());
    }

    public bool Die(Creature lastAttacker)
    {
        return Die(null, null, lastAttacker);
    }

    public bool Die(TYPE type, LOG log, Creature lastAttacker)
    {
        return GetOwner().GetLifeStats().ReduceHp(type, int.MaxValue, 0, log, lastAttacker) == 0;
    }

    /// <summary>Use skill with default level 1.</summary>
    public bool UseSkill(int skillId)
    {
        return UseSkill(skillId, 1);
    }

    /// <summary>Returns true if successful usage.</summary>
    public virtual bool UseSkill(int skillId, int skillLevel)
    {
        try
        {
            Creature creature = GetOwner();
            Skill skill = Aion.GameServer.SkillEngine.SkillEngine.GetInstance().GetSkill(creature, skillId, skillLevel, creature.GetTarget());
            if (skill != null)
            {
                return skill.UseSkill();
            }
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Exception during skill use: " + skillId);
        }
        return false;
    }

    public bool UseChargeSkill(Skill startSkill, long chargeTimeMillis)
    {
        SkillChargeCondition chargeCondition = startSkill.GetSkillTemplate().GetSkillChargeCondition();
        ChargeSkillEntry chargeSkill = chargeCondition == null ? null : DataManager.SKILL_CHARGE_DATA.GetChargedSkillEntry(chargeCondition.GetValue());
        if (chargeSkill == null || chargeTimeMillis < chargeSkill.GetMinTime() * startSkill.GetCastSpeedForAnimationBoostAndChargeSkills())
        {
            if (GetOwner() is Player.Player player)
                AuditLogger.Log(player, "tried to use charge skill " + startSkill.GetSkillId() + " after " + chargeTimeMillis);
            return false;
        }
        try
        {
            int index = 0, chargeTimeSum = 0;
            foreach (ChargedSkill skill in chargeSkill.GetSkills())
            {
                chargeTimeSum += (int)(skill.GetTime() * startSkill.GetCastSpeedForAnimationBoostAndChargeSkills());
                if (chargeTimeSum >= chargeTimeMillis || ++index == chargeSkill.GetSkills().Count - 1)
                    break;
            }
            int skillId = chargeSkill.GetSkills()[index].GetId();
            ChargeSkill skill2 = Aion.GameServer.SkillEngine.SkillEngine.GetInstance().GetChargeSkill(GetOwner(), skillId, startSkill.GetSkillLevel(), index + 1, startSkill);
            if (skill2 != null)
                return skill2.UseSkill();
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Could not use charge skill " + startSkill.GetSkillId() + " with charge time " + chargeTimeMillis);
        }
        finally
        {
            startSkill.CancelCast();
        }
        return false;
    }

    public Skill AbortCast()
    {
        Creature creature = GetOwner();
        Skill castingSkill = creature.GetCastingSkill();
        if (castingSkill != null)
        {
            castingSkill.CancelCast();
            creature.SetCasting(null);
        }
        if (creature is Npc npc)
        {
            creature.GetAi().SetSubStateIfNot(Aion.GameServer.Ai.AiSubState.None);
            npc.GetGameStats().SetLastSkill(null);
        }
        return castingSkill;
    }

    public virtual void CancelCurrentSkill(Creature lastAttacker)
    {
        CancelCurrentSkill(lastAttacker, null);
    }

    /// <summary>Cancel current skill and remove cooldown.</summary>
    public virtual void CancelCurrentSkill(Creature lastAttacker, SM_SYSTEM_MESSAGE msg)
    {
        Skill castingSkill = AbortCast();
        if (castingSkill == null)
            return;

        PacketSendUtility.BroadcastPacketAndReceive(GetOwner(), new SM_SKILL_CANCEL(GetOwner(), castingSkill.GetSkillTemplate().GetSkillId()));
        if (GetOwner().GetAi() is Aion.GameServer.Ai.NpcAI npcAI)
        {
            npcAI.OnGeneralEvent(Aion.GameServer.Ai.Event.AiEventType.AttackComplete);
        }
        if (lastAttacker is Player.Player)
        {
            PacketSendUtility.SendPacket((Player.Player)lastAttacker, SM_SYSTEM_MESSAGE.STR_SKILL_TARGET_SKILL_CANCELED());
        }
    }

    /// <summary>Cancel use Item.</summary>
    public virtual void CancelUseItem()
    {
    }

    public override void OnAfterSpawn()
    {
        base.OnAfterSpawn();
        GetOwner().RevalidateZones();
        if (actor == null && GetOwner().GetMoveController() != null && GeoService.GetInstance().WorldHasTerrainMaterials(GetOwner().GetWorldId()))
        {
            actor = new Aion.GameServer.Controllers.Observer.TerrainZoneCollisionMaterialActor(GetOwner());
            GetOwner().GetObserveController().AddObserver(actor);
        }
    }

    public override void OnDespawn()
    {
        base.OnDespawn();
        if (actor != null)
        {
            actor.Abort();
            GetOwner().GetObserveController().RemoveObserver(actor);
            actor = null;
        }
        CancelTask(Aion.GameServer.Model.TaskId.DECAY);
        GetOwner().GetMoveController().AbortMove();
        GetOwner().GetAggroList().Clear();
    }

    // Java parity: org.apache.commons.lang3.ArrayUtils.add
    private static CalculationType[] ArrAdd(CalculationType[] a, CalculationType v)
    {
        CalculationType[] r = new CalculationType[a.Length + 1];
        Array.Copy(a, r, a.Length);
        r[a.Length] = v;
        return r;
    }

    private sealed class DelayedOnAttack
    {
        private Creature target;
        private Creature creature;
        private int finalDamage;
        private AttackStatus attackStatus;
        private Effect criticalEffect;

        public DelayedOnAttack(Creature target, Creature creature, int finalDamage, AttackStatus attackStatus, Effect criticalEffect)
        {
            this.target = target;
            this.creature = creature;
            this.finalDamage = finalDamage;
            this.attackStatus = attackStatus;
            this.criticalEffect = criticalEffect;
        }

        public void Run()
        {
            target.GetController().OnAttack(creature, finalDamage, attackStatus, criticalEffect);
            target = null;
            creature = null;
            criticalEffect = null;
        }
    }
}

/// <summary>
/// Java parity: generic typing of <see cref="CreatureController"/> (Java <c>CreatureController&lt;T extends Creature&gt;</c>).
/// Non-generic base + this generic shim (no C# wildcard generics); re-exposes the typed owner surface.
/// </summary>
public abstract class CreatureController<T> : CreatureController where T : Creature
{
    public void SetOwner(T owner) => SetOwnerObject(owner);

    public new T GetOwner() => (T)GetOwnerObject();
}
