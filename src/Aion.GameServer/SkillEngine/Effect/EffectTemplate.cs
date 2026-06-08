using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Controllers.Effect;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Model.Templates.Npc;
using Aion.GameServer.SkillEngine.change;
using Aion.GameServer.SkillEngine.condition;
using Aion.GameServer.SkillEngine.Effect.Modifier;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils.Stats;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.SkillEngine.Effect;

/// <summary>
/// Base of the effect-template hierarchy (~170 subclasses). Java parity: skillengine/effect/EffectTemplate.
/// Java @XmlAccessorType(FIELD): all fields are XML-bound (attributes via @XmlAttribute, else elements).
/// C# XmlSerializer needs public members → public properties with [XmlAttribute/XmlElement(name)].
/// Subclasses must be registered with [XmlInclude(typeof(...))] here as they are ported (from Java @XmlSeeAlso).
/// </summary>
public abstract class EffectTemplate
{
    private static readonly ILogger log = NullLogger.Instance;

    [XmlElement("modifiers")]
    public ActionModifiers? Modifiers;

    [XmlElement("change")]
    public List<Change>? Change;

    [XmlAttribute("effectid")]
    public int Effectid;

    [XmlAttribute("duration2")]
    public int Duration2;

    [XmlAttribute("duration1")]
    public int Duration1;

    [XmlAttribute("randomtime")]
    public int RandomTime;

    [XmlAttribute("e")]
    public int Position;

    [XmlAttribute("basiclvl")]
    public int BasicLvl;

    [XmlAttribute("hittype")]
    public HitType Hittype = HitType.EVERYHIT;

    [XmlAttribute("hittypeprob2")]
    public int HitTypeProb = 100;

    [XmlAttribute("element")]
    public SkillElement Element = SkillElement.NONE;

    [XmlElement("subeffect")]
    public SubEffect? SubEffectField;

    [XmlElement("conditions")]
    public Conditions? EffectConditions;

    [XmlElement("subconditions")]
    public Conditions? EffectSubConditions;

    [XmlAttribute("hoptype")]
    public HopType? Hoptype;

    [XmlAttribute("hopa")]
    public int HopA; // effects the aggro-value (hate)

    [XmlAttribute("hopb")]
    public int HopB; // effects the aggro-value (hate)

    [XmlAttribute("noresist")]
    public bool NoResist;

    [XmlAttribute("accmod1")]
    public int AccMod1; // accdelta

    [XmlAttribute("accmod2")]
    public int AccMod2; // accvalue

    [XmlAttribute("preeffect")]
    public int[]? PreEffects;

    [XmlAttribute("preeffect_prob")]
    public int PreEffectProb = 100;

    [XmlAttribute("critprobmod2")]
    public int CritProbMod2 = 100;

    [XmlAttribute("critadddmg1")]
    public int CritAddDmg1 = 0;

    [XmlAttribute("critadddmg2")]
    public int CritAddDmg2 = 0;

    [XmlAttribute("value")]
    public int Value;

    [XmlAttribute("delta")]
    public int Delta;

    [XmlAttribute("skill_efficiency")]
    public int SkillEfficiency;

    [XmlAttribute("max_damage_chance")]
    public int MaxDamageChance;

    [XmlAttribute("max_damage_delta")]
    public int MaxDamageDelta;

    public int GetValue() => Value;

    public int GetDelta() => Delta;

    public int GetDuration2() => Duration2;

    public int GetDuration1() => Duration1;

    public int GetRandomTime() => RandomTime;

    public ActionModifiers? GetModifiers() => Modifiers;

    public List<Change>? GetChange() => Change;

    public int GetEffectId() => Effectid;

    public int GetPosition() => Position;

    public int GetBasicLvl() => BasicLvl;

    public SkillElement GetElement() => Element;

    public int GetPreEffectProb() => PreEffectProb;

    private int[]? GetPreEffects() => PreEffects;

    public bool IsNoResist() => NoResist;

    public int GetCritProbMod2() => CritProbMod2;

    public int GetCritAddDmg1() => CritAddDmg1;

    public int GetCritAddDmg2() => CritAddDmg2;

    /// <summary>Gets the effect conditions status.</summary>
    public Conditions? GetEffectConditions() => EffectConditions;

