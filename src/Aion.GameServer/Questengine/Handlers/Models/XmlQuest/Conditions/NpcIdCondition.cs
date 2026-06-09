using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Questengine.Model;

namespace Aion.GameServer.Questengine.Handlers.Models.XmlQuest.Conditions;

/// <summary>Java parity: .../conditions/NpcIdCondition.</summary>
[XmlType("NpcIdCondition")]
public class NpcIdCondition : QuestCondition
{
    [XmlAttribute("values")] protected int values;

    public override bool DoCheck(QuestEnv env)
    {
        int id = 0;
        VisibleObject visibleObject = env.GetVisibleObject();
        if (visibleObject is Npc npc)
        {
            id = npc.GetNpcId();
        }
        switch (GetOp())
        {
            case ConditionOperation.EQUAL:
                return id == values;
            case ConditionOperation.GREATER:
                return id > values;
            case ConditionOperation.GREATER_EQUAL:
                return id >= values;
            case ConditionOperation.LESSER:
                return id < values;
            case ConditionOperation.LESSER_EQUAL:
                return id <= values;
            case ConditionOperation.NOT_EQUAL:
                return id != values;
            default:
                return false;
        }
    }
}
