using System;
using System.Collections.Generic;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Model.Templates.Item;
using Aion.GameServer.Model.Templates.Item.Enums;
using Aion.GameServer.Model.Templates.Npc;
using Aion.GameServer.SkillEngine.Effect;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.World;

namespace Aion.GameServer.Utils.Stats;

/// <summary>
/// Calculations based on aionsource character-stats research.
/// Java parity: utils/stats/StatFunctions (@author ATracer, alexa026, Neon).
/// </summary>
public class StatFunctions
{
    // Java parity helper: Math.round(float)->int and Math.round(double)->long use floor(x+0.5), not banker's rounding.
    private static int JRound(float a) => (int)Math.Floor(a + 0.5f);
    private static long JRound(double a) => (long)Math.Floor(a + 0.5d);

    /// <summary>
    /// XP reward from target. <paramref name="maxLevelInRange"/> is the reward receiver level (solo) or max player level in range (group).
    /// </summary>
    public static long CalculateExperienceReward(int maxLevelInRange, Npc target)
    {
        WorldMapInstance instance = target.GetPosition().GetWorldMapInstance();
        int baseXP = CalculateBaseExp(target);
        float mapMulti = instance.GetInstanceHandler().GetExpMultiplier(); // map modifier to approach retail exp values
        if (instance.GetParent().IsInstanceType() && instance.GetMaxPlayers() >= 2 && instance.GetMaxPlayers() <= 6)
        {
            mapMulti *= instance.GetMaxPlayers(); // on retail you get mob EP * max instance member count (only for group instances)
            mapMulti /= RatesConfig.XP_SOLO_RATES[0]; // custom: divide by regular xp rates, so they will not affect the rewarded XP
        }
        int xpPercentage = XPRewardEnum.XpRewardFrom(target.GetLevel() - maxLevelInRange);
        long rewardXP = JRound(baseXP * mapMulti * (xpPercentage / 100f));
        return rewardXP;
    }

    /// <summary>
    /// Experience value identical to the ones seen on aion databases (but seen retail exp rewards are always higher).
    /// </summary>
    private static int CalculateBaseExp(Npc npc)
    {
        int maxHp = npc.GetObjectTemplate().GetStatsTemplate().GetMaxHp();
        if (maxHp <= 0)
            return 0;
        float multiplier;
        switch (npc.GetRating())
        {
            case NpcRating.JUNK:
                multiplier = 2f;
                break;
            case NpcRating.NORMAL:
                multiplier = 2.2f;
                break;
            case NpcRating.ELITE:
                multiplier = 4f;
                break;
            case NpcRating.HERO:
                multiplier = 5.4f;
                break;
            case NpcRating.LEGENDARY:
                multiplier = 6.4f;
                break;
            default:
                throw new ArgumentException("Could not calculate experience reward for " + npc + " due to unknown rating.");
        }
        multiplier += (int)npc.GetRank() * 0.2f;
        return JRound(maxHp * multiplier);
    }

    /// <summary>DP reward from target.</summary>
    public static int CalculateDPReward(Player player, Creature target)
    {
        int playerLevel = player.GetCommonData().GetLevel();
        int targetLevel = target.GetLevel();
        NpcRating npcRating = ((Npc)target).GetObjectTemplate().GetRating();

        // TODO: fix to see monster Rating level, NORMAL lvl 1, 2 | ELITE lvl 1, 2 etc..
        int baseDP = targetLevel * CalculateRatingMultiplier(npcRating);
        int xpPercentage = XPRewardEnum.XpRewardFrom(targetLevel - playerLevel);
        return Rates.DP_PVE.CalcResult(player, (int)Math.Floor((double)(baseDP * xpPercentage / 100f)));
    }

    /// <summary>AP reward.</summary>
    public static int CalculatePvEApGained(Player player, Creature target)
    {
        if (player.GetCommonData().GetLevel() - target.GetLevel() > 10)
            return 1;

        float apNpcRate = GetApNpcRating(((Npc)target).GetObjectTemplate().GetRating());

        // TODO: find out why they give 1/4 AP base(normal NpcRate) (5 AP retail)
        if (target.GetName().Equals("flame hoverstone"))
            apNpcRate = 0.5f;

        return Rates.AP_PVE.CalcResult(player, (int)Math.Floor((double)(15 * apNpcRate)));
    }

