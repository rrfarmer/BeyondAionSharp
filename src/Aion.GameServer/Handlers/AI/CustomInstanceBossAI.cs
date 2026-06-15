using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Ai;
using Aion.GameServer.Custom.Instance;
using Aion.GameServer.Custom.Instance.Neuralnetwork;
using Aion.GameServer.Dataholders.LoadingUtils.Adapters;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Model.Templates.Items.Enums;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine;
using Aion.GameServer.SkillEngine.Condition;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.SkillEngine.Properties;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Stats;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// @author Jo
/// </summary>
[AIName("custom_instance_boss")]
public class CustomInstanceBossAI : GeneralNpcAI
{
    private static readonly ILogger log = NullLogger.Instance;
    private PlayerModel model;
    private ScheduledTask skillTask, castTimeout;
    private int previousSkill, rank;

    private List<int> skillSet;
    private bool onlyAttack;

    public CustomInstanceBossAI(Npc owner) : base(owner)
    {
    }

    public override float ModifyDamage(Creature attacker, float damage, Effect effect)
    {
        return damage * 0.42f; // pseudo PvP reduce
    }

    public override float ModifyOwnerDamage(float damage, Creature effected, Effect effect)
    {
        return damage * 0.58f; // pseudo PvP reduce
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        previousSkill = -1;

        WorldMapInstance wmi = GetPosition().GetWorldMapInstance();
        if (!(wmi.GetInstanceHandler() is RoahCustomInstanceHandler))
            return;

        int playerId = wmi.GetRegisteredObjects().First();
        rank = CustomInstanceService.GetInstance().LoadOrCreateRank(playerId).GetRank();

        Player p = World.World.GetInstance().GetPlayer(playerId);
        if (p == null)
        {
            log.LogError(new Exception(), "[CI_ROAH] No player object found for player id: " + playerId
                + ". Either the player is offline or the central artifact was destroyed by something else.");
            return;
        }

        if (p.GetPlayerClass() == PlayerClass.RIDER)
        {
            onlyAttack = true;
        }
        else
        {
            onlyAttack = false;
            AdaptAppearance(p);
            AdaptStats(p);
            GetOwner().SetSeeState(CreatureSeeState.Search2);
            // model player behavior
            skillSet = ((RoahCustomInstanceHandler)wmi.GetInstanceHandler()).GetSkillSet();
            model = ((RoahCustomInstanceHandler)wmi.GetInstanceHandler()).GetPlayerModel();
        }
    }

    protected override void HandleBackHome()
    {
        base.HandleBackHome();

        // prevent reset-abusing
        if (GetOwner().GetSkillCoolDowns() != null)
            GetOwner().GetSkillCoolDowns().Clear();
        GetLifeStats().SetCurrentHpPercent(100);
    }

