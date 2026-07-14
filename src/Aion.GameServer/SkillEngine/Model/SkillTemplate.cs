using System.Linq;
using System.Xml.Serialization;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.Templates;
using Aion.GameServer.SkillEngine.Action;
using Aion.GameServer.SkillEngine.Condition;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.PeriodicAction;
using Aion.GameServer.SkillEngine.Properties;
using Aion.GameServer.Utils;

namespace Aion.GameServer.SkillEngine.Model;

/// <summary>
/// Java parity: skillengine/model/SkillTemplate. Skill data definition (XML-bound).
/// getType() → GetTypeValue() (Object.GetType clash). Cone refs use lowercase namespaces
/// (action/condition/properties/periodicaction) to avoid class/namespace FQN clashes.
/// </summary>
public class SkillTemplate : L10n
{
    [XmlElement("properties")]
    public Aion.GameServer.SkillEngine.Properties.Properties? properties;

    [XmlElement("startconditions")]
    public Conditions? startconditions;

    [XmlElement("useconditions")]
    public Conditions? useconditions;

    [XmlElement("endconditions")]
    public Conditions? endconditions;

    [XmlElement("effects")]
    public Aion.GameServer.SkillEngine.Effects.Effects? effects;

    [XmlElement("actions")]
    public Actions? actions;

    [XmlElement("periodicactions")]
    public PeriodicActions? periodicActions;

    [XmlElement("motion")]
    public Motion? motion;

    [XmlAttribute("skill_id")]
    public int skillId;

    [XmlAttribute("name")]
    public string? name;

    [XmlAttribute("nameId")]
    public int nameId;

    [XmlAttribute("stack")]
    public string? stack;

    [XmlAttribute("group")]
    public string? group;

    [XmlAttribute("cooldownId")]
    public int cooldownId;

    [XmlAttribute("lvl")]
    public int lvl;

    [XmlAttribute("skilltype")]
    public SkillType type = SkillType.NONE;

    [XmlAttribute("skillsubtype")]
    public SkillSubType subType;

    [XmlAttribute("skill_category")]
    public SkillCategory skillCategory = SkillCategory.NONE;

    [XmlAttribute("tslot")]
    public SkillTargetSlot targetSlot;

    [XmlAttribute("tslot_level")]
    public int targetSlotLevel;

    [XmlAttribute("dispel_category")]
    public DispelCategoryType dispelCategory = DispelCategoryType.NONE;

    [XmlAttribute("req_dispel_level")]
    public int reqDispelLevel;

    [XmlAttribute("req_dispel_count")]
    public int reqDispelCount;

    [XmlAttribute("activation")]
    public ActivationAttribute activationAttribute;

    [XmlAttribute("duration")]
    public int duration;

    [XmlAttribute("toggle_timer")]
    public int toggleTimer;

    [XmlAttribute("cooldown")]
    public int cooldown;

    [XmlAttribute("cooldown_delta_lv")]
    public int cooldownDeltaLv;

    [XmlAttribute("penalty_skill_id")]
    public int penaltySkillId;

    [XmlAttribute("penalty_skill_send_msg")]
    public bool penaltySkillSendMsg = false;

    [XmlAttribute("pvp_damage")]
    public int pvpDamage;

    [XmlAttribute("pvp_duration")]
    public int pvpDuration;

    [XmlAttribute("chain_skill_prob")]
    public int chainSkillProb = 100;

    [XmlAttribute("cancel_rate")]
    public int cancelRate;

    [XmlAttribute("stance")]
    public bool stance;

    [XmlAttribute("avatar")]
    public bool isDeityAvatar;

    [XmlAttribute("ground")]
    public bool isGroundSkill; // TODO remove!

    [XmlAttribute("ammospeed")]
    public int ammoSpeed;

    [XmlAttribute("conflict_id")]
    public int conflictId;

