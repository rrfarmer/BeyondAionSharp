using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Utils.Stats;

namespace Aion.GameServer.Model.Stats.Container;

/// <summary>Java parity: model/stats/container/TrapGameStats.</summary>
public class TrapGameStats : NpcGameStats
{
    public TrapGameStats(Npc owner)
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
            case StatEnum.BOOST_MAGICAL_SKILL:
            case StatEnum.MAGICAL_ACCURACY:
                // bonus is calculated from stat bonus of master (only green value)
                stat.SetBonusRate(0.7f); // TODO: retail formula?
                return owner.GetMaster().GetGameStats().GetItemStatBoost(statEnum, stat);
        }
        return stat;
    }

    public override Stat2 GetAttackRange()
    {
        int baseValue = 5;
        string ownerName = owner.GetName();
        if (ownerName.Equals("destruction trap") || ownerName.Equals("explosion trap") || ownerName.Equals("sandstorm trap")
            || ownerName.Equals("skybound trap") || ownerName.Equals("spike bite trap") || ownerName.Equals("storm mine")
            || ownerName.Equals("scrapped mechanisms"))
        {
            baseValue = 10;
        }
        else if (ownerName.Equals("trap of clairvoyance"))
        {
            baseValue = 30;
        }
        else if (ownerName.Equals("propelling trap"))
        {
            baseValue = 3;
        }
        return GetStat(StatEnum.ATTACK_RANGE, baseValue);
    }

    public override Stat2 GetMAccuracy()
    {
        int value = 1000;
        switch (owner.GetName())
        {
            case "destruction trap":
                value = 1876;
                break;
            case "spike bite trap":
            case "explosion trap":
            case "spike trap":
            case "sleep trap":
            case "sandstorm trap":
            case "propelling trap":
            case "poisoning trap":
            case "trap of slowing":
            case "blazing trap":
            case "glue trap": //spike trap
            case "trap of dust": //sandstorm
            case "shock trap": //propelling trap
            case "trap of sleep":
            case "trap of burst":
            case "collision trap":
                value = 2361;
                break;
            case "storm mine":
            case "skybound trap":
            case "trap of vengeful spirit": //skybound trap
                value = 2406;
                break;
            case "trap of clairvoyance":
                value = 1050;
                break;
            case "snare trap":
            case "scrapped mechanisms":
                value = 2528;
                break;
        }
        return GetStat(StatEnum.MAGICAL_ACCURACY, value);
    }
}
