using Aion.GameServer.Model.Actions;
using Aion.GameServer.Model.Templates.Items;
using System;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Templates.Items.Enums;
using Aion.GameServer.Model.Templates.Stats;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Stats;

namespace Aion.GameServer.Model.Stats.Container;

/// <summary>Java parity: model/stats/container/PlayerGameStats (@author xavier).</summary>
public class PlayerGameStats : CreatureGameStats<Player>
{
    private StatsTemplate statsTemplate;
    private int cachedAttackSpeed;
    private int maxDamageChance;
    private float minDamageRatio;
    private float skillEfficiency;

    public PlayerGameStats(Player owner)
        : base(owner)
    {
        UpdateStatsTemplate();
    }

    // Java parity helper: Math.round(float) = floor(x+0.5).
    private static int JRound(float a) => (int)Math.Floor(a + 0.5f);

    // Java parity: org.apache.commons.lang3.ArrayUtils.add / contains / removeElement.
    private static CalculationType[] ArrAdd(CalculationType[] a, CalculationType v)
    {
        CalculationType[] r = new CalculationType[a.Length + 1];
        Array.Copy(a, r, a.Length);
        r[a.Length] = v;
        return r;
    }

    private static bool ArrContains(CalculationType[] a, CalculationType v) => Array.IndexOf(a, v) >= 0;

    private static CalculationType[] ArrRemove(CalculationType[] a, CalculationType v)
    {
        int idx = Array.IndexOf(a, v);
        if (idx < 0)
            return a;
        CalculationType[] r = new CalculationType[a.Length - 1];
        Array.Copy(a, 0, r, 0, idx);
        Array.Copy(a, idx + 1, r, idx, a.Length - idx - 1);
        return r;
    }

    protected override void OnStatsChange(Effect effect)
    {
        base.OnStatsChange(effect);
        UpdateStatsVisually();
        CheckSpeedStats();
    }

    public void UpdateStatsAndSpeedVisually()
    {
        OnStatsChange(null);
    }

    public void UpdateStatsVisually()
    {
        UpdateStatInfo();
    }

    protected override bool CheckSpeedStats()
    {
        bool speedChanged = base.CheckSpeedStats();
        int currentAttackSpeed = GetAttackSpeed().GetCurrent();
        if (currentAttackSpeed != cachedAttackSpeed)
        {
            if (!speedChanged) // prevent double packet broadcast (super.checkSpeedStats() already broadcasts on true)
                UpdateSpeedInfo();
            cachedAttackSpeed = currentAttackSpeed;
            return true;
        }
        return speedChanged;
    }

    public override StatsTemplate GetStatsTemplate()
    {
        return statsTemplate;
    }

    public void UpdateStatsTemplate()
    {
        this.statsTemplate = owner.GetPlayerClass().CreateStatsTemplate(owner.GetLevel());
    }

    public Stat2 GetMaxDp()
    {
        return GetStat(StatEnum.MAXDP, 4000);
    }

    public Stat2 GetFlyTime()
    {
        return GetStat(StatEnum.FLY_TIME, CustomConfig.BASE_FLYTIME);
    }

    public override Stat2 GetAttackSpeed()
    {
        int baseV = 1500;
        Equipment equipment = owner.GetEquipment();
        Item mainHandWeapon = equipment.GetMainHandWeapon();

        if (mainHandWeapon != null)
        {
            baseV = mainHandWeapon.GetItemTemplate().GetWeaponStats().GetAttackSpeed();
            Item offWeapon = owner.GetEquipment().GetOffHandWeapon();
            if (offWeapon == mainHandWeapon)
                offWeapon = null;
            if (offWeapon != null)
                baseV += offWeapon.GetItemTemplate().GetWeaponStats().GetAttackSpeed() / 4;
        }
        return GetStat(StatEnum.ATTACK_SPEED, baseV);
    }

