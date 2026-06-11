using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.Commons.Utils;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Items;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Model.Templates.Items.Enums;
using Aion.GameServer.Model.Templates.Npc;
using Aion.GameServer.Skillengine.Change;
using Aion.GameServer.Skillengine.Effects;
using Aion.GameServer.Skillengine.Effects.Modifier;
using Aion.GameServer.Skillengine.Model;
using Aion.GameServer.Utils.Stats;
using ResourceType = Aion.GameServer.Skillengine.Model.EffectReserved.ResourceType;

namespace Aion.GameServer.Controllers.Attack;

/// <summary>Java parity: controllers/attack/AttackUtil (ATracer). Combat math: calculatePhys/MagAttackResult, adjustDamageByStatModifiers (block/parry/crit/fortitude/defense/pvp), additional-hit-count amplification, npc-ai damage mods, calculateSkillResult (full skill damage pipeline: status, weapon attack, skill/bonus dmg, crit, defense, block/parry, shared, pvp, shield), magical-over-time, physical/magical status rolls, cancelCastOn/removeTargetFrom, weapon group/multiplier. CalculationType... varargs->params; ArrayUtils.add/removeElement->local array helpers; switch-arrow/expression->C# switch; instanceof X x->is X x; Func enum (skillengine.change); EffectReserved.ResourceType alias; IllegalArgument->Argument. AttackResult/AttackStatus/StatFunctions/effect templates red-tolerated.</summary>
public class AttackUtil
{
    /// <summary>
    /// Calculates physical attack status + main/off-hand damage, applies stat modifiers, amplifies by hit count.
    /// </summary>
    public static List<AttackResult> CalculatePhysAttackResult(Creature attacker, Creature attacked, params CalculationType[] calculationTypes)
    {
        AttackStatus attackStatus = CalculatePhysicalStatus(attacker, attacked, true, 0, 100, false, false);
        List<AttackResult> attackResultList = StatFunctions.CalculateAttackDamage(attacker, SkillElement.NONE, attackStatus, calculationTypes);
        AdjustDamageByStatModifiers(attacker, attacked, attackStatus, attackResultList, SkillElement.NONE);
        AmplifyDamageByAdditionalHitCount(attacker, attackStatus, attackResultList);
        ModifyDamageByNpcAi(attacker, attacked, attackResultList);
        attacked.GetObserveController().CheckShieldStatus(attackResultList, null, attacker);
        return attackResultList;
    }