    /// <summary>Points lost in PvP death.</summary>
    public static int CalculatePvPApLost(Player defeated, Player winner)
    {
        int pointsLost = defeated.GetAbyssRank().GetRank().GetPointsLost();

        // Level penalty calculation
        int difference = winner.GetLevel() - defeated.GetLevel();

        if (difference >= 5)
            pointsLost = JRound(pointsLost * 0.1f);
        else if (difference == 4)
            pointsLost = JRound(pointsLost * 0.65f);
        else if (difference == 3)
            pointsLost = JRound(pointsLost * 0.85f);

        return Rates.AP_PVP_LOST.CalcResult(defeated, pointsLost);
    }

    /// <summary>Points gained in PvP kill.</summary>
    public static int CalculatePvpApGained(Player defeated, int winnerAbyssRank, int maxLevel)
    {
        int pointsGained = defeated.GetAbyssRank().GetRank().GetPointsGained();

        // Level penalty calculation
        int difference = maxLevel - defeated.GetLevel();

        if (difference > 4)
        {
            pointsGained = JRound(pointsGained * 0.1f);
        }
        else if (difference < -3)
        {
            pointsGained = JRound(pointsGained * 1.3f);
        }
        else
        {
            switch (difference)
            {
                case 3:
                    pointsGained = JRound(pointsGained * 0.85f);
                    break;
                case 4:
                    pointsGained = JRound(pointsGained * 0.65f);
                    break;
                case -2:
                    pointsGained = JRound(pointsGained * 1.1f);
                    break;
                case -3:
                    pointsGained = JRound(pointsGained * 1.2f);
                    break;
            }
        }

        // Abyss rank penalty calculation
        int defeatedAbyssRank = defeated.GetAbyssRank().GetRank().GetId();
        int abyssRankDifference = winnerAbyssRank - defeatedAbyssRank;

        if (winnerAbyssRank <= 7 && abyssRankDifference > 0)
        {
            float penaltyPercent = abyssRankDifference * 0.05f;

            pointsGained -= JRound(pointsGained * penaltyPercent);
        }

        return pointsGained;
    }

    /// <summary>XP points gained in PvP kill. TODO: Find the correct formula.</summary>
    public static int CalculatePvpXpGained(Player defeated, int winnerAbyssRank, int maxLevel)
    {
        int pointsGained = 5000;

        // Level penalty calculation
        int difference = maxLevel - defeated.GetLevel();

        if (difference > 4)
        {
            pointsGained = JRound(pointsGained * 0.1f);
        }
        else if (difference < -3)
        {
            pointsGained = JRound(pointsGained * 1.3f);
        }
        else
        {
            switch (difference)
            {
                case 3:
                    pointsGained = JRound(pointsGained * 0.85f);
                    break;
                case 4:
                    pointsGained = JRound(pointsGained * 0.65f);
                    break;
                case -2:
                    pointsGained = JRound(pointsGained * 1.1f);
                    break;
                case -3:
                    pointsGained = JRound(pointsGained * 1.2f);
                    break;
            }
        }

        // Abyss rank penalty calculation
        int defeatedAbyssRank = defeated.GetAbyssRank().GetRank().GetId();
        int abyssRankDifference = winnerAbyssRank - defeatedAbyssRank;

        if (winnerAbyssRank <= 7 && abyssRankDifference > 0)
        {
            float penaltyPercent = abyssRankDifference * 0.05f;

            pointsGained -= JRound(pointsGained * penaltyPercent);
        }

        return pointsGained;
    }

    public static int CalculatePvpDpGained(Player defeated, int maxRank, int maxLevel)
    {
        int pointsGained;

        // base values
        int baseDp = 1064;
        int dpPerRank = 57;

        // adjust by rank
        pointsGained = (defeated.GetAbyssRank().GetRank().GetId() - maxRank) * dpPerRank + baseDp;

        // adjust by level
        pointsGained = StatFunctions.AdjustPvpDpGained(pointsGained, defeated.GetLevel(), maxLevel);

        return pointsGained;
    }

    public static int AdjustPvpDpGained(int points, int defeatedLvl, int killerLvl)
    {
        int pointsGained = points;

        int difference = killerLvl - defeatedLvl;
        // adjust by level
        if (difference >= 10)
            pointsGained = 0;
        else if (difference >= 0)
            pointsGained = (int)(pointsGained - pointsGained * difference * 0.1);
        else if (difference <= -10)
            pointsGained = (int)(pointsGained * 1.1);
        else
            pointsGained = (int)(pointsGained + pointsGained * Math.Abs(difference) * 0.01);

        return pointsGained;
    }

