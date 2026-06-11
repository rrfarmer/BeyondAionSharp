using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Items;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.SkillEngine.Condition;

/// <summary>
/// Java parity: skillengine/condition/PolishChargeCondition (Rolandas, Cheatkiller).
/// </summary>
public class PolishChargeCondition : ChargeCondition
{
    public override bool Validate(Skill env)
    {
        if (env.GetEffector() is Player)
        {
            Player effector = (Player)env.GetEffector();
            foreach (Item item in effector.GetEquipment().GetEquippedItems())
            {
                if (item.GetItemTemplate().IsWeapon() && item.GetIdianStone() != null)
                {
                    if ((item.GetEquipmentSlot() & ItemSlot.MAIN_OFF_HAND.GetSlotIdMask()) != 0
                        || (item.GetEquipmentSlot() & ItemSlot.SUB_OFF_HAND.GetSlotIdMask()) != 0)
                    {
                        continue;
                    }
                    item.GetIdianStone().DecreasePolishCharge(effector, value);
                }
            }
        }
        return true;
    }
}