    public static void AdjustDamageByStatModifiers(Creature attacker, Creature attacked, AttackStatus status, List<AttackResult> attackResultList, SkillElement element)
    {
        float mainMultiplier = 1;
        float offMultiplier = 1;
        int reduceMax = int.MaxValue;
        float reduceRatio = 0;
        switch (AttackStatus.GetBaseStatus(status))
        {
            case AttackStatus.DODGE:
            case AttackStatus.RESIST:
                return;
            case AttackStatus.BLOCK:
                if (attacked is Player p)
                {
                    Item shield = p.GetEquipment().GetEquippedShield();
                    if (shield != null)
                    {
                        reduceMax = shield.GetItemTemplate().GetWeaponStats().GetReduceMax();
                        reduceRatio = attacked.GetGameStats().GetReverseStat(StatEnum.DAMAGE_REDUCE, 100).GetCurrent() / 100f;
                    }
                }
                else
                {
                    reduceRatio = 10; // NPCs reduce damage by min. 10%. TODO: Implement blocking for npcs without shield + check ratio for different npcs
                }
                break;
            case AttackStatus.PARRY:
                mainMultiplier *= 0.6f;
                offMultiplier *= 0.6f;
                break;
        }

        if (status.IsCritical())
        {
            mainMultiplier = 1.5f;
            if (element == SkillElement.NONE)
            {
                ItemGroup? mainHandGroup = GetWeaponGroup(attacker, true);
                if (mainHandGroup != null)
                {
                    mainMultiplier = GetWeaponMultiplier(mainHandGroup.Value);
                    ItemGroup? offHandGroup = GetWeaponGroup(attacker, false);
                    if (offHandGroup != null)
                    {
                        offMultiplier = GetWeaponMultiplier(offHandGroup.Value);
                    }
                }
            }
            if (attacked is Player)
            {
                int fortitude;
                if (element == SkillElement.NONE)
                { // if stat != null ? why
                    fortitude = attacked.GetGameStats().GetStat(StatEnum.PHYSICAL_CRITICAL_DAMAGE_REDUCE, 0).GetCurrent();
                }
                else
                {
                    fortitude = attacked.GetGameStats().GetStat(StatEnum.MAGICAL_CRITICAL_DAMAGE_REDUCE, 0).GetCurrent();
                }
                mainMultiplier = (mainMultiplier - fortitude / 1000f);
                offMultiplier = (offMultiplier + fortitude / 1000f);
            }
        }

        int maxListIndex = Math.Min(attackResultList.Count, 2);
        if (maxListIndex < attackResultList.Count) // should never happen but log just in case
            NullLoggerFactory.Instance.CreateLogger(nameof(AttackUtil)).LogWarning("attackResultList has more elements than expected (" + attackResultList.Count + ")");
        for (int i = 0; i < maxListIndex; i++)
        {
            float damageMultiplier = i == 0 ? mainMultiplier : offMultiplier;
            bool isPhysical = element == SkillElement.NONE;
            StatEnum attackStat = isPhysical ? StatEnum.PHYSICAL_ATTACK : StatEnum.MAGICAL_ATTACK;
            StatEnum defenseStat = isPhysical ? StatEnum.PHYSICAL_DEFENSE : StatEnum.MAGICAL_DEFEND;
            float defenseBase = isPhysical ? attacked.GetGameStats().GetPDef().GetBase() : attacked.GetGameStats().GetMDef().GetBase();
            float defenseBonus = isPhysical ? attacked.GetGameStats().GetPDef().GetBonus() : attacked.GetGameStats().GetMDef().GetBonus();
            float defense = StatFunctions.AdjustStatByMovementModifier(attacked, defenseStat, defenseBase) + defenseBonus;
            float damage = attackResultList[i].GetDamage() - (defense / 10);
            damage *= damageMultiplier;
            damage = StatFunctions.AdjustStatByMovementModifier(attacker, attackStat, damage);
            if (reduceRatio > 0)
            {
                float dmgToReduce = damage - (damage * reduceRatio);
                if (dmgToReduce > reduceMax)
                {
                    dmgToReduce = reduceMax;
                }
                damage -= dmgToReduce;
            }
            damage = StatFunctions.AdjustDamageByPvpOrPveModifiers(attacker, attacked, damage, 0, false, element);
            if (damage < 1)
            {
                damage = 1;
            }
            attackResultList[i].SetDamage(damage);
        }
    }

    private static int[] CalculateAdditionalHitCount(Creature attacker, AttackStatus status, List<AttackResult> attackList)
    {
        int[] hitCount = new int[2];
        if (attacker is Player p && (status != AttackStatus.DODGE && status != AttackStatus.RESIST))
        {
            Item mainHandWeapon = p.GetEquipment().GetMainHandWeapon();
            if (mainHandWeapon != null)
            {
                hitCount[0] = Rnd.Get(0, mainHandWeapon.GetItemTemplate().GetWeaponStats().GetHitCount()) - 1;
                if (attackList.Count > 1)
                {
                    Item offHandWeapon = p.GetEquipment().GetOffHandWeapon();
                    if (offHandWeapon != null && offHandWeapon.GetItemTemplate().GetItemSubType() != ItemSubType.SHIELD)
                    {
                        hitCount[1] = Rnd.Get(0, offHandWeapon.GetItemTemplate().GetWeaponStats().GetHitCount() - 1);
                    }
                }
            }
        }
        return hitCount;
    }