    public override Stat2 GetMovementSpeed()
    {
        Stat2 movementSpeed;
        StatsTemplate pst = GetStatsTemplate();
        if (owner.IsInPlayerMode(PlayerMode.RIDE))
        {
            Aion.GameServer.Model.Templates.Ride.RideInfo ride = owner.ride;
            int runSpeed = (int)pst.GetRunSpeed() * 1000;
            if (owner.IsInState(CreatureState.FLYING))
            {
                movementSpeed = new AdditionStat(StatEnum.FLY_SPEED, runSpeed, owner);
                movementSpeed.AddToBonus((int)(ride.GetFlySpeed() * 1000) - runSpeed);
            }
            else
            {
                float speed = owner.IsInSprintMode() ? ride.GetSprintSpeed() : ride.GetMoveSpeed();
                movementSpeed = new AdditionStat(StatEnum.SPEED, runSpeed, owner);
                movementSpeed.AddToBonus((int)(speed * 1000) - runSpeed);
            }
        }
        else if (owner.IsInFlyingState())
            movementSpeed = GetStat(StatEnum.FLY_SPEED, JRound(pst.GetFlySpeed() * 1000));
        else if (owner.IsInState(CreatureState.FLYING) && !owner.IsInState(CreatureState.RESTING))
            movementSpeed = GetStat(StatEnum.SPEED, 12000);
        else if (owner.IsInState(CreatureState.WALK_MODE))
            movementSpeed = GetStat(StatEnum.SPEED, JRound(pst.GetWalkSpeed() * 1000));
        else
            movementSpeed = GetStat(StatEnum.SPEED, JRound(pst.GetRunSpeed() * 1000));
        return movementSpeed;
    }

    public override Stat2 GetAttackRange()
    {
        int baseV = 1500;
        int minWeaponRange = int.MaxValue;
        Equipment equipment = owner.GetEquipment();
        Item mainHandWeapon = equipment.GetMainHandWeapon();
        Item offHandWeapon = equipment.GetOffHandWeapon();
        if (mainHandWeapon != null)
            minWeaponRange = mainHandWeapon.GetItemTemplate().GetWeaponStats().GetAttackRange();
        if (offHandWeapon != null && !equipment.IsShieldEquipped())
            minWeaponRange = Math.Min(minWeaponRange, offHandWeapon.GetItemTemplate().GetWeaponStats().GetAttackRange());
        return GetStat(StatEnum.ATTACK_RANGE, minWeaponRange == int.MaxValue ? baseV : minWeaponRange);
    }

    public override Stat2 GetParry()
    {
        int baseV = GetStatsTemplate().GetParry();
        Item mainHandWeapon = owner.GetEquipment().GetMainHandWeapon();
        if (mainHandWeapon != null)
        {
            baseV += mainHandWeapon.GetItemTemplate().GetWeaponStats().GetParry();
        }
        return GetStat(StatEnum.PARRY, baseV);
    }

    public override Stat2 GetMainHandPAttack(params CalculationType[] calculationTypes)
    {
        calculationTypes = ArrAdd(calculationTypes, CalculationType.MAIN_HAND);
        float baseV = GetStatsTemplate().GetAttack();
        Equipment equipment = owner.GetEquipment();
        Item mainHandWeapon = equipment.GetMainHandWeapon();
        if (mainHandWeapon != null)
        {
            if (mainHandWeapon.GetItemTemplate().GetAttackType().IsMagical())
                return new AdditionStat(StatEnum.MAIN_HAND_POWER, 0, owner);
            if (ArrContains(calculationTypes, CalculationType.DISPLAY))
            {
                baseV = mainHandWeapon.GetItemTemplate().GetWeaponStats().GetMeanDamage();
            }
            else
            {
                baseV = Rnd.Get(mainHandWeapon.GetItemTemplate().GetWeaponStats().GetMinDamage(),
                    mainHandWeapon.GetItemTemplate().GetWeaponStats().GetMaxDamage());
            }
            if (ArrContains(calculationTypes, CalculationType.APPLY_POWER_SHARD_DAMAGE))
            {
                baseV += GetPowerShardDamage(true, ArrContains(calculationTypes, CalculationType.REMOVE_POWER_SHARD));
            }
        }
        Stat2 stat = GetStat(StatEnum.PHYSICAL_ATTACK, baseV, calculationTypes);
        calculationTypes = ArrRemove(calculationTypes, CalculationType.MAIN_HAND);
        return GetStat(StatEnum.MAIN_HAND_POWER, stat, calculationTypes);
    }

