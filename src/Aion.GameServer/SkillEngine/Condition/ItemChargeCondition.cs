using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.SkillEngine.condition;

/// <summary>
/// Java parity: skillengine/condition/ItemChargeCondition (Rolandas).
/// </summary>
public class ItemChargeCondition : ChargeCondition
{
    public override bool Validate(Stat2 env, IStatFunction statFunction)
    {
        StatOwner owner = statFunction.GetOwner();
        if (owner is Item)
        {
            Item item = (Item)owner;
            return item.GetChargeLevel() >= value;
        }
        return false;
    }

    public override bool Validate(Skill env)
    {
        return false;
    }
}