    private static void AmplifyDamageByAdditionalHitCount(Creature attacker, AttackStatus status, List<AttackResult> attackList)
    {
        int[] hitCount = CalculateAdditionalHitCount(attacker, status, attackList);
        for (int i = 0; i < hitCount[0] + hitCount[1]; i++)
        {
            if (i < hitCount[0])
            { // amplify main hand damage
                if (attackList[0].GetDamage() >= 10)
                    attackList.Add(new AttackResult((int)(attackList[0].GetDamage() * 0.1), AttackStatus.NORMALHIT, attackList[0].GetHitType()));
            }
            else
            { // amplify off hand damage
                if (attackList[1].GetDamage() >= 10)
                    attackList.Add(new AttackResult((int)(attackList[1].GetDamage() * 0.1), AttackStatus.OFFHAND_NORMALHIT, attackList[1].GetHitType()));
            }
        }
    }

    private static void ModifyDamageByNpcAi(Creature attacker, Creature attacked, List<AttackResult> attackStatus)
    {
        if (!(attacker is Npc || attacked is Npc))
            return;
        foreach (AttackResult status in attackStatus)
        {
            float modifiedDamage = status.GetDamage();
            if (attacker is Npc)
                modifiedDamage = attacker.GetAi().ModifyOwnerDamage(modifiedDamage, attacked, null);
            if (attacked is Npc)
                modifiedDamage = attacked.GetAi().ModifyDamage(attacker, modifiedDamage, null);
            status.SetDamage(modifiedDamage);
        }
    }

    private static float CalculateBlockedDamage(Creature attacked, float damage)
    {
        int reduceStat = attacked.GetGameStats().GetReverseStat(StatEnum.DAMAGE_REDUCE, 100).GetCurrent();
        float reduceVal = damage - (damage * reduceStat / 100);
        if (attacked is Player)
        {
            Item shield = ((Player)attacked).GetEquipment().GetEquippedShield();
            if (shield != null)
            {
                int reduceMax = shield.GetItemTemplate().GetWeaponStats().GetReduceMax();
                if (reduceMax > 0 && reduceMax < reduceVal)
                    reduceVal = reduceMax;
            }
        }
        return damage - reduceVal;
    }

    private static float CalculateWeaponCritical(SkillElement element, Creature attacked, float damage, ItemGroup? group, int critAddDmg, StatEnum? stat, bool isMain)
    {
        float coeficient = 1.5f;
        if (element == SkillElement.NONE && group != null)
        {
            coeficient = GetWeaponMultiplier(group.Value);
        }

        if (stat != null && attacked is Player)
        { // Strike Fortitude lowers the crit multiplier
            switch (stat.Value)
            {
                case StatEnum.PHYSICAL_CRITICAL_DAMAGE_REDUCE:
                case StatEnum.MAGICAL_CRITICAL_DAMAGE_REDUCE:
                    int fortitude = attacked.GetGameStats().GetStat(stat.Value, 0).GetCurrent();
                    coeficient = isMain ? (coeficient - fortitude / 1000f) : (coeficient + fortitude / 1000f);
                    break;
            }
        }

        // add critical add dmg
        coeficient += critAddDmg / 100f;
        return damage * coeficient;
    }

    private static float GetWeaponMultiplier(ItemGroup group)
    {
        return group switch
        {
            ItemGroup.DAGGER => 2.3f,
            ItemGroup.SWORD => 2.2f,
            ItemGroup.MACE => 2f,
            ItemGroup.GREATSWORD or ItemGroup.POLEARM => 1.8f,
            ItemGroup.STAFF or ItemGroup.BOW => 1.7f,
            _ => 1.5f,
        };
    }