    /// <summary>Applies BOOST_HATE modifiers from equipment and buffs.</summary>
    public static int CalculateHate(Creature creature, int value)
    {
        int boostHate = creature.GetGameStats().GetStat(StatEnum.BOOST_HATE, 100).GetCurrent();
        return (int)((long)value * (1000 + boostHate) / 1000);
    }

    public static List<AttackResult> CalculateAttackDamage(Creature attacker,
        SkillElement element, AttackStatus status, params CalculationType[] calculationTypes)
    {
        List<AttackResult> attackResultList = new List<AttackResult>();
        if (AttackStatusExtensions.GetBaseStatus(status) == AttackStatus.DODGE || AttackStatusExtensions.GetBaseStatus(status) == AttackStatus.RESIST)
        {
            attackResultList.Add(new AttackResult(0, AttackStatusExtensions.GetBaseStatus(status)));
            return attackResultList;
        }
        Stat2 mainHandAttack;
        Stat2 offHandAttack = null;
        HitType hitType = HitType.PHHIT;
        if (element == SkillElement.NONE)
        {
            mainHandAttack = attacker.GetGameStats().GetMainHandPAttack(calculationTypes);
            if (attacker is Player p)
                offHandAttack = p.GetGameStats().GetOffHandPAttack(calculationTypes);
        }
        else
        {
            hitType = HitType.MAHIT;
            mainHandAttack = attacker.GetGameStats().GetMainHandMAttack(calculationTypes);
            if (attacker is Player p)
                offHandAttack = p.GetGameStats().GetOffHandMAttack(calculationTypes);
        }

        if (attacker is Player player)
        {
            Equipment equipment = player.GetEquipment();
            Item mainHandWeapon = equipment.GetMainHandWeapon();
            if (mainHandWeapon != null)
            {
                Item offHandWeapon = equipment.GetOffHandWeapon();
                WeaponStats mainWeaponStats = mainHandWeapon.GetItemTemplate().GetWeaponStats();
                WeaponStats offWeaponStats = (offHandWeapon == null || offHandWeapon.GetItemTemplate().GetItemSubType() == ItemSubType.SHIELD)
                    ? null : offHandWeapon.GetItemTemplate().GetWeaponStats();
                if (mainWeaponStats != null)
                {
                    float mainHandDamage = mainHandAttack.GetExactCurrent();
                    float offHandDamage = offHandAttack.GetExactCurrent();
                    if (Array.IndexOf(calculationTypes, CalculationType.SKILL) >= 0)
                    { // 80% of damage is added on retail
                        if (offWeaponStats != null)
                        {
                            float totalBaseDamage = (offHandAttack.GetExactBaseWithoutBaseRate() * player.GetGameStats().GetSkillEfficiency() + mainHandAttack.GetExactBaseWithoutBaseRate()) * 0.8f;
                            mainHandDamage = (mainHandAttack.GetExactCurrentWithoutFixedBonus() + totalBaseDamage * offHandAttack.GetFixedBonusRate()) * 0.8f;
                            offHandDamage = (offHandAttack.GetExactCurrentWithoutFixedBonus() + totalBaseDamage * mainHandAttack.GetFixedBonusRate()) * 0.8f * player.GetGameStats().GetSkillEfficiency();
                        }
                    }
                    else
                    {
                        if (Rnd.NextInt(1000) >= player.GetGameStats().GetMaxDamageChance())
                        {
                            offHandDamage *= player.GetGameStats().GetMinDamageRatio();
                            if (offHandDamage <= 0 && offWeaponStats != null)
                            {
                                offHandDamage = 1;
                            }
                        }
                    }
                    attackResultList.Add(new AttackResult(mainHandDamage, status, hitType));
                    if (offWeaponStats != null)
                        attackResultList.Add(new AttackResult(offHandDamage, AttackStatusExtensions.GetOffHandStats(status), hitType));
                }
            }
            else
            { // Attack without weapon
                // "no weapon" damage has a power of 70, whereas weapons have their own power
                // TODO: parse values, but for now we can ignore it since most player weapons have a power of 100
                float damage = Rnd.Get(16, 20) * (1 + ((player.GetGameStats().GetPower().GetCurrent() - 100) / 100f * 70f) / 100f) + mainHandAttack.GetBonus();
                attackResultList.Add(new AttackResult(damage, status, hitType));
            }
        }
        else
        {
            int val = attacker is Homing ? 100 : Rnd.Get(80, 120);
            attackResultList.Add(new AttackResult(mainHandAttack.GetCurrent() * val / 100f, status, hitType));
        }
        return attackResultList;
    }

