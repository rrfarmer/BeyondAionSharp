using Aion.GameServer.Configs.Main;
using Aion.GameServer.SkillEngine.Condition;
using System;
using System.Collections.Generic;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Action;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Properties;
using Aion.GameServer.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using CastState = Aion.GameServer.SkillEngine.Properties.Properties.CastState;
using Aion.GameServer.Utils.Audit;
using Aion.GameServer.Model.Skill;

namespace Aion.GameServer.SkillEngine.Model;

/// <summary>
/// Java parity: skillengine/model/Skill (@author ATracer, Wakizashi, Neon). Runtime skill cast driver.
/// </summary>
public class Skill
{
    private static readonly ILogger log = NullLogger.Instance;

    private readonly List<Creature> effectedList;
    private Creature firstTarget;
    protected readonly Creature effector;
    private readonly int skillLevel;
    protected SkillMethod skillMethod;
    protected readonly StartMovingListener moveListener;
    private readonly SkillTemplate skillTemplate;
    private bool firstTargetRangeCheck = true;
    private readonly ItemTemplate itemTemplate;
    private int itemObjectId = 0;
    private int targetType;
    private bool chainSuccess = true;
    private bool isCancelled = false;
    private bool blockedPenaltySkill = false;
    private float x;
    private float y;
    private float z;
    private sbyte h;
    private int boostSkillCost;
    /// <summary>Duration that depends on BOOST_CASTING_TIME</summary>
    private int baseCastDuration;
    private int castDuration;
    private int clientHitTime; // from CM_CASTSPELL
    private int hitTime; // time when effect is applied
    private float castSpeedForAnimationBoostAndChargeSkills; // cast speed can boost the animation time of the current skill and the hit time of the following skill
    private long castStartTime;
    private string chainCategory = null;
    private int chainUsageDuration = 0;
    private int hate;
    private volatile DeathObserver firstTargetDieObserver;

    public enum SkillMethod
    {
        CAST,
        ITEM,
        PASSIVE,
        PROVOKED,
        PENALTY
    }

    // Java parity helpers.
    private static int JRound(float a) => (int)Math.Floor(a + 0.5f);
    private static long CurrentTimeMillis() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    /// <summary>Each skill is a separate object upon invocation. Skill level populated from player SkillList.</summary>
    public Skill(SkillTemplate skillTemplate, Player effector, Creature firstTarget)
        : this(skillTemplate, effector, effector.GetSkillList().GetSkillLevel(skillTemplate.GetSkillId()), firstTarget, null)
    {
    }

    public Skill(SkillTemplate skillTemplate, Player effector, Creature firstTarget, int skillLevel)
        : this(skillTemplate, effector, skillLevel, firstTarget, null)
    {
    }

    public Skill(SkillTemplate skillTemplate, Creature effector, int skillLvl, Creature firstTarget, ItemTemplate itemTemplate)
    {
        this.effectedList = new List<Creature>();
        this.moveListener = new StartMovingListener();
        this.firstTarget = firstTarget;
        this.skillLevel = skillLvl;
        this.skillTemplate = skillTemplate;
        this.effector = effector;
        this.baseCastDuration = skillTemplate.GetDuration();
        this.castDuration = skillTemplate.GetDuration();
        this.itemTemplate = itemTemplate;
        InitializeSkillMethod();
    }

    protected virtual void InitializeSkillMethod()
    {
        if (itemTemplate != null)
            skillMethod = SkillMethod.ITEM;
        else if (skillTemplate.IsPassive())
            skillMethod = SkillMethod.PASSIVE;
        else if (skillTemplate.IsProvoked())
            skillMethod = SkillMethod.PROVOKED;
        else
            skillMethod = SkillMethod.CAST;
    }

    /// <summary>Check if the skill can be used.</summary>
    public bool CanUseSkill(CastState castState)
    {
        var properties = skillTemplate.GetProperties();
        if (properties != null && !properties.Validate(this, castState))
        {
            log.LogDebug("properties failed");
            return false;
        }

        if (!PreCastCheck())
            return false;

        // check for counter skill
        if (effector is Player player)
        {
            if (skillMethod == SkillMethod.CAST && chainCategory == null) // category gets set in preCastCheck()
                player.GetChainSkills().ResetChain();

            if (skillTemplate.GetCounterSkill() != null)
            {
                long time = player.GetLastCounterSkill(skillTemplate.GetCounterSkill());
                if ((time + 5000) < CurrentTimeMillis())
                {
                    log.LogDebug("chain skill failed, too late");
                    return false;
                }
            }

            if (skillMethod == SkillMethod.ITEM && baseCastDuration > 0 && player.GetMoveController().IsInMove())
            {
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_ITEM_CANCELED());
                return false;
            }
        }