    public static void CalculateSkillResult(Effect effect, int skillDamage, DamageEffect template, bool ignoreShield)
    {
        Creature effector = effect.GetEffector();
        Creature effected = effect.GetEffected();
        // define values
        ActionModifier modifier = template.GetActionModifiers(effect);
        SkillElement element = template.GetElement();
        int randomDamageType = template is SkillAttackInstantEffect skillAttackInstantEffect ? skillAttackInstantEffect.GetRnddmg() : 0;
        bool useTemplateDmg = IsUseTemplateDmg(effect, template);
        bool send = !(template is DelayedSpellAttackInstantEffect) && !(template is ProcAtkInstantEffect);
        bool shouldIncreaseByOneTimeBoost = !(template is ProcAtkInstantEffect);

        AttackStatus status = element switch
        {
            SkillElement.NONE => CalculatePhysicalStatus(effector, effected, template, effect.GetSkillLevel()),
            _ => CalculateMagicalStatus(effector, effected, template.GetCritProbMod2(), true, effect.GetSkillTemplate().IsMcritApplied()),
        };

        int baseAttack = 0;
        float bonus = 0;
        HitType ht = HitType.PHHIT;
        List<AttackResult> weaponAttack = new List<AttackResult>();
        float damage = 0;
        CalculationType[] calculationTypes = new CalculationType[] { CalculationType.SKILL };
        if (effector is Player p && p.GetEquipment().IsDualWeaponEquipped())
            calculationTypes = ArrayAdd(calculationTypes, CalculationType.DUAL_WIELD);
        if (!useTemplateDmg)
        {
            if (effector is SummonedObject && !(effector is Servant))
            {
                ht = effect.GetSkillType() == SkillType.MAGICAL ? HitType.MAHIT : HitType.PHHIT;
                baseAttack = effector.GetGameStats().GetMainHandPAttack(calculationTypes).GetBase();
                weaponAttack = StatFunctions.CalculateAttackDamage(effect.GetEffector(), SkillElement.NONE, status, calculationTypes);
            }
            else
            {
                switch (effect.GetSkillType())
                {
                    case SkillType.MAGICAL:
                        ht = HitType.MAHIT;
                        baseAttack = effector.GetGameStats().GetMainHandMAttack(calculationTypes).GetBase();
                        if (baseAttack == 0 && effector.GetAttackType() == ItemAttackType.PHYSICAL)
                        { // dirty fix for staffs and maces -.-
                            calculationTypes = ArrayAdd(calculationTypes, CalculationType.APPLY_POWER_SHARD_DAMAGE);
                            if (element == SkillElement.NONE)
                            { // fix for magical skills which actually inflict physical damage
                                calculationTypes = ArrayAdd(calculationTypes, CalculationType.REMOVE_POWER_SHARD);
                                weaponAttack = StatFunctions.CalculateAttackDamage(effect.GetEffector(), SkillElement.NONE, status, calculationTypes);
                                calculationTypes = ArrayRemoveElement(calculationTypes, CalculationType.REMOVE_POWER_SHARD); // remove to prevent power shards being removed again in baseAttack calculation
                            }
                            else
                            {
                                calculationTypes = ArrayAdd(calculationTypes, CalculationType.REMOVE_POWER_SHARD);
                            }
                            baseAttack = effector.GetGameStats().GetMainHandPAttack(calculationTypes).GetBase();
                        }
                        break;
                    default:
                        if (element == SkillElement.NONE)
                        {
                            calculationTypes = ArrayAdd(calculationTypes, CalculationType.APPLY_POWER_SHARD_DAMAGE);
                            baseAttack = effector.GetGameStats().GetMainHandPAttack(calculationTypes).GetBase();
                            calculationTypes = ArrayAdd(calculationTypes, CalculationType.REMOVE_POWER_SHARD);
                            weaponAttack = StatFunctions.CalculateAttackDamage(effect.GetEffector(), SkillElement.NONE, status, calculationTypes);
                        }
                        else
                        {
                            baseAttack = effector.GetGameStats().GetMainHandMAttack(calculationTypes).GetBase();
                        }
                        break;
                }
            }
        }
        foreach (AttackResult res in weaponAttack)
        {
            damage += res.GetExactDamage();
        }
        // add skill damage
        switch (template.GetMode())
        {
            case Func.ADD:
                damage += skillDamage;
                break;
            case Func.PERCENT:
                damage += baseAttack * skillDamage / 100f;
                break;
        }

        // add bonus damage
        if (modifier != null)
        {
            bonus = modifier.Analyze(effect);
            switch (modifier.GetFunc())
            {
                case Func.ADD:
                    break;
                case Func.PERCENT:
                    bonus = baseAttack * bonus / 100f;
                    break;
            }
        }

        bool isPhysical = element == SkillElement.NONE;
        if (!useTemplateDmg)
        {
            float damageMultiplier;
            if (isPhysical)
            {
                damageMultiplier = effector.GetObserveController().GetBasePhysicalDamageMultiplier(true);
                damage += bonus;
            }
            else
            {
                damageMultiplier = shouldIncreaseByOneTimeBoost ? effector.GetObserveController().GetBaseMagicalDamageMultiplier() : 1f;
                damage = StatFunctions.CalculateMagicalSkillDamage(effector, effected, damage, (int)bonus, template, true, true);
            }
            if (template.ShouldApplyAttackerMovementModifier())
            {
                damage = StatFunctions.AdjustStatByMovementModifier(effector, isPhysical ? StatEnum.PHYSICAL_ATTACK : StatEnum.MAGICAL_ATTACK, damage);
            }
            damage *= damageMultiplier;
        }

        if (randomDamageType > 0)
            damage = RandomizeDamage(randomDamageType, damage);

        if (status.IsCritical())
        {
            int critAddDmg = template.CalculateCritAddDmg(effect);
            StatEnum stat = element == SkillElement.NONE ? StatEnum.PHYSICAL_CRITICAL_DAMAGE_REDUCE : StatEnum.MAGICAL_CRITICAL_DAMAGE_REDUCE;
            damage = CalculateWeaponCritical(element, effected, damage, GetWeaponGroup(effector, true), critAddDmg, stat, true);
        }

        if (isPhysical)
        {
            float def = effected.GetGameStats().GetPDef().GetBonus() + StatFunctions.AdjustStatByMovementModifier(effected, StatEnum.PHYSICAL_DEFENSE,
                    effected.GetGameStats().GetPDef().GetBase());
            damage -= def / 10;
        }

        switch (AttackStatus.GetBaseStatus(status))
        {
            case AttackStatus.BLOCK:
                damage = CalculateBlockedDamage(effected, damage);
                break;
            case AttackStatus.PARRY:
                damage *= 0.6f;
                break;
        }

        if (effector is Npc)
        {
            damage = effector.GetAi().ModifyOwnerDamage(damage, effected, effect);
        }

        if (effect.GetSkill() != null && effect.GetSkill().GetEffectedList().Count > 1 && template.IsShared())
        {
            damage /= effect.GetSkill().GetEffectedList().Count;
        }
        damage = StatFunctions.AdjustDamageByPvpOrPveModifiers(effector, effected, damage, effect.GetPvpDamage(), useTemplateDmg, element);

        if (damage < 0)
            damage = 0;

        if (effected is Npc)
        {
            damage = effected.GetAi().ModifyDamage(effector, damage, effect);
        }
        CalculateEffectResult(effect, effected, (int)damage, status, ht, ignoreShield, template.GetPosition(), send);
    }