    /// <summary>Applies elemental defense to incoming damage (reduces linearly using a level-scaled denominator).</summary>
    private static float ReduceDamageByElementalDefense(Creature effector, Creature attacked, SkillElement element, float damage)
    {
        float elementalDenominator = GetElementalDefenseDenominator(effector, attacked);
        int rawDefense = attacked.GetGameStats().GetElementalDefenseFor(element);
        int adjustedDefense = (int)AdjustStatByMovementModifier(attacked, element.GetStatForElement(), rawDefense);
        adjustedDefense = StatCapUtil.ClampStatValue(element.GetStatForElement(), attacked, adjustedDefense);
        return damage * (1f - adjustedDefense / elementalDenominator);
    }

    private static int GetElementalDefenseDenominator(Creature effector, Creature attacked)
    {
        if (attacked is Player)
        {
            int level = Math.Min(effector.GetLevel(), attacked.GetLevel());
            return StatCapUtil.GetElementalDefenseBaseValue() + Math.Max(0, level - 50) * 10;
        }
        return StatCapUtil.GetElementalDefenseBaseValue();
    }

    public static float CalculateMagicalSkillDamage(Creature effector, Creature target, float baseDamage, int bonus, EffectTemplate template,
        bool useMagicBoost, bool useKnowledge)
    {
        float damage = baseDamage;
        if (!(template is NoReduceSpellATKInstantEffect))
        {
            var sgs = effector.GetGameStats();
            var tgs = target.GetGameStats();
            float magicBoost = useMagicBoost ? sgs.GetMBoost().GetCurrent() : 0;
            magicBoost -= effector is Trap ? 0 : tgs.GetMBResist().GetCurrent();
            magicBoost = (int)Math.Max(0, Limit(StatEnum.BOOST_MAGICAL_SKILL, magicBoost));
            float knowledge = useKnowledge ? sgs.GetKnowledge().GetCurrent() : 100; // this line might be wrong now
            damage *= (1 + (magicBoost / (knowledge * 10)));
            damage = sgs.GetStat(StatEnum.BOOST_SPELL_ATTACK, (int)damage).GetCurrent();
        }

        // add bonus damage
        damage += bonus;
        if (template.GetElement() != SkillElement.NONE && !(template is NoReduceSpellATKInstantEffect))
        {
            damage = ReduceDamageByElementalDefense(effector, target, template.GetElement(), damage);
            // damage is reduced by 100 per 1000 mdef
            damage -= AdjustStatByMovementModifier(target, StatEnum.MAGICAL_DEFEND, target.GetGameStats().GetMDef().GetCurrent()) / 10f;
        }

        if (damage < 0)
        {
            damage = 0;
        }
        else if (effector is Npc && !(effector is SummonedObject))
        {
            int rnd = (int)(damage * 0.08f);
            damage += Rnd.Get(-rnd, rnd);
        }

        return damage;
    }

    /// <summary>Calculates MAGICAL CRITICAL chance.</summary>
    public static bool CalculateMagicalCriticalRate(Creature attacker, Creature attacked, int criticalProb, bool applyMcrit)
    {
        if (attacker is Servant || attacker is Homing || !applyMcrit)
            return false;

        float critical = attacker.GetGameStats().GetMCritical().GetCurrent() - attacked.GetGameStats().GetMCR().GetCurrent();
        // add critical Prob
        if (criticalProb != 100)
        {
            if (critical <= 0)
                critical = 1;
            critical *= criticalProb / 100f;
        }

        return Rnd.NextInt(1000) < Limit(StatEnum.MAGICAL_CRITICAL, critical);
    }

