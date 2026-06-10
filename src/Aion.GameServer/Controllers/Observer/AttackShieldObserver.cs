using System;
using System.Collections.Generic;
using Aion.Commons.Utils;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Summons;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Skillengine.Effect;
using Aion.GameServer.Skillengine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Controllers.Observer;

/// <summary>Java parity: controllers/observer/AttackShieldObserver (ATracer, Sippolo, kecimis, Luzien, Neon) : AttackCalcObserver. Shield/reflector/protect/convert damage mechanics. ctor chaining→`: this(...)`; nested SM_ATTACK_STATUS.TYPE/LOG + Effect.ForceType qualified; HealType→HealType? (Java passes null); enum switch (HitType/ShieldType/HealType); instanceof Npc/Summon→is; Rnd.chance→Rnd.Chance; Math.min/max→Math.Min/Max; shieldType.getId()→GetId(). Effect/AttackResult/enums/SkillTemplate red-tolerated.</summary>
public class AttackShieldObserver : AttackCalcObserver
{
    private readonly Effect effect;
    private readonly HitType hitType;
    private readonly ShieldType shieldType;
    private readonly int hit;
    private readonly bool hitPercent;
    private int totalHit;
    private readonly bool totalHitPercent;
    private readonly int probability;
    private readonly int minRadius;
    private readonly int maxRadius;
    private readonly HealType? healType;
    private readonly int mpValue;

    private bool totalHitPercentSet = false;

    public AttackShieldObserver(int hit, int totalHit, bool percent, Effect effect, HitType type, ShieldType shieldType, int probability)
        : this(hit, totalHit, percent, false, effect, type, shieldType, probability, 0, 100, null, 0)
    {
    }

    public AttackShieldObserver(int hit, int totalHit, bool percent, Effect effect, HitType type, ShieldType shieldType, int probability,
        int mpValue)
        : this(hit, totalHit, percent, false, effect, type, shieldType, probability, 0, 100, null, mpValue)
    {
    }

    public AttackShieldObserver(int hit, int totalHit, bool hitPercent, bool totalHitPercent, Effect effect, HitType type, ShieldType shieldType,
        int probability, int minRadius, int maxRadius, HealType? healType, int mpValue)
    {
        this.hit = hit;
        this.totalHit = totalHit; // total absorbed dmg for shield, percentage for reflector, received dmg percentage for protect
        this.effect = effect;
        this.hitPercent = hitPercent;
        this.totalHitPercent = totalHitPercent;
        this.hitType = type;
        this.shieldType = shieldType;
        this.probability = probability;
        this.minRadius = minRadius; // only for reflector
        this.maxRadius = maxRadius; // for reflector / protect
        this.healType = healType; // only for ConvertHeal
        this.mpValue = mpValue;
    }