    private static bool IsUseTemplateDmg(Effect effect, EffectTemplate template)
    {
        if (template is NoReduceSpellATKInstantEffect)
            return true;
        if (template is ProcAtkInstantEffect && effect.GetSkillTemplate().IsProvoked() || effect.GetStack().StartsWith("IDEVENT"))
        { // proc effects of skills like 8583
            // TODO: find pattern or extract <apply_magical_skill_boost_bonus> and <apply_magical_critical> from server files. What about missing ones?
            switch (effect.GetStack().ToLower())
            {
                case "nwi_delayspell_dd_proca_tal":
                case "ngu_vritra_delayspell_dd_proca_tal":
                case "sgfi_procts_air":
                case "ab1_artifact_hellfire":
                case "ldf4b_c3_artifact_tiamat_delayatk":
                case "ldf4b_t4_artifact_crystal_dd":
                case "ldf4b_t3_artifact_fury_dd":
                case "ldf4b_t2_artifact_gravity_openaerial":
                case "ldf4b_t2_artifact_gravity_dd":
                case "ldf4b_t1_artifact_crack_stumble_mpatk":
                case "ldf4b_t1_artifact_crack_dd":
                case "idtiamat_tahabata_adddmgtobleed":
                case "kn_turnaggressiveeffect":
                case "tiamatdown_tiamatagent_bomb":
                case "idtiamat_thor_procatk":
                case "idyun_vasharti_refdmg_red":
                case "idyun_vasharti_refdmg_blue":
                case "ldf4b_d3_buff_poison_proc":
                case "ldf4b_tatar_procatk":
                case "idforest_wave_trico_proclight":
                case "idevent01_areadot":
                    return true;
            }
        }
        return false;
    }