    public static int CalculateRatingMultiplier(NpcRating npcRating)
    {
        // FIXME: to correct formula, have any reference?
        switch (npcRating)
        {
            case NpcRating.JUNK:
            case NpcRating.NORMAL:
                return 2;
            case NpcRating.ELITE:
                return 3;
            case NpcRating.HERO:
                return 4;
            case NpcRating.LEGENDARY:
                return 5;
            default:
                return 1;
        }
    }

    public static int GetApNpcRating(NpcRating npcRating)
    {
        switch (npcRating)
        {
            case NpcRating.JUNK:
                return 1;
            case NpcRating.NORMAL:
                return 2;
            case NpcRating.ELITE:
                return 4;
            case NpcRating.HERO:
                return 35;// need check
            case NpcRating.LEGENDARY:
                return 2500;// need check
            default:
                return 1;
        }
    }

    /// <summary>Returns adjusted damage according to PVE or PVP modifiers.</summary>
    public static float AdjustDamageByPvpOrPveModifiers(Creature attacker, Creature target, float baseDamage, int pvpDamage, bool useTemplateDmg,
        SkillElement element)
    {
        int attackBonus = 0;
        int defenseBonus = 0;
        float damage = baseDamage;
        bool pvpTarget = attacker.IsPvpTarget(target);
        if (pvpTarget)
        {
            if (pvpDamage > 0)
                damage *= pvpDamage * 0.01f;
            damage *= 0.42f; // PVP modifier 42%, last checked on NA (4.9) 19.03.2016
            if (!useTemplateDmg)
            {
                attackBonus = attacker.GetGameStats().GetStat(StatEnum.PVP_ATTACK_RATIO, 0).GetCurrent();
                defenseBonus = target.GetGameStats().GetStat(StatEnum.PVP_DEFEND_RATIO, 0).GetCurrent();
                if (attacker.GetRace() != target.GetRace() && !attacker.IsInInstance())
                    attackBonus += Aion.GameServer.Model.Siege.Influence.GetInstance().GetPvpRaceBonusRatio(attacker.GetRace());
                switch (element)
                {
                    case SkillElement.NONE:
                        attackBonus += attacker.GetGameStats().GetStat(StatEnum.PVP_ATTACK_RATIO_PHYSICAL, 0).GetCurrent();
                        defenseBonus += target.GetGameStats().GetStat(StatEnum.PVP_DEFEND_RATIO_PHYSICAL, 0).GetCurrent();
                        break;
                    default:
                        attackBonus += attacker.GetGameStats().GetStat(StatEnum.PVP_ATTACK_RATIO_MAGICAL, 0).GetCurrent();
                        defenseBonus += target.GetGameStats().GetStat(StatEnum.PVP_DEFEND_RATIO_MAGICAL, 0).GetCurrent();
                        break;
                }
            }
        }
        else if (!useTemplateDmg)
        {
            if (attacker is Player)
            { // npcs dmg is not reduced because of the level difference GF (4.9) 23.04.2016
                damage *= 1f - GetNpcLevelDiffMod(target, attacker);
            }
            attackBonus = attacker.GetGameStats().GetStat(StatEnum.PVE_ATTACK_RATIO, 0).GetCurrent();
            defenseBonus = target.GetGameStats().GetStat(StatEnum.PVE_DEFEND_RATIO, 0).GetCurrent();
            switch (element)
            {
                case SkillElement.NONE:
                    attackBonus += attacker.GetGameStats().GetStat(StatEnum.PVE_ATTACK_RATIO_PHYSICAL, 0).GetCurrent();
                    defenseBonus += target.GetGameStats().GetStat(StatEnum.PVE_DEFEND_RATIO_PHYSICAL, 0).GetCurrent();
                    break;
                default:
                    attackBonus += attacker.GetGameStats().GetStat(StatEnum.PVE_ATTACK_RATIO_MAGICAL, 0).GetCurrent();
                    defenseBonus += target.GetGameStats().GetStat(StatEnum.PVE_DEFEND_RATIO_MAGICAL, 0).GetCurrent();
                    break;
            }
        }
        CombatMode mode = pvpTarget ? CombatMode.PVP : CombatMode.PVE;
        // PvP/PvE ratio caps are applied after aggregation (retail behavior)
        attackBonus = StatCapUtil.LimitValueForPvpOrPveStat(mode, RatioType.ATTACK, attackBonus);
        defenseBonus = StatCapUtil.LimitValueForPvpOrPveStat(mode, RatioType.DEFENSE, defenseBonus);
        float multiplier = 1f + (attackBonus - defenseBonus) / 1000f;
        // Retail behavior: damage multiplier has a minimum cap of 10% (0.1f)
        multiplier = Math.Max(multiplier, 0.1f);

        return damage * multiplier;
    }