    // Java parity: @XmlAttribute("counter_skill") AttackStatus (nullable). XmlSerializer cannot bind
    // Nullable<T> as an attribute, so round-trip via a string proxy. A handful of skills carry a
    // comma-separated value (e.g. "BLOCK,RESIST") which no single AttackStatus matches; JAXB's lenient
    // default unmarshal-event handler leaves the field null in that case, so we mirror that (null on any
    // unparseable token).
    [XmlIgnore]
    public AttackStatus? counterSkill = null;

    [XmlAttribute("counter_skill")]
    public string? CounterSkillRaw
    {
        get => counterSkill?.ToString();
        set => counterSkill = JavaEnum.TryValueOf(value, out AttackStatus parsed) ? parsed : null;
    }

    [XmlAttribute("noremoveatdie")]
    public bool noRemoveOnDie = false;

    [XmlAttribute("no_save_on_logout")]
    public bool noSaveOnLogout = false;

    [XmlAttribute("stigma")]
    public StigmaType stigmaType = StigmaType.NONE;

    [XmlAttribute("applymcrit")]
    public bool applyMcrit = true;

    [XmlAttribute("hostile_type")]
    public HostileType hostileType = HostileType.NONE;

    public Aion.GameServer.SkillEngine.Properties.Properties? GetProperties() => properties;

    public Conditions? GetStartconditions() => startconditions;

    public Conditions? GetUseconditions() => useconditions;

    public Aion.GameServer.SkillEngine.Effects.Effects? GetEffects() => effects;

    public Actions? GetActions() => actions;

    public PeriodicActions? GetPeriodicActions() => periodicActions;

    public Motion? GetMotion() => motion;

    public int GetSkillId() => skillId;

    public string? GetName() => name;

    public int GetL10nId() => nameId;

    // Java parity: L10n::getL10n() default method — instance-callable in Java; provide concretely
    // (C# default-interface methods are not instance-visible) to mirror the original.
    public string? GetL10n() => Aion.GameServer.Utils.ChatUtil.L10n(GetL10nId());

    public string? GetStack() => stack;

    public string? GetGroup() => group;

    public int GetLvl() => lvl;

    /// <summary>Java parity: getType().</summary>
    public SkillType GetTypeValue() => type;

    // Java parity: getType() - GetType_ is the project-wide getType() convention name (Object.GetType clash).
    public SkillType GetType_() => type;

    public SkillSubType GetSubType() => subType;

    public SkillTargetSlot GetTargetSlot() => targetSlot;

    public int GetTargetSlotLevel() => targetSlotLevel;

    public DispelCategoryType GetDispelCategory() => dispelCategory;

    public int GetReqDispelLevel() => reqDispelLevel;

    public int GetReqDispelCount() => reqDispelCount;

    public int GetDuration() => duration;

    public int GetToggleTimer() => toggleTimer;

    public StigmaType GetStigmaType() => stigmaType;

    public ActivationAttribute GetActivationAttribute() => activationAttribute;

    public bool IsPassive() => activationAttribute == ActivationAttribute.PASSIVE;

    public bool IsToggle() => activationAttribute == ActivationAttribute.TOGGLE;

    public bool IsProvoked() => activationAttribute == ActivationAttribute.PROVOKED;

    public bool IsMaintain() => activationAttribute == ActivationAttribute.MAINTAIN;

    public bool IsActive() => activationAttribute == ActivationAttribute.ACTIVE;

    public bool IsCharge() => activationAttribute == ActivationAttribute.CHARGE;

    public EffectTemplate? GetEffectTemplate(int position)
    {
        return effects != null && effects.GetEffects().Count >= position ? effects.GetEffects()[position - 1] : null;
    }

    public int GetCooldown() => cooldown;

    public int GetCooldownDeltaLv() => cooldownDeltaLv;

    public int GetPenaltySkillId() => penaltySkillId;

    public bool ShouldPenaltySkillSendMsg() => penaltySkillSendMsg;

    public int GetPvpDamage() => pvpDamage;

    public int GetPvpDuration() => pvpDuration;

    public int GetChainSkillProb() => chainSkillProb;

    public int GetCancelRate() => cancelRate;

    public bool IsStance() => stance;