    private static float RandomizeDamage(int randomDamageType, float damage)
    {
        float multiplier;
        switch (randomDamageType)
        {
            case 1:
                {
                    int roll = Rnd.Get(0, 19);
                    multiplier = roll <= 6 ? 0.5f :
                        roll <= 12 ? 1.0f :
                            1.5f;
                    break;
                }
            case 2:
                multiplier = Rnd.Chance() < 70.0f ? 0.6f : 2.0f;
                break;
            case 3:
                {
                    int roll = Rnd.Get(0, 19);
                    multiplier = roll <= 6 ? 0.9f :
                        roll <= 12 ? 1.0f :
                            1.1f;
                    break;
                }
            case 6:
                multiplier = Rnd.Chance() < 70.0f ? 1.0f : 2.0f;
                break;
            case 4:
            case 5:
            case 7:
            case 8:
            case 9:
            case 10:
                multiplier = 1.0f;
                break;
            default:
                throw new ArgumentException(
                    "Unhandled random damage type rnddmg=\"" + randomDamageType + "\"");
        }

        return damage * multiplier;
    }

    private static void CalculateEffectResult(Effect effect, Creature effected, int damage, AttackStatus status, HitType hitType, bool ignoreShield,
        int position, bool send)
    {
        AttackResult attackResult = new AttackResult(damage, status, hitType);
        if (!ignoreShield)
        {
            effected.GetObserveController().CheckShieldStatus(new List<AttackResult> { attackResult }, effect, effect.GetEffector());
            effect.SetReflectedDamage(attackResult.GetReflectedDamage());
            effect.SetReflectedSkillId(attackResult.GetReflectedSkillId());
            effect.SetMpAbsorbed(attackResult.GetMpAbsorbed());
            effect.SetMpShieldSkillId(attackResult.GetMpShieldSkillId());
            effect.SetProtectedDamage(attackResult.GetProtectedDamage());
            effect.SetProtectedSkillId(attackResult.GetProtectedSkillId());
            effect.SetProtectorId(attackResult.GetProtectorId());
            effect.SetShieldDefense(attackResult.GetShieldType());
        }
        effect.SetReserveds(new EffectReserved(position, attackResult.GetDamage(), ResourceType.HP, true, send), false);
        effect.SetAttackStatus(attackResult.GetAttackStatus());
        effect.SetLaunchSubEffect(attackResult.IsLaunchSubEffect());
    }