    /// <summary>Gets the sub effect conditions status.</summary>
    public Conditions? GetEffectSubConditions() => EffectSubConditions;

    public ActionModifier? GetActionModifiers(SkillEngine.Model.Effect effect)
    {
        if (Modifiers == null)
            return null;

        // Only one of modifiers will be applied now
        foreach (ActionModifier modifier in Modifiers.GetActionModifiers())
        {
            if (modifier.Check(effect))
                return modifier;
        }

        return null;
    }

    public SubEffect? GetSubEffect() => SubEffectField;

    public int GetAccMod1() => AccMod1;

    public int GetAccMod2() => AccMod2;

    /// <summary>The base value (damage, heal, etc.) per the skill template (matches the description value).</summary>
    protected int CalculateBaseValue(SkillEngine.Model.Effect effect)
    {
        return Value + Delta * effect.GetSkillLevel();
    }

    public int CalculateCritAddDmg(SkillEngine.Model.Effect effect)
    {
        return CritAddDmg2 + CritAddDmg1 * effect.GetSkillLevel();
    }

    /// <summary>Calculate effect result.</summary>
    public void Calculate(SkillEngine.Model.Effect effect)
    {
        Calculate(effect, null, null, Element);
    }

    public bool Calculate(SkillEngine.Model.Effect effect, StatEnum? statEnum, SpellStatus? spellStatus)
    {
        return Calculate(effect, statEnum, spellStatus, Element);
    }

    public bool Calculate(SkillEngine.Model.Effect effect, StatEnum? statEnum, SpellStatus? spellStatus, SkillElement element)
    {
        if (effect.GetSkillTemplate().IsPassive())
        {
            AddSuccessEffect(effect, spellStatus);
            return true;
        }

        if (statEnum != null && IsAlteredState(statEnum.Value) && IsImmuneToAbnormal(effect, statEnum.Value))
        {
            return false;
        }

        if (!effect.IsForcedEffect())
        {
            if (!ValidateEffectConditions(effect))
                return false;
            if (!ValidatePreEffects(effect))
                return false;
            if (IsDodgedOrResisted(effect, statEnum))
            {
                if (GetPosition() != 1 && !(effect.EffectInPos(1) is DamageEffect))
                    effect.GetSuccessEffects().Clear();
                return false;
            }
        }
        AddSuccessEffect(effect, spellStatus);
        CalculateDamage(effect);
        return true;
    }

    private bool ValidateEffectConditions(SkillEngine.Model.Effect effect)
    {
        return EffectConditions == null || EffectConditions.Validate(effect);
    }

    private bool ValidatePreEffects(SkillEngine.Model.Effect effect)
    {
        if (GetPreEffects() != null)
        {
            foreach (int position in GetPreEffects()!)
            {
                if (!effect.IsInSuccessEffects(position))
                    return false;
            }
            if (Rnd.Chance() >= GetPreEffectProb())
                return false;
        }
        return true;
    }

    private bool IsDodgedOrResisted(SkillEngine.Model.Effect effect, StatEnum? statEnum)
    {
        return CanDodgeOrResist(effect) && (!CheckEffectResistRate(effect, statEnum) || !CheckDodgeOrResistRate(effect));
    }

    protected bool CanDodgeOrResist(SkillEngine.Model.Effect effect)
    {
        if (NoResist)
            return false;
        if (effect.GetSkillTemplate().GetActivationAttribute() == ActivationAttribute.TOGGLE) // Sprinting must not be dodged by Focused Evasion
            return false;
        return true;
    }

    /// <returns>true = no dodge/resist, false = dodged/resisted</returns>
    private bool CheckDodgeOrResistRate(SkillEngine.Model.Effect effect)
    {
        int accuracyModifier = AccMod2 + AccMod1 * effect.GetSkillLevel() + effect.GetAccModBoost();
        if (effect.GetSkillTemplate().GetSubType() == SkillSubType.DEBUFF)
            accuracyModifier += effect.GetEffector().GetGameStats().GetStat(StatEnum.BOOST_RESIST_DEBUFF, 0).GetCurrent();
        if (Element == SkillElement.NONE)
            return !StatFunctions.CheckIsDodgedHit(effect.GetEffector(), effect.GetEffected(), accuracyModifier);
        return Rnd.Get(1, 1000) > StatFunctions.CalculateMagicalResistRate(effect.GetEffector(), effect.GetEffected(), accuracyModifier, Element);
    }

