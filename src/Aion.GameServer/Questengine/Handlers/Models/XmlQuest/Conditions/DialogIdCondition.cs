using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Questengine.Model;

namespace Aion.GameServer.Questengine.Handlers.Models.XmlQuest.Conditions;

/// <summary>Java parity: .../conditions/DialogIdCondition.</summary>
[XmlType("DialogIdCondition")]
public class DialogIdCondition : QuestCondition
{
    [XmlAttribute("value")] protected int value;

    public int GetValue()
    {
        return value;
    }

    public override bool DoCheck(QuestEnv env)
    {
        switch (GetOp())
        {
            case ConditionOperation.EQUAL:
                return env.GetDialogActionId() == value;
            case ConditionOperation.NOT_EQUAL:
                return env.GetDialogActionId() != value;
            default:
                return false;
        }
    }
}