    /// <summary>
    /// Calculates magical attack status + damage, applies stat modifiers, amplifies by hit count.
    /// </summary>
    public static List<AttackResult> CalculateMagAttackResult(Creature attacker, Creature attacked, SkillElement element, params CalculationType[] calculationTypes)
    {
        AttackStatus attackStatus = CalculateMagicalStatus(attacker, attacked, 100, false, true);
        List<AttackResult> attackResultList = StatFunctions.CalculateAttackDamage(attacker, element, attackStatus, calculationTypes);
        AdjustDamageByStatModifiers(attacker, attacked, attackStatus, attackResultList, element);
        AmplifyDamageByAdditionalHitCount(attacker, attackStatus, attackResultList);
        ModifyDamageByNpcAi(attacker, attacked, attackResultList);
        attacked.GetObserveController().CheckShieldStatus(attackResultList, null, attacker);
        return attackResultList;
    }

    public static int CalculateMagicalOverTimeSkillResult(Effect effect, float skillDamage, EffectTemplate template, bool useMagicBoost)
    {
        Creature effector = effect.GetEffector();
        Creature effected = effect.GetEffected();
        float damage;

        if (effector is Trap)
        {
            damage = skillDamage;
        }
        else
        {
            float damageMultiplier = effector.GetObserveController().GetBaseMagicalDamageMultiplier();
            damage = StatFunctions.CalculateMagicalSkillDamage(effector, effected, skillDamage, 0, template, useMagicBoost, false);
            damage = damage * damageMultiplier;

            AttackStatus status = effect.GetAttackStatus();
            // calculate attack status only if it has not been forced already
            if (status == AttackStatus.NORMALHIT && template.GetPosition() == 1)
                status = CalculateMagicalStatus(effector, effected, template.GetCritProbMod2(), true, effect.GetSkillTemplate().IsMcritApplied());
            if (status == AttackStatus.CRITICAL)
            {
                int critAddDmg = template.CalculateCritAddDmg(effect);
                damage = CalculateWeaponCritical(template.GetElement(), effected, damage, GetWeaponGroup(effector, true), critAddDmg,
                    StatEnum.MAGICAL_CRITICAL_DAMAGE_REDUCE, true);
            }
            damage = StatFunctions.AdjustDamageByPvpOrPveModifiers(effector, effected, damage, effect.GetPvpDamage(), false, template.GetElement());
        }

        if (damage < 1)
            damage = 1;

        if (effected is Npc)
            damage = effected.GetAi().ModifyDamage(effector, damage, effect);

        return (int)damage;
    }

    private static AttackStatus CalculatePhysicalStatus(Creature attacker, Creature attacked, EffectTemplate template, int skillLevel)
    {
        int accMod = template.GetAccMod2() + template.GetAccMod1() * skillLevel;
        bool cannotMiss = template is SkillAttackInstantEffect skillAttackInstantEffect && skillAttackInstantEffect.IsCannotmiss();
        return CalculatePhysicalStatus(attacker, attacked, true, accMod, template.GetCritProbMod2(), true, cannotMiss);
    }

    private static AttackStatus CalculatePhysicalStatus(Creature attacker, Creature attacked, bool isMainHand, int accMod, int criticalProb,
        bool isSkill, bool cannotMiss)
    {
        AttackStatus status = AttackStatus.NORMALHIT;

        if (!cannotMiss)
        {
            if (!isSkill && StatFunctions.CheckIsDodgedHit(attacker, attacked, accMod))
                status = AttackStatus.DODGE;
            else if (attacked is Player player && player.GetEquipment().IsShieldEquipped()
                && StatFunctions.CheckIsBlockedHit(attacker, attacked, accMod))
                status = AttackStatus.BLOCK;
            else if (attacked is Player && StatFunctions.CheckIsParriedHit(attacker, attacked, accMod))
                status = AttackStatus.PARRY;
        }
        else
        {
            StatFunctions.CheckIsDodgedHit(attacker, attacked, accMod);
            StatFunctions.CheckIsBlockedHit(attacker, attacked, accMod);
            StatFunctions.CheckIsParriedHit(attacker, attacked, accMod);
        }
        if (StatFunctions.CheckIsPhysicalCriticalHit(attacker, attacked, isMainHand, criticalProb, isSkill))
        {
            status = status switch
            {
                AttackStatus.BLOCK => AttackStatus.CRITICAL_BLOCK,
                AttackStatus.PARRY => AttackStatus.CRITICAL_PARRY,
                AttackStatus.DODGE => AttackStatus.CRITICAL_DODGE,
                _ => AttackStatus.CRITICAL,
            };
        }
        return isMainHand ? status : AttackStatus.GetOffHandStats(status);
    }

