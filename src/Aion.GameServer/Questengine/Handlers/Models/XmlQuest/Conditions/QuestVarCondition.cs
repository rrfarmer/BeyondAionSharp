using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.QuestEngine.Handlers.Models.XmlQuest.Conditions;

/// <summary>Java parity: .../conditions/QuestVarCondition.</summary>
[XmlType("QuestVarCondition")]
public class QuestVarCondition : QuestCondition
{
    [XmlAttribute("value")] public int value;
    [XmlAttribute("var_id")] public int varId;

    public override bool DoCheck(QuestEnv env)
    {
        QuestState qs = env.GetPlayer().GetQuestStateList().GetQuestState(env.GetQuestId());
        if (qs == null)
        {
            return false;
        }
        int var = qs.GetQuestVars().GetVarById(varId);
        switch (GetOp())
        {
            case ConditionOperation.EQUAL:
                return var == value;
            case ConditionOperation.GREATER:
                return var > value;
            case ConditionOperation.GREATER_EQUAL:
                return var >= value;
            case ConditionOperation.LESSER:
                return var < value;
            case ConditionOperation.LESSER_EQUAL:
                return var <= value;
            case ConditionOperation.NOT_EQUAL:
                return var != value;
            default:
                return false;
        }
    }
}
