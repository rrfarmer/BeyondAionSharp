using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils.Stats;

namespace Aion.GameServer.Model.Stats.Container;

/// <summary>Java parity: model/stats/container/HomingGameStats.</summary>
public class HomingGameStats : SummonedObjectGameStats
{
    public HomingGameStats(Npc owner)
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
                stat.SetBonusRate(0.2f);
                return owner.GetMaster().GetGameStats().GetItemStatBoost(statEnum, stat);
        }
        return stat;
    }

    public override Stat2 GetMainHandMAttack(params CalculationType[] calculationTypes)
    {
        Homing homing = (Homing)owner;
        int power = GetStatsTemplate().GetMagicalAttack();
        SkillTemplate skill = DataManager.SKILL_DATA.GetSkillTemplate(homing.GetSkillId());
        int skillLvl = skill.GetLvl();
        if (homing.GetName().Equals("gryphu"))
            power = 324;
        switch (skillLvl)
        {
            case 3:
                if (homing.GetName().Equals("stone energy"))
                    power = 316;
                if (homing.GetName().Equals("water energy"))
                    power = 362;
                break;
            case 4:
                if (homing.GetName().Equals("cyclone servant"))
                    power = 1166;
                if (homing.GetName().Equals("fire energy"))
                    power = 313;
                if (homing.GetName().Equals("wind servant"))
                    power = 373;
                if (homing.GetName().Equals("stone energy"))
                    power = 384;
                break;
            case 5:
                if (homing.GetName().Equals("cyclone servant"))
                    power = 1221;
                break;
            case 6:
                if (homing.GetName().Equals("cyclone servant"))
                    power = 1283;
                break;
            case 7:
                if (homing.GetName().Equals("cyclone servant"))
                    power = 1342;
                break;
        }

        switch (homing.GetLevel())
        {
            case 65:
                if (homing.GetName().Equals("elemental energy"))
                    power = 1100;
                break;
        }

        return GetStat(StatEnum.MAGICAL_ATTACK, power, calculationTypes);
    }
}