    /// <summary>
    /// Every +100 delta of (MR - MA) = +10% to resist; if the difference is 1000 = 100% resist
    /// </summary>
    public static AttackStatus CalculateMagicalStatus(Creature attacker, Creature attacked, int criticalProb, bool isSkill, bool applyMcrit)
    {
        if (!isSkill)
        {
            if (Rnd.Get(1, 1000) <= StatFunctions.CalculateMagicalResistRate(attacker, attacked, 0, SkillElement.NONE))
                return AttackStatus.RESIST;
        }

        if (StatFunctions.CalculateMagicalCriticalRate(attacker, attacked, criticalProb, applyMcrit))
        {
            return AttackStatus.CRITICAL;
        }

        return AttackStatus.NORMALHIT;
    }

    public static void CancelCastOn(Creature target)
    {
        target.GetKnownList().ForEachObject(visibleObject =>
        {
            if (visibleObject is Creature creature && visibleObject.GetTarget() == target)
            {
                if (creature.GetCastingSkill() != null && creature.GetCastingSkill().GetFirstTarget().Equals(target))
                    creature.GetController().CancelCurrentSkill(null);
            }
        });
    }

    /// <summary>
    /// Send a packet to everyone who is targeting creature.
    /// </summary>
    public static void RemoveTargetFrom(Creature obj)
    {
        RemoveTargetFrom(obj, false);
    }

    public static void RemoveTargetFrom(Creature obj, bool validateSee)
    {
        obj.GetKnownList().ForEachPlayer(player =>
        {
            if (player.GetTarget() == obj && (!validateSee || !player.CanSee(obj)))
                player.SetTarget(null);
        });
    }

    private static ItemGroup? GetWeaponGroup(Creature effector, bool mainHand)
    {
        if (effector is Player)
        {
            Item weapon = mainHand ? ((Player)effector).GetEquipment().GetMainHandWeapon() : ((Player)effector).GetEquipment().GetOffHandWeapon();
            if (weapon != null)
            {
                return weapon.GetItemTemplate().GetItemGroup();
            }
        }
        else if (effector is Npc)
        {
            NpcTemplate temp = DataManager.NPC_DATA.GetNpcTemplate(((Npc)effector).GetNpcId());
            NpcEquippedGear npcGear = temp.GetEquipment();
            if (npcGear != null && npcGear.GetItem(mainHand ? ItemSlot.MAIN_HAND : ItemSlot.MAIN_OFF_HAND) != null)
            {
                return npcGear.GetItem(mainHand ? ItemSlot.MAIN_HAND : ItemSlot.MAIN_OFF_HAND).GetItemGroup();
            }
        }
        return null;
    }

    // org.apache.commons.lang3.ArrayUtils.add -> append element to a new array
    private static T[] ArrayAdd<T>(T[] array, T element)
    {
        T[] result = new T[array.Length + 1];
        Array.Copy(array, result, array.Length);
        result[array.Length] = element;
        return result;
    }

    // org.apache.commons.lang3.ArrayUtils.removeElement -> remove first occurrence into a new array (clone if absent)
    private static T[] ArrayRemoveElement<T>(T[] array, T element)
    {
        int idx = Array.IndexOf(array, element);
        if (idx < 0)
            return (T[])array.Clone();
        T[] result = new T[array.Length - 1];
        Array.Copy(array, 0, result, 0, idx);
        Array.Copy(array, idx + 1, result, idx, array.Length - idx - 1);
        return result;
    }
}
