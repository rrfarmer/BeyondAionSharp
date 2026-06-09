using System.Xml.Serialization;
using Aion.GameServer.Questengine.Handlers.Models.XmlQuest.Conditions;
using Aion.GameServer.Questengine.Handlers.Models.XmlQuest.Operations;
using Aion.GameServer.Questengine.Model;

namespace Aion.GameServer.Questengine.Handlers.Models.XmlQuest;

/// <summary>Java parity: questEngine/handlers/models/xmlQuest/QuestDialog (@XmlType DialogAction).</summary>
[XmlType("DialogAction")]
public class QuestDialog
{
    [XmlElement("conditions")] protected QuestConditions conditions;
    [XmlElement("operations")] protected QuestOperations operations;
    [XmlAttribute("id")] protected int id;

    public bool Operate(QuestEnv env, QuestState qs)
    {
        if (env.GetDialogActionId() != id)
            return false;
        if (conditions == null || conditions.CheckConditionOfSet(env))
        {
            if (operations != null)
            {
                return operations.Operate(env);
            }
        }
        return false;
    }
}