    public Stat2 GetOffHandPAttack(params CalculationType[] calculationTypes)
    {
        Equipment equipment = owner.GetEquipment();
        Item offHandWeapon = equipment.GetOffHandWeapon();
        if (offHandWeapon != null && !offHandWeapon.Equals(equipment.GetMainHandWeapon()) && offHandWeapon.GetItemTemplate().IsWeapon())
        {
            calculationTypes = ArrAdd(calculationTypes, CalculationType.OFF_HAND);
            float baseV;
            if (ArrContains(calculationTypes, CalculationType.DISPLAY))
            {
                baseV = offHandWeapon.GetItemTemplate().GetWeaponStats().GetMeanDamage();
            }
            else
            {
                baseV = Rnd.Get(offHandWeapon.GetItemTemplate().GetWeaponStats().GetMinDamage(),
                    offHandWeapon.GetItemTemplate().GetWeaponStats().GetMaxDamage());
            }
            if (ArrContains(calculationTypes, CalculationType.APPLY_POWER_SHARD_DAMAGE))
                baseV += GetPowerShardDamage(false, ArrContains(calculationTypes, CalculationType.REMOVE_POWER_SHARD));
            Stat2 stat = GetStat(StatEnum.PHYSICAL_ATTACK, baseV, calculationTypes);
            if (ArrContains(calculationTypes, CalculationType.DISPLAY))
            {
                stat.SetBaseRate(stat.GetBaseRate() * GetOffHandDamageRatio());
                stat.SetBonusRate(stat.GetBonusRate() * GetOffHandDamageRatio());
            }
            calculationTypes = ArrRemove(calculationTypes, CalculationType.OFF_HAND);
            return GetStat(StatEnum.OFF_HAND_POWER, stat, calculationTypes);
        }
        return new AdditionStat(StatEnum.OFF_HAND_POWER, 0, owner);
    }

    public override Stat2 GetMainHandMAttack(params CalculationType[] calculationTypes)
    {
        calculationTypes = ArrAdd(calculationTypes, CalculationType.MAIN_HAND);
        float baseV = GetStatsTemplate().GetMagicalAttack();
        Equipment equipment = owner.GetEquipment();
        Item mainHandWeapon = equipment.GetMainHandWeapon();
        if (mainHandWeapon != null)
        {
            if (!mainHandWeapon.GetItemTemplate().GetAttackType().IsMagical())
                return new AdditionStat(StatEnum.MAIN_HAND_POWER, 0, owner);
            baseV = mainHandWeapon.GetItemTemplate().GetWeaponStats().GetMeanDamage();
            if (ArrContains(calculationTypes, CalculationType.APPLY_POWER_SHARD_DAMAGE))
                baseV += GetPowerShardDamage(true, ArrContains(calculationTypes, CalculationType.REMOVE_POWER_SHARD));
        }
        Stat2 stat = GetStat(StatEnum.MAGICAL_ATTACK, baseV, calculationTypes);
        calculationTypes = ArrRemove(calculationTypes, CalculationType.MAIN_HAND);
        return GetStat(StatEnum.MAIN_HAND_POWER, stat, calculationTypes);
    }

    public Stat2 GetOffHandMAttack(params CalculationType[] calculationTypes)
    {
        Equipment equipment = owner.GetEquipment();
        Item offHandWeapon = equipment.GetOffHandWeapon();
        if (offHandWeapon != null && !offHandWeapon.Equals(equipment.GetMainHandWeapon()) && offHandWeapon.GetItemTemplate().IsWeapon())
        {
            calculationTypes = ArrAdd(calculationTypes, CalculationType.OFF_HAND);
            float baseV = offHandWeapon.GetItemTemplate().GetWeaponStats().GetMeanDamage();
            if (ArrContains(calculationTypes, CalculationType.APPLY_POWER_SHARD_DAMAGE))
                baseV += GetPowerShardDamage(false, ArrContains(calculationTypes, CalculationType.REMOVE_POWER_SHARD));
            Stat2 stat = GetStat(StatEnum.MAGICAL_ATTACK, baseV, calculationTypes);
            if (ArrContains(calculationTypes, CalculationType.DISPLAY))
            {
                stat.SetBaseRate(stat.GetBaseRate() * GetOffHandDamageRatio());
                stat.SetBonusRate(stat.GetBonusRate() * GetOffHandDamageRatio());
            }
            calculationTypes = ArrRemove(calculationTypes, CalculationType.OFF_HAND);
            return GetStat(StatEnum.OFF_HAND_POWER, stat, calculationTypes);
        }
        return new AdditionStat(StatEnum.OFF_HAND_POWER, 0, owner);
    }

