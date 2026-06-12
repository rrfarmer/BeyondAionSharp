using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Model.Templates.Items.Enums;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.SkillEngine.Properties;
using ForceType = Aion.GameServer.SkillEngine.Model.Effect.ForceType;

namespace Aion.GameServer.SkillEngine;

/// <summary>
/// Java parity: skillengine/SkillEngine (ATracer). Singleton factory for Skill/ChargeSkill/PenaltySkill + direct effect application.
/// Effect.ForceType nested value-class -> ForceType alias; switch-arrow on ItemGroup -> shared case labels; instanceof+cast -> is
/// Creature; getType() -> GetSkillType() (avoid Object.GetType); varargs hasAnyEffect -> params; Integer duration -> int?. EffectType/
/// ItemGroup/SkillType/ActivationAttribute SCREAMING. Skill/Effect/Properties/DataManager red-tolerated.
/// </summary>
public class SkillEngine
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(SkillEngine));
    private static readonly SkillEngine skillEngine = new SkillEngine();

    /// <summary>should not be instantiated directly</summary>
    private SkillEngine()
    {
    }

    /// <summary>This method is used for skills that were learned by player</summary>
    public Skill GetSkillFor(Player player, int skillId, VisibleObject firstTarget)
    {
        SkillTemplate template = DataManager.SKILL_DATA.GetSkillTemplate(skillId);

        if (template == null)
            return null;

        return GetSkillFor(player, template, firstTarget);
    }

    /// <summary>This method is used for skills that were learned by player</summary>
    public Skill GetSkillFor(Player player, SkillTemplate template, VisibleObject firstTarget)
    {
        // player doesn't have such skill and ist not provoked
        if (template.GetActivationAttribute() != ActivationAttribute.PROVOKED)
        {
            if (!player.GetSkillList().IsSkillPresent(template.GetSkillId()))
                return null;
        }

        Creature target = null;
        if (firstTarget is Creature creature)
            target = creature;

        return new Skill(template, player, target);
    }

    public Skill GetSkillFor(Player player, SkillTemplate template, VisibleObject firstTarget, int skillLevel)
    {
        Creature target = null;
        if (firstTarget is Creature creature)
            target = creature;

        return new Skill(template, player, target, skillLevel);
    }

    /// <summary>This method is used for not learned skills (item skills etc)</summary>
    public Skill GetSkill(Creature creature, int skillId, int skillLevel, VisibleObject firstTarget)
    {
        return GetSkill(creature, skillId, skillLevel, firstTarget, null);
    }

    public Skill GetSkill(Creature creature, int skillId, int skillLevel, VisibleObject firstTarget, ItemTemplate itemTemplate)
    {
        SkillTemplate template = DataManager.SKILL_DATA.GetSkillTemplate(skillId);

        if (template == null)
            return null;

        Creature target = null;
        if (firstTarget is Creature c)
            target = c;
        return new Skill(template, creature, skillLevel, target, itemTemplate);
    }

    public ChargeSkill GetChargeSkill(Creature creature, int skillId, int skillLevel, int motionId, Skill startSkill)
    {
        SkillTemplate template = DataManager.SKILL_DATA.GetSkillTemplate(skillId);
        if (template == null)
            return null;
        return new ChargeSkill(template, creature, skillLevel, motionId, startSkill);
    }

    public PenaltySkill GetPenaltySkill(Creature effector, int skillId, int skillLevel)
    {
        SkillTemplate template = DataManager.SKILL_DATA.GetSkillTemplate(skillId);
        if (template == null)
            return null;

        return new PenaltySkill(template, effector, skillLevel);
    }

    public static SkillEngine GetInstance()
    {
        return skillEngine;
    }

    public Effect ApplyEffectDirectly(int skillId, Creature effector, Creature effected)
    {
        SkillTemplate skillTemplate = CheckAndGetSkillTemplate(skillId);
        return skillTemplate == null ? null : ApplyEffect(effector, effected, skillTemplate, skillTemplate.GetLvl(), null, ForceType.DEFAULT);
    }

    /// <summary>
    /// Applies effects of given skill directly without checking properties. Should only be used from handlers, or when you are sure.
    /// duration: null = native duration, number = forced (0 = permanent). forceType: null = not forced, else the force identifier.
    /// </summary>
    public Effect ApplyEffectDirectly(int skillId, Creature effector, Creature effected, int? duration, ForceType forceType)
    {
        SkillTemplate skillTemplate = CheckAndGetSkillTemplate(skillId);
        return skillTemplate == null ? null : ApplyEffect(effector, effected, skillTemplate, skillTemplate.GetLvl(), duration, forceType);
    }

    public Effect ApplyEffectDirectly(int skillId, int lvl, Creature effector, Creature effected, int? duration, ForceType forceType)
    {
        SkillTemplate skillTemplate = CheckAndGetSkillTemplate(skillId);
        return skillTemplate == null ? null : ApplyEffect(effector, effected, skillTemplate, lvl, duration, forceType);
    }

    public Effect ApplyEffectDirectly(SkillTemplate skillTemplate, int skillLevel, Creature effector, Creature effected)
    {
        return ApplyEffect(effector, effected, skillTemplate, skillLevel, null, ForceType.DEFAULT);
    }

    /// <summary>Applies the skill's effects to one or multiple targets, depending on its template properties. Returns affected targets.</summary>
    public List<Creature> ApplyEffectsDirectly(int skillId, Creature effector, Creature firstTarget, float x, float y, float z)
    {
        SkillTemplate skillTemplate = DataManager.SKILL_DATA.GetSkillTemplate(skillId);
        Aion.GameServer.SkillEngine.Properties.Properties properties = skillTemplate.GetProperties();
        List<Creature> targets = new List<Creature>();
        targets.Add(firstTarget);
        if (properties != null) // add valid targets in range
            properties.ValidateEffectedList(targets, firstTarget, effector, skillTemplate, x, y, z);
        foreach (Creature target in targets)
            ApplyEffect(effector, target, skillTemplate, skillTemplate.GetLvl(), null, ForceType.DEFAULT);
        return targets;
    }

    /// <summary>Similar to ApplyEffectDirectly, but effect is not forced (checks for resists etc).</summary>
    public Effect ApplyEffect(int skillId, Creature effector, Creature effected)
    {
        SkillTemplate skillTemplate = CheckAndGetSkillTemplate(skillId);
        return skillTemplate == null ? null : ApplyEffect(effector, effected, skillTemplate, skillTemplate.GetLvl(), null, null);
    }

    private Effect ApplyEffect(Creature effector, Creature effected, SkillTemplate skillTemplate, int lvl, int? duration, ForceType forceType)
    {
        Effect ef = new Effect(effector, effected, skillTemplate, lvl, duration, forceType);
        ef.Initialize();
        ef.ApplyEffect();
        return ef;
    }

    private SkillTemplate CheckAndGetSkillTemplate(int skillId)
    {
        SkillTemplate skillTemplate = DataManager.SKILL_DATA.GetSkillTemplate(skillId);
        if (skillTemplate == null)
        {
            log.LogWarning(new ArgumentException(), "Could not apply effect, invalid skill id " + skillId);
            return null;
        }
        return skillTemplate;
    }

    public Effect CreateCriticalEffect(Player attacker, Creature target, int skillId)
    {
        if (target.GetEffectController().IsUnderNormalShield())
            return null;
        if (skillId != 0)
        {
            SkillTemplate skillTemplate = DataManager.SKILL_DATA.GetSkillTemplate(skillId);
            if (skillTemplate.GetType_() == SkillType.MAGICAL) // magical skills do not stun
                return null;
            if (skillTemplate.HasAnyEffect(true, EffectType.PULLED, EffectType.STUMBLE, EffectType.STAGGER, EffectType.STUN, EffectType.BACKDASH,
                EffectType.DASH, EffectType.MOVEBEHIND, EffectType.RANDOMMOVELOC, EffectType.RECALLINSTANT)
                || !skillTemplate.HasAnyEffect(EffectType.SKILLATKDRAININSTANT, EffectType.SKILLATTACKINSTANT))
                return null;
        }

        int id = 0;
        ItemGroup? mainHandWeaponType = attacker.GetEquipment().GetMainHandWeaponType(); // Java ItemGroup is a nullable enum reference -> int? wrapper
        if (mainHandWeaponType != null)
        {
            switch (mainHandWeaponType.Value)
            {
                case ItemGroup.POLEARM:
                case ItemGroup.STAFF:
                case ItemGroup.GREATSWORD:
                    id = 8218; // stumble
                    break;
                case ItemGroup.BOW:
                    id = 8217; // stun
                    break;
            }
        }

        if (id == 0)
            return null;

        SkillTemplate skillTemplate2 = CheckAndGetSkillTemplate(id);
        if (skillTemplate2 != null)
        {
            Effect ef = new Effect(attacker, target, skillTemplate2, skillTemplate2.GetLvl(), null, null, true);
            ef.Initialize();
            return ef;
        }
        return null;
    }
}
