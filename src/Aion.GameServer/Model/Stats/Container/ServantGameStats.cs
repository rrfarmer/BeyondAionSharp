using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Utils.Stats;

namespace Aion.GameServer.Model.Stats.Container;

/// <summary>Java parity: model/stats/container/ServantGameStats.</summary>
public class ServantGameStats : SummonedObjectGameStats
{
    private int fixedMBoost;
    private int fixedHealBoost;
    private int fixedMagicalAccuracy;

    public ServantGameStats(Npc owner)
        : base(owner)
    {
    }

    public override Stat2 GetStat(StatEnum statEnum, float baseValue, params CalculationType[] calculationTypes)
    {
        return base.GetStat(statEnum, statEnum == StatEnum.HEAL_BOOST ? fixedHealBoost : baseValue, calculationTypes);
    }

    public override Stat2 GetMBoost()
    {
        return GetStat(StatEnum.BOOST_MAGICAL_SKILL, fixedMBoost);
    }

    public override Stat2 GetMAccuracy()
    {
        return GetStat(StatEnum.MAGICAL_ACCURACY, fixedMagicalAccuracy);
    }

    // TODO: there might be more stats which are set only at spawn
    public void SetUpStats()
    {
        SetFixedMBoost();
        SetFixedHealBoost();
        SetFixedMagicalAccuracy();
    }

    private void SetFixedMBoost()
    {
        fixedMBoost = owner.GetMaster().GetGameStats().GetMBoost().GetBonus();
    }

    private void SetFixedHealBoost()
    {
        Stat2 healBoostStat = base.GetStat(StatEnum.HEAL_BOOST, 0);
        healBoostStat.SetBonusRate(0.5f);
        fixedHealBoost = owner.GetMaster().GetGameStats().GetItemStatBoost(StatEnum.HEAL_BOOST, healBoostStat).GetCurrent();
        if (fixedHealBoost > 500)
        {
            fixedHealBoost = 500;
        }
    }

    private void SetFixedMagicalAccuracy()
    {
        Stat2 magicalAccuracyStat = base.GetStat(StatEnum.MAGICAL_ACCURACY, GetStatsTemplate().GetMacc());
        magicalAccuracyStat.SetBaseRate(1.2f);
        fixedMagicalAccuracy = owner.GetMaster().GetGameStats().GetItemStatBoost(StatEnum.MAGICAL_ACCURACY, magicalAccuracyStat).GetCurrent();
    }
}
