using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Utils.Stats;

namespace Aion.GameServer.Model.Stats.Container;

/// <summary>Java parity: model/stats/container/SummonedObjectGameStats.</summary>
public class SummonedObjectGameStats : NpcGameStats
{
    public SummonedObjectGameStats(Npc owner)
        : base(owner)
    {
    }

    public override Stat2 GetStat(StatEnum statEnum, float baseValue, params CalculationType[] calculationTypes)
    {
        Stat2 stat = base.GetStat(statEnum, baseValue, calculationTypes);
        if (owner.GetMaster() == null)
            return stat;
        switch (statEnum)
        {
            case StatEnum.MAGICAL_ATTACK:
            case StatEnum.MAGICAL_ACCURACY:
            case StatEnum.MAGICAL_RESIST:
                stat.SetBonusRate(0.2f);
                return owner.GetMaster().GetGameStats().GetItemStatBoost(statEnum, stat);
            case StatEnum.PHYSICAL_ACCURACY:
                stat.SetBonusRate(0.2f);
                owner.GetMaster().GetGameStats().GetItemStatBoost(StatEnum.MAIN_HAND_ACCURACY, stat);
                return owner.GetMaster().GetGameStats().GetItemStatBoost(statEnum, stat);
            case StatEnum.PHYSICAL_ATTACK:
                stat.SetBonusRate(0.2f);
                owner.GetMaster().GetGameStats().GetItemStatBoost(StatEnum.MAIN_HAND_POWER, stat);
                return owner.GetMaster().GetGameStats().GetItemStatBoost(statEnum, stat);
        }
        return stat;
    }

    public override Stat2 GetMBoost()
    {
        return GetStat(StatEnum.BOOST_MAGICAL_SKILL, (int)(owner.GetMaster().GetGameStats().GetMBoost().GetCurrent() * 0.6f));
    }
}
