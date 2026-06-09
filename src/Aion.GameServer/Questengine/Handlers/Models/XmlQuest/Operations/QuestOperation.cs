using System.Xml.Serialization;
using Aion.GameServer.Questengine.Model;

namespace Aion.GameServer.Questengine.Handlers.Models.XmlQuest.Operations;

/// <summary>Java parity: questEngine/handlers/models/xmlQuest/operations/QuestOperation. @XmlSeeAlso→[XmlInclude] (op subclasses red until ported).</summary>
[XmlType("QuestOperation")]
[XmlInclude(typeof(TakeItemOperation))]
[XmlInclude(typeof(StartQuestOperation))]
[XmlInclude(typeof(SetQuestVarOperation))]
[XmlInclude(typeof(NpcDialogOperation))]
[XmlInclude(typeof(GiveItemOperation))]
[XmlInclude(typeof(SetQuestStatusOperation))]
public abstract class QuestOperation
{
    public abstract void DoOperate(QuestEnv env);
}
