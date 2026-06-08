using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Items;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.SkillEngine.condition;

/// <summary>
/// Java parity: skillengine/condition/ChargeArmorCondition (Rolandas, Cheatkiller).
/// </summary>
public class ChargeArmorCondition : ChargeCondition
{
    public override bool Validate(Skill env)
    {
        if (env.GetEffector() is Player effector)
        {
            foreach (Item item in effector.GetEquipment().GetEquippedItems())
            {
                if (item.GetItemTemplate().IsArmor() && item.GetConditioningInfo() != null)
                {
                    if ((item.GetEquipmentSlot() & ItemSlot.SUB_OFF_HAND.GetSlotIdMask()) != 0)
                        continue;
                    item.GetConditioningInfo().UpdateChargePoints(-value);
                }
            }
        }
        return true;
    }
}
