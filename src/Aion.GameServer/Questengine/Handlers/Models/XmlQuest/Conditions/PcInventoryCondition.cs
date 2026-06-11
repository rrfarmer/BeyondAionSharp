using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Questengine.Model;

namespace Aion.GameServer.Questengine.Handlers.Models.XmlQuest.Conditions;

/// <summary>Java parity: .../conditions/PcInventoryCondition.</summary>
[XmlType("PcInventoryCondition")]
public class PcInventoryCondition : QuestCondition
{
    [XmlAttribute("item_id")] protected int itemId;
    [XmlAttribute("count")] protected long count;

    public int GetItemId()
    {
        return itemId;
    }

    public long GetCount()
    {
        return count;
    }

    public override bool DoCheck(QuestEnv env)
    {
        Player player = env.GetPlayer();
        long itemCount = player.GetInventory().GetItemCountByItemId(itemId);
        switch (GetOp())
        {
            case ConditionOperation.EQUAL:
                return itemCount == count;
            case ConditionOperation.GREATER:
                return itemCount > count;
            case ConditionOperation.GREATER_EQUAL:
                return itemCount >= count;
            case ConditionOperation.LESSER:
                return itemCount < count;
            case ConditionOperation.LESSER_EQUAL:
                return itemCount <= count;
            case ConditionOperation.NOT_EQUAL:
                return itemCount != count;
            default:
                return false;
        }
    }
}