    /// <summary>
    /// Must be called only once since this method triggers observe controller checks with side effects (e.g. consume one always dodge effect activation).
    /// </summary>
    public static bool CheckIsDodgedHit(Creature attacker, Creature attacked, int accMod)
    {
        // check if attacker is blinded
        if (attacker.GetObserveController().CheckAttackerStatus(AttackStatus.DODGE))
            return true;
        // check always dodge
        if (attacked.GetObserveController().CheckAttackStatus(AttackStatus.DODGE))
            return true;

        float accuracy = attacker.GetGameStats().GetMainHandPAccuracy().GetCurrent() + accMod;
        float dodge = attacked.GetGameStats().GetEvasion().GetBonus()
            + AdjustStatByMovementModifier(attacked, StatEnum.EVASION, attacked.GetGameStats().GetEvasion().GetBase());
        float dodgeRate = dodge - accuracy;
        if (attacked is Npc npc)
        {
            // static npcs never dodge
            if (npc.HasStatic())
                return false;
            dodgeRate *= 1 + GetNpcLevelDiffMod(attacked, attacker);
        }
        return Rnd.NextInt(1000) < Limit(StatEnum.EVASION, dodgeRate);
    }

    /// <summary>
    /// Must be called only once since this method triggers observe controller checks with side effects (e.g. consume one always parry effect activation).
    /// </summary>
    public static bool CheckIsParriedHit(Creature attacker, Creature attacked, int accMod)
    {
        // check always parry
        if (attacked.GetObserveController().CheckAttackStatus(AttackStatus.PARRY))
            return true;

        float accuracy = attacker.GetGameStats().GetMainHandPAccuracy().GetCurrent() + accMod;
        float parry = attacked.GetGameStats().GetParry().GetBonus()
            + AdjustStatByMovementModifier(attacked, StatEnum.PARRY, attacked.GetGameStats().GetParry().GetBase());
        return Rnd.NextInt(1000) < Limit(StatEnum.PARRY, parry - accuracy);
    }

    /// <summary>
    /// Must be called only once since this method triggers observe controller checks with side effects (e.g. consume one always block effect activation).
    /// </summary>
    public static bool CheckIsBlockedHit(Creature attacker, Creature attacked, int accMod)
    {
        // check always block
        if (attacked.GetObserveController().CheckAttackStatus(AttackStatus.BLOCK))
            return true;

        float accuracy = attacker.GetGameStats().GetMainHandPAccuracy().GetCurrent() + accMod;

        float block = attacked.GetGameStats().GetBlock().GetBonus()
            + AdjustStatByMovementModifier(attacked, StatEnum.BLOCK, attacked.GetGameStats().GetBlock().GetBase());
        return Rnd.NextInt(1000) < Limit(StatEnum.BLOCK, block - accuracy);
    }

    public static bool CheckIsPhysicalCriticalHit(Creature attacker, Creature attacked, bool isMainHand, int criticalProb, bool isSkill)
    {
        if (attacker is Servant || attacker is Homing)
            return false;
        float criticalRate;
        if (attacker is Player && !isMainHand)
            criticalRate = ((PlayerGameStats)attacker.GetGameStats()).GetOffHandPCritical().GetCurrent();
        else
            criticalRate = attacker.GetGameStats().GetMainHandPCritical().GetCurrent();

        // check one time boost skill critical
        AttackerCriticalStatus acStatus = attacker.GetObserveController().CheckAttackerCriticalStatus(AttackStatus.CRITICAL, isSkill);
        if (acStatus.IsResult())
        {
            if (acStatus.IsPercent())
                criticalRate *= (1 + acStatus.GetValue() / 100);
            else
                return Rnd.NextInt(1000) < acStatus.GetValue();
        }

        criticalRate -= attacked.GetGameStats().GetPCR().GetCurrent();

        // add critical Prob
        if (criticalProb != 100 && criticalRate > 0)
        {
            criticalRate *= criticalProb / 100f;
        }
        return Rnd.NextInt(1000) < Limit(StatEnum.PHYSICAL_CRITICAL, criticalRate);
    }

