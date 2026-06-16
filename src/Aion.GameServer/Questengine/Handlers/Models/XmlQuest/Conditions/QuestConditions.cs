using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.QuestEngine.Handlers.Models.XmlQuest.Conditions;

/// <summary>Java parity: .../conditions/QuestConditions. @XmlElements→stacked [XmlElement(name,typeof)].</summary>
[XmlType("QuestConditions")]
public class QuestConditions
{
    [XmlElement("quest_status", typeof(QuestStatusCondition))]
    [XmlElement("npc_id", typeof(NpcIdCondition))]
    [XmlElement("pc_inventory", typeof(PcInventoryCondition))]
    [XmlElement("quest_var", typeof(QuestVarCondition))]
    [XmlElement("dialog_id", typeof(DialogIdCondition))]
    public List<QuestCondition> conditions;

    [XmlAttribute("operate")] public ConditionUnionType operate;

    public bool CheckConditionOfSet(QuestEnv env)
    {
        bool inCondition = (operate == ConditionUnionType.AND);
        foreach (QuestCondition cond in conditions)
        {
            bool bCond = cond.DoCheck(env);
            switch (operate)
            {
                case ConditionUnionType.AND:
                    if (!bCond)
                        return false;
                    inCondition = inCondition && bCond;
                    break;
                case ConditionUnionType.OR:
                    if (bCond)
                        return true;
                    inCondition = inCondition || bCond;
                    break;
            }
        }
        return inCondition;
    }
}