    private void AddSuccessEffect(SkillEngine.Model.Effect effect, SpellStatus? spellStatus)
    {
        effect.AddSuccessEffect(this);
        if (spellStatus != null)
            effect.SetSpellStatus(spellStatus.Value);
    }

    /// <summary>Apply effect to effected.</summary>
    public abstract void ApplyEffect(SkillEngine.Model.Effect effect);

    /// <summary>Start effect on effected.</summary>
    public virtual void StartEffect(SkillEngine.Model.Effect effect)
    {
    }

    public void CalculateDamage(SkillEngine.Model.Effect effect)
    {
        // evaluate skill reflect for non-dmg skills
        AttackResult attackResult = new AttackResult(0, effect.GetAttackStatus(), Hittype);
        effect.GetEffected().GetObserveController().CheckShieldStatus(new List<AttackResult> { attackResult }, effect, effect.GetEffector(),
            ShieldType.SKILL_REFLECTOR);
        if (attackResult.GetShieldType() != 0)
        {
            effect.SetShieldDefense(effect.GetShieldDefense() | attackResult.GetShieldType());
            effect.SetReflectedSkillId(attackResult.GetReflectedSkillId());
        }
    }

    public void CalculateSubEffect(SkillEngine.Model.Effect effect)
    {
        if (SubEffectField == null)
            return;
        ActionModifiers? mod = GetModifiers();
        if (mod != null)
        {
            ActionModifier? modifier = GetActionModifiers(effect);
            if (modifier == null)
            {
                return;
            }
        }
        // Pre-Check for sub effect conditions
        if (!EffectSubConditionsCheck(effect))
        {
            effect.SetSubEffectAborted(true);
            return;
        }

        // chance to trigger subeffect
        if (Rnd.Chance() >= SubEffectField.GetChance())
            return;

        SkillTemplate template = DataManager.SKILL_DATA.GetSkillTemplate(SubEffectField.GetSkillId());
        int level = 1;
        int accBoost = effect.GetAccModBoost();
        if (SubEffectField.IsAddEffect())
        { // Only used by signet bursts
            level = effect.GetSignetBurstedCount();
            accBoost = short.MaxValue; // sub effects cannot be resisted by magic resist in case of signet bursts
        }
        SkillEngine.Model.Effect newEffect = new SkillEngine.Model.Effect(effect.GetEffector(), effect.GetOriginalEffected(), template, level, null, effect.GetForceType(), true);
        newEffect.SetShieldDefense(effect.GetShieldDefense());
        newEffect.SetAccModBoost(accBoost);
        newEffect.Initialize();
        if (newEffect.GetSpellStatus() != SpellStatus.DODGE && newEffect.GetSpellStatus() != SpellStatus.RESIST)
            effect.SetSpellStatus(newEffect.GetSpellStatus());
        effect.SetSubEffect(newEffect);
        effect.SetSubEffectType(newEffect.GetSubEffectType());
        effect.SetTargetLoc(newEffect.GetTargetX(), newEffect.GetTargetY(), newEffect.GetTargetZ());
    }

    /// <summary>Check all sub effect condition statuses for effect.</summary>
    private bool EffectSubConditionsCheck(SkillEngine.Model.Effect effect)
    {
        return EffectSubConditions == null || EffectSubConditions.Validate(effect);
    }

    public int CalculateHate(SkillEngine.Model.Effect effect)
    {
        if (Hoptype != null)
        {
            int hate = 0;
            switch (Hoptype)
            {
                case HopType.DAMAGE:
                    hate = effect.GetReserveds(0).GetValue();
                    goto case HopType.SKILLLV;
                case HopType.SKILLLV:
                    int skillLvl = effect.GetSkillLevel();
                    hate += HopB + HopA * skillLvl; // Aggro-value of the effect
                    break;
                default:
                    throw new NotSupportedException("Unhandled effect type " + Hoptype + " for hate calculation");
            }
            return System.Math.Max(1, hate);
        }
        return 0;
    }