        return ValidateEffectedList();
    }

    private bool ValidateEffectedList()
    {
        if (effector is Player player)
        {
            if (CanUseSkill(player))
                effectedList.RemoveAll(effected => !IsValidTarget(player, effected));
            else
                effectedList.Clear();
        }

        if (targetType == 0 && effectedList.Count == 0)
        { // target selected but no target will be hit
            if (GetTargetRangeAttribute() != TargetRangeAttribute.AREA)
            { // don't restrict AoE activation
                if (effector is Player player)
                    PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_SKILL_TARGET_IS_NOT_VALID());
                return false;
            }
        }

        return true;
    }

    private bool CanUseSkill(Player player)
    {
        if (player.IsUsingFlightTransporterOrWindstream())
            return false;
        if (!GetSkillTemplate().HasEvadeEffect() && player.GetEffectController().IsInAnyAbnormalState(AbnormalState.CANT_ATTACK_STATE))
            return false;
        if (player.GetStore() != null)
            return false;
        return true;
    }

    private bool IsValidTarget(Player player, Creature target)
    {
        if (target is Player targetPlayer)
        {
            if (targetPlayer.IsUsingFlightTransporterOrWindstream())
                return false;
            if (target.GetRace() != player.GetRace())
            {
                if (!target.IsEnemyFrom(player))
                    return false;
            }
            else if (targetPlayer.IsDueling(player) && GetSkillTemplate().GetProperties().GetTargetRelation() != TargetRelationAttribute.ENEMY)
            {
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_SKILL_TARGET_IS_NOT_VALID());
                return false;
            }
        }

        if (target.GetLifeStats().IsAboutToDie() && !IsNonTargetAOE())
            return false;

        if (target.IsDead() && !GetSkillTemplate().HasResurrectEffect() && !IsNonTargetAOE())
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_SKILL_TARGET_IS_NOT_VALID());
            return false;
        }

        // cant resurrect non players and non dead
        if (GetSkillTemplate().HasResurrectEffect() && (!(target is Player) || !target.IsDead()))
            return false;

        return true;
    }

    /// <summary>Skill entry point. Returns true if usage is successful.</summary>
    public virtual bool UseSkill()
    {
        return UseSkill(SecurityConfig.CHECK_ANIMATIONS, true);
    }

    public bool UseNoAnimationSkill()
    {
        return UseSkill(false, true);
    }

    public bool UseWithoutPropSkill()
    {
        return UseSkill(false, false);
    }

    private bool UseSkill(bool checkAnimation, bool checkproperties)
    {
        boostSkillCost = 0;
        effector.GetObserveController().NotifyBoostSkillCostObservers(this);

        if (checkproperties && !CanUseSkill(CastState.CAST_START))
            return false;

        UpdateCastDurationAndSpeed();
        UpdateHitTime(checkAnimation);

        // notify skill use observers
        if (skillMethod == SkillMethod.CAST || skillMethod == SkillMethod.ITEM)
            effector.GetObserveController().NotifyStartSkillCastObservers(this);

        // start casting
        effector.SetCasting(this);

        // send packets to start casting
        if (skillMethod == SkillMethod.CAST || skillMethod == SkillMethod.ITEM)
        {
            castStartTime = CurrentTimeMillis();
            StartCast();
            if (effector is Npc)
                effector.GetAi().SetSubStateIfNot(Aion.GameServer.Ai.AISubState.CAST);
        }

        effector.GetObserveController().Attach(moveListener);

        if (effector is Npc npc)
        {
            NpcSkillEntry currentNpcSkillEntry = npc.GetGameStats().GetLastSkill();
            if (currentNpcSkillEntry != null)
            {
                currentNpcSkillEntry.SetLastTimeUsed();
                npc.GetGameStats().SetNextSkillDelay(currentNpcSkillEntry.GetNextSkillTime());
            }
            else
            {
                npc.GetGameStats().SetNextSkillDelay(-1);
            }
        }
        effector.GetAi().OnStartUseSkill(skillTemplate, skillLevel);
        if (skillTemplate.IsCharge())
        {
            ThreadPoolManager.GetInstance().Schedule(_ => { CancelCurrentSkillCast(); return System.Threading.Tasks.ValueTask.CompletedTask; }, TimeSpan.FromMilliseconds(castDuration));
        }
        else if (castDuration > 0)
        {
            ThreadPoolManager.GetInstance().Schedule(_ => { EndCast(); return System.Threading.Tasks.ValueTask.CompletedTask; }, TimeSpan.FromMilliseconds(castDuration));
        }
        else
        {
            EndCast();
        }
        return true;
    }

    private void SetCooldowns()
    {
        int cooldown = effector.GetSkillCooldown(skillTemplate);
        if (cooldown != 0)
        {
            if (skillTemplate.GetCooldownDeltaLv() != 0)
                cooldown = cooldown + skillTemplate.GetCooldownDeltaLv() * skillLevel;
            effector.SetSkillCoolDown(skillTemplate.GetCooldownId(), cooldown * 100 + CurrentTimeMillis());
        }
    }

    public int GetCooldown()
    {
        int cooldown = effector.GetSkillCooldown(skillTemplate);
        if (cooldown != 0)
            if (skillTemplate.GetCooldownDeltaLv() != 0)
                cooldown = cooldown + skillTemplate.GetCooldownDeltaLv() * skillLevel;
        return cooldown;
    }

    protected void UpdateCastDurationAndSpeed()
    {
        if (effector is Npc npc)
        { // TODO: check if all skills should be effected
            castDuration = JRound(baseCastDuration * (npc.GetGameStats().GetCastSpeed() / 1000f));
            castSpeedForAnimationBoostAndChargeSkills = 1f;
        }
        else if (skillTemplate.IsCharge())
        {
            bool isChargeTimeFixed = UpdateChargeBaseCastDuration();
            castDuration = isChargeTimeFixed ? baseCastDuration : CalculateChargeCastDuration();
            castSpeedForAnimationBoostAndChargeSkills = (float)castDuration / baseCastDuration;
        }
        else
        {
            castDuration = CalculateCastDuration();
            castSpeedForAnimationBoostAndChargeSkills = 1 - effector.GetGameStats().GetStat(StatEnum.BOOST_CASTING_TIME, 1000).GetBonus() / 1000f;
        }
    }

    private bool UpdateChargeBaseCastDuration()
    {
        // cast/attack speed can affect charge time since 4.8
        bool isChargeTimeFixed = !IsCastDurationAffectedByCastSpeed(); // fear and sleep charge skills are excluded, just like with regular casts
        SkillChargeCondition chargeCondition = skillTemplate.GetSkillChargeCondition();
        if (chargeCondition != null)
        {
            int maxCastDuration = 0;
            ChargeSkillEntry skillCharge = DataManager.SKILL_CHARGE_DATA.GetChargedSkillEntry(chargeCondition.GetValue());
            foreach (ChargedSkill chargedSkill in skillCharge.GetSkills())
            {
                if (!isChargeTimeFixed && !DataManager.SKILL_DATA.GetSkillTemplate(chargedSkill.GetId()).IsCastDurationAffectedByCastSpeed())
                    isChargeTimeFixed = true;
                maxCastDuration += chargedSkill.GetTime();
            }
            baseCastDuration = maxCastDuration;
        }
        return isChargeTimeFixed;
    }

    private int CalculateChargeCastDuration()
    {
        bool isPhysicalClass = effector is Player player
            && (player.GetPlayerClass().IsPhysicalClass() || player.GetPlayerClass() == PlayerClass.RIDER || player.GetPlayerClass() == PlayerClass.GUNNER);
        int castDuration;
        if (isPhysicalClass) // TODO check if attack speed should also affect magical classes
            castDuration = (int)effector.GetGameStats().GetPositiveStat(StatEnum.ATTACK_SPEED, baseCastDuration);
        else
            castDuration = CalculateMagicalCastDuration();
        return Math.Max(castDuration, (int)(baseCastDuration * 0.25f));
    }

    private int CalculateCastDuration()
    {
        // ap & cash revival stones, or 2nd+ time of multicast-skill activation
        if (GetSkillId() == 10802 || GetMultiCastCount() > 0)
            return 0;
        if (skillTemplate.GetTypeValue() != SkillType.MAGICAL || !IsCastDurationAffectedByCastSpeed())
            return baseCastDuration;
        return CalculateMagicalCastDuration();
    }

    private int CalculateMagicalCastDuration()
    {
        int baseDurationCap = JRound(baseCastDuration * 0.25f);
        //casting time stats cap 75%
        int castDuration = Math.Max(effector.GetGameStats().GetPositiveReverseStat(StatEnum.BOOST_CASTING_TIME, baseCastDuration), baseDurationCap);
        int boostValue = effector.GetGameStats().GetPositiveReverseStat(StatEnum.BOOST_CASTING_TIME_SKILL, baseCastDuration);
        StatEnum? skillCastBoostStat = GetSkillCastBoostStat();
        if (skillCastBoostStat != null)
            boostValue = effector.GetGameStats().GetPositiveReverseStat(skillCastBoostStat.Value, boostValue);

        int buffDelta = baseCastDuration - boostValue;
        castDuration -= buffDelta;

        if (!IsSummonType(skillTemplate.GetSubType()))
        {
            castDuration = Math.Max(castDuration, baseDurationCap);
        }
        return Math.Max(castDuration, 0);
    }

    private StatEnum? GetSkillCastBoostStat()
    {
        return skillTemplate.GetSubType() switch
        {
            SkillSubType.SUMMON => StatEnum.BOOST_CASTING_TIME_SUMMON,
            SkillSubType.SUMMONHOMING => StatEnum.BOOST_CASTING_TIME_SUMMONHOMING,
            SkillSubType.SUMMONTRAP => StatEnum.BOOST_CASTING_TIME_TRAP,
            SkillSubType.HEAL => StatEnum.BOOST_CASTING_TIME_HEAL,
            SkillSubType.ATTACK => StatEnum.BOOST_CASTING_TIME_ATTACK,
            _ => (StatEnum?)null
        };
    }

    private bool IsSummonType(SkillSubType type)
    {
        return type == SkillSubType.SUMMON || type == SkillSubType.SUMMONHOMING || type == SkillSubType.SUMMONTRAP;
    }

    protected void UpdateHitTime(bool checkAnimation)
    {
        hitTime = clientHitTime;
        if (!checkAnimation || !(effector is Player player) || skillMethod != SkillMethod.CAST && skillMethod != SkillMethod.ITEM)
            return;

        float animationTimeUntilFirstHit = DataManager.MOTION_DATA.CalculateAnimationTimeUntilFirstHit(player, this);
        int toleranceMillis = 1;
        if (skillTemplate.GetAmmoSpeed() != 0)
        {
            float distance = (float)PositionUtil.GetDistance(player, firstTarget);
            if (player.GetMoveController().IsInMove() || firstTarget.GetMoveController().IsInMove()) // subtract the run distance until ammo is actually fired
                distance -= PositionUtil.CalculateMaxCoveredDistance(player, JRound(animationTimeUntilFirstHit));
            float distanceTolerance = GetDistanceTolerance(player, firstTarget);
            float ammoTime = Math.Max(0, distance / skillTemplate.GetAmmoSpeed() * 1000);
            toleranceMillis += Math.Max(0, (int)Math.Ceiling(distanceTolerance / skillTemplate.GetAmmoSpeed() * 1000));
            animationTimeUntilFirstHit += ammoTime;
        }

        int motionDelay = skillTemplate.GetMotion() == null ? 0 : skillTemplate.GetMotion().GetDelay();
        int serverHitTime = motionDelay + JRound(animationTimeUntilFirstHit);
        if (serverHitTime > clientHitTime)
        {
            hitTime = serverHitTime;
            if (IsSuspiciousClientHitTime(clientHitTime, serverHitTime, toleranceMillis, player))
            {
                List<string> uncertainties = CollectUncertaintyFactorsForHitTime(player, toleranceMillis);
                string uncertaintyFactors = uncertainties.Count == 0 ? "" : " Uncertainty factors: " + string.Join(", ", uncertainties);
                AuditLogger.Log(player,
                    string.Format("modified hit time for skill {0} (client < server: {1}/{2}).{3}", GetSkillId(), clientHitTime, serverHitTime, uncertaintyFactors));
            }
        }
    }

    private float GetDistanceTolerance(Player player, Creature target)
    {
        long nowMillis = CurrentTimeMillis();
        // even when not yet moving on server side, the player can just have started to move before casting (CM_MOVE is sent after CM_CASTSPELL)
        long maxMovementMillis = player.GetMoveController().IsInMove() ? 1000 : 200;
        long movementMillis = Math.Min(maxMovementMillis, nowMillis - player.GetMoveController().GetLastMoveUpdate());
        float distanceTolerance = PositionUtil.CalculateMaxCoveredDistance(player, movementMillis);
        if (target.GetMoveController().IsInMove())
            distanceTolerance += PositionUtil.CalculateMaxCoveredDistance(target, nowMillis - target.GetMoveController().GetLastMoveUpdate());
        return distanceTolerance;
    }

    private bool IsSuspiciousClientHitTime(int clientHitTime, int serverHitTime, int tolerance, Player player)
    {
        if (clientHitTime >= serverHitTime - tolerance)
            return false;
        if (clientHitTime == 0 && (itemTemplate != null || skillTemplate.GetMotion() != null && skillTemplate.GetMotion().IsInstantSkill()))
            return false; // effects apply immediately (damage too, though visually delayed)
        if (clientHitTime == 0 && player.IsInRobotMode() && (player.GetLastSkill().IsMultiCast() || DataManager.SKILL_CHARGE_DATA.IsChargeSkill(player.GetLastSkill())))
            return false; // AT sends no hitTime when casting a non-instant skill within the animation time of a previous multiCast or charge skill, like 2640
        return true;
    }

    private List<string> CollectUncertaintyFactorsForHitTime(Player player, int toleranceMillis)
    {
        List<string> uncertainties = new List<string>();
        if (AllowAnimationBoostByCastSpeed() && !player.IsHitTimeBoosted())
            uncertainties.Add("cast speed");
        if (skillTemplate.GetAmmoSpeed() != 0)
            uncertainties.Add("movement (calculated tolerance: " + toleranceMillis + " ms)");
        if (clientHitTime == 0 && player.IsInRobotMode()) // TODO remove once isSuspiciousClientHitTime() identifies all false positives
            uncertainties.Add("Aethertech being weird 🤷‍♂️ (previous skill: " + player.GetLastSkill().GetSkillId() + ")");
        return uncertainties;
    }

    private void StartPenaltySkill()
    {
        int penaltySkill = skillTemplate.GetPenaltySkillId();
        if (penaltySkill == 0)
            return;
        if (GetSkillTemplate().ShouldPenaltySkillSendMsg())
        {
            PenaltySkill penaltySkill1 = Aion.GameServer.SkillEngine.SkillEngine.GetInstance().GetPenaltySkill(effector, penaltySkill, 1);
            if (penaltySkill1 != null)
            {
                penaltySkill1.UseSkill();
            }
        }
        else
        {
            Aion.GameServer.SkillEngine.SkillEngine.GetInstance().ApplyEffectDirectly(penaltySkill, firstTarget, effector);
        }
    }

    /// <summary>Start casting of skill.</summary>
    private void StartCast()
    {
        int targetObjId = firstTarget != null ? firstTarget.GetObjectId() : 0;
        bool needsCast = itemTemplate != null && itemTemplate.IsCombatActivated();
        if (skillMethod == SkillMethod.CAST || needsCast)
        {
            switch (targetType)
            {
                case 0: // PlayerObjectId as Target
                    PacketSendUtility.BroadcastPacketAndReceive(effector,
                        new SM_CASTSPELL(effector, skillTemplate.GetSkillId(), skillLevel, targetType, targetObjId, castDuration,
                            castSpeedForAnimationBoostAndChargeSkills, AllowAnimationBoostByCastSpeed()));
                    if (effector is Npc)
                    {
                        Aion.GameServer.Ai.Handler.ShoutEventHandler.OnCast((Aion.GameServer.Ai.NpcAI)effector.GetAi(), firstTarget);
                    }
                    break;

                case 3: // Target not in sight?
                    PacketSendUtility.BroadcastPacketAndReceive(effector,
                        new SM_CASTSPELL(effector, skillTemplate.GetSkillId(), skillLevel, targetType, targetObjId, castDuration,
                            castSpeedForAnimationBoostAndChargeSkills, AllowAnimationBoostByCastSpeed()));
                    break;

                case 1: // XYZ as Target
                    PacketSendUtility.BroadcastPacketAndReceive(effector,
                        new SM_CASTSPELL(effector, skillTemplate.GetSkillId(), skillLevel, targetType, x, y, z, castDuration,
                            castSpeedForAnimationBoostAndChargeSkills, AllowAnimationBoostByCastSpeed()));
                    break;
            }
            if (effector is Player player)
                player.SetNextSkillUse(CurrentTimeMillis() + GSConfig.MIN_SKILL_CAST_INTERVAL_MILLIS);
        }
        else if (skillMethod == SkillMethod.ITEM && castDuration > 0)
        {
            PacketSendUtility.BroadcastPacketAndReceive(effector, new SM_ITEM_USAGE_ANIMATION(effector.GetObjectId(), firstTarget.GetObjectId(),
                itemObjectId, itemTemplate.GetTemplateId(), castDuration, 0, 0));
        }

        if (firstTarget != null && !firstTarget.Equals(effector) && !skillTemplate.HasResurrectEffect() && (castDuration > 0)
            && skillTemplate.GetProperties().GetFirstTarget() != FirstTargetAttribute.POINT
            && skillTemplate.GetProperties().GetFirstTarget() != FirstTargetAttribute.ME)
        {
            if ((effector is Npc && ((Npc)effector).IsBoss())
                || (skillTemplate.GetProperties().GetFirstTarget() == FirstTargetAttribute.TARGET && skillTemplate.GetProperties().GetEffectiveDist() > 0))
            {
                return;
            }
            firstTargetDieObserver = new DeathObserver(_ => GetEffector().GetController().CancelCurrentSkill(null, SM_SYSTEM_MESSAGE.STR_SKILL_TARGET_LOST()));
            firstTarget.GetObserveController().Attach(firstTargetDieObserver);
        }
    }

    public void CancelCast()
    {
        if (isCancelled)
            return;
        isCancelled = true;
        RemoveObservers();
    }

    private void CancelCurrentSkillCast()
    {
        if (!isCancelled && Equals(effector.GetCastingSkill()))
            effector.GetController().CancelCurrentSkill(null, null);
    }

    /// <summary>Apply effects and perform actions specified in skill template.</summary>
    protected void EndCast()
    {
        RemoveObservers();
        if (!effector.IsCasting() || isCancelled)
            return;
        // check if target is out of skill range or other requirements are not met (anymore)
        var properties = skillTemplate.GetProperties();
        if (properties != null && !properties.EndCastValidate(this) || !ValidateEffectedList() || !PreUsageCheck())
        {
            effector.GetController().CancelCurrentSkill(null); // calls effector.setCasting(null) and sends skill cancel packet
            return;
        }
        effector.SetCasting(null);

        // try removing item, if its not possible return to prevent exploits
        if (effector is Player && skillMethod == SkillMethod.ITEM)
        {
            Item item = ((Player)effector).GetInventory().GetItemByObjId(itemObjectId);
            if (item == null)
                return;
            if (item.GetActivationCount() > 1)
            {
                item.SetActivationCount(item.GetActivationCount() - 1);
            }
            else
            {
                if (!((Player)effector).GetInventory().DecreaseByObjectId(item.GetObjectId(), 1, Aion.GameServer.Services.Items.ItemPacketService.ItemUpdateType.DEC_ITEM_USE))
                    return;
            }
        }

        EndCondCheck();

        // Perform necessary actions (use mp,dp items etc)
        var skillActions = skillTemplate.GetActions();
        if (skillActions != null)
        {
            foreach (Aion.GameServer.SkillEngine.Action.Action action in skillActions.GetActions())
            {
                if (!action.Act(this))
                    return;
            }
        }

        // Create effects and precalculate result
        int dashStatus = 0;
        int resistCount = 0;
        bool blockedChain = false;
        bool blockedStance = false;
        List<Effect> effects = new List<Effect>();
        if (skillTemplate.GetEffects() != null)
        {
            foreach (Creature effected in effectedList)
            {
                Effect effect = new Effect(this, effected);
                effect.Initialize();
                if (effected is Player)
                {
                    if (effect.GetEffectResult() == EffectResult.CONFLICT)
                        blockedStance = true;
                }
                int worldId = effector.GetWorldId();
                int instanceId = effector.GetInstanceId();
                effect.SetWorldPosition(worldId, instanceId, x, y, z);
                effects.Add(effect);
                if (firstTarget == null || firstTarget.Equals(effected))
                    dashStatus = effect.GetDashStatus().GetId();
                if (effect.GetAttackStatus() == AttackStatus.RESIST || effect.GetAttackStatus() == AttackStatus.DODGE)
                {
                    resistCount++;
                }
            }

            if (resistCount == effectedList.Count)
            {
                blockedChain = true;
                blockedPenaltySkill = true;
            }

            // exception for point point skills(example Ice Sheet)
            if (effectedList.Count == 0)
            {
                if (this.IsPointPointSkill())
                {
                    Effect effect = new Effect(this, null);
                    effect.Initialize();
                    effect.SetWorldPosition(effector.GetWorldId(), effector.GetInstanceId(), x, y, z);
                    effects.Add(effect);
                }
            }
        }

        bool setCooldowns = true;
        if (effector is Player playerEffector)
        {
            if (skillTemplate.IsStance() && !blockedStance && skillMethod == SkillMethod.CAST)
                playerEffector.GetController().StartStance(skillTemplate.GetSkillId());
            if (GetMultiCastCount() > 0)
                setCooldowns = false;

            // Check Chain Skill Trigger Rate, only for chain skills and only for player
            if (chainCategory != null)
            {
                if (blockedChain)
                    chainSuccess = false;
                else
                    chainSuccess = Rnd.Chance() < skillTemplate.GetChainSkillProb() || CustomConfig.SKILL_CHAIN_DISABLE_TRIGGERRATE;

                if (chainSuccess)
                    playerEffector.GetChainSkills().UpdateChain(chainCategory, chainUsageDuration);
                else
                    playerEffector.GetChainSkills().ResetChain();
            }

            Aion.GameServer.QuestEngine.QuestEngine.GetInstance().OnUseSkill(new Aion.GameServer.QuestEngine.Model.QuestEnv(effector.GetTarget(), (Player)effector, 0), skillTemplate.GetSkillId());
        }

        if (setCooldowns)
            SetCooldowns();

        // Use penalty skill (now 100% success)
        if (!blockedPenaltySkill)
            StartPenaltySkill();

        if (IsInstantSkill())
            ApplyEffect(effects);
        else
            ThreadPoolManager.GetInstance().Schedule(_ => { ApplyEffect(effects); return System.Threading.Tasks.ValueTask.CompletedTask; }, TimeSpan.FromMilliseconds(hitTime));

        if (skillMethod == SkillMethod.PENALTY || skillMethod == SkillMethod.CAST || skillMethod == SkillMethod.ITEM)
        {
            bool sentCastSpellResultPacket = SendCastSpellEnd(dashStatus, effects);
            if (sentCastSpellResultPacket && skillMethod != SkillMethod.PENALTY && effector is Player player)
            {
                // animation times must be calculated after applyEffect of instant skills in order to honor speed buffs from this skill
                var animation = DataManager.MOTION_DATA.CalculateAnimationTimesAfterLastHit(player, this);
                long nowMillis = CurrentTimeMillis();
                if (animation != null && AllowAnimationBoostByCastSpeed())
                {
                    int latencyToleranceMillis = 50; // animation starts after client receives SM_CASTSPELL_RESULT, so add a few milliseconds
                    player.SetHitTimeBoost(nowMillis + animation.FullDurationMillis + latencyToleranceMillis, GetCastSpeedForAnimationBoostAndChargeSkills());
                }
                else
                {
                    player.SetHitTimeBoost(0, 0);
                }
                if (animation != null) // Math.Max because nextSkillUse set from startCast() must not be undercut
                    player.SetNextSkillUse(Math.Max(player.GetNextSkillUse(), nowMillis + animation.LastHitMillis));
            }
        }

        if (skillTemplate.IsDeityAvatar() && effector is Player player2)
        {
            Aion.GameServer.Services.Abyss.AbyssService.AnnounceAbyssSkillUsage(player2, skillTemplate.GetL10n());
        }

        effector.GetAi().OnEndUseSkill(skillTemplate, skillLevel);
        if (effector is Npc npc)
        {
            NpcSkillEntry lastSkill = npc.GetGameStats().GetLastSkill();
            if (lastSkill != null)
                lastSkill.FireOnEndCastEvents(npc);

            Aion.GameServer.Ai.Manager.SkillAttackManager.AfterUseSkill((Aion.GameServer.Ai.NpcAI)npc.GetAi());
        }

        if (skillMethod == SkillMethod.CAST)
        {
            effector.GetObserveController().NotifyEndSkillCastObservers(this);
        }
        effector.GetWorldMapInstance().GetInstanceHandler().OnEndCastSkill(this);
    }

    private void RemoveObservers()
    {
        if (firstTargetDieObserver != null)
            firstTarget.GetObserveController().RemoveObserver(firstTargetDieObserver);
        effector.GetObserveController().RemoveObserver(moveListener);
    }

    private void AddResistedEffectHateAndNotifyFriends(List<Effect> effects)
    {
        if (effects == null || effects.Count == 0)
        {
            return;
        }
        foreach (Effect effect in effects)
        {
            if (effect.GetTauntHate() >= 0 && (effect.GetAttackStatus() == AttackStatus.RESIST || effect.GetAttackStatus() == AttackStatus.DODGE))
            {
                effect.GetEffected().GetAggroList().AddHate(effector, 1);
                effect.GetEffected().GetKnownList()
                    .ForEachNpc(obj => obj.GetAi().OnCreatureEvent(Aion.GameServer.Ai.Event.AiEventType.CreatureNeedsSupport, effect.GetEffected()));
            }
        }
    }

    private void ApplyEffect(List<Effect> effects)
    {
        // Apply effects to effected objects
        foreach (Effect effect in effects)
            effect.ApplyEffect();

        AddResistedEffectHateAndNotifyFriends(effects);
    }

    private bool SendCastSpellEnd(int dashStatus, List<Effect> effects)
    {
        bool sentCastSpellPacket = false;
        if (itemTemplate != null && !itemTemplate.IsCombatActivated())
        {
            PacketSendUtility.BroadcastPacketAndReceive(effector,
                new SM_ITEM_USAGE_ANIMATION(effector.GetObjectId(), firstTarget.GetObjectId(), itemObjectId, itemTemplate.GetTemplateId(), 0, 1, 0));
        }
        else
        {
            Aion.GameServer.Ai.Event.AiEventType? et = skillTemplate.GetSubType() == SkillSubType.ATTACK ? Aion.GameServer.Ai.Event.AiEventType.CreatureNeedsHelp : (Aion.GameServer.Ai.Event.AiEventType?)null;
            switch (targetType)
            {
                case 0: // PlayerObjectId as Target
                case 3: // Target not in sight?
                    PacketSendUtility.BroadcastPacketAndReceive(effector, new SM_CASTSPELL_RESULT(this, effects, hitTime, chainSuccess, dashStatus), et);
                    sentCastSpellPacket = true;
                    break;
                case 1: // XYZ as Target
                    PacketSendUtility.BroadcastPacketAndReceive(effector,
                        new SM_CASTSPELL_RESULT(this, effects, hitTime, chainSuccess, dashStatus, targetType), et);
                    sentCastSpellPacket = true;
                    break;
            }
        }
        if (skillMethod == SkillMethod.ITEM && effector is Player player)
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_USE_ITEM(GetItemTemplate().GetL10n()));
        return sentCastSpellPacket;
    }

    /// <summary>Check all conditions before starting cast.</summary>
    private bool PreCastCheck()
    {
        var skillConditions = skillTemplate.GetStartconditions();
        return skillConditions == null || skillConditions.Validate(this);
    }

    /// <summary>Check all conditions before using skill.</summary>
    private bool PreUsageCheck()
    {
        var skillConditions = skillTemplate.GetUseconditions();
        return skillConditions == null || skillConditions.Validate(this);
    }

    /// <summary>Check all conditions after using skill.</summary>
    private bool EndCondCheck()
    {
        var skillConditions = skillTemplate.GetEndConditions();
        return skillConditions == null || skillConditions.Validate(this);
    }

    public void SetBoostSkillCost(int value)
    {
        boostSkillCost = value;
    }

    public int GetBoostSkillCost()
    {
        return boostSkillCost;
    }

    public List<Creature> GetEffectedList()
    {
        return effectedList;
    }

    public Creature GetEffector()
    {
        return effector;
    }

    public int GetSkillLevel()
    {
        return skillLevel;
    }

    public int GetSkillId()
    {
        return skillTemplate.GetSkillId();
    }

    public StartMovingListener GetMoveListener()
    {
        return moveListener;
    }

    public SkillTemplate GetSkillTemplate()
    {
        return skillTemplate;
    }

    public Creature GetFirstTarget()
    {
        return firstTarget;
    }

    public void SetFirstTarget(Creature firstTarget)
    {
        this.firstTarget = firstTarget;
    }

    public bool IsPassive()
    {
        return skillTemplate.GetActivationAttribute() == ActivationAttribute.PASSIVE;
    }

    public bool IsFirstTargetRangeCheck()
    {
        return firstTargetRangeCheck;
    }

    public FirstTargetAttribute? GetFirstTargetAttribute()
    {
        return skillTemplate.GetProperties() == null ? (FirstTargetAttribute?)null : skillTemplate.GetProperties().GetFirstTarget();
    }

    public TargetRangeAttribute? GetTargetRangeAttribute()
    {
        return skillTemplate.GetProperties() == null ? (TargetRangeAttribute?)null : skillTemplate.GetProperties().GetTargetType();
    }

    /// <summary>True if the present skill is a non-targeted, non-point AOE skill.</summary>
    public bool IsNonTargetAOE()
    {
        return GetFirstTargetAttribute() == FirstTargetAttribute.ME && GetTargetRangeAttribute() == TargetRangeAttribute.AREA;
    }

    /// <summary>True if the present skill is a targeted AOE skill.</summary>
    private bool IsTargetAOE()
    {
        return GetFirstTargetAttribute() == FirstTargetAttribute.TARGET && GetTargetRangeAttribute() == TargetRangeAttribute.AREA;
    }

    /// <summary>True if the present skill is a self buff includes items (such as scroll buffs).</summary>
    public bool IsSelfBuff()
    {
        return GetFirstTargetAttribute() == FirstTargetAttribute.ME && GetTargetRangeAttribute() == TargetRangeAttribute.ONLYONE
            && skillTemplate.GetSubType() == SkillSubType.BUFF && !skillTemplate.IsDeityAvatar();
    }

    /// <summary>True if the present skill has self as first target.</summary>
    public bool IsFirstTargetSelf()
    {
        return GetFirstTargetAttribute() == FirstTargetAttribute.ME;
    }

    /// <summary>True if the present skill is a Point skill.</summary>
    public bool IsPointSkill()
    {
        return GetFirstTargetAttribute() == FirstTargetAttribute.POINT;
    }

    public void SetFirstTargetRangeCheck(bool firstTargetRangeCheck)
    {
        this.firstTargetRangeCheck = firstTargetRangeCheck;
    }

    public ItemTemplate GetItemTemplate()
    {
        return itemTemplate;
    }

    public void SetItemObjectId(int id)
    {
        this.itemObjectId = id;
    }

    public int GetItemObjectId()
    {
        return itemObjectId;
    }

    public void SetTargetType(int targetType, float x, float y, float z)
    {
        this.targetType = targetType;
        this.x = x;
        this.y = y;
        this.z = z;
    }

    /// <summary>Calculated position after skill.</summary>
    public void SetTargetPosition(float x, float y, float z, sbyte h)
    {
        this.x = x;
        this.y = y;
        this.z = z;
        this.h = h;
    }

    public float GetX()
    {
        return x;
    }

    public float GetY()
    {
        return y;
    }

    public float GetZ()
    {
        return z;
    }

    public sbyte GetH()
    {
        return h;
    }

    protected void SetCastStartTime(long castStartTime)
    {
        this.castStartTime = castStartTime;
    }

    public void SetClientHitTime(int time)
    {
        this.clientHitTime = time;
    }

    public int GetHitTime()
    {
        return hitTime;
    }

    protected void SetCastSpeedForAnimationBoostAndChargeSkills(float castSpeedForAnimationBoostAndChargeSkills)
    {
        this.castSpeedForAnimationBoostAndChargeSkills = castSpeedForAnimationBoostAndChargeSkills;
    }

    public float GetCastSpeedForAnimationBoostAndChargeSkills()
    {
        return castSpeedForAnimationBoostAndChargeSkills;
    }

    /// <summary>The game client allows to boost the animation time of a skill via cast speed.</summary>
    public bool AllowAnimationBoostByCastSpeed()
    {
        return IsMagical();
    }

    private bool IsCastDurationAffectedByCastSpeed()
    {
        return skillMethod == SkillMethod.CAST && skillTemplate.IsCastDurationAffectedByCastSpeed();
    }

    public void SetChainCategory(string chainCategory)
    {
        this.chainCategory = chainCategory;
    }

    public void SetChainUsageDuration(int duration)
    {
        this.chainUsageDuration = duration;
    }

    public SkillMethod GetSkillMethod()
    {
        return this.skillMethod;
    }

    private bool IsPointPointSkill()
    {
        return this.GetSkillTemplate().GetProperties().GetFirstTarget() == FirstTargetAttribute.POINT
            && this.GetSkillTemplate().GetProperties().GetTargetType() == TargetRangeAttribute.POINT;
    }

    public int GetMultiCastCount()
    {
        return skillTemplate.IsMultiCast() && effector is Player p ? p.GetChainSkills().GetCurrentChainCount(chainCategory) : 0;
    }

    public long GetCastStartTime()
    {
        return castStartTime;
    }

    public bool IsInstantSkill()
    {
        return hitTime == 0 || skillTemplate.GetMotion() != null && skillTemplate.GetMotion().IsInstantSkill();
    }

    public int GetHate()
    {
        return hate;
    }

    public void SetHate(int hate)
    {
        this.hate = hate;
    }

    private bool IsMagical()
    {
        return skillTemplate.GetTypeValue() == SkillType.MAGICAL && skillTemplate.GetSubType() != SkillSubType.NONE;
    }
}
