using System.Linq;
using Aion.Commons.Utils;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Model.Templates.Item.Enums;
using Aion.GameServer.Utils.Stats;

namespace Aion.GameServer.Model.Stats.Calc.Functions;

/// <summary>Java parity: model/stats/calc/functions/StatWeaponMasteryFunction (ATracer). : StatRateFunction. switch on base `stat` field→Stat; ArrayUtils.contains→LINQ Contains; CalculationType...→params; .equals→.Equals; Rnd.get(0,v). ItemGroup/Stat2/Player.GetEquipment red-tolerated.</summary>
public class StatWeaponMasteryFunction : StatRateFunction
{
    private readonly ItemGroup itemGroup;

    public StatWeaponMasteryFunction(ItemGroup itemGroup, StatEnum name, int value, bool bonus) : base(name, value, bonus)
    {
        this.itemGroup = itemGroup;
    }

    public override void Apply(Stat2 stat, params CalculationType[] calculationTypes)
    {
        Player player = (Player)stat.GetOwner();
        ItemGroup mainWeapon = player.GetEquipment().GetMainHandWeaponType();
        ItemGroup offHandWeapon = player.GetEquipment().GetOffHandWeaponType();
        switch (this.Stat)
        {
            case StatEnum.MAIN_HAND_POWER:
                if (mainWeapon != null && mainWeapon.Equals(itemGroup))
                {
                    ApplyTo(stat, calculationTypes);
                }
                break;
            case StatEnum.OFF_HAND_POWER:
                if (offHandWeapon != null && offHandWeapon.Equals(itemGroup))
                    ApplyTo(stat, calculationTypes);
                break;
            default:
                if (mainWeapon != null && mainWeapon.Equals(itemGroup))
                    ApplyTo(stat, calculationTypes);
                break;
        }
    }

    private void ApplyTo(Stat2 stat, params CalculationType[] calculationTypes)
    {
        if (IsBonus())
        {
            int bonusRate = GetValue();
            if (calculationTypes.Contains(CalculationType.SKILL) && calculationTypes.Contains(CalculationType.DUAL_WIELD))
            {
                bonusRate = Rnd.Get(0, GetValue());
            }
            stat.SetFixedBonusRate(bonusRate / 100f);
        }
        else
        {
            // TODO: Check if calculations differ if its not a bonus type.
            stat.SetBase(stat.GetExactBaseWithoutBaseRate() * stat.CalculatePercent(GetValue()));
        }
    }
}