    public bool IsCastDurationAffectedByCastSpeed()
    {
        if (IsDeityAvatar())
            return false;
        if (HasAnyEffect(EffectType.SLEEP, EffectType.FEAR, EffectType.RETURN, EffectType.ESCAPE))
            return false; // sleep and fear skills are no longer affected by cast speed since 1.5.0.5
        if (GetActions() != null && GetActions()!.GetActions().Any(action => action is ItemUseAction)) // e.g. Herb Treatment
            return false;
        return true;
    }

    public bool HasAnyEffect(params EffectType[] effectTypes)
    {
        return HasAnyEffect(false, effectTypes);
    }

    public bool HasAnyEffect(bool checkSubEffects, params EffectType[] effectTypes)
    {
        if (effects == null)
            return false;
        if (effects.HasAnyEffectType(effectTypes))
            return true;
        if (checkSubEffects)
        {
            foreach (EffectTemplate et in effects.GetEffects())
            {
                if (et.GetSubEffect() != null)
                {
                    if (DataManager.SKILL_DATA.GetSkillTemplate(et.GetSubEffect()!.GetSkillId()).HasAnyEffect(effectTypes)) // should we check recursively?
                        return true;
                }
            }
        }
        return false;
    }

    /// <summary>resurrectbase is excluded because of different behavior.</summary>
    public bool HasResurrectEffect()
    {
        return HasAnyEffect(EffectType.RESURRECT, EffectType.RESURRECTPOSITIONAL);
    }

    public bool HasEvadeEffect()
    {
        return HasAnyEffect(EffectType.EVADE);
    }

    public bool HasRecallInstant()
    {
        return HasAnyEffect(EffectType.RECALLINSTANT);
    }

    public int GetCooldownId()
    {
        return (cooldownId > 0) ? cooldownId : skillId;
    }

    public bool IsDeityAvatar() => isDeityAvatar;

    public bool IsGroundSkill() => isGroundSkill;

    public AttackStatus? GetCounterSkill() => counterSkill;

    public int GetAmmoSpeed() => ammoSpeed;

    public int GetConflictId() => conflictId;

    public bool IsNoRemoveOnDie() => noRemoveOnDie;

    public bool IsNoSaveOnLogout() => noSaveOnLogout;

    public bool IsMultiCast()
    {
        ChainCondition? chainCondition = GetChainCondition();
        return chainCondition != null && chainCondition.GetAllowedActivations() > 1;
    }

    public ChainCondition? GetChainCondition()
    {
        if (startconditions != null)
        {
            foreach (Aion.GameServer.SkillEngine.Condition.Condition cond in startconditions.GetConditions())
            {
                if (cond is ChainCondition chain)
                    return chain;
            }
        }
        return null;
    }

    public RideRobotCondition? GetRideRobotCondition()
    {
        if (useconditions != null)
        {
            foreach (Aion.GameServer.SkillEngine.Condition.Condition cond in useconditions.GetConditions())
            {
                if (cond is RideRobotCondition ride)
                    return ride;
            }
        }
        return null;
    }

    public SkillChargeCondition? GetSkillChargeCondition()
    {
        if (startconditions != null)
        {
            foreach (Aion.GameServer.SkillEngine.Condition.Condition cond in startconditions.GetConditions())
            {
                if (cond is SkillChargeCondition charge)
                    return charge;
            }
        }
        return null;
    }

    public HpCondition? GetHpCondition()
    {
        foreach (Aion.GameServer.SkillEngine.Condition.Condition c in startconditions!.GetConditions())
            if (c is HpCondition hp)
            {
                return hp;
            }
        return null;
    }

    public PlayerMovedCondition? GetMovedCondition()
    {
        foreach (Aion.GameServer.SkillEngine.Condition.Condition c in startconditions!.GetConditions())
            if (c is PlayerMovedCondition moved)
            {
                return moved;
            }
        return null;
    }

    public Conditions? GetEndConditions() => endconditions;

    public SkillCategory GetSkillCategory() => skillCategory;

    public bool IsMcritApplied() => applyMcrit;

    public HostileType GetHostileType() => hostileType;
}