    public override Stat2 GetMainHandPCritical()
    {
        int baseV = GetStatsTemplate().GetPcrit();
        Equipment equipment = owner.GetEquipment();
        Item mainHandWeapon = equipment.GetMainHandWeapon();
        if (mainHandWeapon != null && !mainHandWeapon.GetItemTemplate().GetAttackType().IsMagical())
        {
            baseV += mainHandWeapon.GetItemTemplate().GetWeaponStats().GetCritical();
        }
        return GetStat(StatEnum.PHYSICAL_CRITICAL, baseV);
    }

    public Stat2 GetOffHandPCritical()
    {
        int baseV = GetStatsTemplate().GetPcrit();
        Equipment equipment = owner.GetEquipment();
        Item offHandWeapon = equipment.GetOffHandWeapon();
        if (offHandWeapon != null && !offHandWeapon.Equals(equipment.GetMainHandWeapon()) && offHandWeapon.GetItemTemplate().IsWeapon()
            && !offHandWeapon.GetItemTemplate().GetAttackType().IsMagical())
        {
            baseV += offHandWeapon.GetItemTemplate().GetWeaponStats().GetCritical();
            return GetStat(StatEnum.PHYSICAL_CRITICAL, baseV);
        }
        return new AdditionStat(StatEnum.OFF_HAND_CRITICAL, 0, owner);
    }

    public override Stat2 GetMainHandPAccuracy()
    {
        int baseV = GetStatsTemplate().GetAccuracy();
        Item mainHandWeapon = owner.GetEquipment().GetMainHandWeapon();
        if (mainHandWeapon != null)
        {
            baseV += mainHandWeapon.GetItemTemplate().GetWeaponStats().GetPhysicalAccuracy();
        }
        return GetStat(StatEnum.PHYSICAL_ACCURACY, baseV);
    }

    public Stat2 GetOffHandPAccuracy()
    {
        Equipment equipment = owner.GetEquipment();
        Item offHandWeapon = equipment.GetOffHandWeapon();
        if (offHandWeapon != null && !offHandWeapon.Equals(equipment.GetMainHandWeapon()) && offHandWeapon.GetItemTemplate().IsWeapon())
        {
            int baseV = GetStatsTemplate().GetAccuracy();
            baseV += offHandWeapon.GetItemTemplate().GetWeaponStats().GetPhysicalAccuracy();
            return GetStat(StatEnum.PHYSICAL_ACCURACY, baseV);
        }
        return new AdditionStat(StatEnum.OFF_HAND_ACCURACY, 0, owner);
    }

    public override Stat2 GetMBoost()
    {
        int baseV = GetStatsTemplate().GetMagicBoost();
        Item mainHandWeapon = owner.GetEquipment().GetMainHandWeapon();
        if (mainHandWeapon != null)
        {
            baseV += mainHandWeapon.GetItemTemplate().GetWeaponStats().GetBoostMagicalSkill();
        }
        return GetStat(StatEnum.BOOST_MAGICAL_SKILL, baseV);
    }

    public override Stat2 GetMAccuracy()
    {
        int baseV = GetStatsTemplate().GetMacc();
        Item mainHandWeapon = owner.GetEquipment().GetMainHandWeapon();
        if (mainHandWeapon != null)
        {
            baseV += mainHandWeapon.GetItemTemplate().GetWeaponStats().GetMagicalAccuracy();
        }
        return GetStat(StatEnum.MAGICAL_ACCURACY, baseV);
    }

    public override Stat2 GetMCritical()
    {
        int baseV = GetStatsTemplate().GetMcrit();
        Item mainHandWeapon = owner.GetEquipment().GetMainHandWeapon();
        if (mainHandWeapon != null && mainHandWeapon.GetItemTemplate().GetAttackType().IsMagical())
        {
            baseV += mainHandWeapon.GetItemTemplate().GetWeaponStats().GetCritical();
        }
        return GetStat(StatEnum.MAGICAL_CRITICAL, baseV);
    }

    public override Stat2 GetHpRegenRate()
    {
        int baseV = owner.GetLevel() + 3;
        if (owner.IsInState(CreatureState.RESTING))
            baseV *= 8;
        baseV = (int)(baseV * (GetHealth().GetCurrent() / 100f));
        return GetStat(StatEnum.REGEN_HP, baseV);
    }

    public override Stat2 GetMpRegenRate()
    {
        int baseV = owner.GetLevel() + 8;
        if (owner.IsInState(CreatureState.RESTING))
            baseV *= 8;
        baseV = (int)(baseV * (GetWill().GetCurrent() / 100f));
        return GetStat(StatEnum.REGEN_MP, baseV);
    }