    public void StartSubEffect(SkillEngine.Model.Effect effect)
    {
        if (SubEffectField == null)
            return;
        // Apply-Check for sub effect conditions
        if (effect.IsSubEffectAbortedBySubConditions())
            return;
        if (effect.GetSubEffect() != null)
            effect.GetSubEffect().ApplyEffect();
    }

    /// <summary>Do periodic effect on effected.</summary>
    public virtual void OnPeriodicAction(SkillEngine.Model.Effect effect)
    {
    }

    /// <summary>End effect on effected.</summary>
    public virtual void EndEffect(SkillEngine.Model.Effect effect)
    {
    }

    /// <returns>true = no resist, false = resisted</returns>
    public bool CheckEffectResistRate(SkillEngine.Model.Effect effect, StatEnum? statEnum)
    {
        if (statEnum == null)
            return true;

        Creature effected = effect.GetEffected();
        Creature effector = effect.GetEffector();

        if (effected == null || effected.GetGameStats() == null || effector == null || effector.GetGameStats() == null)
            return false;

        // Stun like effects cannot be applied as long as a shield is active
        if (IsProtectedByShield(effected, statEnum.Value))
            return false;

        int effectPower = 1000;

        if (IsAlteredState(statEnum.Value))
            effectPower -= effected.GetGameStats().GetAbnormalResistance().GetCurrent();

        // effect resistance
        effectPower -= effected.GetGameStats().GetResistance(statEnum.Value).GetCurrent();

        // calculate cumulative resist chance for fear, sleep and paralyze if effector and effected are players
        bool isEffectorPlayer = (CustomConfig.COUNT_SUMMON_EFFECTS_FOR_CUMULATIVE_RESIST ? effector.GetMaster() : effector) is Player;
        if (isEffectorPlayer && effected is Player player)
            effectPower -= player.GetEffectController().GetCumulativeResistance(CumulativeResistType.Get(statEnum.Value));

        // penetration
        StatEnum? penetrationStat = GetPenetrationStat(statEnum.Value);
        if (penetrationStat != null)
            effectPower += effector.GetGameStats().GetStat(penetrationStat.Value, 0).GetCurrent();

        // resist mod
        if (effectPower > 0 && effector.IsPvpTarget(effected))
        { // pvp
            int lvlDiff = effected.GetLevel() - effector.GetLevel();
            if (lvlDiff > 4)
            {
                float reductionRate = 0.1f * (lvlDiff - 4);
                effectPower = (int)(effectPower * System.Math.Max(1 - reductionRate, 0.1f));
            }
        }
        return Rnd.Get(1, 1000) <= effectPower;
    }

    private bool IsImmuneToAbnormal(SkillEngine.Model.Effect effect, StatEnum statEnum)
    {
        Creature effected = effect.GetEffected();
        if (effected != effect.GetEffector())
        {
            if (effected is Npc || effected is Summon)
            {
                if (effected.GetAi().Ask(AIQuestion.IS_IMMUNE_TO_ABNORMAL_STATES))
                    return true;
                if (((NpcTemplate)effected.GetObjectTemplate()).GetStatsTemplate().GetRunSpeed() == 0)
                    return statEnum == StatEnum.PULLED_RESISTANCE || statEnum == StatEnum.STAGGER_RESISTANCE || statEnum == StatEnum.STUMBLE_RESISTANCE;
            }
        }
        return false;
    }

    /// <returns>true = altered state effect, false = Poison/Bleed dot (normal Dots have null statEnum here)</returns>
    private bool IsAlteredState(StatEnum stat)
    {
        return stat switch
        {
            StatEnum.BLEED_RESISTANCE or StatEnum.POISON_RESISTANCE => false,
            _ => true,
        };
    }

    private bool IsProtectedByShield(Creature effected, StatEnum stat)
    {
        return stat switch
        {
            StatEnum.STUMBLE_RESISTANCE or StatEnum.OPENAERIAL_RESISTANCE or StatEnum.SPIN_RESISTANCE or StatEnum.STAGGER_RESISTANCE => effected.GetEffectController().IsUnderNormalShield(),
            _ => false,
        };
    }

    private StatEnum? GetPenetrationStat(StatEnum statEnum)
    {
        if (Enum.TryParse(statEnum.ToString() + "_PENETRATION", out StatEnum toReturn))
            return toReturn;
        log.LogWarning("Missing statenum penetration for " + statEnum.ToString());
        return null;
    }
}
