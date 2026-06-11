using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Items;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.SkillEngine.Condition;

/// <summary>
/// Java parity: skillengine/condition/ChargeWeaponCondition (Rolandas, Cheatkiller).
/// </summary>
public class ChargeWeaponCondition : ChargeCondition
{
    public override bool Validate(Skill env)
    {
        if (env.GetEffector() is Player effector)
        {
            foreach (Item item in effector.GetEquipment().GetEquippedItems())
            {
                if (item.GetItemTemplate().IsWeapon() && item.GetConditioningInfo() != null)
                {
                    if ((item.GetEquipmentSlot() & ItemSlot.MAIN_OFF_HAND.GetSlotIdMask()) != 0
                        || (item.GetEquipmentSlot() & ItemSlot.SUB_OFF_HAND.GetSlotIdMask()) != 0)
                        continue;
                    item.GetConditioningInfo().UpdateChargePoints(-value);
                }
            }
        }
        return true;
    }
}
