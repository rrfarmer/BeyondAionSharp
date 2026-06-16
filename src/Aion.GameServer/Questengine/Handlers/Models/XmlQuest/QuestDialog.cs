using System.Xml.Serialization;
using Aion.GameServer.QuestEngine.Handlers.Models.XmlQuest.Conditions;
using Aion.GameServer.QuestEngine.Handlers.Models.XmlQuest.Operations;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.QuestEngine.Handlers.Models.XmlQuest;

/// <summary>Java parity: questEngine/handlers/models/xmlQuest/QuestDialog (@XmlType DialogAction).</summary>
[XmlType("DialogAction")]
public class QuestDialog
{
    [XmlElement("conditions")] public QuestConditions conditions;
    [XmlElement("operations")] public QuestOperations operations;
    [XmlAttribute("id")] public int id;

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