    public static int CalculateMagicalResistRate(Creature attacker, Creature attacked, int accMod, SkillElement element)
    {
        if (attacked.GetObserveController().CheckAttackStatus(AttackStatus.RESIST))
            return 1000;
        if (element != SkillElement.NONE && attacked is Summon summon && element == summon.GetAlwaysResistElement())
            return 1000;

        int levelDiff = attacked.GetLevel() - attacker.GetLevel();
        int mResi = attacked.GetGameStats().GetMResist().GetCurrent();
        int resistRate = mResi - attacker.GetGameStats().GetMAccuracy().GetCurrent() - accMod;

        if (mResi > 0 && levelDiff > 4) // only apply if creature has mres > 0 (to keep effect of AI#modifyOwnerStat)
            resistRate += (levelDiff - 4) * 100;

        if (attacker is Player && attacked is Player) // checked on retail: only applies to PvP
            return Math.Min(500, resistRate);

        return (int)Limit(StatEnum.MAGICAL_RESIST, resistRate);
    }

    public static int CalculateFallDamage(Player player, float distance)
    {
        int fallDamage = 0;
        if (distance >= FallDamageConfig.MAXIMUM_DISTANCE_DAMAGE)
        {
            fallDamage = player.GetLifeStats().GetCurrentHp();
        }
        else if (distance >= FallDamageConfig.MINIMUM_DISTANCE_DAMAGE)
        {
            float dmgPerMeter = player.GetLifeStats().GetMaxHp() * FallDamageConfig.FALL_DAMAGE_PERCENTAGE / 100f;
            fallDamage = (int)(distance * dmgPerMeter);
        }
        return fallDamage;
    }

    public static float AdjustStatByMovementModifier(Creature creature, StatEnum? stat, float value)
    {
        if (!(creature is Player player) || stat == null)
            return value;

        // https://web.archive.org/web/20170429204823/gameguide.na.aiononline.com/aion/Combat
        switch (player.GetMoveController().GetMovementDirection())
        {
            case MovementDirection.FORWARD:
                switch (stat)
                {
                    case StatEnum.PHYSICAL_ATTACK:
                    case StatEnum.MAGICAL_ATTACK:
                        return value * 1.1f; // verified on 4.6 PTS
                    case StatEnum.FIRE_RESISTANCE:
                    case StatEnum.EARTH_RESISTANCE:
                    case StatEnum.WATER_RESISTANCE:
                    case StatEnum.WIND_RESISTANCE:
                    case StatEnum.LIGHT_RESISTANCE:
                    case StatEnum.DARK_RESISTANCE:
                        return value - 50; // verified on 4.6 PTS
                    case StatEnum.MAGICAL_DEFEND:
                    case StatEnum.PHYSICAL_DEFENSE:
                        return value * 0.8f; // verified on 4.6 PTS
                }
                break;
            case MovementDirection.SIDEWAYS:
                switch (stat)
                {
                    case StatEnum.PHYSICAL_ATTACK:
                    case StatEnum.MAGICAL_ATTACK:
                    case StatEnum.SPEED:
                        return value * 0.8f; // verified on 4.6 PTS
                    case StatEnum.EVASION:
                        return value + 300;
                }
                break;
            case MovementDirection.BACKWARD:
                switch (stat)
                {
                    case StatEnum.PHYSICAL_ATTACK:
                    case StatEnum.MAGICAL_ATTACK:
                        return value * 0.8f; // verified on 4.6 PTS
                    case StatEnum.SPEED:
                        return value * 0.6f; // verified on 4.6 PTS
                    case StatEnum.PARRY:
                    case StatEnum.BLOCK:
                        return value + 500;
                }
                break;
        }
        return value;
    }

    private static float GetNpcLevelDiffMod(Creature target, Creature attacker)
    {
        int levelDiff = target.GetLevel() - attacker.GetLevel();
        if (levelDiff <= 2)
            return 0f;
        if (levelDiff >= 12)
            return 1f;
        return (levelDiff - 2) * 0.1f;
    }

    /// <summary>Returns the smaller of <paramref name="value"/> and the difference limit for this StatEnum.</summary>
    public static float Limit(StatEnum statEnum, float value)
    {
        return Math.Min(StatCapUtil.GetDifferenceLimit(statEnum), value);
    }
}
