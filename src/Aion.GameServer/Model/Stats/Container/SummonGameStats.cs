using System;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Templates.Stats;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Stats;

namespace Aion.GameServer.Model.Stats.Container;

/// <summary>Java parity: model/stats/container/SummonGameStats.</summary>
public class SummonGameStats : CreatureGameStats<Summon>
{
    public SummonGameStats(Summon owner)
        : base(owner)
    {
    }

    // Java parity helper: Math.round(float) = floor(x+0.5).
    private static int JRound(float a) => (int)Math.Floor(a + 0.5f);

    protected override void OnStatsChange(Effect effect)
    {
        UpdateStatsAndSpeedVisually();
    }

    public void UpdateStatsAndSpeedVisually()
    {
        UpdateStatsVisually();
        CheckSpeedStats();
    }

    public void UpdateStatsVisually()
    {
        owner.GetGameStats().UpdateStatInfo();
    }

    public override Stat2 GetStat(StatEnum statEnum, float baseValue, params CalculationType[] calculationTypes)
    {
        Stat2 stat = base.GetStat(statEnum, baseValue, calculationTypes);
        if (owner.GetMaster() == null)
            return stat;
        switch (statEnum)
        {
            case StatEnum.MAXHP:
            case StatEnum.PHYSICAL_ATTACK:
            case StatEnum.MAGICAL_ATTACK:
            case StatEnum.EVASION:
            case StatEnum.PARRY:
            case StatEnum.PHYSICAL_DEFENSE:
            case StatEnum.MAGICAL_DEFEND:
            case StatEnum.MAGIC_SKILL_BOOST_RESIST:
            case StatEnum.MAGICAL_CRITICAL:
            case StatEnum.MAGICAL_RESIST:
                return GetStatWithBonusRate(statEnum, stat, 0.5f);
            case StatEnum.PHYSICAL_CRITICAL:
                return GetStatWithBonusRate(StatEnum.MAIN_HAND_CRITICAL, stat, 0.5f);
            case StatEnum.PHYSICAL_ACCURACY:
                return GetStatWithBonusRate(StatEnum.MAIN_HAND_ACCURACY, stat, 0.5f);
            case StatEnum.BOOST_MAGICAL_SKILL:
            case StatEnum.MAGICAL_ACCURACY:
                return GetStatWithBonusRate(statEnum, stat, 0.8f);
            case StatEnum.PARALYZE_RESISTANCE:
            case StatEnum.SLEEP_RESISTANCE:
            case StatEnum.POISON_RESISTANCE:
                if ("lava spirit".Equals(owner.GetObjectTemplate().GetName()) || "tempest spirit".Equals(owner.GetObjectTemplate().GetName()))
                {
                    stat.AddToBase(100);
                }
                break;
            case StatEnum.EARTH_RESISTANCE:
                if ("lava spirit".Equals(owner.GetObjectTemplate().GetName()))
                {
                    stat.AddToBase(200);
                }
                else if ("wind spirit".Equals(owner.GetObjectTemplate().GetName()))
                {
                    stat.AddToBase(-200);
                }
                break;
            case StatEnum.FIRE_RESISTANCE:
                if ("lava spirit".Equals(owner.GetObjectTemplate().GetName()))
                {
                    stat.AddToBase(200);
                }
                else if ("water spirit".Equals(owner.GetObjectTemplate().GetName()))
                {
                    stat.AddToBase(-200);
                }
                break;
            case StatEnum.WIND_RESISTANCE:
                if ("tempest spirit".Equals(owner.GetObjectTemplate().GetName()))
                {
                    stat.AddToBase(200);
                }
                else if ("earth spirit".Equals(owner.GetObjectTemplate().GetName()))
                {
                    stat.AddToBase(-200);
                }
                break;
            case StatEnum.WATER_RESISTANCE:
                if ("tempest spirit".Equals(owner.GetObjectTemplate().GetName()))
                {
                    stat.AddToBase(200);
                }
                else if ("fire spirit".Equals(owner.GetObjectTemplate().GetName()))
                {
                    stat.AddToBase(-200);
                }
                break;
        }
        return stat;
    }

    private Stat2 GetStatWithBonusRate(StatEnum statEnum, Stat2 stat, float bonusRate)
    {
        Stat2 statToReturn = owner.GetMaster().GetGameStats().GetItemStatBoost(statEnum, stat);
        statToReturn.SetBonusRate(bonusRate);
        return statToReturn;
    }

    public override StatsTemplate GetStatsTemplate()
    {
        return owner.GetObjectTemplate().GetStatsTemplate();
    }

    public override Stat2 GetAttackSpeed()
    {
        return GetStat(StatEnum.ATTACK_SPEED, owner.GetObjectTemplate().GetAttackSpeed());
    }

    public override Stat2 GetMovementSpeed()
    {
        int bonusSpeed = 0;
        Player master = owner.GetMaster();
        if (master != null && master.IsFlying())
        {
            bonusSpeed += 3000;
        }
        return GetStat(StatEnum.SPEED, JRound(GetStatsTemplate().GetRunSpeed() * 1000) + bonusSpeed);
    }

    public override Stat2 GetAttackRange()
    {
        return GetStat(StatEnum.ATTACK_RANGE, owner.GetObjectTemplate().GetAttackRange() * 1000);
    }

    public override Stat2 GetHpRegenRate()
    {
        int baseValue = (int)(owner.GetLifeStats().GetMaxHp() * (owner.GetMode() == SummonMode.REST ? 0.05f : 0.025f));
        return GetStat(StatEnum.REGEN_HP, baseValue);
    }

    public override Stat2 GetMpRegenRate()
    {
        throw new InvalidOperationException("No mp regen for Summon");
    }

    public override void UpdateStatInfo()
    {
        Player master = owner.GetMaster();
        if (master != null)
        {
            PacketSendUtility.SendPacket(master, new Aion.GameServer.Network.Aion.ServerPackets.SM_SUMMON_UPDATE(owner));
        }
    }
}
