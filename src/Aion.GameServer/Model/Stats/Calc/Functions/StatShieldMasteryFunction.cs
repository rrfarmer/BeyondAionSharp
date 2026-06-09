using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Utils.Stats;

namespace Aion.GameServer.Model.Stats.Calc.Functions;

/// <summary>Java parity: model/stats/calc/functions/StatShieldMasteryFunction.</summary>
public class StatShieldMasteryFunction : StatRateFunction
{
    public StatShieldMasteryFunction(StatEnum name, int value, bool bonus)
        : base(name, value, bonus)
    {
    }

    public override void Apply(Stat2 stat, params CalculationType[] calculationTypes)
    {
        Player player = (Player) stat.GetOwner();
        if (player.GetEquipment().IsShieldEquipped())
            base.Apply(stat, calculationTypes);
    }
}