    public override void HandleCreatureDetected(Creature creature)
    {
        base.HandleCreatureDetected(creature);
        WorldMapInstance wmi = GetPosition().GetWorldMapInstance();
        if (!(wmi.GetInstanceHandler() is RoahCustomInstanceHandler))
            return;

        if (PositionUtil.GetDistance(GetPosition().GetX(), GetPosition().GetY(), GetPosition().GetZ(), creature.GetX(), creature.GetY(),
            creature.GetZ()) <= 45 && GetPosition().GetWorldMapInstance().IsRegistered(creature.GetObjectId()))
        {
            GetAggroList().AddHate(creature, 100); // early aggro

            if (skillTask == null && !onlyAttack)
                skillTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ => { CheckSkillRotation(); return ValueTask.CompletedTask; }, TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(200));
        }
    }

    private void CheckSkillRotation()
    {
        if (!GetOwner().IsCasting())
        {
            int skillID = GetPlayerSkill();

            if (skillID != -1 && castTimeout == null)
            {
                Skill skill = SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), skillID, 1, GetTarget());
                skill.UseSkill();

                // workaround for slide-cast bug:
                GetEffectController().SetAbnormal(AbnormalState.SANCTUARY);
                long castDuration = (long)(skill.GetSkillTemplate().GetDuration() + (0.3f * GetOwner().GetGameStats().GetAttackSpeed().GetCurrent()));
                if (skill.GetSkillTemplate().GetCooldown() == 0) // item skills
                    castDuration = 0;

                castTimeout = ThreadPoolManager.GetInstance().Schedule(() =>
                {
                    GetEffectController().UnsetAbnormal(AbnormalState.SANCTUARY);
                    if (GetOwner().GetCastingSkill() != null)
                        GetOwner().GetCastingSkill().CancelCast();

                    // little break after timeout (for walking)
                    castTimeout = ThreadPoolManager.GetInstance().Schedule(() => castTimeout = null, 1000L);
                }, castDuration); // after cast / instant skill: 300ms * attack speed
            }
        }
    }

    private int GetPlayerSkill()
    {
        if (GetTarget() == null || !(GetTarget() is Creature))
            return -1;

        // remove shock
        if ((GetEffectController().IsInAnyAbnormalState(AbnormalState.ANY_STUN) || GetEffectController().IsAbnormalSet(AbnormalState.OPENAERIAL))
            && GetOwner().GetSkillCoolDown(1968) <= DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
            return 283;

        if (GetEffectController().IsInAnyAbnormalState(AbnormalState.CANT_ATTACK_STATE))
            return -1;

        // compute skill selection:
        if (model != null)
        {
            // assess game state:
            double[] inputArray = new PlayerModelEntry(GetOwner(), -1, (Creature)GetTarget()).ToStateInputArray(skillSet, previousSkill);

            List<double> output = model.GetOutputEstimation(inputArray);
            for (int i = 0; i < output.Count; i++)
            {
                Skill skillI = SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), skillSet[i], 1, GetTarget());
                if (skillI == null)
                {
                    log.LogWarning("Detected a skill input with not existent template [skillId=" + skillSet[i] + "].");
                    output[i] = -1d;
                    continue;
                }

                int cdID = -1; // item skills that have no cdID
                bool isDPskill = false;
                if (skillI.GetSkillTemplate() != null)
                {
                    cdID = skillI.GetSkillTemplate().GetCooldownId();
                    if (skillI.GetSkillTemplate().GetStartconditions() != null)
                    {
                        foreach (Condition c in skillI.GetSkillTemplate().GetStartconditions().GetConditions())
                        {
                            if (c is DpCondition)
                            {
                                isDPskill = true;
                                break;
                            }
                        }
                    }
                }

                // rule out:
                if (isDPskill || !skillI.CanUseSkill(Properties.CastState.CAST_START)
                    || (skillI.GetSkillTemplate().GetType_() == SkillType.MAGICAL && GetEffectController().IsAbnormalSet(AbnormalState.SILENCE))
                    || (skillI.GetSkillTemplate().GetType_() == SkillType.PHYSICAL && GetEffectController().IsAbnormalSet(AbnormalState.BIND))
                    || skillI.GetSkillTemplate().IsCharge() || skillI.IsPointSkill()
                    || (cdID != -1 && GetOwner().GetSkillCoolDown(cdID) > DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()))
                    output[i] = -1d; // -1 = minimum probability
            }

            int skillIndex = PlayerModelController.GetMaxIndex(output);
            if (output[skillIndex] == -1) // if no CDs available: auto-attack
                return -1;

            return skillSet[skillIndex];
        }
        return -1;
    }

    public override void OnEndUseSkill(SkillTemplate skillTemplate, int skillLevel)
    {
        base.OnEndUseSkill(skillTemplate, skillLevel);
        previousSkill = skillTemplate.GetSkillId();

        if (castTimeout != null)
        {
            castTimeout.Cancel(true);
            castTimeout = null;
        }

        if (skillTemplate.GetCooldown() == 0) // item skills: prevent spamming
            GetOwner().SetSkillCoolDown(skillTemplate.GetCooldownId(), DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 60000);

        GetEffectController().UnsetAbnormal(AbnormalState.SANCTUARY);
    }

    private void AdaptAppearance(Player player)
    {
        List<ItemTemplate> equipmentList = new List<ItemTemplate>();
        if (player.GetEquipment().GetMainHandWeapon() != null) // weapons manually to exclude swap weapon slots
            equipmentList.Add(player.GetEquipment().GetMainHandWeapon().GetItemSkinTemplate());
        if (player.GetEquipment().GetOffHandWeapon() != null)
            equipmentList.Add(player.GetEquipment().GetOffHandWeapon().GetItemSkinTemplate());
        foreach (Item i in player.GetEquipment().GetEquippedItems())
            if (i.GetItemTemplate().GetEquipmentType() == EquipType.ARMOR && i.GetItemTemplate().GetItemSubType() != ItemSubType.SHIELD)
                equipmentList.Add(i.GetItemSkinTemplate());

        NpcEquipmentList v = new NpcEquipmentList();
        v.items = equipmentList.ToArray();
        GetOwner().OverrideEquipmentList(v);
    }

    private void AdaptStats(Player player)
    {
        PlayerGameStats pgs = player.GetGameStats();
        List<StatSetFunction> functions = new List<StatSetFunction>();
        functions.Add(new StatSetFunction(StatEnum.EARTH_RESISTANCE, pgs.GetElementalDefenseFor(SkillElement.EARTH)));
        functions.Add(new StatSetFunction(StatEnum.FIRE_RESISTANCE, pgs.GetElementalDefenseFor(SkillElement.FIRE)));
        functions.Add(new StatSetFunction(StatEnum.WATER_RESISTANCE, pgs.GetElementalDefenseFor(SkillElement.WATER)));
        functions.Add(new StatSetFunction(StatEnum.WIND_RESISTANCE, pgs.GetElementalDefenseFor(SkillElement.WIND)));
        functions.Add(new StatSetFunction(StatEnum.ABNORMAL_RESISTANCE_ALL, pgs.GetAbnormalResistance().GetCurrent()));
        functions.Add(new StatSetFunction(StatEnum.ACCURACY, pgs.GetAccuracy().GetCurrent()));
        functions.Add(new StatSetFunction(StatEnum.AGILITY, pgs.GetAgility().GetCurrent()));
        functions.Add(new StatSetFunction(StatEnum.ATTACK_RANGE, 2500));
        functions.Add(new StatSetFunction(StatEnum.BLOCK, pgs.GetBlock().GetCurrent()));
        functions.Add(new StatSetFunction(StatEnum.EVASION, pgs.GetEvasion().GetCurrent()));
        functions.Add(new StatSetFunction(StatEnum.HEALTH, pgs.GetHealth().GetCurrent()));
        functions.Add(new StatSetFunction(StatEnum.KNOWLEDGE, pgs.GetKnowledge().GetCurrent()));
        functions.Add(new StatSetFunction(StatEnum.MAGIC_SKILL_BOOST_RESIST, pgs.GetMBResist().GetCurrent()));
        functions.Add(new StatSetFunction(StatEnum.MAGICAL_RESIST, pgs.GetMResist().GetCurrent()));
        functions.Add(new StatSetFunction(StatEnum.MAGICAL_ACCURACY, pgs.GetMAccuracy().GetCurrent()));
        functions.Add(new StatSetFunction(StatEnum.MAGICAL_CRITICAL, pgs.GetMCritical().GetCurrent()));
        functions.Add(new StatSetFunction(StatEnum.MAIN_HAND_POWER, pgs.GetMainHandPAttack(CalculationType.DISPLAY).GetCurrent()));
        int maxHP = pgs.GetMaxHp().GetCurrent();
        maxHP += (int)(maxHP * rank / 10f);
        if (onlyAttack)
            maxHP *= 10;
        functions.Add(new StatSetFunction(StatEnum.MAXHP, maxHP));
        functions.Add(new StatSetFunction(StatEnum.MAXMP, pgs.GetMaxMp().GetCurrent()));
        functions.Add(new StatSetFunction(StatEnum.OFF_HAND_ACCURACY, pgs.GetOffHandPAccuracy().GetCurrent()));
        functions.Add(new StatSetFunction(StatEnum.OFF_HAND_ATTACK_SPEED, pgs.GetAttackSpeed().GetCurrent()));
        functions.Add(new StatSetFunction(StatEnum.OFF_HAND_CRITICAL, pgs.GetOffHandPCritical().GetCurrent()));
        functions.Add(new StatSetFunction(StatEnum.OFF_HAND_POWER, pgs.GetOffHandPAttack(CalculationType.DISPLAY).GetCurrent()));
        functions.Add(new StatSetFunction(StatEnum.PARRY, pgs.GetParry().GetCurrent()));
        functions.Add(new StatSetFunction(StatEnum.PHYSICAL_ACCURACY, pgs.GetMainHandPAccuracy().GetCurrent()));
        functions.Add(new StatSetFunction(StatEnum.PHYSICAL_DEFENSE, pgs.GetPDef().GetCurrent()));
        functions.Add(new StatSetFunction(StatEnum.PHYSICAL_CRITICAL_RESIST, pgs.GetPCR().GetCurrent()));
        functions.Add(new StatSetFunction(StatEnum.POWER, pgs.GetPower().GetCurrent()));
        functions.Add(new StatSetFunction(StatEnum.SPEED, pgs.GetMovementSpeed().GetCurrent()));
        functions.Add(new StatSetFunction(StatEnum.WILL, pgs.GetWill().GetCurrent()));
        functions.Add(new StatSetFunction(StatEnum.ATTACK_SPEED, pgs.GetAttackSpeed().GetCurrent()));
        // Work-around for not considered dual wield stats for NPCs
        int pAtk = pgs.GetMainHandPAttack().GetCurrent();
        if (player.GetEquipment().GetOffHandWeapon() != null)
            pAtk += pgs.GetOffHandPAttack(CalculationType.DISPLAY).GetCurrent() / 2;
        functions.Add(new StatSetFunction(StatEnum.PHYSICAL_ATTACK, pAtk));

        if (player.GetPlayerClass().IsPhysicalClass())
        {
            functions.Add(new StatSetFunction(StatEnum.PHYSICAL_CRITICAL, pgs.GetMainHandPCritical().GetCurrent()));
        }
        else
        {
            functions.Add(new StatSetFunction(StatEnum.MAGICAL_CRITICAL, pgs.GetMCritical().GetCurrent()));
            functions.Add(new StatSetFunction(StatEnum.MAGICAL_ATTACK, pgs.GetMainHandMAttack().GetCurrent()));
            functions.Add(new StatSetFunction(StatEnum.BOOST_MAGICAL_SKILL, pgs.GetMBoost().GetCurrent()));
        }

        GetOwner().GetGameStats().AddEffect(null, functions);
        GetLifeStats().SetCurrentHp(GetLifeStats().GetMaxHp());
    }

    private void CancelTasks()
    {
        if (skillTask != null && !skillTask.IsCancelled)
            skillTask.Cancel(true);
        if (castTimeout != null && !castTimeout.IsCancelled)
            castTimeout.Cancel(true);
    }

    protected override void HandleDied()
    {
        CancelTasks();
        if (model == null)
            if (onlyAttack)
                PacketSendUtility.BroadcastToMap(GetOwner(), new SM_MESSAGE(GetOwner(), "Remarkable... ", ChatType.BRIGHT_YELLOW_CENTER));
            else
                PacketSendUtility.BroadcastToMap(GetOwner(),
                    new SM_MESSAGE(GetOwner(), "Remarkable ... I will ... remember you.", ChatType.BRIGHT_YELLOW_CENTER));
        else
            PacketSendUtility.BroadcastToMap(GetOwner(), new SM_MESSAGE(GetOwner(), "One day ... I will ... suppress you!", ChatType.BRIGHT_YELLOW_CENTER));
        base.HandleDied();
    }

    protected override void HandleDespawned()
    {
        CancelTasks();
        base.HandleDespawned();
    }
}