    public override void CheckShield(List<AttackResult> attackList, Effect attackerEffect, Creature attacker)
    {
        foreach (AttackResult attackResult in attackList)
        {
            AttackStatus baseStatus = AttackStatus.GetBaseStatus(attackResult.GetAttackStatus());
            if (baseStatus == AttackStatus.DODGE || baseStatus == AttackStatus.RESIST)
                continue;

            // Handle Hit Types for Shields
            switch (hitType)
            {
                case HitType.EVERYHIT:
                    break;
                case HitType.SKILL:
                    if (attackerEffect == null)
                        continue;
                    break;
                default:
                    if (attackResult.GetHitType() != null && hitType != attackResult.GetHitType())
                        continue;
                    break;
            }

            if (probability < 100 && Rnd.Chance() >= probability)
                continue;

            // shield type 2 or 16, normal shield, MP
            if (shieldType == ShieldType.NORMAL || shieldType == ShieldType.MPSHIELD)
            {
                int damage = attackResult.GetDamage();

                int absorbedDamage;
                if (hitPercent)
                    absorbedDamage = damage * hit / 100;
                else
                    absorbedDamage = Math.Min(damage, hit);

                absorbedDamage = Math.Min(absorbedDamage, totalHit);
                totalHit -= absorbedDamage;

                if (absorbedDamage > 0)
                    attackResult.SetShieldType(shieldType.GetId());
                attackResult.SetDamage(damage - absorbedDamage);

                // don't launch sub effect if damage is fully absorbed
                if (absorbedDamage >= damage && !IsPunchShield(attackerEffect))
                    attackResult.SetLaunchSubEffect(false);

                if (mpValue != 0)
                {
                    int mp = (int)(absorbedDamage * 0.01f * mpValue);
                    // TODO recheck sm_attack_status
                    effect.GetEffected().GetLifeStats().ReduceMp(SM_ATTACK_STATUS.TYPE.USED_MP, mp, 0, SM_ATTACK_STATUS.LOG.REGULAR);
                    attackResult.SetMpAbsorbed(mp);
                    attackResult.SetMpShieldSkillId(effect.GetSkillId());
                }

                if (totalHit <= 0)
                {
                    effect.EndEffect();
                    return;
                }
            }
            else if (shieldType == ShieldType.REFLECTOR || shieldType == ShieldType.SKILL_REFLECTOR) // shield type 1, reflected damage
            {
                if (minRadius != 0)
                {
                    if (PositionUtil.IsInRange(attacker, effect.GetEffected(), minRadius, false))
                        continue;
                }
                if (PositionUtil.IsInRange(attacker, effect.GetEffected(), maxRadius, false))
                {
                    int reflectedHit = attackResult.GetDamage();
                    if (hit > 0 || totalHit > 0)
                    {
                        int reflectedDamage = attackResult.GetDamage() * totalHit / 100;
                        reflectedHit = Math.Max(reflectedDamage, hit); // percentage of damage, but at least hit value
                    }
                    attackResult.SetShieldType(shieldType.GetId());
                    if (attacker is Npc)
                    {
                        reflectedHit = (int)attacker.GetAi().ModifyDamage(attacker, reflectedHit, effect);
                    }
                    attackResult.SetReflectedDamage(reflectedHit);
                    attackResult.SetReflectedSkillId(effect.GetSkillId());

                    if (shieldType == ShieldType.SKILL_REFLECTOR) // whole skill reflections are applied implicitly, see Effect#getEffected()
                    {
                        attackerEffect.SetForceType(Effect.ForceType.DEFAULT); // make sure it hits the effector (no checks needed at this point)
                        effect.EndEffect(); // one skill reflection ends the shield effect
                        return;
                    }
                    else // apply reflect damage
                    {
                        attacker.GetController().OnAttack(effect.GetEffected(), effect, SM_ATTACK_STATUS.TYPE.REGULAR, reflectedHit, false, SM_ATTACK_STATUS.LOG.REGULAR, null, null);
                    }
                }
                break;
            }
            else if (shieldType == ShieldType.PROTECT) // shield type 8, protect effect (ex. skillId: 417 Bodyguard I)
            {
                if (effect.GetEffector() == null || effect.GetEffector().IsDead())
                {
                    effect.EndEffect();
                    break;
                }
                if (effect.GetEffector() is Summon
                    && (((Summon)effect.GetEffector()).GetMode() == SummonMode.RELEASE || ((Summon)effect.GetEffector()).GetMaster() == null))
                {
                    effect.EndEffect();
                    break;
                }

                if (PositionUtil.IsInRange(effect.GetEffector(), effect.GetEffected(), maxRadius, false))
                {
                    int damageProtected = 0;
                    int effectorDamage = 0;

                    if (hitPercent)
                    {
                        damageProtected = (int)(attackResult.GetDamage() * hit * 0.01);
                        if (totalHit > 0) // reduce the effectively received damage (totalHit = percent of received dmg)
                            effectorDamage = attackResult.GetDamage() * totalHit / 100;
                        else
                            effectorDamage = attackResult.GetDamage();
                    }
                    else
                        damageProtected = hit;
                    int finalDamage = Math.Max(0, attackResult.GetDamage() - damageProtected);
                    attackResult.SetDamage(finalDamage);
                    attackResult.SetShieldType(shieldType.GetId());
                    attackResult.SetProtectedSkillId(effect.GetSkillId());
                    attackResult.SetProtectedDamage(effectorDamage);
                    attackResult.SetProtectorId(effect.GetEffectorId());
                    effect.GetEffector().GetController().OnAttack(attacker, attackerEffect, SM_ATTACK_STATUS.TYPE.PROTECTDMG, effectorDamage, false, SM_ATTACK_STATUS.LOG.REGULAR,
                        attackResult.GetAttackStatus(), null);
                    // dont launch subeffect if damage is fully absorbed
                    if (!IsPunchShield(attackerEffect))
                        attackResult.SetLaunchSubEffect(false);
                }
            }
            else if (shieldType == ShieldType.CONVERT) // shield type 0, convertHeal
            {
                int damage = attackResult.GetDamage();

                int absorbedDamage = damage;

                if (totalHitPercent && !totalHitPercentSet)
                {
                    totalHit = (int)(totalHit * 0.01 * effect.GetEffected().GetGameStats().GetHealth().GetCurrent());
                    totalHitPercentSet = true;
                }

                absorbedDamage = Math.Min(absorbedDamage, totalHit);
                totalHit -= absorbedDamage;

                attackResult.SetDamage(damage - absorbedDamage);

                // heal part
                int healValue = 0;
                if (hitPercent)
                    healValue = damage * hit / 100;
                else
                    healValue = hit;

                switch (healType)
                {
                    case HealType.HP:
                        effect.GetEffected().GetLifeStats().IncreaseHp(SM_ATTACK_STATUS.TYPE.HP, healValue, effect, SM_ATTACK_STATUS.LOG.REGULAR);
                        break;
                    case HealType.MP:
                        effect.GetEffected().GetLifeStats().IncreaseMp(SM_ATTACK_STATUS.TYPE.HEAL_MP, healValue, effect.GetSkillId(), SM_ATTACK_STATUS.LOG.REGULAR);
                        break;
                }

                // dont launch subeffect if damage is fully absorbed
                if (absorbedDamage >= damage && !IsPunchShield(attackerEffect))
                    attackResult.SetLaunchSubEffect(false);

                if (totalHit <= 0)
                {
                    effect.EndEffect();
                    return;
                }
            }
        }
    }

    private bool IsPunchShield(Effect effect)
    {
        if (effect == null)
            return false;
        foreach (EffectTemplate template in effect.GetEffectTemplates())
        {
            if (template.GetSubEffect() != null)
            {
                SkillTemplate skill = DataManager.SKILL_DATA.GetSkillTemplate(template.GetSubEffect().GetSkillId());
                if (skill.IsProvoked())
                    return true;
            }
        }
        return false;
    }

    public ShieldType GetShieldType()
    {
        return shieldType;
    }
}