    public override void UpdateStatInfo()
    {
        PacketSendUtility.SendPacket(owner, new SM_STATS_INFO(owner));
    }

    public override void UpdateSpeedInfo()
    {
        PacketSendUtility.BroadcastToSightedPlayers(owner, new SM_EMOTION(owner, EmotionType.CHANGE_SPEED), true);
    }

    public int GetHealthDependentAdditionalHp()
    {
        return CalculateBaseStatDependentAdditionalValue(GetHealth(), owner.GetPlayerClass().GetHealthMultiplier());
    }

    public int GetWillDependentAdditionalMp()
    {
        return CalculateBaseStatDependentAdditionalValue(GetWill(), owner.GetPlayerClass().GetWillMultiplier());
    }

    public int GetAgilityDependentAdditionalBaseBlock()
    {
        return CalculateBaseStatDependentAdditionalValue(GetAgility(), owner.GetPlayerClass().GetAgilityMultiplier());
    }

    public int GetAgilityDependentAdditionalBaseParry()
    {
        return CalculateBaseStatDependentAdditionalValue(GetAgility(), owner.GetPlayerClass().GetAgilityMultiplier());
    }

    public int GetAgilityDependentAdditionalBaseEvasion()
    {
        return CalculateBaseStatDependentAdditionalValue(GetAgility(), owner.GetPlayerClass().GetAgilityMultiplier());
    }

    public int GetAccuracyDependentAdditionalBasePhysicalAccuracy()
    {
        return CalculateBaseStatDependentAdditionalValue(GetAccuracy(), owner.GetPlayerClass().GetAccuracyMultiplier());
    }

    public int GetAccuracyDependentAdditionalBasePhysicalCritical()
    {
        return CalculateBaseStatDependentAdditionalValue(GetAccuracy(), owner.GetPlayerClass().GetAccuracyMultiplier() / 20);
    }

    private int CalculateBaseStatDependentAdditionalValue(Stat2 baseStat, int multiplier)
    {
        return (int)((baseStat.GetCurrent() - 100) / 100f * multiplier);
    }

    private int GetPowerShardDamage(bool mainHand, bool removePowerShards)
    {
        if (owner.IsInState(CreatureState.POWERSHARD))
        {
            Equipment equipment = owner.GetEquipment();
            Item weapon = mainHand ? equipment.GetMainHandWeapon() : equipment.GetOffHandWeapon();
            Item firstShard = equipment.GetMainHandPowerShard();
            Item secondShard = equipment.GetOffHandPowerShard();
            if (weapon != null && weapon.GetItemTemplate().GetItemSubType() != ItemSubType.SHIELD)
            {
                int dmg = 0;
                if (mainHand)
                {
                    if (firstShard != null)
                    {
                        dmg = firstShard.GetItemTemplate().GetWeaponBoost();
                        if (removePowerShards)
                            owner.GetEquipment().UsePowerShard(firstShard, 1);
                    }
                    if (weapon.GetItemTemplate().IsTwoHandWeapon() && secondShard != null)
                    {
                        dmg += secondShard.GetItemTemplate().GetWeaponBoost();
                        if (removePowerShards)
                            owner.GetEquipment().UsePowerShard(secondShard, 1);
                    }
                }
                else if (secondShard != null)
                {
                    dmg = secondShard.GetItemTemplate().GetWeaponBoost();
                    if (removePowerShards)
                        owner.GetEquipment().UsePowerShard(secondShard, 1);
                }
                return dmg;
            }
        }
        return 0;
    }

    public float GetSkillEfficiency()
    {
        return skillEfficiency;
    }

    public int GetMaxDamageChance()
    {
        return maxDamageChance;
    }

    public float GetMinDamageRatio()
    {
        return minDamageRatio;
    }

    public void SetSkillEfficiency(float skillEfficiency)
    {
        this.skillEfficiency = skillEfficiency;
    }

    public void SetMaxDamageChance(int maxDamageChance)
    {
        this.maxDamageChance = maxDamageChance;
    }

    public void SetMinDamageRatio(float minDamageRatio)
    {
        this.minDamageRatio = minDamageRatio;
    }

    public float GetOffHandDamageRatio()
    {
        return GetMinDamageRatio() * (1 - GetMaxDamageChance() / 1000f) + GetMaxDamageChance() / 1000f;
    }
}
